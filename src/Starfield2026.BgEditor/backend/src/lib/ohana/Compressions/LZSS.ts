// Ported from Compressions\LZSS.cs
// Dictionary-based LZSS decompressor (used by IECP containers)

import { BinaryReader } from '../Core/BinaryReader.js';

export class LZSS {
  /**
   * Decompress LZSS data from a BinaryReader.
   * Reads from current position to end of stream.
   */
  static decompress(data: BinaryReader, decodedLength: number): Buffer {
    const input = data.readBytes(data.length - data.position);
    let inputOffset = 0;

    const output = Buffer.alloc(decodedLength);
    const dictionary = Buffer.alloc(4096);

    let outputOffset = 0;
    let dictionaryOffset = 4078;

    let mask = 0x80;
    let header = 0;

    while (outputOffset < decodedLength) {
      mask <<= 1;
      if (mask === 0x100) {
        header = input[inputOffset++];
        mask = 1;
      }

      if ((header & mask) > 0) {
        if (outputOffset === output.length) break;
        output[outputOffset++] = input[inputOffset];
        dictionary[dictionaryOffset] = input[inputOffset++];
        dictionaryOffset = (dictionaryOffset + 1) & 0xfff;
      } else {
        const value = input[inputOffset++] | (input[inputOffset++] << 8);
        let length = ((value >> 8) & 0xf) + 3;
        let position = ((value & 0xf000) >> 4) | (value & 0xff);

        while (length > 0) {
          dictionary[dictionaryOffset] = dictionary[position];
          output[outputOffset++] = dictionary[dictionaryOffset];
          dictionaryOffset = (dictionaryOffset + 1) & 0xfff;
          position = (position + 1) & 0xfff;
          length--;
        }
      }
    }

    return output;
  }
}
