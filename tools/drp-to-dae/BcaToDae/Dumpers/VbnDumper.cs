using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Dumpers
{
    public static class VbnDumper
    {
        public static void Dump(byte[] data)
        {
            Console.WriteLine("\n--- VBN (Skeleton) ---");
            
            bool isBigEndian = Encoding.ASCII.GetString(data, 0, 4) == "VBN ";
            Console.WriteLine($"Endianness: {(isBigEndian ? "Big" : "Little")}");

            int ReadInt32(int offset)
            {
                if (isBigEndian)
                    return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
                return data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24);
            }

            short ReadInt16(int offset)
            {
                if (isBigEndian)
                    return (short)((data[offset] << 8) | data[offset + 1]);
                return (short)(data[offset] | (data[offset + 1] << 8));
            }

            int boneCount = ReadInt32(0x08);
            int[] typeCounts = new int[4];
            for (int i = 0; i < 4; i++)
                typeCounts[i] = ReadInt32(0x0C + i * 4);

            Console.WriteLine($"Bone count: {boneCount}");
            Console.WriteLine($"Type counts: [{typeCounts[0]}, {typeCounts[1]}, {typeCounts[2]}, {typeCounts[3]}]");

            int headerSize = 0x1C;
            int boneHeaderSize = 64 + 4 + 4 + 4; // name + type + parent + id = 76
            int transformSize = 12 + 12 + 12; // pos + rot + scale = 36

            int boneDataStart = headerSize;
            int transformDataStart = headerSize + boneCount * boneHeaderSize;

            Console.WriteLine($"\n--- Bones (first 15) ---");
            for (int i = 0; i < Math.Min(15, boneCount); i++)
            {
                int entryStart = boneDataStart + i * boneHeaderSize;
                string name = ReadNullString(data, entryStart, 64);
                int boneType = ReadInt32(entryStart + 64);
                int parentIdx = ReadInt32(entryStart + 68);
                int boneId = ReadInt32(entryStart + 72);

                int transformStart = transformDataStart + i * transformSize;
                float posX = ReadFloat(data, transformStart, isBigEndian);
                float posY = ReadFloat(data, transformStart + 4, isBigEndian);
                float posZ = ReadFloat(data, transformStart + 8, isBigEndian);

                Console.WriteLine($"  [{i}] \"{name}\" type={boneType} parent={parentIdx} id=0x{boneId:X8} pos=({posX:F3}, {posY:F3}, {posZ:F3})");
            }

            if (boneCount > 15)
                Console.WriteLine($"  ... ({boneCount - 15} more bones)");

            Console.WriteLine($"\n--- Bone names list ---");
            var names = new List<string>();
            for (int i = 0; i < boneCount; i++)
            {
                int entryStart = boneDataStart + i * boneHeaderSize;
                string name = ReadNullString(data, entryStart, 64);
                names.Add(name);
            }

            for (int i = 0; i < Math.Min(30, names.Count); i++)
                Console.WriteLine($"  {i}: \"{names[i]}\"");
            if (names.Count > 30)
                Console.WriteLine($"  ... ({names.Count - 30} more)");
        }

        private static string ReadNullString(byte[] data, int offset, int maxLen)
        {
            int len = 0;
            while (offset + len < data.Length && len < maxLen && data[offset + len] != 0)
                len++;
            return Encoding.ASCII.GetString(data, offset, len);
        }

        private static float ReadFloat(byte[] data, int offset, bool bigEndian)
        {
            byte[] bytes = new byte[4];
            if (bigEndian)
            {
                bytes[0] = data[offset + 3];
                bytes[1] = data[offset + 2];
                bytes[2] = data[offset + 1];
                bytes[3] = data[offset];
            }
            else
            {
                bytes[0] = data[offset];
                bytes[1] = data[offset + 1];
                bytes[2] = data[offset + 2];
                bytes[3] = data[offset + 3];
            }
            return BitConverter.ToSingle(bytes, 0);
        }
    }
}
