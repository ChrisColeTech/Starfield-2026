using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dumpers
{
    public static class HashComparer
    {
        public static void Compare(string bcaPath, string vbnPath)
        {
            byte[] bcaData = File.ReadAllBytes(bcaPath);
            byte[] vbnData = File.ReadAllBytes(vbnPath);

            Console.WriteLine("\n========== HASH COMPARISON ==========\n");

            // Parse VBN
            bool isBigEndian = Encoding.ASCII.GetString(vbnData, 0, 4) == "VBN ";
            int boneCount = isBigEndian
                ? (vbnData[8] << 24) | (vbnData[9] << 16) | (vbnData[10] << 8) | vbnData[11]
                : BitConverter.ToInt32(vbnData, 8);

            Console.WriteLine($"VBN: {boneCount} bones, {(isBigEndian ? "Big" : "Little")}-endian");

            var vbnBones = new List<(string name, uint id, int index)>();
            int headerSize = 0x1C;
            int boneHeaderSize = 76;

            for (int i = 0; i < boneCount; i++)
            {
                int entryStart = headerSize + i * boneHeaderSize;
                int nameEnd = entryStart;
                while (nameEnd < entryStart + 64 && vbnData[nameEnd] != 0) nameEnd++;
                string name = Encoding.ASCII.GetString(vbnData, entryStart, nameEnd - entryStart);

                int idOff = entryStart + 72;
                uint id;
                if (isBigEndian)
                    id = (uint)((vbnData[idOff] << 24) | (vbnData[idOff + 1] << 16) | (vbnData[idOff + 2] << 8) | vbnData[idOff + 3]);
                else
                    id = BitConverter.ToUInt32(vbnData, idOff);

                vbnBones.Add((name, id, i));
            }

            // Compute various CRC32 variants
            Console.WriteLine("\n--- VBN Bones: ID vs CRC32 ---");
            var crc32Map = new Dictionary<uint, string>();
            var vbnIdMap = new Dictionary<uint, string>();
            foreach (var (name, id, index) in vbnBones)
            {
                uint crc = Crc32(Encoding.ASCII.GetBytes(name));
                bool match = crc == id;
                crc32Map[crc] = name;
                vbnIdMap[id] = name;
                Console.WriteLine($"  [{index,2}] \"{name,-20}\" id=0x{id:X8} crc32=0x{crc:X8} {(match ? "MATCH!" : "")}");
            }

            // Parse BCA tracks
            uint trackCount = BitConverter.ToUInt32(bcaData, 0x08);
            uint frameCount = BitConverter.ToUInt32(bcaData, 0x18);
            int maxTracks = (bcaData.Length - 0x28) / 24;
            int actualTracks = Math.Min((int)trackCount, maxTracks);
            Console.WriteLine($"\nBCA: {trackCount} tracks (header), {actualTracks} tracks (fit in file), {frameCount} frames");

            var bcaTracks = new List<(uint hash, float defVal, uint keys, uint flags, uint dataOff, uint dataSize, int index)>();
            for (int i = 0; i < actualTracks; i++)
            {
                int off = 0x28 + i * 24;
                uint hash = BitConverter.ToUInt32(bcaData, off);
                float defVal = BitConverter.ToSingle(bcaData, off + 4);
                uint keys = BitConverter.ToUInt32(bcaData, off + 8);
                uint flags = BitConverter.ToUInt32(bcaData, off + 12);
                uint dataOff = BitConverter.ToUInt32(bcaData, off + 16);
                uint dataSize = BitConverter.ToUInt32(bcaData, off + 20);
                bcaTracks.Add((hash, defVal, keys, flags, dataOff, dataSize, i));
            }

            // Direct VBN ID matching
            Console.WriteLine("\n--- Direct BCA hash → VBN bone ID matches ---");
            int directMatches = 0;
            foreach (var track in bcaTracks)
            {
                if (vbnIdMap.ContainsKey(track.hash))
                {
                    directMatches++;
                    Console.WriteLine($"  BCA[{track.index}] 0x{track.hash:X8} = VBN \"{vbnIdMap[track.hash]}\" keys={track.keys} flags=0x{track.flags:X}");
                }
            }
            Console.WriteLine($"Direct VBN ID matches: {directMatches} / {bcaTracks.Count}");

            // CRC32 matching
            Console.WriteLine("\n--- BCA hash → CRC32(bone name) matches ---");
            int crcMatches = 0;
            foreach (var track in bcaTracks)
            {
                if (crc32Map.ContainsKey(track.hash))
                {
                    crcMatches++;
                    Console.WriteLine($"  BCA[{track.index}] 0x{track.hash:X8} = CRC32(\"{crc32Map[track.hash]}\") keys={track.keys} flags=0x{track.flags:X}");
                }
            }
            Console.WriteLine($"CRC32 matches: {crcMatches} / {bcaTracks.Count}");

            // Try DRP-style CRC (inverted first 4 bytes)
            Console.WriteLine("\n--- BCA hash → DRP-style CRC matches ---");
            var drpCrcMap = new Dictionary<uint, string>();
            foreach (var (name, id, index) in vbnBones)
            {
                uint drpCrc = CalcCrcDrpStyle(name);
                drpCrcMap[drpCrc] = name;
            }
            int drpMatches = 0;
            foreach (var track in bcaTracks)
            {
                if (drpCrcMap.ContainsKey(track.hash))
                {
                    drpMatches++;
                    Console.WriteLine($"  BCA[{track.index}] 0x{track.hash:X8} = DRP_CRC(\"{drpCrcMap[track.hash]}\")");
                }
            }
            Console.WriteLine($"DRP-style CRC matches: {drpMatches} / {bcaTracks.Count}");

            // Flag analysis
            Console.WriteLine("\n--- Flag Distribution ---");
            var flagGroups = bcaTracks.GroupBy(t => t.flags).OrderBy(g => g.Key);
            foreach (var g in flagGroups)
            {
                Console.WriteLine($"  0x{g.Key:X2}: {g.Count()} tracks (binary: {Convert.ToString(g.Key, 2).PadLeft(8, '0')})");
            }

            // Analyze flag bit meanings
            Console.WriteLine("\n--- Flag Bit Analysis ---");
            Console.WriteLine("  Bit pattern analysis (flags vs keyframe count):");
            var animatedTracks = bcaTracks.Where(t => t.keys > 0).ToList();
            var staticTracks = bcaTracks.Where(t => t.keys == 0).ToList();
            Console.WriteLine($"  Animated tracks: {animatedTracks.Count}, Static tracks: {staticTracks.Count}");
            Console.WriteLine($"  Animated track flags: {string.Join(", ", animatedTracks.Select(t => $"0x{t.flags:X}").Distinct())}");
            Console.WriteLine($"  Static track flags: {string.Join(", ", staticTracks.Select(t => $"0x{t.flags:X}").Distinct())}");

            // Theory: tracks are per-component, grouped by bone
            Console.WriteLine("\n--- Component Grouping Theory ---");
            // Check if 192 tracks / 85 bones = ~2.26 tracks/bone
            // Maybe tracks for: rotX, rotY, rotZ per bone (but 192/3 = 64 bones, not 85)
            // Or: rot per bone (quaternion = 1 track) + pos (1 track) = ~2 per bone
            Console.WriteLine($"  Tracks/bones ratio: {(float)bcaTracks.Count / boneCount:F2}");
            Console.WriteLine($"  192/1 = {192/1} (one track per something)");
            Console.WriteLine($"  192/2 = {192/2} bones (2 tracks each: rot+pos?)");
            Console.WriteLine($"  192/3 = {192/3} (3 components: X,Y,Z)");

            // Print all tracks with analysis
            Console.WriteLine("\n--- All BCA Tracks ---");
            foreach (var track in bcaTracks)
            {
                string matchInfo = "";
                if (vbnIdMap.ContainsKey(track.hash))
                    matchInfo = $" VBN=\"{vbnIdMap[track.hash]}\"";
                else if (crc32Map.ContainsKey(track.hash))
                    matchInfo = $" CRC32=\"{crc32Map[track.hash]}\"";
                else if (drpCrcMap.ContainsKey(track.hash))
                    matchInfo = $" DRP=\"{drpCrcMap[track.hash]}\"";

                string animInfo = track.keys > 0 ? $" ANIMATED(keys={track.keys})" : "";
                Console.WriteLine($"  [{track.index,3}] 0x{track.hash:X8} def={track.defVal,10:F4} flags=0x{track.flags:X2} dOff={track.dataOff} dSz={track.dataSize}{matchInfo}{animInfo}");
            }
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
