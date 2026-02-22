using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Dumpers
{
    public static class ValidationUtils
    {
        public static bool LooksLikeOffset(uint v, long fileSize, uint align = 4)
        {
            if (v == 0) return false;
            if (v >= fileSize) return false;
            if (align != 0 && (v % align) != 0) return false;
            return true;
        }

        public static void DumpStructAsU32(byte[] data, int start, int length, string label)
        {
            Console.WriteLine($"-- {label} @0x{start:X} len=0x{length:X}");
            for (int i = 0; i + 4 <= length; i += 4)
            {
                uint u = BitConverter.ToUInt32(data, start + i);
                float f = BitConverter.ToSingle(data, start + i);
                Console.WriteLine($"  +0x{i:X2}: 0x{u:X8}  ({u})  float={f:F4}");
            }
        }

        public static void HuntStrideTable(byte[] data, int stride, int maxStartsToTry = 0x400)
        {
            int fileSize = data.Length;
            Console.WriteLine($"\n--- Hunting stride table (stride=0x{stride:X}) ---");

            for (int start = 0; start < Math.Min(fileSize, maxStartsToTry); start += 4)
            {
                int structsToCheck = 5;
                if (start + stride * structsToCheck > fileSize) break;

                int plausible = 0;

                for (int s = 0; s < structsToCheck; s++)
                {
                    int off = start + s * stride;

                    ushort a = BitConverter.ToUInt16(data, off + 0);
                    ushort b = BitConverter.ToUInt16(data, off + 2);
                    ushort c = BitConverter.ToUInt16(data, off + 4);

                    bool idxish = (a < 500) || (b < 500) || (c < 500);
                    if (idxish) plausible++;
                }

                if (plausible >= 4)
                {
                    Console.WriteLine($"\nPossible stride table @0x{start:X} stride=0x{stride:X} (plausible={plausible}/{structsToCheck})");
                    DumpStructAsU32(data, start, stride, "CandidateStruct0");
                    DumpStructAsU32(data, start + stride, stride, "CandidateStruct1");
                }
            }
        }

        public static void TryReadOffsetTable(byte[] data, int start, int countGuess = 256)
        {
            long fileSize = data.Length;
            Console.WriteLine($"\n--- Offset table probe @0x{start:X} ---");

            int good = 0;
            var offsets = new List<uint>();
            
            for (int i = 0; i < countGuess; i++)
            {
                int off = start + i * 4;
                if (off + 4 > data.Length) break;
                uint v = BitConverter.ToUInt32(data, off);

                if (LooksLikeOffset(v, fileSize, 4))
                {
                    good++;
                    if (offsets.Count < 20)
                        offsets.Add(v);
                }
            }

            Console.WriteLine($"Offset-like entries in first {countGuess}: {good}");
            
            if (offsets.Count > 0)
            {
                Console.WriteLine("First offsets:");
                for (int i = 0; i < offsets.Count; i++)
                {
                    Console.WriteLine($"  [{i}]: 0x{offsets[i]:X}");
                }
            }
        }

        public static void ScanForFloatArrays(byte[] data, int start, int minLength = 8)
        {
            Console.WriteLine($"\n--- Scanning for float arrays @0x{start:X} ---");
            
            int consecutiveFloats = 0;
            int arrayStart = -1;
            
            for (int i = start; i < data.Length - 4; i += 4)
            {
                float f = BitConverter.ToSingle(data, i);
                
                if (float.IsNormal(f) || f == 0)
                {
                    if (consecutiveFloats == 0) arrayStart = i;
                    consecutiveFloats++;
                }
                else
                {
                    if (consecutiveFloats >= minLength)
                    {
                        Console.WriteLine($"Float array @0x{arrayStart:X} len={consecutiveFloats}");
                        if (consecutiveFloats <= 16)
                        {
                            for (int j = 0; j < consecutiveFloats; j++)
                            {
                                float val = BitConverter.ToSingle(data, arrayStart + j * 4);
                                Console.WriteLine($"  [{j}]: {val:F6}");
                            }
                        }
                    }
                    consecutiveFloats = 0;
                }
            }
        }
    }
}
