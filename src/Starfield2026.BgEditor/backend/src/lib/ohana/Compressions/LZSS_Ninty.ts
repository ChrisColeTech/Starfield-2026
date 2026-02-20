// Ported from Compressions\LZSS_Ninty.cs
// Sliding-window LZSS decompressor (Nintendo variant, type 0x11)

export class LZSS_Ninty {
  /**
   * Decompress a buffer that starts with a 4-byte header:
   *   byte 0 = 0x11 (compression type)
   *   bytes 1-3 = decodedLength (little-endian 24-bit)
   * Remaining bytes are compressed payload.
   */
  static decompress(buf: Buffer): Buffer {
    const decodedLength = buf.readUInt32LE(0) >>> 8;
    return LZSS_Ninty.decompressRaw(buf.subarray(4), decodedLength);
  }

  /**
   * Decompress raw LZSS_Ninty payload (no header) to a buffer of the given length.
   */
  static decompressRaw(input: Buffer, decodedLength: number): Buffer {
    let inputOffset = 0;
    const output = Buffer.alloc(decodedLength);
    let outputOffset = 0;

    let mask = 0;
    let header = 0;

    while (outputOffset < decodedLength) {
      if ((mask >>>= 1) === 0) {
        header = input[inputOffset++];
        mask = 0x80;
      }

      if ((header & mask) === 0) {
        output[outputOffset++] = input[inputOffset++];
      } else {
        let byte1: number, byte2: number, byte3: number, byte4: number;
        byte1 = input[inputOffset++];
        let position: number, length: number;
        switch (byte1 >> 4) {
          case 0:
            byte2 = input[inputOffset++];
            byte3 = input[inputOffset++];

            position = ((byte2 & 0xf) << 8) | byte3;
            length = (((byte1 & 0xf) << 4) | (byte2 >> 4)) + 0x11;
            break;
          case 1:
            byte2 = input[inputOffset++];
            byte3 = input[inputOffset++];
            byte4 = input[inputOffset++];

            position = ((byte3 & 0xf) << 8) | byte4;
            length = (((byte1 & 0xf) << 12) | (byte2 << 4) | (byte3 >> 4)) + 0x111;
            break;
          default:
            byte2 = input[inputOffset++];

            position = ((byte1 & 0xf) << 8) | byte2;
            length = (byte1 >> 4) + 1;
            break;
        }
        position++;

        while (length > 0) {
          output[outputOffset] = output[outputOffset - position];
          outputOffset++;
          length--;
        }
      }
    }

    return output;
  }
}
