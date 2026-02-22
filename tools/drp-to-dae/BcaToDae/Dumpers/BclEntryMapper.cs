using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dumpers
{
    public static class BclEntryMapper
    {
        public static void Map(string bclPath, string bcaPath, string vbnPath)
        {
            byte[] bcl = File.ReadAllBytes(bclPath);
            byte[] bca = File.ReadAllBytes(bcaPath);
            byte[] vbn = File.ReadAllBytes(vbnPath);

            Console.WriteLine("\n========== BCL ENTRY MAPPER ==========\n");

            // Parse VBN
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
            var bcaHashSet = new HashSet<uint>();
            var bcaTracksByHash = new Dictionary<uint, (int index, float defVal, uint keys, uint flags)>();
            for (int t = 0; t < maxTracks; t++)
            {
                int toff = 0x28 + t * 24;
                uint hash = BitConverter.ToUInt32(bca, toff);
                float defVal = BitConverter.ToSingle(bca, toff + 4);
                uint keys = BitConverter.ToUInt32(bca, toff + 8);
                uint flags = BitConverter.ToUInt32(bca, toff + 12);
                bcaHashSet.Add(hash);
                bcaTracksByHash[hash] = (t, defVal, keys, flags);
            }

            // BCL header
            uint bclEntryCount = BitConverter.ToUInt32(bcl, 0x08);
            Console.WriteLine($"BCL: {bcl.Length} bytes, {bclEntryCount} entries");
            Console.WriteLine($"VBN: {boneCount} bones");
            Console.WriteLine($"BCA: {maxTracks} tracks, {bcaHashSet.Count} unique hashes");

            // Strategy: scan the BCL for all uint32 values that match BCA hashes
            // Then map them to BCL "entries" based on position
            var hashLocations = new List<(uint hash, int offset)>();
            for (int i = 0; i <= bcl.Length - 4; i += 4)
            {
                uint val = BitConverter.ToUInt32(bcl, i);
                if (bcaHashSet.Contains(val))
                {
                    hashLocations.Add((val, i));
                }
            }

            Console.WriteLine($"\nFound {hashLocations.Count} BCA hash occurrences in BCL");

            // What offset does the actual data start?  The header seems to go from 0 to ~0x50
            // Then there's metadata for each entry. Let's look at the structure more carefully.
            
            // The BCL has 137 entries. If we divide the file into 137 equal regions:
            float avgEntrySize = (float)(bcl.Length - 0x34) / bclEntryCount; // subtract header
            Console.WriteLine($"Average entry size (after 0x34 header): {avgEntrySize:F1} bytes");

            // Let's try to find entry boundaries by looking at pattern breaks
            // The BCL seems to have: header (0x34), then 137 variable-size entries
            // Each entry likely starts with some metadata, then contains hash references

            // Alternative approach: since we know the dominant gap between hashes is 0xBC (188 bytes),
            // and this could be the stride of sub-entries within each bone's data,
            // let's look at what's between hashes

            // Group hashes by proximity (entries within same bone)
            Console.WriteLine("\n--- Hash groups by proximity ---");
            var groups = new List<List<(uint hash, int offset)>>();
            var currentGroup = new List<(uint hash, int offset)>();

            for (int i = 0; i < hashLocations.Count; i++)
            {
                if (currentGroup.Count == 0)
                {
                    currentGroup.Add(hashLocations[i]);
                }
                else
                {
                    int gap = hashLocations[i].offset - currentGroup.Last().offset;
                    // If gap > 1000, probably a new entry
                    if (gap > 500)
                    {
                        groups.Add(currentGroup);
                        currentGroup = new List<(uint hash, int offset)> { hashLocations[i] };
                    }
                    else
                    {
                        currentGroup.Add(hashLocations[i]);
                    }
                }
            }
            if (currentGroup.Count > 0) groups.Add(currentGroup);

            Console.WriteLine($"Found {groups.Count} groups (potential bone entries)");
            Console.WriteLine();

            for (int g = 0; g < groups.Count; g++)
            {
                var group = groups[g];
                string boneName = g < boneNames.Count ? boneNames[g] : "???";
                Console.Write($"  Group[{g,3}] ({boneName,-22}) offset=0x{group[0].offset:X5} hashes={group.Count}:");

                foreach (var (hash, off) in group)
                {
                    var track = bcaTracksByHash[hash];
                    string flagStr = $"0x{track.flags:X2}";
                    Console.Write($" [{track.index}:{flagStr}]");
                }
                Console.WriteLine();
            }

            // Now look at the flag pattern per group
            Console.WriteLine("\n--- Flag pattern analysis ---");
            Console.WriteLine("  Flag low 2 bits meaning (guess):");
            Console.WriteLine("  Flags low nibble: 0x02=rotX? 0x06=rotY? 0x0A=rotZ? 0x0E=posX? 0x12=posY? 0x16=posZ? 0x1A=sclX? 0x1E=sclY? 0x22=sclZ?");
            Console.WriteLine();

            // Study flag distribution per group
            foreach (int g in new[] {0, 1, 2, 3, 4, 8, 10, 14})
            {
                if (g >= groups.Count) break;
                var group = groups[g];
                string boneName = g < boneNames.Count ? boneNames[g] : "???";
                Console.Write($"  Group[{g}] ({boneName}): ");
                foreach (var (hash, off) in group)
                {
                    var track = bcaTracksByHash[hash];
                    Console.Write($"flags=0x{track.flags:X2}(keys={track.keys}) ");
                }
                Console.WriteLine();
            }
        }
    }
}
