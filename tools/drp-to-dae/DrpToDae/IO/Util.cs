using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace System.IO
{
    public static class Util
    {
        public static uint calc_crc(string filename)
        {
            var b = Encoding.ASCII.GetBytes(filename);
            for (var i = 0; i < 4 && i < filename.Length; i++)
                b[i] = (byte)(~filename[i] & 0xff);
            return Crc32(b) & 0xFFFFFFFF;
        }

        private static uint Crc32(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            uint[] table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int j = 0; j < 8; j++)
                    c = (c & 1) != 0 ? (0xEDB88320 ^ (c >> 1)) : (c >> 1);
                table[i] = c;
            }
            foreach (byte b in data)
                crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFF;
        }

        public static byte[] Compress(byte[] src)
        {
            using (var destStream = new MemoryStream())
            {
                destStream.WriteByte(0x78);
                destStream.WriteByte(0x9C);
                using (var compressor = new DeflateStream(destStream, CompressionLevel.Optimal, true))
                {
                    compressor.Write(src, 0, src.Length);
                }
                return destStream.ToArray();
            }
        }

        public static byte[] DeCompress(byte[] src)
        {
            // Skip zlib header (2 bytes) if present
            int offset = 0;
            if (src.Length > 2 && src[0] == 0x78 && (src[1] == 0x9C || src[1] == 0x01 || src[1] == 0xDA))
            {
                offset = 2;
            }

            using (var ms = new MemoryStream(src, offset, src.Length - offset))
            using (var decompressor = new DeflateStream(ms, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                decompressor.CopyTo(output);
                return output.ToArray();
            }
        }

        public static void SetWord(ref byte[] data, long value, long offset)
        {
            if (offset % 4 != 0) throw new Exception("Odd word offset");
            if (offset >= data.Length)
            {
                Array.Resize<byte>(ref data, (int)offset + 4);
            }

            data[offset + 3] = (byte)((value & 0xFF000000) / 0x1000000);
            data[offset + 2] = (byte)((value & 0xFF0000) / 0x10000);
            data[offset + 1] = (byte)((value & 0xFF00) / 0x100);
            data[offset + 0] = (byte)((value & 0xFF) / 0x1);
        }
        public static void SetHalf(ref byte[] data, short value, long offset)
        {
            if (offset % 2 != 0) throw new Exception("Odd word offset");
            if (offset >= data.Length)
            {
                Array.Resize<byte>(ref data, (int)offset + 4);
            }

            data[offset + 1] = (byte)((value & 0xFF00) / 0x100);
            data[offset + 0] = (byte)((value & 0xFF) / 0x1);
        }
    }
    public enum Endianness
    {
        Big = 0,
        Little = 1
    }
}
