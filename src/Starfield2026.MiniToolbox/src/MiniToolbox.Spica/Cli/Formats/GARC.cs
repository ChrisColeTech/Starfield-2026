using System.IO;
using System.Text;

namespace SpicaCli.Formats
{
    /// <summary>
    /// GARC (Game ARChive) container reader.
    /// Magic: "CRAG" (GARC reversed). Used in Pokemon 3DS ROMs.
    /// </summary>
    public static class GARC
    {
        public struct GARCEntry
        {
            public long Offset;
            public int Length;
        }

        /// <summary>
        /// Check if stream starts with GARC magic "CRAG".
        /// </summary>
        public static bool IsGARC(Stream stream)
        {
            if (stream.Length < 4) return false;

            long pos = stream.Position;
            byte[] magic = new byte[4];
            stream.Read(magic, 0, 4);
            stream.Seek(pos, SeekOrigin.Begin);

            return Encoding.ASCII.GetString(magic) == "CRAG";
        }

        /// <summary>
        /// Read all entry offsets and lengths from a GARC archive.
        /// </summary>
        public static GARCEntry[] GetEntries(Stream data)
        {
            BinaryReader input = new BinaryReader(data);

            // GARC header
            string magic = Encoding.ASCII.GetString(input.ReadBytes(4)); // "CRAG"
            uint garcHeaderLength = input.ReadUInt32();
            ushort endian = input.ReadUInt16();
            ushort version = input.ReadUInt16();
            uint sectionCount = input.ReadUInt32();
            uint dataOffset = input.ReadUInt32();
            uint decompressedLength = input.ReadUInt32();
            uint compressedLength = input.ReadUInt32();

            // Seek past GARC header to FATO section
            data.Seek(garcHeaderLength, SeekOrigin.Begin);

            // FATO (File Allocation Table Offsets)
            long fatoPosition = data.Position;
            string fatoMagic = Encoding.ASCII.GetString(input.ReadBytes(4)); // "OTAF"
            uint fatoLength = input.ReadUInt32();
            ushort fatoEntries = input.ReadUInt16();
            input.ReadUInt16(); // padding 0xFFFF

            long fatbPosition = fatoPosition + fatoLength;

            var entries = new List<GARCEntry>();

            for (int i = 0; i < fatoEntries; i++)
            {
                data.Seek(fatoPosition + 0xC + i * 4, SeekOrigin.Begin);
                uint fatbOffset = input.ReadUInt32();

                data.Seek(fatbPosition + 0xC + fatbOffset, SeekOrigin.Begin);

                uint flags = input.ReadUInt32();

                for (int bit = 0; bit < 32; bit++)
                {
                    if ((flags & (1 << bit)) > 0)
                    {
                        uint startOffset = input.ReadUInt32();
                        uint endOffset = input.ReadUInt32();
                        uint length = input.ReadUInt32();

                        entries.Add(new GARCEntry
                        {
                            Offset = startOffset + dataOffset,
                            Length = (int)length
                        });
                    }
                }
            }

            return entries.ToArray();
        }

        /// <summary>
        /// Read a single entry's data from the GARC archive.
        /// </summary>
        public static byte[] ReadEntry(Stream data, GARCEntry entry)
        {
            data.Seek(entry.Offset, SeekOrigin.Begin);
            byte[] buffer = new byte[entry.Length];
            data.Read(buffer, 0, buffer.Length);
            return buffer;
        }
    }
}
