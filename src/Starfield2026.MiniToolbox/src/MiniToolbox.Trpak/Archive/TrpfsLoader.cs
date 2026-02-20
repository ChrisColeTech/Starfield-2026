using MiniToolbox.Core.Archive;
using System.Runtime.InteropServices;
using MiniToolbox.Trpak.Flatbuffers.TR.ResourceDictionary;
using MiniToolbox.Core.Utils;

namespace MiniToolbox.Trpak.Archive
{
    /// <summary>
    /// Loads files from a TRPFD/TRPFS archive pair (Pokémon Scarlet/Violet, Legends Arceus).
    /// data.trpfd = FlatBuffer file descriptor (hashes → pack names/indices).
    /// data.trpfs = ONEFILE header + FlatBuffer FileSystem (hashes → offsets) + packed data.
    /// </summary>
    public class TrpfsLoader
    {
        private readonly CustomFileDescriptor _fd;
        private readonly Flatbuffers.TR.ResourceDictionary.FileSystem _fs;
        private readonly string _trpfsPath;
        private readonly TrpakHashCache _hashCache;

        // Pack cache: packHash → deserialized PackedArchive
        private readonly Dictionary<ulong, PackedArchive> _packCache = new();

        public TrpfsLoader(string arcDirectory, TrpakHashCache? hashCache = null)
        {
            string trpfdPath = Path.Combine(arcDirectory, "data.trpfd");
            _trpfsPath = Path.Combine(arcDirectory, "data.trpfs");

            if (!File.Exists(trpfdPath))
                throw new FileNotFoundException("data.trpfd not found.", trpfdPath);
            if (!File.Exists(_trpfsPath))
                throw new FileNotFoundException("data.trpfs not found.", _trpfsPath);

            _fd = FlatBufferConverter.DeserializeFrom<CustomFileDescriptor>(trpfdPath);
            _fs = ReadFileSystem(_trpfsPath);
            _hashCache = hashCache ?? new TrpakHashCache();
        }

        /// <summary>Number of files in the descriptor.</summary>
        public int FileCount => _fd.FileHashes?.Length ?? 0;

        /// <summary>All pack names registered in the descriptor.</summary>
        public IReadOnlyList<string> PackNames => _fd.PackNames;

        /// <summary>
        /// Extract a file by its romfs-relative path (e.g. "pokemon/pokemon_model/pm0025/pm0025_00/mdl/pm0025_00.trmdl").
        /// </summary>
        public byte[]? ExtractFile(string romfsRelativePath)
        {
            string normalized = NormalizePath(romfsRelativePath);
            ulong hash = FnvHash.Hash(normalized);
            return ExtractFile(hash);
        }

        /// <summary>
        /// Extract a file by its FNV hash.
        /// </summary>
        public byte[]? ExtractFile(ulong fileHash)
        {
            if (!TryResolvePackInfo(fileHash, out string packName, out long packSize))
                return null;

            ulong packHash = FnvHash.Hash(packName);
            if (!TryGetPack(packHash, packSize, out var pack))
                return null;

            int entryIndex = FindEntryIndex(pack, fileHash);
            if (entryIndex < 0)
                return null;

            var entry = pack.FileEntry[entryIndex];
            byte[] buffer = entry.FileBuffer;

            // EncryptionType != -1 means Oodle-compressed
            if (entry.EncryptionType != -1)
            {
                var decompressed = OodleDecompressor.Decompress(buffer, (int)entry.FileSize);
                if (decompressed == null)
                    throw new InvalidDataException($"Oodle decompression failed for hash 0x{fileHash:X16}");
                buffer = decompressed;
            }

            return buffer;
        }

        /// <summary>
        /// Find all file hashes whose human-readable names match a predicate.
        /// Requires a populated hash cache.
        /// </summary>
        public IEnumerable<(ulong hash, string name)> FindFiles(Func<string, bool> namePredicate)
        {
            if (_fd.FileHashes == null) yield break;

            foreach (ulong hash in _fd.FileHashes)
            {
                string? name = _hashCache.GetName(hash);
                if (name != null && namePredicate(name))
                    yield return (hash, name);
            }
        }

        /// <summary>
        /// Find all files with a given extension (e.g. ".trmdl").
        /// </summary>
        public IEnumerable<(ulong hash, string name)> FindFilesByExtension(string extension)
        {
            return FindFiles(name => name.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
        }

        #region Private helpers

        private static string NormalizePath(string path)
        {
            path = (path ?? string.Empty).Replace('\\', '/').Trim();
            if (path.StartsWith("romfs://", StringComparison.OrdinalIgnoreCase))
                path = path["romfs://".Length..];
            if (path.StartsWith("trpfs://", StringComparison.OrdinalIgnoreCase))
                path = path["trpfs://".Length..];
            return path.TrimStart('/');
        }

        private static Flatbuffers.TR.ResourceDictionary.FileSystem ReadFileSystem(string trpfsPath)
        {
            using var br = new BinaryReader(File.OpenRead(trpfsPath));

            // OneFileHeader: 8 bytes magic + 8 bytes offset
            br.ReadUInt64(); // magic
            long fsOffset = br.ReadInt64();

            br.BaseStream.Position = fsOffset;
            byte[] fsData = br.ReadBytes((int)(br.BaseStream.Length - fsOffset));

            return FlatBufferConverter.DeserializeFrom<Flatbuffers.TR.ResourceDictionary.FileSystem>(fsData);
        }

        private bool TryResolvePackInfo(ulong fileHash, out string packName, out long packSize)
        {
            packName = string.Empty;
            packSize = 0;

            if (_fd.FileHashes == null || _fd.FileInfo == null || _fd.PackNames == null || _fd.PackInfo == null)
                return false;

            int idx = Array.IndexOf(_fd.FileHashes, fileHash);
            if (idx >= 0)
            {
                ulong packIndex = _fd.FileInfo[idx].PackIndex;
                if (packIndex < (ulong)_fd.PackNames.Length && packIndex < (ulong)_fd.PackInfo.Length)
                {
                    packName = _fd.PackNames[packIndex];
                    packSize = checked((long)_fd.PackInfo[packIndex].FileSize);
                    return !string.IsNullOrWhiteSpace(packName);
                }
                return false;
            }

            // Check unused files
            if (_fd.UnusedHashes != null && _fd.UnusedFileInfo != null)
            {
                int unusedIdx = Array.IndexOf(_fd.UnusedHashes, fileHash);
                if (unusedIdx >= 0 && unusedIdx < _fd.UnusedFileInfo.Length)
                {
                    ulong packIndex = _fd.UnusedFileInfo[unusedIdx].PackIndex;
                    if (packIndex < (ulong)_fd.PackNames.Length && packIndex < (ulong)_fd.PackInfo.Length)
                    {
                        packName = _fd.PackNames[packIndex];
                        packSize = checked((long)_fd.PackInfo[packIndex].FileSize);
                        return !string.IsNullOrWhiteSpace(packName);
                    }
                }
            }

            return false;
        }

        private bool TryGetPack(ulong packHash, long packSize, out PackedArchive pack)
        {
            if (_packCache.TryGetValue(packHash, out pack!))
                return true;

            int fileIndex = Array.IndexOf(_fs.FileHashes, packHash);
            if (fileIndex < 0)
            {
                pack = null!;
                return false;
            }

            // Read raw pack bytes from TRPFS
            using var br = new BinaryReader(File.OpenRead(_trpfsPath));
            br.BaseStream.Position = (long)_fs.FileOffsets[fileIndex];
            byte[] packBytes = br.ReadBytes((int)packSize);

            pack = FlatBufferConverter.DeserializeFrom<PackedArchive>(packBytes);
            _packCache[packHash] = pack;
            return true;
        }

        private static int FindEntryIndex(PackedArchive pack, ulong fileHash)
        {
            if (pack.FileHashes == null) return -1;
            return Array.IndexOf(pack.FileHashes, fileHash);
        }

        #endregion
    }
}
