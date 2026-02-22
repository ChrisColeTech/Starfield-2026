using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Dumpers
{
    public static class BchDumper
    {
        public static void Dump(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            Console.WriteLine("\n--- BCH (Bone Curve Header) ---");
            
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
            uint val34 = br.ReadUInt32();

            Console.WriteLine($"0x08: 0x{val08:X8} ({val08})");
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
            Console.WriteLine($"0x34: 0x{val34:X8} ({val34})");

            Console.WriteLine("\n--- Offset Validation ---");
            uint[] potentialOffsets = { val08, val0C, val10, val18, val20, val28, val30 };
            string[] offsetNames = { "0x08", "0x0C", "0x10", "0x18", "0x20", "0x28", "0x30" };
            
            for (int i = 0; i < potentialOffsets.Length; i++)
            {
                if (ValidationUtils.LooksLikeOffset(potentialOffsets[i], data.Length))
                {
                    Console.WriteLine($"{offsetNames[i]}: 0x{potentialOffsets[i]:X} - VALID OFFSET");
                }
            }

            // Hunt for stride tables with common sizes
            ValidationUtils.HuntStrideTable(data, 0x14);  // 20 bytes
            ValidationUtils.HuntStrideTable(data, 0x20);  // 32 bytes
            ValidationUtils.HuntStrideTable(data, 0x34);  // 52 bytes
        }

        public static void DeepAnalyze(byte[] data)
        {
            Console.WriteLine("\n--- BCH Deep Analysis ---");
            
            uint val08 = BitConverter.ToUInt32(data, 0x08);
            uint val34 = BitConverter.ToUInt32(data, 0x34);
            
            Console.WriteLine($"0x08: {val08} (bone count?)");
            Console.WriteLine($"0x34: {val34}");
            
            // Look for repeating structures
            int structSize = 0x34;
            int tableStart = 0x3C;
            
            Console.WriteLine($"\n--- Analyzing potential bone table at 0x{tableStart:X} ---");
            
            int count = 0;
            for (int i = tableStart; i + structSize <= data.Length; i += structSize)
            {
                count++;
                if (count <= 5)
                {
                    Console.WriteLine($"\nStruct {count - 1} @ 0x{i:X}:");
                    
                    // Read first few fields
                    ushort idx0 = BitConverter.ToUInt16(data, i);
                    ushort idx2 = BitConverter.ToUInt16(data, i + 2);
                    ushort idx4 = BitConverter.ToUInt16(data, i + 4);
                    
                    Console.WriteLine($"  Bone index candidates: {idx0}, {idx2}, {idx4}");
                    
                    // Check for offset fields
                    for (int j = 0; j < structSize; j += 4)
                    {
                        uint val = BitConverter.ToUInt32(data, i + j);
                        if (ValidationUtils.LooksLikeOffset(val, data.Length))
                        {
                            Console.WriteLine($"  +0x{j:X2}: 0x{val:X} (offset)");
                        }
                    }
                }
            }
            
            Console.WriteLine($"\nTotal structs found: {count}");
        }
    }
}
