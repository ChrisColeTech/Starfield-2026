using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Dumpers
{
    public static class BcaDumper
    {
        public static void Dump(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            Console.WriteLine("\n--- BCA (Bone Curve Animation) ---");
            
            ms.Seek(4, SeekOrigin.Begin);
            ushort versionHi = br.ReadUInt16();
            ushort versionLo = br.ReadUInt16();
            Console.WriteLine($"Version: {versionHi}.{versionLo}");

            uint trackCount = br.ReadUInt32();
            uint val0C = br.ReadUInt32();
            uint val10 = br.ReadUInt32();
            float defaultRotation = br.ReadSingle();
            uint frameCount = br.ReadUInt32();
            uint keyframeCount = br.ReadUInt32();
            uint val20 = br.ReadUInt32();
            uint val24 = br.ReadUInt32();

            Console.WriteLine($"Track count (0x08): {trackCount}");
            Console.WriteLine($"0x0C: {val0C}");
            Console.WriteLine($"0x10: 0x{val10:X8}");
            Console.WriteLine($"Default rotation (0x14): {defaultRotation:F4} rad ({defaultRotation * 180 / Math.PI:F1} deg)");
            Console.WriteLine($"Frame count (0x18): {frameCount}");
            Console.WriteLine($"Keyframe count (0x1C): {keyframeCount}");
            Console.WriteLine($"0x20: {val20}");
            Console.WriteLine($"0x24: {val24}");

            ParseTracks(data, (int)trackCount);
        }

        private static void ParseTracks(byte[] data, int trackCount)
        {
            Console.WriteLine($"\n--- Parsing {trackCount} tracks ---");
            
            int offset = 0x28;
            var tracks = new List<BcaTrack>();
            
            for (int i = 0; i < trackCount && offset + 0x18 <= data.Length; i++)
            {
                var track = new BcaTrack
                {
                    BoneHash = BitConverter.ToUInt32(data, offset),
                    DefaultValue = BitConverter.ToSingle(data, offset + 4),
                    KeyframeCount = BitConverter.ToUInt32(data, offset + 8),
                    Flags = BitConverter.ToUInt32(data, offset + 12),
                    DataOffset = BitConverter.ToUInt32(data, offset + 16),
                    DataSize = BitConverter.ToUInt32(data, offset + 20)
                };
                
                tracks.Add(track);
                
                if (i < 10)
                {
                    Console.WriteLine($"  [{i}] hash=0x{track.BoneHash:X8} def={track.DefaultValue:F4} keys={track.KeyframeCount} flags=0x{track.Flags:X} dataOff=0x{track.DataOffset:X} dataSize={track.DataSize}");
                }
                
                offset += 0x18;
            }
            
            if (tracks.Count > 10)
                Console.WriteLine($"  ... and {tracks.Count - 10} more tracks");
            
            AnalyzeTrackData(data, tracks);
        }

        private static void AnalyzeTrackData(byte[] data, List<BcaTrack> tracks)
        {
            Console.WriteLine("\n--- Analyzing track data ---");
            
            int headerSize = 0x28 + tracks.Count * 0x18;
            Console.WriteLine($"Header size: 0x{headerSize:X} ({headerSize} bytes)");
            Console.WriteLine($"File size: {data.Length} bytes");
            Console.WriteLine($"Data section: {data.Length - headerSize} bytes");
            
            if (tracks.Count > 0 && tracks[0].DataOffset > 0)
            {
                Console.WriteLine($"\nFirst track data at 0x{tracks[0].DataOffset:X}:");
                for (int i = 0; i < Math.Min(16, tracks[0].DataSize); i += 4)
                {
                    if (tracks[0].DataOffset + i + 4 > data.Length) break;
                    float f = BitConverter.ToSingle(data, (int)tracks[0].DataOffset + i);
                    uint u = BitConverter.ToUInt32(data, (int)tracks[0].DataOffset + i);
                    Console.WriteLine($"  +0x{i:X2}: 0x{u:X8} = {f:F6}");
                }
            }
            
            Console.WriteLine("\n--- Raw bytes at 0x28 ---");
            for (int i = 0; i < Math.Min(128, data.Length - 0x28); i += 16)
            {
                Console.Write($"  {0x28 + i:X4}: ");
                for (int j = 0; j < 16 && 0x28 + i + j < data.Length; j++)
                {
                    Console.Write($"{data[0x28 + i + j]:X2} ");
                }
                Console.WriteLine();
            }
        }

        public static void DeepAnalyze(byte[] data)
        {
            Console.WriteLine("\n--- BCA Deep Analysis ---");
            
            uint trackCount = BitConverter.ToUInt32(data, 0x08);
            float defaultRotation = BitConverter.ToSingle(data, 0x14);
            uint frameCount = BitConverter.ToUInt32(data, 0x18);
            uint keyframeCount = BitConverter.ToUInt32(data, 0x1C);
            
            Console.WriteLine($"Tracks: {trackCount}, Frames: {frameCount}, Keyframes: {keyframeCount}");
            Console.WriteLine($"Default rotation: {defaultRotation:F4} rad");
            
            ParseTracks(data, (int)trackCount);
            
            Console.WriteLine("\n--- Comparing with VBN hashes ---");
            Console.WriteLine("VBN bone hashes for reference:");
            Console.WriteLine("  BASE: 0x4F101F52");
            Console.WriteLine("  CENTER_RT: 0x7024616A");
            Console.WriteLine("  Spine1: 0x93136EBA");
            Console.WriteLine("  Head: 0x4ADBF3FE");
            
            uint firstTrackHash = BitConverter.ToUInt32(data, 0x28);
            Console.WriteLine($"\nFirst BCA track hash: 0x{firstTrackHash:X8}");
            Console.WriteLine("This hash does NOT match any VBN bone hash directly!");
        }
        
        private class BcaTrack
        {
            public uint BoneHash;
            public float DefaultValue;
            public uint KeyframeCount;
            public uint Flags;
            public uint DataOffset;
            public uint DataSize;
        }
    }
}
