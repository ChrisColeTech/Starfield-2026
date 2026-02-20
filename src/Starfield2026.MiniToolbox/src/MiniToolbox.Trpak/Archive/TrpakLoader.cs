using MiniToolbox.Core.Archive;
using System.Runtime.InteropServices;

namespace MiniToolbox.Trpak.Archive
{
    /// <summary>
    /// Loads TRPAK (GFLXPACK) archives.
    /// Ported from gftool GFPakSerializer.Deserialize().
    /// 
    /// Usage:
    ///   var cache = new TrpakHashCache();
    ///   cache.LoadHashList(File.ReadAllLines("hashlist.txt"));
    ///   var archive = TrpakLoader.Load("model.trpak", cache);
    ///   var trmdl = archive.FindFilesByExtension(".trmdl").First();
    /// </summary>
    public static class TrpakLoader
    {
        public static TrpakArchive Load(string path, TrpakHashCache? hashCache = null)
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);
            return Load(br, hashCache);
        }

        public static TrpakArchive Load(BinaryReader br, TrpakHashCache? hashCache = null)
        {
            hashCache ??= new TrpakHashCache();

            // Read header
            var header = ReadStruct<TrpakHeader>(br, TrpakHeader.Size);

            // Embedded file offset + hash offset
            ulong embeddedFileOff = br.ReadUInt64();
            ulong embeddedFileHashOff = br.ReadUInt64();

            // Folder offsets
            var folderOffsets = new long[header.FolderNumber];
            for (int i = 0; i < header.FolderNumber; i++)
                folderOffsets[i] = br.ReadInt64();

            // File hashes (absolute path hashes)
            br.BaseStream.Position = (long)embeddedFileHashOff;
            var fileHashes = new ulong[header.FileNumber];
            for (int i = 0; i < header.FileNumber; i++)
                fileHashes[i] = br.ReadUInt64();

            // File headers
            br.BaseStream.Position = (long)embeddedFileOff;
            var fileHeaders = new TrpakFileHeader[header.FileNumber];
            for (int i = 0; i < header.FileNumber; i++)
                fileHeaders[i] = ReadStruct<TrpakFileHeader>(br, TrpakFileHeader.Size);

            // Decompress files
            var files = new byte[header.FileNumber][];
            for (int i = 0; i < header.FileNumber; i++)
            {
                var fh = fileHeaders[i];
                var rawBytes = br.ReadBytes((int)fh.FileSize);

                files[i] = fh.CompressionType switch
                {
                    TrpakCompressionType.Lz4 => Lz4Decompressor.Decompress(rawBytes, (int)fh.BufferSize),
                    TrpakCompressionType.Oodle => OodleDecompressor.Decompress(rawBytes, (int)fh.BufferSize) ?? rawBytes,
                    _ => rawBytes
                };
            }

            // Build folder/file structure
            var archive = new TrpakArchive();
            for (int i = 0; i < header.FolderNumber; i++)
            {
                br.BaseStream.Position = folderOffsets[i];
                var folderHeader = ReadStruct<TrpakFolderHeader>(br, TrpakFolderHeader.Size);

                string folderName = hashCache.GetName(folderHeader.Hash) ?? folderHeader.Hash.ToString("X16");

                var folder = new TrpakFolder { Path = folderName };

                for (int j = 0; j < folderHeader.ContentNumber; j++)
                {
                    var content = ReadStruct<TrpakFolderIndex>(br, TrpakFolderIndex.Size);
                    int fileIndex = (int)content.Index;

                    string fileName = hashCache.GetName(content.Hash) ?? content.Hash.ToString("X16");

                    // Try to resolve the full path from the file hash
                    string fullPath = hashCache.GetName(fileHashes[fileIndex])
                                      ?? (folderName + fileName);

                    // Auto-register resolved paths
                    if (hashCache.GetName(fileHashes[fileIndex]) == null)
                        hashCache.Add(fileHashes[fileIndex], fullPath);

                    folder.Files.Add(new TrpakFile
                    {
                        Name = fileName,
                        FullName = fullPath,
                        Data = fileIndex < files.Length ? files[fileIndex] : Array.Empty<byte>()
                    });
                }

                archive.Folders.Add(folder);
            }

            return archive;
        }

        private static T ReadStruct<T>(BinaryReader br, int size) where T : struct
        {
            var bytes = br.ReadBytes(size);
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }
    }
}
