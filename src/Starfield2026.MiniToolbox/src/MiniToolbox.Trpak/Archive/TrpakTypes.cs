using System.Runtime.InteropServices;
using System.Text;

namespace MiniToolbox.Trpak.Archive
{
    #region Binary Structs

    internal enum TrpakCompressionType : ushort
    {
        None = 0,
        Zlib = 1,
        Lz4 = 2,
        Oodle = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TrpakHeader
    {
        public const ulong Magic = 0x4B434150_584C4647; // "GFLXPACK"
        public const int Size = 0x18;
        public ulong magic;
        public uint Version;
        public uint Relocated;
        public uint FileNumber;
        public uint FolderNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TrpakFolderHeader
    {
        public const int Size = 0x10;
        public ulong Hash;
        public uint ContentNumber;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TrpakFolderIndex
    {
        public const int Size = 0x10;
        public ulong Hash;
        public uint Index;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TrpakFileHeader
    {
        public const int Size = 0x18;
        public ushort Level;
        public TrpakCompressionType CompressionType;
        public uint BufferSize;
        public uint FileSize;
        public uint Reserved;
        public ulong FilePointer;
    }

    #endregion

    #region FNV Hash

    /// <summary>
    /// FNV-1a 64-bit hash used by TRPAK for file/folder name lookup.
    /// Ported from gftool GFFNV.cs.
    /// </summary>
    public static class FnvHash
    {
        private const ulong FnvPrime = 0x00000100000001b3;
        private const ulong FnvBasis = 0xCBF29CE484222645;

        public static ulong Hash(string str)
        {
            byte[] buf = Encoding.UTF8.GetBytes(str);
            ulong result = FnvBasis;
            foreach (byte b in buf)
            {
                result ^= b;
                result *= FnvPrime;
            }
            return result;
        }
    }

    #endregion

    #region Hash Cache

    /// <summary>
    /// Maps FNV hashes back to human-readable file paths.
    /// Ported from gftool GFPakHashCache.cs.
    /// </summary>
    public class TrpakHashCache
    {
        private readonly Dictionary<ulong, string> _cache = new();

        public int Count => _cache.Count;

        /// <summary>
        /// Load a binary hash cache file (GFPAKHashCache.bin format).
        /// </summary>
        public void LoadBinaryCache(string path)
        {
            if (!File.Exists(path)) return;
            using var br = new BinaryReader(File.OpenRead(path));
            var count = br.ReadUInt64();
            for (ulong i = 0; i < count; i++)
            {
                var hash = br.ReadUInt64();
                var name = br.ReadString();
                _cache[hash] = name;
            }
        }

        /// <summary>
        /// Load a text hash list (one "hash path" per line).
        /// </summary>
        public void LoadHashList(IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                if (TryParseHex(parts[0], out ulong hash))
                    _cache[hash] = parts[1];
                else
                    _cache[FnvHash.Hash(parts[1])] = parts[1];
            }
        }

        public void Add(ulong hash, string name) => _cache[hash] = name;

        public void MergeFrom(Dictionary<ulong, string> entries)
        {
            foreach (var (hash, name) in entries)
                _cache.TryAdd(hash, name);
        }

        public string? GetName(ulong hash) => _cache.TryGetValue(hash, out var name) ? name : null;

        public IReadOnlyList<string> AllPaths() => _cache.Values.Distinct().ToList();

        private static bool TryParseHex(string text, out ulong value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
            return ulong.TryParse(text, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out value);
        }
    }

    #endregion

    #region Output Models

    public class TrpakArchive
    {
        public List<TrpakFolder> Folders { get; } = new();

        /// <summary>
        /// Find a file by its full path (case-insensitive).
        /// </summary>
        public TrpakFile? FindFile(string fullPath)
        {
            foreach (var folder in Folders)
                foreach (var file in folder.Files)
                    if (string.Equals(file.FullName, fullPath, StringComparison.OrdinalIgnoreCase))
                        return file;
            return null;
        }

        /// <summary>
        /// Find all files matching a predicate.
        /// </summary>
        public IEnumerable<TrpakFile> FindFiles(Func<TrpakFile, bool> predicate)
        {
            return Folders.SelectMany(f => f.Files).Where(predicate);
        }

        /// <summary>
        /// Find all files with a specific extension.
        /// </summary>
        public IEnumerable<TrpakFile> FindFilesByExtension(string extension)
        {
            return FindFiles(f => f.FullName.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
        }
    }

    public class TrpakFolder
    {
        public string Path { get; set; } = string.Empty;
        public List<TrpakFile> Files { get; } = new();
    }

    public class TrpakFile
    {
        public string Name { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }

    #endregion
}
