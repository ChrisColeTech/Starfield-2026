// Ported from Compressions\BLZ.cs
// Backward LZ77 decompressor

import { BinaryReader } from '../Core/BinaryReader.js';

export class BLZ {
  /**
   * Decompress data compressed with Backward LZ77 algorithm.
   * Reads from the end of the buffer backwards.
   */
  static decompress(data: BinaryReader): Buffer {
    data.seek(0);
    const input = data.readBytes(data.length);

    // Read trailer fields from end of buffer (backwards)
    let inputOffset = input.length;
    const incrementalLength = BLZ.readInt(input, inputOffset);
    inputOffset -= 4;
    const lengths = BLZ.readUInt(input, inputOffset);
    inputOffset -= 4;
    const headerLength = lengths >>> 24;
    const encodedLength = lengths & 0xffffff;
    const decodedLength = (encodedLength + incrementalLength) >>> 0;
    const totalLength = (decodedLength + (input.length - encodedLength)) >>> 0;
    inputOffset = input.length - headerLength;

    let output: Buffer = Buffer.alloc(totalLength);
    let outputOffset = 0;

    let mask = 0;
    let header = 0;

    while (outputOffset < decodedLength) {
      mask >>>= 1;
      if (mask === 0) {
        header = input[--inputOffset];
        mask = 0x80;
      }

      if ((header & mask) === 0) {
        if (outputOffset === output.length) break;
        output[outputOffset++] = input[--inputOffset];
      } else {
        const high = input[--inputOffset];
        const low = input[--inputOffset];
        const value = (low | (high << 8)) & 0xffff;
        let length = (value >> 12) + 3;
        const position = (value & 0xfff) + 3;
        while (length > 0) {
          output[outputOffset] = output[outputOffset - position];
          outputOffset++;
          if (outputOffset === output.length) break;
          length--;
        }
      }
    }

    // Invert the output buffer
    output = BLZ.invert(output);

    // Copy uncompressed prefix from input over the beginning of output
    const uncompressedLen = input.length - encodedLength;
    input.copy(output, 0, 0, uncompressedLen);

    return output;
  }

  private static invert(data: Buffer): Buffer {
    const output = Buffer.alloc(data.length);
    for (let i = 0; i < data.length; i++) {
      output[i] = data[(data.length - 1) - i];
    }
    return output;
  }

  private static readUInt(input: Buffer, offset: number): number {
    // Read 4 bytes ending at offset (backwards), big-endian order
    const a = input[offset - 1];
    const b = input[offset - 2];
    const c = input[offset - 3];
    const d = input[offset - 4];
    return (d | (c << 8) | (b << 16) | (a << 24)) >>> 0;
  }

  private static readInt(input: Buffer, offset: number): number {
    // Read 4 bytes ending at offset (backwards), big-endian order, signed
    const a = input[offset - 1];
    const b = input[offset - 2];
    const c = input[offset - 3];
    const d = input[offset - 4];
    return (d | (c << 8) | (b << 16) | (a << 24)) | 0;
  }
}
