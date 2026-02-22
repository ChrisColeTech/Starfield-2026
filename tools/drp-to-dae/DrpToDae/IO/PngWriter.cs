using System;
using System.IO;
using System.IO.Compression;
using DrpToDae.Formats.NUT;

namespace DrpToDae.IO
{
    public static class PngWriter
    {
        public static void SaveAsPng(byte[] rgbaData, int width, int height, string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

                WriteIhdrChunk(bw, width, height);
                WriteIdatChunk(bw, rgbaData, width, height);
                WriteIendChunk(bw);
            }
        }

        private static void WriteIhdrChunk(BinaryWriter bw, int width, int height)
        {
            byte[] data = new byte[13];
            WriteBigEndianInt32(data, 0, width);
            WriteBigEndianInt32(data, 4, height);
            data[8] = 8;
            data[9] = 6;
            data[10] = 0;
            data[11] = 0;
            data[12] = 0;

            WriteChunk(bw, "IHDR", data);
        }

        private static void WriteIdatChunk(BinaryWriter bw, byte[] rgbaData, int width, int height)
        {
            byte[] rawData = new byte[height * (1 + width * 4)];
            int srcIdx = 0;
            int dstIdx = 0;

            for (int y = 0; y < height; y++)
            {
                rawData[dstIdx++] = 0;
                for (int x = 0; x < width; x++)
                {
                    rawData[dstIdx++] = rgbaData[srcIdx + 0];
                    rawData[dstIdx++] = rgbaData[srcIdx + 1];
                    rawData[dstIdx++] = rgbaData[srcIdx + 2];
                    rawData[dstIdx++] = rgbaData[srcIdx + 3];
                    srcIdx += 4;
                }
            }

            byte[] compressed = ZlibCompress(rawData);
            WriteChunk(bw, "IDAT", compressed);
        }

        private static void WriteIendChunk(BinaryWriter bw)
        {
            WriteChunk(bw, "IEND", Array.Empty<byte>());
        }

        private static void WriteChunk(BinaryWriter bw, string type, byte[] data)
        {
            WriteBigEndianInt32ToStream(bw, data.Length);
            bw.Write(System.Text.Encoding.ASCII.GetBytes(type));
            bw.Write(data);

            uint crc = CalculateCrc(System.Text.Encoding.ASCII.GetBytes(type), data);
            WriteBigEndianInt32ToStream(bw, (int)crc);
        }

        private static byte[] ZlibCompress(byte[] data)
        {
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(0x78);
                ms.WriteByte(0x9C);

                using (var deflate = new DeflateStream(ms, CompressionLevel.Optimal, true))
                {
                    deflate.Write(data, 0, data.Length);
                }

                uint adler = CalculateAdler32(data);
                ms.WriteByte((byte)((adler >> 24) & 0xFF));
                ms.WriteByte((byte)((adler >> 16) & 0xFF));
                ms.WriteByte((byte)((adler >> 8) & 0xFF));
                ms.WriteByte((byte)(adler & 0xFF));

                return ms.ToArray();
            }
        }

        private static uint CalculateAdler32(byte[] data)
        {
            uint a = 1, b = 0;
            foreach (byte bt in data)
            {
                a = (a + bt) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }

        private static readonly uint[] CrcTable = GenerateCrcTable();

        private static uint[] GenerateCrcTable()
        {
            uint[] table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                {
                    c = (c & 1) != 0 ? (0xEDB88320 ^ (c >> 1)) : (c >> 1);
                }
                table[n] = c;
            }
            return table;
        }

        private static uint CalculateCrc(byte[] type, byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte b in type)
                crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            foreach (byte b in data)
                crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFF;
        }

        private static void WriteBigEndianInt32(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)((value >> 24) & 0xFF);
            buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 3] = (byte)(value & 0xFF);
        }

        private static void WriteBigEndianInt32ToStream(BinaryWriter bw, int value)
        {
            bw.Write((byte)((value >> 24) & 0xFF));
            bw.Write((byte)((value >> 16) & 0xFF));
            bw.Write((byte)((value >> 8) & 0xFF));
            bw.Write((byte)(value & 0xFF));
        }
    }
}
