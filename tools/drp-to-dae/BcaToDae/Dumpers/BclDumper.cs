using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Dumpers
{
    public static class BclDumper
    {
        public static void Dump(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            Console.WriteLine("\n--- BCL (Bone Curve List) ---");
            
            ms.Seek(4, SeekOrigin.Begin);
            ushort versionHi = br.ReadUInt16();
            ushort versionLo = br.ReadUInt16();
            Console.WriteLine($"Version: {versionHi}.{versionLo}");

            uint val08 = br.ReadUInt32();
            uint val0C = br.ReadUInt32();
            uint val10 = br.ReadUInt32();
            uint val14 = br.ReadUInt32();
            uint val18 = br.ReadUInt32();
            uint val1C = br.ReadUInt32();
            uint val20 = br.ReadUInt32();
            uint val24 = br.ReadUInt32();
            uint val28 = br.ReadUInt32();
            uint val2C = br.ReadUInt32();
            uint val30 = br.ReadUInt32();

            Console.WriteLine($"0x08: 0x{val08:X8} ({val08}) - likely bone count");
            Console.WriteLine($"0x0C: 0x{val0C:X8} ({val0C})");
            Console.WriteLine($"0x10: 0x{val10:X8} ({val10})");
            Console.WriteLine($"0x14: 0x{val14:X8} ({val14})");
            Console.WriteLine($"0x18: 0x{val18:X8} ({val18})");
            Console.WriteLine($"0x1C: 0x{val1C:X8} ({val1C})");
            Console.WriteLine($"0x20: 0x{val20:X8} ({val20})");
            Console.WriteLine($"0x24: 0x{val24:X8} ({val24})");
            Console.WriteLine($"0x28: 0x{val28:X8} ({val28})");
            Console.WriteLine($"0x2C: 0x{val2C:X8} ({val2C})");
            Console.WriteLine($"0x30: 0x{val30:X8} ({val30})");

            Console.WriteLine("\n--- Offset Validation ---");
            uint[] potentialOffsets = { val0C, val10, val14, val18, val20, val28, val30 };
            string[] offsetNames = { "0x0C", "0x10", "0x14", "0x18", "0x20", "0x28", "0x30" };
            
            for (int i = 0; i < potentialOffsets.Length; i++)
            {
                if (ValidationUtils.LooksLikeOffset(potentialOffsets[i], data.Length))
                {
                    Console.WriteLine($"{offsetNames[i]}: 0x{potentialOffsets[i]:X} - VALID OFFSET");
                }
            }

            int boneCount = (int)val08;
            if (boneCount > 0 && boneCount < 500)
            {
                Console.WriteLine($"\n--- Attempting bone list parse ({boneCount} bones) ---");
                
                // Try different struct sizes
                int[] structSizes = { 8, 12, 16, 20, 24 };
                foreach (int structSize in structSizes)
                {
                    int tableStart = 0x14;
                    int expectedSize = tableStart + (boneCount * structSize);
                    
                    if (expectedSize <= data.Length)
                    {
                        Console.WriteLine($"\nTrying struct size {structSize}:");
                        
                        bool foundNames = false;
                        for (int i = 0; i < Math.Min(5, boneCount); i++)
                        {
                            int entryOff = tableStart + (i * structSize);
                            uint v0 = BitConverter.ToUInt32(data, entryOff);
                            uint v1 = BitConverter.ToUInt32(data, entryOff + 4);
                            
                            Console.WriteLine($"  [{i}]: 0x{v0:X8} 0x{v1:X8}");
                            
                            if (v1 > 0 && v1 < data.Length)
                            {
                                string name = TryReadString(data, (int)v1);
                                if (!string.IsNullOrEmpty(name) && name.Length > 2)
                                {
                                    Console.WriteLine($"       -> \"{name}\"");
                                    foundNames = true;
                                }
                            }
                        }
                        
                        if (foundNames)
                        {
                            Console.WriteLine($"  ^^^ Likely correct struct size: {structSize}");
                            break;
                        }
                    }
                }
            }
        }

        private static string TryReadString(byte[] data, int offset)
        {
            if (offset < 0 || offset >= data.Length) return "";
            
            int len = 0;
            while (offset + len < data.Length && data[offset + len] != 0 && len < 64)
                len++;
            
            if (len == 0) return "";
            return Encoding.ASCII.GetString(data, offset, len);
        }

        public static void DeepAnalyze(byte[] data)
        {
            Console.WriteLine("\n--- BCL Deep Analysis ---");
            
            int boneCount = (int)BitConverter.ToUInt32(data, 0x08);
            Console.WriteLine($"Bone count: {boneCount}");
            
            // Try to find string table
            Console.WriteLine("\n--- Searching for string table ---");
            
            int stringTableStart = -1;
            for (int i = 0x100; i < data.Length - 100; i++)
            {
                // Look for typical bone name patterns
                if (i + 4 < data.Length)
                {
                    string s = TryReadString(data, i);
                    if (s.Length >= 3 && IsLikelyBoneName(s))
                    {
                        stringTableStart = i;
                        Console.WriteLine($"Possible string table at 0x{i:X}: \"{s}\"");
                        break;
                    }
                }
            }
            
            if (stringTableStart > 0)
            {
                Console.WriteLine("\nStrings found:");
                int offset = stringTableStart;
                int count = 0;
                while (offset < data.Length && count < 50)
                {
                    string s = TryReadString(data, offset);
                    if (s.Length >= 2)
                    {
                        Console.WriteLine($"  0x{offset:X}: \"{s}\"");
                        offset += s.Length + 1;
                        count++;
                    }
                    else
                    {
                        offset++;
                    }
                }
            }
            
            // Analyze the actual data structure
            Console.WriteLine("\n--- Data structure analysis ---");
            
            // Look for patterns in first 0x100 bytes after header
            for (int off = 0x14; off < Math.Min(0x100, data.Length); off += 4)
            {
                uint val = BitConverter.ToUInt32(data, off);
                if (val != 0)
                {
                    Console.WriteLine($"  0x{off:X2}: 0x{val:X8} ({val})");
                }
            }
        }

        private static bool IsLikelyBoneName(string s)
        {
            // Common bone name patterns
            string[] patterns = { "Hip", "Spine", "Head", "Arm", "Leg", "Hand", "Foot", "Bone", "Joint" };
            foreach (var p in patterns)
            {
                if (s.ToLower().Contains(p.ToLower())) return true;
            }
            return false;
        }
    }
}
