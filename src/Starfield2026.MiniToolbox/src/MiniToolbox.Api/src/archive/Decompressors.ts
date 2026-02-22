import koffi from 'koffi';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const _oodleDllPath = path.resolve(__dirname, '..', 'lib', 'oo2core_8_win64.dll');

let oodleLib: ReturnType<typeof koffi.load> | null = null;

/**
 * Decompression algorithms for TRPAK archives.
 * Ported from C# Decompressors.cs
 */

/**
 * Oodle decompression via native library.
 * Implemented with Koffi matching C# signature.
 */
export class OodleDecompressor {
    static Decompress(input: Buffer, decompressedLength: number): Buffer | null {
        try {
            if (!oodleLib) {
                oodleLib = koffi.load(_oodleDllPath);
            }
            const OodleLZ_Decompress = oodleLib.func(`int64_t __cdecl OodleLZ_Decompress(
                void *buffer, int64_t bufferSize,
                void *result, int64_t outputBufferSize,
                int32_t fuzz,
                int32_t crc,
                int32_t verbosity,
                int64_t context,
                int64_t e,
                int64_t callback,
                int64_t callback_ctx,
                int64_t scratch,
                int64_t scratch_size,
                int32_t threadPhase
            )`);

            const resultBuf = Buffer.alloc(decompressedLength);
            const decodedSize = OodleLZ_Decompress(
                input,
                input.length,
                resultBuf,
                resultBuf.length,
                1,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                3
            );

            if (decodedSize > 0) {
                return resultBuf.slice(0, Number(decodedSize));
            }
            return null;
        } catch (e) {
            console.error('Oodle decompression error:', e);
            return null;
        }
    }
}

/**
 * LZ4 decompression.
 * Ported from gftool LZ4.cs.
 */
export class Lz4Decompressor {
    static Decompress(compressed: Buffer, decompressedLength: number): Buffer {
        const dec = Buffer.alloc(decompressedLength);
        let cmpPos = 0;
        let decPos = 0;

        const getLength = (length: number): number => {
            if (length === 0xF) {
                let sum: number;
                do {
                    length += (sum = compressed[cmpPos++]);
                } while (sum === 0xFF);
            }
            return length;
        };

        do {
            const token = compressed[cmpPos++];
            let encCount = token & 0xF;
            const litCount = getLength((token >> 4) & 0xF);

            // Copy literal data
            dec.set(compressed.subarray(cmpPos, cmpPos + litCount), decPos);
            cmpPos += litCount;
            decPos += litCount;

            if (cmpPos >= compressed.length) break;

            // Read back-reference offset
            const back = compressed[cmpPos++] | (compressed[cmpPos++] << 8);
            encCount = getLength(encCount) + 4;
            const encPos = decPos - back;

            if (encCount <= back) {
                // Fast path: no overlap, use buffer copy
                dec.set(dec.subarray(encPos, encPos + encCount), decPos);
                decPos += encCount;
            } else {
                // Slow path: overlapping copy, byte-by-byte
                for (let i = 0; i < encCount; i++) {
                    dec[decPos++] = dec[encPos + i];
                }
            }
        } while (cmpPos < compressed.length && decPos < dec.length);

        return dec;
    }
}
