using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Dumpers
{
    public static class BclAnalyzer
    {
        public static void Analyze(string bclPath, string bcaPath, string vbnPath)
        {
            byte[] bclData = File.ReadAllBytes(bclPath);
            byte[] bcaData = File.ReadAllBytes(bcaPath);
            byte[] vbnData = File.ReadAllBytes(vbnPath);

            Console.WriteLine("\n========== BCL DEEP ANALYSIS ==========\n");
            Console.WriteLine($"BCL: {bclData.Length} bytes ({bclData.Length / 1024.0:F1} KB)");
            Console.WriteLine($"BCA: {bcaData.Length} bytes");
            Console.WriteLine($"VBN: {vbnData.Length} bytes");

            // Parse VBN bone names
            bool vbnBE = Encoding.ASCII.GetString(vbnData, 0, 4) == "VBN ";
            int boneCount = vbnBE
                ? (vbnData[8] << 24) | (vbnData[9] << 16) | (vbnData[10] << 8) | vbnData[11]
                : BitConverter.ToInt32(vbnData, 8);
            Console.WriteLine($"VBN bones: {boneCount}");

            var boneNames = new List<string>();
            for (int i = 0; i < boneCount; i++)
            {
                int off = 0x1C + i * 76;
                int end = off;
                while (end < off + 64 && vbnData[end] != 0) end++;
                boneNames.Add(Encoding.ASCII.GetString(vbnData, off, end - off));
            }

            // BCL header analysis
            Console.WriteLine("\n--- BCL Header ---");
            Console.WriteLine($"  Magic: {Encoding.ASCII.GetString(bclData, 0, 4)}");
            for (int off = 4; off < Math.Min(0x40, bclData.Length); off += 4)
            {
                uint val = BitConverter.ToUInt32(bclData, off);
                float fval = BitConverter.ToSingle(bclData, off);
                if (val != 0)
                    Console.WriteLine($"  0x{off:X2}: 0x{val:X8} ({val}) float={fval:F4}");
            }

            // Search for bone names in BCL
            Console.WriteLine("\n--- Bone Name Search in BCL ---");
            int foundNames = 0;
            foreach (string name in boneNames)
            {
                byte[] nameBytes = Encoding.ASCII.GetBytes(name);
                for (int i = 0; i <= bclData.Length - nameBytes.Length; i++)
                {
                    bool match = true;
                    for (int j = 0; j < nameBytes.Length; j++)
                    {
                        if (bclData[i + j] != nameBytes[j]) { match = false; break; }
                    }
                    if (match && (i + nameBytes.Length >= bclData.Length || bclData[i + nameBytes.Length] == 0))
                    {
                        Console.WriteLine($"  Found \"{name}\" at offset 0x{i:X}");
                        foundNames++;
                        break;
                    }
                }
            }
            Console.WriteLine($"  Total bone names found: {foundNames} / {boneNames.Count}");

            // Search for ALL strings in BCL
            Console.WriteLine("\n--- All Strings in BCL ---");
            var strings = new List<(int offset, string text)>();
            int strStart = -1;
            for (int i = 0; i < bclData.Length; i++)
            {
                byte b = bclData[i];
                if (b >= 32 && b < 127)
                {
                    if (strStart < 0) strStart = i;
                }
                else
                {
                    if (strStart >= 0 && i - strStart >= 3)
                    {
                        string s = Encoding.ASCII.GetString(bclData, strStart, i - strStart);
                        strings.Add((strStart, s));
                    }
                    strStart = -1;
                }
            }
            Console.WriteLine($"  Found {strings.Count} strings (min 3 chars):");
            foreach (var (off, text) in strings)
            {
                if (text.Length > 80)
                    Console.WriteLine($"  0x{off:X4}: \"{text.Substring(0, 80)}...\"");
                else
                    Console.WriteLine($"  0x{off:X4}: \"{text}\"");
            }

            // BCA track analysis
            uint trackCount = BitConverter.ToUInt32(bcaData, 0x08);
            int maxTracks = (bcaData.Length - 0x28) / 24;
            int actualTracks = Math.Min((int)trackCount, maxTracks);

            // Look for BCA track hashes in BCL
            Console.WriteLine("\n--- BCA Track Hashes in BCL ---");
            int hashesFound = 0;
            for (int t = 0; t < actualTracks; t++)
            {
                uint hash = BitConverter.ToUInt32(bcaData, 0x28 + t * 24);
                // Search for this 4-byte value in BCL
                for (int i = 0; i <= bclData.Length - 4; i++)
                {
                    if (BitConverter.ToUInt32(bclData, i) == hash)
                    {
                        hashesFound++;
                        Console.WriteLine($"  BCA track[{t}] hash 0x{hash:X8} found at BCL offset 0x{i:X}");
                        break;
                    }
                }
            }
            Console.WriteLine($"  Hashes found in BCL: {hashesFound} / {actualTracks}");

            // VBN bone hashes in BCL
            Console.WriteLine("\n--- VBN Bone IDs in BCL ---");
            int boneIdsFound = 0;
            for (int i = 0; i < boneCount; i++)
            {
                int idOff = 0x1C + i * 76 + 72;
                uint id = vbnBE
                    ? (uint)((vbnData[idOff] << 24) | (vbnData[idOff + 1] << 16) | (vbnData[idOff + 2] << 8) | vbnData[idOff + 3])
                    : BitConverter.ToUInt32(vbnData, idOff);

                for (int j = 0; j <= bclData.Length - 4; j++)
                {
                    if (BitConverter.ToUInt32(bclData, j) == id)
                    {
                        boneIdsFound++;
                        Console.WriteLine($"  VBN bone[{i}] \"{boneNames[i]}\" id=0x{id:X8} found at BCL offset 0x{j:X}");
                        break;
                    }
                }
            }
            Console.WriteLine($"  Bone IDs found in BCL: {boneIdsFound} / {boneCount}");

            // Hex dump key regions
            Console.WriteLine("\n--- BCL First 256 bytes ---");
            HexDump(bclData, 0, Math.Min(256, bclData.Length));

            // Look for repeating struct patterns
            Console.WriteLine("\n--- BCL Structure Detection ---");
            uint bclBoneCount = BitConverter.ToUInt32(bclData, 0x08);
            Console.WriteLine($"  BCL bone count field (0x08): {bclBoneCount}");

            // The actual data might start after a small header
            // Try finding the first non-zero region after header zeros
            int dataStart = 0x30;
            for (int i = 0x0C; i < Math.Min(0x100, bclData.Length); i++)
            {
                if (bclData[i] != 0)
                {
                    dataStart = i;
                    break;
                }
            }
            Console.WriteLine($"  First non-zero data after 0x0C: offset 0x{dataStart:X}");

            // Try to find struct boundaries by looking for repeating 0x00000000 patterns
            Console.WriteLine("\n--- BCL Data around first data offset ---");
            HexDump(bclData, dataStart, Math.Min(128, bclData.Length - dataStart));

            // Check if BCL has a pointer table at the start (after header)
            Console.WriteLine("\n--- BCL Pointer Table Check ---");
            // Many formats have a table of offsets to entries
            List<uint> possiblePointers = new List<uint>();
            for (int off = 0x30; off < Math.Min(0x30 + bclBoneCount * 4, bclData.Length); off += 4)
            {
                uint val = BitConverter.ToUInt32(bclData, off);
                if (val > 0 && val < bclData.Length)
                {
                    possiblePointers.Add(val);
                    if (possiblePointers.Count <= 20)
                        Console.WriteLine($"  0x{off:X}: -> 0x{val:X} ({val})");
                }
            }
            Console.WriteLine($"  {possiblePointers.Count} possible pointers found");

            if (possiblePointers.Count > 2)
            {
                Console.WriteLine("  Pointer deltas:");
                for (int i = 1; i < Math.Min(10, possiblePointers.Count); i++)
                {
                    int delta = (int)(possiblePointers[i] - possiblePointers[i - 1]);
                    Console.WriteLine($"    [{i-1}]->[{i}]: {delta} bytes (0x{delta:X})");
                }
            }
        }

        private static void HexDump(byte[] data, int offset, int length)
        {
            for (int i = 0; i < length; i += 16)
            {
                int addr = offset + i;
                int lineLen = Math.Min(16, length - i);
                Console.Write($"  {addr:X6}: ");
                for (int j = 0; j < 16; j++)
                {
                    if (j < lineLen && addr + j < data.Length)
                        Console.Write($"{data[addr + j]:X2} ");
                    else
                        Console.Write("   ");
                    if (j == 7) Console.Write(" ");
                }
                Console.Write(" |");
                for (int j = 0; j < lineLen && addr + j < data.Length; j++)
                {
                    byte b = data[addr + j];
                    Console.Write(b >= 32 && b < 127 ? (char)b : '.');
                }
                Console.WriteLine("|");
            }
        }
    }
}
