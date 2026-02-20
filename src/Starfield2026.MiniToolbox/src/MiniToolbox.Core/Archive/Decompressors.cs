using System.Runtime.InteropServices;

namespace MiniToolbox.Core.Archive
{
    /// <summary>
    /// Oodle decompression via P/Invoke to oo2core_8_win64.dll.
    /// Ported from gftool Oodle.cs.
    /// </summary>
    public static class OodleDecompressor
    {
        private const string OodleLibraryPath = "oo2core_8_win64";

        [DllImport(OodleLibraryPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern long OodleLZ_Decompress(
            ref byte buffer, long bufferSize,
            ref byte result, long outputBufferSize,
            int fuzz = 1, int crc = 0, int verbosity = 0,
            long context = 0, long e = 0, long callback = 0,
            long callback_ctx = 0, long scratch = 0, long scratch_size = 0,
            int threadPhase = 3);

        public static byte[]? Decompress(ReadOnlySpan<byte> input, int decompressedLength)
        {
            var result = new byte[decompressedLength];
            var dest = result.AsSpan();
            long decoded = OodleLZ_Decompress(
                ref MemoryMarshal.GetReference(input), input.Length,
                ref MemoryMarshal.GetReference(dest), result.Length);
            return decoded > 0 ? result : null;
        }
    }

    /// <summary>
    /// LZ4 decompression. Ported from gftool LZ4.cs.
    /// </summary>
    public static class Lz4Decompressor
    {
        public static byte[] Decompress(byte[] compressed, int decompressedLength)
        {
            var dec = new byte[decompressedLength];
            int cmpPos = 0, decPos = 0;

            int GetLength(int length)
            {
                if (length == 0xF)
                {
                    byte sum;
                    do { length += (sum = compressed[cmpPos++]); } while (sum == 0xFF);
                }
                return length;
            }

            do
            {
                byte token = compressed[cmpPos++];
                int encCount = token & 0xF;
                int litCount = GetLength((token >> 4) & 0xF);

                Buffer.BlockCopy(compressed, cmpPos, dec, decPos, litCount);
                cmpPos += litCount;
                decPos += litCount;

                if (cmpPos >= compressed.Length) break;

                int back = compressed[cmpPos++] | (compressed[cmpPos++] << 8);
                encCount = GetLength(encCount) + 4;
                int encPos = decPos - back;

                if (encCount <= back)
                {
                    Buffer.BlockCopy(dec, encPos, dec, decPos, encCount);
                    decPos += encCount;
                }
                else
                {
                    while (encCount-- > 0) dec[decPos++] = dec[encPos++];
                }
            }
            while (cmpPos < compressed.Length && decPos < dec.Length);

            return dec;
        }
    }
}
