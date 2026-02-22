using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Dumpers
{
    public static class BcsDumper
    {
        public static void Dump(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            Console.WriteLine("\n--- BCS (Bone Curve Scene/Script) ---");
            
            ms.Seek(4, SeekOrigin.Begin);
            ushort versionHi = br.ReadUInt16();
            ushort versionLo = br.ReadUInt16();
            Console.WriteLine($"Version: {versionHi}.{versionLo}");

            uint hash08 = br.ReadUInt32();
            uint offset0C = br.ReadUInt32();
            uint offset10 = br.ReadUInt32();
            uint offset14 = br.ReadUInt32();
            uint val18 = br.ReadUInt32();
            uint val1C = br.ReadUInt32();
            uint frameCount = br.ReadUInt32();
            uint val24 = br.ReadUInt32();
            uint val28 = br.ReadUInt32();
            uint val2C = br.ReadUInt32();
            uint val30 = br.ReadUInt32();
            uint nameOffset = br.ReadUInt32();

            Console.WriteLine($"0x08 hash: 0x{hash08:X8}");
            Console.WriteLine($"0x0C offset: 0x{offset0C:X}");
            Console.WriteLine($"0x10 offset: 0x{offset10:X}");
            Console.WriteLine($"0x14 offset: 0x{offset14:X}");
            Console.WriteLine($"0x18: {val18}");
            Console.WriteLine($"0x1C: {val1C}");
            Console.WriteLine($"0x20 frame count: {frameCount}");
            Console.WriteLine($"0x24: {val24}");
            Console.WriteLine($"0x30 offset: 0x{val30:X}");
            Console.WriteLine($"0x34 name offset: 0x{nameOffset:X}");

            if (ValidationUtils.LooksLikeOffset(nameOffset, data.Length))
            {
                ms.Seek(nameOffset, SeekOrigin.Begin);
                string name = ReadNullTerminatedString(br);
                Console.WriteLine($"Animation name: \"{name}\"");
            }

            Console.WriteLine("\n--- Event/Track Entries ---");
            
            if (ValidationUtils.LooksLikeOffset(offset0C, data.Length) && ValidationUtils.LooksLikeOffset(offset10, data.Length))
            {
                int entryCount = (int)((offset10 - offset0C) / 8);
                Console.WriteLine($"Entry count estimate: {entryCount}");
                
                ms.Seek(offset0C, SeekOrigin.Begin);
                for (int i = 0; i < Math.Min(20, entryCount); i++)
                {
                    uint eventOffset = br.ReadUInt32();
                    uint eventNameOffset = br.ReadUInt32();
                    
                    if (eventNameOffset > 0 && eventNameOffset < data.Length)
                    {
                        long pos = ms.Position;
                        ms.Seek(eventNameOffset, SeekOrigin.Begin);
                        string eventName = ReadNullTerminatedString(br);
                        ms.Seek(pos, SeekOrigin.Begin);
                        
                        Console.WriteLine($"  Event {i}: \"{eventName}\" (data @ 0x{eventOffset:X})");
                    }
                }
            }
        }

        private static string ReadNullTerminatedString(BinaryReader br)
        {
            var bytes = new List<byte>();
            byte b;
            long maxLen = 256;
            while ((b = br.ReadByte()) != 0 && bytes.Count < maxLen)
            {
                bytes.Add(b);
            }
            return Encoding.ASCII.GetString(bytes.ToArray());
        }
    }

    public static class BhaDumper
    {
        public static void Dump(byte[] data)
        {
            Console.WriteLine("\n--- BHA (Bone Hitbox Animation) ---");
            Console.WriteLine("(Hitbox data - not needed for animation export)");
            
            uint count10 = BitConverter.ToUInt32(data, 0x10);
            Console.WriteLine($"0x10 count: {count10}");
        }
    }
}
