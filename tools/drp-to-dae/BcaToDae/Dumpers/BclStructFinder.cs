using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dumpers
{
    public static class BclStructFinder
    {
        public static void FindStructure(string bclPath, string bcaPath, string vbnPath)
        {
            byte[] bcl = File.ReadAllBytes(bclPath);
            byte[] bca = File.ReadAllBytes(bcaPath);
            byte[] vbn = File.ReadAllBytes(vbnPath);

            Console.WriteLine("\n========== BCL STRUCTURE FINDER ==========\n");

            // Parse BCA track hashes and find their BCL offsets
            int maxTracks = (bca.Length - 0x28) / 24;
            var trackBclOffsets = new List<(int trackIdx, uint hash, int bclOffset, uint keys, uint flags)>();

            for (int t = 0; t < maxTracks; t++)
            {
                int toff = 0x28 + t * 24;
                uint hash = BitConverter.ToUInt32(bca, toff);
                uint keys = BitConverter.ToUInt32(bca, toff + 8);
                uint flags = BitConverter.ToUInt32(bca, toff + 12);

                for (int i = 0; i <= bcl.Length - 4; i++)
                {
                    if (BitConverter.ToUInt32(bcl, i) == hash)
                    {
                        trackBclOffsets.Add((t, hash, i, keys, flags));
                        break;
                    }
                }
            }

            // Sort by BCL offset
            trackBclOffsets.Sort((a, b) => a.bclOffset.CompareTo(b.bclOffset));

            Console.WriteLine($"Found {trackBclOffsets.Count} BCA hashes in BCL");
            Console.WriteLine("\n--- BCA Hashes sorted by BCL offset ---");
            foreach (var (idx, hash, off, keys, flags) in trackBclOffsets.Take(30))
            {
                Console.WriteLine($"  BCL_off=0x{off:X5} BCA[{idx,3}] hash=0x{hash:X8} keys={keys,3} flags=0x{flags:X2}");
            }

            // Find gaps between consecutive hash locations
            Console.WriteLine("\n--- Gaps between consecutive hash locations ---");
            var gaps = new List<int>();
            for (int i = 1; i < trackBclOffsets.Count; i++)
            {
                int gap = trackBclOffsets[i].bclOffset - trackBclOffsets[i-1].bclOffset;
                gaps.Add(gap);
            }

            // Histogram of gap sizes
            var gapHist = gaps.GroupBy(g => g).OrderByDescending(g => g.Count()).Take(20);
            Console.WriteLine("\n  Gap size histogram (top 20):");
            foreach (var g in gapHist)
            {
                Console.WriteLine($"    {g.Key,6} bytes (0x{g.Key:X}): {g.Count()} occurrences");
            }

            // What's at the hash offset - look at surrounding data
            Console.WriteLine("\n--- Context around first few hash locations ---");
            foreach (var (idx, hash, off, keys, flags) in trackBclOffsets.Take(5))
            {
                int start = Math.Max(0, off - 16);
                int end = Math.Min(bcl.Length, off + 48);
                Console.WriteLine($"\n  BCA[{idx}] hash=0x{hash:X8} at BCL offset 0x{off:X}:");
                HexDump(bcl, start, end - start);

                // Try to interpret as a structure entry
                Console.WriteLine($"  Surrounding uint32 values:");
                for (int o = Math.Max(0, off - 16); o < Math.Min(bcl.Length - 4, off + 32); o += 4)
                {
                    uint val = BitConverter.ToUInt32(bcl, o);
                    float fval = BitConverter.ToSingle(bcl, o);
                    string marker = (o == off) ? " <-- HASH" : "";
                    if (val != 0 || o == off)
                        Console.WriteLine($"    0x{o:X5}: 0x{val:X8} ({val,12}) float={fval,12:F4}{marker}");
                }
            }

            // Theory: BCL entries are variable-width, and the hash appears at a fixed offset within each entry
            // Let's check what's at offset-4 and offset-8 from each hash location
            Console.WriteLine("\n--- Values at fixed offsets relative to hash ---");
            Console.WriteLine("  Testing: what's at hash-4, hash-8, hash+4, hash+8 for each hash");
            for (int relOff = -16; relOff <= 16; relOff += 4)
            {
                var values = new Dictionary<uint, int>();
                int validCount = 0;
                foreach (var (idx, hash, off, keys, flags) in trackBclOffsets)
                {
                    int pos = off + relOff;
                    if (pos >= 0 && pos + 4 <= bcl.Length)
                    {
                        uint val = BitConverter.ToUInt32(bcl, pos);
                        if (!values.ContainsKey(val)) values[val] = 0;
                        values[val]++;
                        validCount++;
                    }
                }
                var top = values.OrderByDescending(v => v.Value).Take(5);
                string topStr = string.Join(", ", top.Select(v => $"0x{v.Key:X}({v.Value})"));
                Console.WriteLine($"  hash{relOff:+0;-0}: unique={values.Count} top: {topStr}");
            }

            // Parse VBN bone names for final reference
            bool vbnBE = Encoding.ASCII.GetString(vbn, 0, 4) == "VBN ";
            int boneCount = vbnBE
                ? (vbn[8] << 24) | (vbn[9] << 16) | (vbn[10] << 8) | vbn[11]
                : BitConverter.ToInt32(vbn, 8);

            Console.WriteLine($"\n--- VBN has {boneCount} bones, BCL has 137 entries ---");
            Console.WriteLine($"  137 - 85 = 52 extra entries (could be IK targets, constraints, etc.)");
            Console.WriteLine($"  190 BCA tracks / 137 BCL entries = {190.0/137:F2} tracks per entry");
            Console.WriteLine($"  190 BCA tracks / 85 VBN bones = {190.0/85:F2} tracks per bone");
        }

        private static void HexDump(byte[] data, int offset, int length)
        {
            for (int i = 0; i < length; i += 16)
            {
                int addr = offset + i;
                int lineLen = Math.Min(16, length - i);
                Console.Write($"    {addr:X6}: ");
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
