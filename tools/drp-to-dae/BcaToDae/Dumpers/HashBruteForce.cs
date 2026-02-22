using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dumpers
{
    public static class HashBruteForce
    {
        public static void Run(string bcaPath, string vbnPath)
        {
            byte[] bca = File.ReadAllBytes(bcaPath);
            byte[] vbn = File.ReadAllBytes(vbnPath);

            Console.WriteLine("\n========== HASH BRUTE FORCE ==========\n");

            // Parse VBN bone names
            bool vbnBE = Encoding.ASCII.GetString(vbn, 0, 4) == "VBN ";
            int boneCount = vbnBE
                ? (vbn[8] << 24) | (vbn[9] << 16) | (vbn[10] << 8) | vbn[11]
                : BitConverter.ToInt32(vbn, 8);

            var boneNames = new List<string>();
            for (int i = 0; i < boneCount; i++)
            {
                int off = 0x1C + i * 76;
                int end = off;
                while (end < off + 64 && vbn[end] != 0) end++;
                boneNames.Add(Encoding.ASCII.GetString(vbn, off, end - off));
            }

            // Parse BCA track hashes
            int maxTracks = (bca.Length - 0x28) / 24;
            var bcaHashes = new HashSet<uint>();
            for (int t = 0; t < maxTracks; t++)
            {
                uint hash = BitConverter.ToUInt32(bca, 0x28 + t * 24);
                bcaHashes.Add(hash);
            }

            Console.WriteLine($"BCA has {bcaHashes.Count} unique hashes, VBN has {boneNames.Count} bones");

            // Suffixes to try
            string[] suffixes = {
                "", "_rotX", "_rotY", "_rotZ", "_rot",
                "_posX", "_posY", "_posZ", "_pos",
                "_sclX", "_sclY", "_sclZ", "_scl",
                ".rotX", ".rotY", ".rotZ", ".rot",
                ".posX", ".posY", ".posZ", ".pos",
                ".sclX", ".sclY", ".sclZ", ".scl",
                "_RotX", "_RotY", "_RotZ", "_Rot",
                "_PosX", "_PosY", "_PosZ", "_Pos",
                "_SclX", "_SclY", "_SclZ", "_Scl",
                "_rx", "_ry", "_rz", "_px", "_py", "_pz",
                "_sx", "_sy", "_sz",
                "_x", "_y", "_z", "_w",
                ".x", ".y", ".z", ".w",
                "_rotation", "_position", "_scale",
                "/rotX", "/rotY", "/rotZ",
                "/posX", "/posY", "/posZ",
                ":rotX", ":rotY", ":rotZ",
            };

            // Case variants
            Func<string, string[]> caseVariants = (s) => new[] {
                s, s.ToLower(), s.ToUpper(),
                char.ToLower(s[0]) + s.Substring(1),
            };

            int totalMatches = 0;
            Console.WriteLine("\n--- Testing CRC32(boneName + suffix) ---");

            foreach (string boneName in boneNames)
            {
                foreach (string suffix in suffixes)
                {
                    foreach (string variant in caseVariants(boneName))
                    {
                        string test = variant + suffix;
                        uint crc = Crc32(Encoding.ASCII.GetBytes(test));
                        if (bcaHashes.Contains(crc))
                        {
                            Console.WriteLine($"  MATCH! CRC32(\"{test}\") = 0x{crc:X8}");
                            totalMatches++;
                        }
                    }
                }
            }

            // Also try DRP-style CRC
            Console.WriteLine("\n--- Testing DRP-CRC(boneName + suffix) ---");
            foreach (string boneName in boneNames)
            {
                foreach (string suffix in suffixes)
                {
                    string test = boneName + suffix;
                    uint drpCrc = CalcCrcDrpStyle(test);
                    if (bcaHashes.Contains(drpCrc))
                    {
                        Console.WriteLine($"  MATCH! DRP_CRC(\"{test}\") = 0x{drpCrc:X8}");
                        totalMatches++;
                    }
                }
            }

            // Try CRC32 with null terminator
            Console.WriteLine("\n--- Testing CRC32(boneName + \\0 + suffix) ---");
            foreach (string boneName in boneNames)
            {
                foreach (string suffix in suffixes)
                {
                    if (suffix.Length == 0) continue;
                    string test = boneName + "\0" + suffix;
                    uint crc = Crc32(Encoding.ASCII.GetBytes(test));
                    if (bcaHashes.Contains(crc))
                    {
                        Console.WriteLine($"  MATCH! CRC32(\"{boneName}\\0{suffix}\") = 0x{crc:X8}");
                        totalMatches++;
                    }
                }
            }

            Console.WriteLine($"\nTotal matches: {totalMatches}");

            // Also try Smash4-style hash: Crc32(name.ToLower())
            Console.WriteLine("\n--- Testing CRC32(boneName.ToLower()) ---");
            int lowerMatches = 0;
            foreach (string boneName in boneNames)
            {
                string lower = boneName.ToLower();
                uint crc = Crc32(Encoding.ASCII.GetBytes(lower));
                if (bcaHashes.Contains(crc))
                {
                    Console.WriteLine($"  MATCH! CRC32(\"{lower}\") = 0x{crc:X8}");
                    lowerMatches++;
                }
            }
            Console.WriteLine($"Lower-case matches: {lowerMatches} / {boneNames.Count}");

            // Check if VBN boneIds might be computed via a different polynomial
            Console.WriteLine("\n--- VBN boneId analysis ---");
            for (int i = 0; i < Math.Min(10, boneCount); i++)
            {
                int idOff = 0x1C + i * 76 + 72;
                uint id = vbnBE
                    ? (uint)((vbn[idOff] << 24) | (vbn[idOff + 1] << 16) | (vbn[idOff + 2] << 8) | vbn[idOff + 3])
                    : BitConverter.ToUInt32(vbn, idOff);
                Console.WriteLine($"  [{i}] \"{boneNames[i]}\" id=0x{id:X8} CRC32=0x{Crc32(Encoding.ASCII.GetBytes(boneNames[i])):X8} CRC32_lower=0x{Crc32(Encoding.ASCII.GetBytes(boneNames[i].ToLower())):X8}");
            }

            // Check: are VBN IDs sequential or increment-based?
            Console.WriteLine("\n--- VBN boneId patterns ---");
            var ids = new List<uint>();
            for (int i = 0; i < boneCount; i++)
            {
                int idOff = 0x1C + i * 76 + 72;
                uint id = vbnBE
                    ? (uint)((vbn[idOff] << 24) | (vbn[idOff + 1] << 16) | (vbn[idOff + 2] << 8) | vbn[idOff + 3])
                    : BitConverter.ToUInt32(vbn, idOff);
                ids.Add(id);
            }
            var sorted = ids.ToList();
            sorted.Sort();
            Console.WriteLine($"  Range: 0x{sorted.First():X8} - 0x{sorted.Last():X8}");
            Console.WriteLine($"  Are sequential? {AreSequential(ids)}");
            Console.WriteLine($"  First 10 deltas:");
            for (int i = 1; i < Math.Min(10, ids.Count); i++)
            {
                long delta = (long)ids[i] - (long)ids[i-1];
                Console.WriteLine($"    [{i-1}]->[{i}]: delta={delta} (0x{delta:X})");
            }

            // Check if BCA hash matches VBN boneId with some transform (XOR, bitflip, endian swap)
            Console.WriteLine("\n--- BCA hash transforms vs VBN IDs ---");
            Console.WriteLine("  Trying: byte-swap, bit-invert, XOR with constants...");
            foreach (uint bcaHash in bcaHashes.Take(5))
            {
                uint swapped = ((bcaHash >> 24) & 0xFF) | ((bcaHash >> 8) & 0xFF00) | ((bcaHash << 8) & 0xFF0000) | ((bcaHash << 24) & 0xFF000000);
                uint inverted = ~bcaHash;

                bool directMatch = ids.Contains(bcaHash);
                bool swapMatch = ids.Contains(swapped);
                bool invertMatch = ids.Contains(inverted);

                if (directMatch || swapMatch || invertMatch)
                {
                    Console.WriteLine($"  0x{bcaHash:X8}: direct={directMatch} swap={swapMatch} invert={invertMatch}");
                }
            }
            // Full check
            int anyTransform = 0;
            foreach (uint bcaHash in bcaHashes)
            {
                uint swapped = ((bcaHash >> 24) & 0xFF) | ((bcaHash >> 8) & 0xFF00) | ((bcaHash << 8) & 0xFF0000) | ((bcaHash << 24) & 0xFF000000);
                if (ids.Contains(bcaHash) || ids.Contains(swapped) || ids.Contains(~bcaHash))
                    anyTransform++;
            }
            Console.WriteLine($"  BCA hashes matching VBN IDs with byte-swap/invert: {anyTransform} / {bcaHashes.Count}");
        }

        private static bool AreSequential(List<uint> ids)
        {
            for (int i = 1; i < ids.Count; i++)
                if (ids[i] != ids[i-1] + 1)
                    return false;
            return true;
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

        private static uint CalcCrcDrpStyle(string name)
        {
            var b = Encoding.ASCII.GetBytes(name);
            for (var i = 0; i < 4 && i < name.Length; i++)
                b[i] = (byte)(~name[i] & 0xff);
            return Crc32(b) & 0xFFFFFFFF;
        }
    }
}
