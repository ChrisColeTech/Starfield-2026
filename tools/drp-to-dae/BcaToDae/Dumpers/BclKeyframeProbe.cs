using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dumpers
{
    public static class BclKeyframeProbe
    {
        public static void Probe(string bclPath, string bcaPath, string vbnPath)
        {
            byte[] bcl = File.ReadAllBytes(bclPath);
            byte[] bca = File.ReadAllBytes(bcaPath);
            byte[] vbn = File.ReadAllBytes(vbnPath);

            Console.WriteLine("\n========== BCL KEYFRAME PROBE ==========\n");

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

            // Parse BCA tracks
            int maxTracks = (bca.Length - 0x28) / 24;
            var bcaHashes = new HashSet<uint>();
            var bcaTracks = new Dictionary<uint, (int index, float defVal, uint keys, uint flags)>();
            for (int t = 0; t < maxTracks; t++)
            {
                int toff = 0x28 + t * 24;
                uint hash = BitConverter.ToUInt32(bca, toff);
                float defVal = BitConverter.ToSingle(bca, toff + 4);
                uint keys = BitConverter.ToUInt32(bca, toff + 8);
                uint flags = BitConverter.ToUInt32(bca, toff + 12);
                bcaHashes.Add(hash);
                bcaTracks[hash] = (t, defVal, keys, flags);
            }

            // Find all BCA hash positions in BCL
            var hashPositions = new List<(uint hash, int offset)>();
            for (int i = 0; i <= bcl.Length - 4; i += 4)
            {
                uint val = BitConverter.ToUInt32(bcl, i);
                if (bcaHashes.Contains(val))
                    hashPositions.Add((val, i));
            }
            hashPositions.Sort((a, b) => a.offset.CompareTo(b.offset));

            Console.WriteLine($"BCL: {bcl.Length} bytes, {BitConverter.ToUInt32(bcl, 8)} entries");
            Console.WriteLine($"BCA: {maxTracks} tracks");
            Console.WriteLine($"VBN: {boneCount} bones");
            Console.WriteLine($"Hash positions found: {hashPositions.Count}");

            // Group into bones by proximity
            var groups = new List<List<(uint hash, int offset)>>();
            var currentGroup = new List<(uint hash, int offset)>();
            for (int i = 0; i < hashPositions.Count; i++)
            {
                if (currentGroup.Count == 0)
                {
                    currentGroup.Add(hashPositions[i]);
                }
                else
                {
                    int gap = hashPositions[i].offset - currentGroup.Last().offset;
                    if (gap > 500)
                    {
                        groups.Add(currentGroup);
                        currentGroup = new List<(uint hash, int offset)> { hashPositions[i] };
                    }
                    else
                    {
                        currentGroup.Add(hashPositions[i]);
                    }
                }
            }
            if (currentGroup.Count > 0) groups.Add(currentGroup);

            Console.WriteLine($"Bone groups: {groups.Count}\n");

            // Probe several simple bones (1-2 tracks) to understand the encoding
            var targetBones = new int[] { 0, 10, 11, 12, 15, 16 }; // BASE, Hip, Neck1, Neck2, R_Shoulder, R_Arm

            foreach (int boneIdx in targetBones)
            {
                if (boneIdx >= groups.Count) break;
                var group = groups[boneIdx];
                string boneName = boneIdx < boneNames.Count ? boneNames[boneIdx] : "???";

                Console.WriteLine($"=== Bone[{boneIdx}] \"{boneName}\" — {group.Count} track(s) ===");

                for (int ti = 0; ti < group.Count; ti++)
                {
                    var (hash, hashOff) = group[ti];
                    var track = bcaTracks[hash];

                    // Determine region end: next hash in this group, or next group start, or reasonable limit
                    int regionEnd;
                    if (ti + 1 < group.Count)
                        regionEnd = group[ti + 1].offset;
                    else if (boneIdx + 1 < groups.Count)
                        regionEnd = groups[boneIdx + 1][0].offset;
                    else
                        regionEnd = Math.Min(hashOff + 800, bcl.Length);

                    int regionSize = regionEnd - hashOff;

                    Console.WriteLine($"\n  Track BCA[{track.index}]: hash=0x{hash:X8} keys={track.keys} flags=0x{track.flags:X2} def={track.defVal:F4}");
                    Console.WriteLine($"  BCL offset: 0x{hashOff:X5}, region size: {regionSize} bytes");

                    // Print raw hex of the sub-entry (first 64 bytes)
                    Console.Write("  Raw hex (first 64 bytes from hash): ");
                    int showBytes = Math.Min(64, regionSize);
                    for (int b = 0; b < showBytes; b++)
                    {
                        if (b > 0 && b % 4 == 0) Console.Write(" ");
                        Console.Write($"{bcl[hashOff + b]:X2}");
                    }
                    Console.WriteLine();

                    // Print context BEFORE the hash (16 bytes)
                    Console.Write("  Pre-hash (-16 bytes):              ");
                    int preStart = Math.Max(0, hashOff - 16);
                    for (int b = preStart; b < hashOff; b++)
                    {
                        if ((b - preStart) > 0 && (b - preStart) % 4 == 0) Console.Write(" ");
                        Console.Write($"{bcl[b]:X2}");
                    }
                    Console.WriteLine();

                    // Try reading as float32 array after the hash
                    Console.WriteLine("  As float32 (after hash):");
                    int floatStart = hashOff + 4; // skip the hash itself
                    int maxFloats = Math.Min(20, (regionEnd - floatStart) / 4);
                    for (int f = 0; f < maxFloats; f++)
                    {
                        float val = BitConverter.ToSingle(bcl, floatStart + f * 4);
                        Console.Write($"    [{f,2}] offset=0x{(floatStart + f * 4):X5} raw=0x{BitConverter.ToUInt32(bcl, floatStart + f * 4):X8} float={val:G6}");
                        
                        // Flag if it looks like a valid animation value
                        if (Math.Abs(val) < 100 && Math.Abs(val) > 0.0001f && !float.IsNaN(val) && !float.IsInfinity(val))
                            Console.Write(" ← plausible");
                        Console.WriteLine();
                    }

                    // Try reading as uint16 quantized values
                    Console.WriteLine("  As uint16 (after hash+4):");
                    int u16Start = hashOff + 8; // skip hash + 4 bytes
                    int maxU16 = Math.Min(20, (regionEnd - u16Start) / 2);
                    bool allZero = true;
                    for (int u = 0; u < maxU16; u++)
                    {
                        ushort val = BitConverter.ToUInt16(bcl, u16Start + u * 2);
                        if (val != 0) allZero = false;
                        if (u < 10 || val != 0)
                            Console.Write($"    [{u,2}] 0x{val:X4} ({val}) norm={val / 65535f:F4}");
                        if (u < 10 || val != 0) Console.WriteLine();
                    }
                    if (allZero) Console.WriteLine("    (all zeros)");

                    // Find first non-zero region after hash
                    Console.WriteLine("  First non-zero regions:");
                    int scanStart = hashOff + 4;
                    int scanEnd = Math.Min(hashOff + regionSize, bcl.Length);
                    int zeroRun = 0;
                    int dataStart = -1;
                    for (int s = scanStart; s < scanEnd; s++)
                    {
                        if (bcl[s] != 0)
                        {
                            if (dataStart == -1 || s - dataStart > 8)
                            {
                                dataStart = s;
                                Console.Write($"    Data at 0x{s:X5} (+{s - hashOff}): ");
                                int showLen = Math.Min(32, scanEnd - s);
                                for (int b = 0; b < showLen; b++)
                                {
                                    if (b > 0 && b % 4 == 0) Console.Write(" ");
                                    Console.Write($"{bcl[s + b]:X2}");
                                }
                                Console.WriteLine();

                                // Interpret this data region as floats
                                if (showLen >= 4)
                                {
                                    Console.Write($"      as float: ");
                                    for (int f = 0; f < Math.Min(8, showLen / 4); f++)
                                    {
                                        float fv = BitConverter.ToSingle(bcl, s + f * 4);
                                        Console.Write($"{fv:G6} ");
                                    }
                                    Console.WriteLine();
                                }
                            }
                        }
                    }
                    Console.WriteLine();
                }
            }

            // Summary: entry size distribution
            Console.WriteLine("\n=== Entry Size Distribution ===");
            for (int g = 0; g < groups.Count && g < 30; g++)
            {
                var group = groups[g];
                int start = group[0].offset;
                int end = g + 1 < groups.Count ? groups[g + 1][0].offset : bcl.Length;
                string bn = g < boneNames.Count ? boneNames[g] : "???";
                Console.WriteLine($"  [{g,2}] {bn,-22} offset=0x{start:X5} size={end - start,5} bytes  tracks={group.Count}  bytes/track={(end - start) / group.Count,4}");
            }
        }
    }
}
