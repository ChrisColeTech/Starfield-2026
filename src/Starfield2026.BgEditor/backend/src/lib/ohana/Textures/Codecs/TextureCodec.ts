import { TextureUtils } from '../TextureUtils.js';

// ---------------------------------------------------------------------------
// OTextureFormat enum -- mirrors RenderBase.OTextureFormat
// We re-export it here so callers can import from TextureCodec directly,
// but consumers can also use the one from RenderBase.
// ---------------------------------------------------------------------------
export enum OTextureFormat {
  rgba8 = 0,
  rgb8 = 1,
  rgba5551 = 2,
  rgb565 = 3,
  rgba4 = 4,
  la8 = 5,
  hilo8 = 6,
  l8 = 7,
  a8 = 8,
  la4 = 9,
  l4 = 0xa,
  a4 = 0xb,
  etc1 = 0xc,
  etc1a4 = 0xd,
  dontCare = 0xe,
}

// ---------------------------------------------------------------------------
// BitmapData -- simple RGBA pixel container (replaces System.Drawing.Bitmap)
// ---------------------------------------------------------------------------
export class BitmapData {
  width: number;
  height: number;
  data: Buffer; // RGBA pixels, 4 bytes per pixel, row-major

  constructor(w: number, h: number) {
    this.width = w;
    this.height = h;
    this.data = Buffer.alloc(w * h * 4);
  }

  setPixel(x: number, y: number, r: number, g: number, b: number, a: number = 255): void {
    const off = (y * this.width + x) * 4;
    this.data[off] = r & 0xff;
    this.data[off + 1] = g & 0xff;
    this.data[off + 2] = b & 0xff;
    this.data[off + 3] = a & 0xff;
  }

  getPixel(x: number, y: number): { r: number; g: number; b: number; a: number } {
    const off = (y * this.width + x) * 4;
    return {
      r: this.data[off],
      g: this.data[off + 1],
      b: this.data[off + 2],
      a: this.data[off + 3],
    };
  }
}

// ---------------------------------------------------------------------------
// TextureCodec -- PICA200 texture decode / encode
// ---------------------------------------------------------------------------
export class TextureCodec {
  private static readonly tileOrder: number[] = [
    0, 1, 8, 9, 2, 3, 10, 11, 16, 17, 24, 25, 18, 19, 26, 27,
    4, 5, 12, 13, 6, 7, 14, 15, 20, 21, 28, 29, 22, 23, 30, 31,
    32, 33, 40, 41, 34, 35, 42, 43, 48, 49, 56, 57, 50, 51, 58, 59,
    36, 37, 44, 45, 38, 39, 46, 47, 52, 53, 60, 61, 54, 55, 62, 63,
  ];

  private static readonly etc1LUT: number[][] = [
    [2, 8, -2, -8],
    [5, 17, -5, -17],
    [9, 29, -9, -29],
    [13, 42, -13, -42],
    [18, 60, -18, -60],
    [24, 80, -24, -80],
    [33, 106, -33, -106],
    [47, 183, -47, -183],
  ];

  // -----------------------------------------------------------------------
  // decode
  // -----------------------------------------------------------------------
  static decode(data: Buffer, width: number, height: number, format: OTextureFormat): BitmapData {
    const output = Buffer.alloc(width * height * 4);
    let dataOffset = 0;
    let toggle = false;

    switch (format) {
      case OTextureFormat.rgba8:
        for (let tY = 0; tY < height / 8; tY++) {
          for (let tX = 0; tX < width / 8; tX++) {
            for (let pixel = 0; pixel < 64; pixel++) {
              const x = this.tileOrder[pixel] % 8;
              const y = (this.tileOrder[pixel] - x) / 8;
              const outputOffset = ((tX * 8) + x + ((tY * 8 + y) * width)) * 4;
              // C# layout: ARGB in data (A at dataOffset, RGB at dataOffset+1)
              data.copy(output, outputOffset, dataOffset + 1, dataOffset + 4);
              output[outputOffset + 3] = data[dataOffset];
              dataOffset += 4;
            }
          }
        }
        break;

      case OTextureFormat.rgb8:
        for (let tY = 0; tY < height / 8; tY++) {
          for (let tX = 0; tX < width / 8; tX++) {
            for (let pixel = 0; pixel < 64; pixel++) {
              const x = this.tileOrder[pixel] % 8;
              const y = (this.tileOrder[pixel] - x) / 8;
              const outputOffset = ((tX * 8) + x + ((tY * 8 + y) * width)) * 4;
              data.copy(output, outputOffset, dataOffset, dataOffset + 3);
              output[outputOffset + 3] = 0xff;
              dataOffset += 3;
            }
          }
        }
        break;

      case OTextureFormat.rgba5551:
        for (let tY = 0; tY < height / 8; tY++) {
          for (let tX = 0; tX < width / 8; tX++) {
            for (let pixel = 0; pixel < 64; pixel++) {
              const x = this.tileOrder[pixel] % 8;
              const y = (this.tileOrder[pixel] - x) / 8;
              const outputOffset = ((tX * 8) + x + ((tY * 8 + y) * width)) * 4;
              const pixelData = data[dataOffset] | (data[dataOffset + 1] << 8);
              let r = ((pixelData >> 1) & 0x1f) << 3;
              let g = ((pixelData >> 6) & 0x1f) << 3;
              let b = ((pixelData >> 11) & 0x1f) << 3;
              const a = (pixelData & 1) * 0xff;
              output[outputOffset] = (r | (r >> 5)) & 0xff;
              output[outputOffset + 1] = (g | (g >> 5)) & 0xff;
              output[outputOffset + 2] = (b | (b >> 5)) & 0xff;
              output[outputOffset + 3] = a & 0xff;
              dataOffset += 2;
            }
          }
        }
        break;

      case OTextureFormat.rgb565:
        for (let tY = 0; tY < height / 8; tY++) {
          for (let tX = 0; tX < width / 8; tX++) {
            for (let pixel = 0; pixel < 64; pixel++) {
              const x = this.tileOrder[pixel] % 8;
              const y = (this.tileOrder[pixel] - x) / 8;
              const outputOffset = ((tX * 8) + x + ((tY * 8 + y) * width)) * 4;
              const pixelData = data[dataOffset] | (data[dataOffset + 1] << 8);
              let r = (pixelData & 0x1f) << 3;
              let g = ((pixelData >> 5) & 0x3f) << 2;
              let b = ((pixelData >> 11) & 0x1f) << 3;
              output[outputOffset] = (r | (r >> 5)) & 0xff;
              output[outputOffset + 1] = (g | (g >> 6)) & 0xff;
              output[outputOffset + 2] = (b | (b >> 5)) & 0xff;
              output[outputOffset + 3] = 0xff;
              dataOffset += 2;
            }
          }
        }
        break;

      case OTextureFormat.rgba4:
        for (let tY = 0; tY < height / 8; tY++) {
          for (let tX = 0; tX < width / 8; tX++) {
            for (let pixel = 0; pixel < 64; pixel++) {
              const x = this.tileOrder[pixel] % 8;
              const y = (this.tileOrder[pixel] - x) / 8;
              const outputOffset = ((tX * 8) + x + ((tY * 8 + y) * width)) * 4;
              const pixelData = data[dataOffset] | (data[dataOffset + 1] << 8);
              const r = (pixelData >> 4) & 0xf;
              const g = (pixelData >> 8) & 0xf;
              const b = (pixelData >> 12) & 0xf;
              const a = pixelData & 0xf;
              output[outputOffset] = (r | (r << 4)) & 0xff;
              output[outputOffset + 1] = (g | (g << 4)) & 0xff;
              output[outputOffset + 2] = (b | (b << 4)) & 0xff;
              output[outputOffset + 3] = (a | (a << 4)) & 0xff;
              dataOffset += 2;
            }
          }
        }
        break;

      case OTextureFormat.la8:
      case OTextureFormat.hilo8:
        for (let tY = 0; tY < height / 8; tY++) {
          for (let tX = 0; tX < width / 8; tX++) {
            for (let pixel = 0; pixel < 64; pixel++) {
              const x = this.tileOrder[pixel] % 8;
              const y = (this.tileOrder[pixel] - x) / 8;
              const outputOffset = ((tX * 8) + x + ((tY * 8 + y) * width)) * 4;
              output[outputOffset] = data[dataOffset];
              output[outputOffset + 1] = data[dataOffset];
              output[outputOffset + 2] = data[dataOffset];
              output[outputOffset + 3] = data[dataOffset + 1];
              dataOffset += 2;
            }
          }
        }
        break;

      case OTextureFormat.l8:
        for (let tY = 0; tY < height / 8; tY++) {
          for (let tX = 0; tX < width / 8; tX++) {
            for (let pixel = 0; pixel < 64; pixel++) {
              const x = this.tileOrder[pixel] % 8;
              const y = (this.tileOrder[pixel] - x) / 8;
              const outputOffset = ((tX * 8) + x + ((tY * 8 + y) * width)) * 4;
              output[outputOffset] = data[dataOffset];
              output[outputOffset + 1] = data[dataOffset];
              output[outputOffset + 2] = data[dataOffset];
              output[outputOffset + 3] = 0xff;
              dataOffset++;
            }
          }
        }
        break;

      case OTextureFormat.a8:
        for (let tY = 0; tY < height / 8; tY++) {
          for (let tX = 0; tX < width / 8; tX++) {
            for (let pixel = 0; pixel < 64; pixel++) {
              const x = this.tileOrder[pixel] % 8;
              const y = (this.tileOrder[pixel] - x) / 8;
              const outputOffset = ((tX * 8) + x + ((tY * 8 + y) * width)) * 4;
              output[outputOffset] = 0xff;
              output[outputOffset + 1] = 0xff;
              output[outputOffset + 2] = 0xff;
              output[outputOffset + 3] = data[dataOffset];
              dataOffset++;
            }
          }
        }
        break;

      case OTextureFormat.la4:
        for (let tY = 0; tY < height / 8; tY++) {
          for (let tX = 0; tX < width / 8; tX++) {
            for (let pixel = 0; pixel < 64; pixel++) {
              const x = this.tileOrder[pixel] % 8;
              const y = (this.tileOrder[pixel] - x) / 8;
              const outputOffset = ((tX * 8) + x + ((tY * 8 + y) * width)) * 4;
              let l = (data[dataOffset] >> 4) & 0xf;
              l = ((l << 4) | l) & 0xff;
              let a = data[dataOffset] & 0xf;
              a = ((a << 4) | a) & 0xff;
              output[outputOffset] = l;
              output[outputOffset + 1] = l;
              output[outputOffset + 2] = l;
              output[outputOffset + 3] = a;
              dataOffset++;
            }
          }
        }
        break;

      case OTextureFormat.l4:
        for (let tY = 0; tY < height / 8; tY++) {
          for (let tX = 0; tX < width / 8; tX++) {
            for (let pixel = 0; pixel < 64; pixel++) {
              const x = this.tileOrder[pixel] % 8;
              const y = (this.tileOrder[pixel] - x) / 8;
              const outputOffset = ((tX * 8) + x + ((tY * 8 + y) * width)) * 4;
              let c: number;
              if (toggle) {
                c = (data[dataOffset++] & 0xf0) >> 4;
              } else {
                c = data[dataOffset] & 0xf;
              }
              toggle = !toggle;
              c = ((c << 4) | c) & 0xff;
              output[outputOffset] = c;
              output[outputOffset + 1] = c;
              output[outputOffset + 2] = c;
              output[outputOffset + 3] = 0xff;
            }
          }
        }
        break;

      case OTextureFormat.a4:
        for (let tY = 0; tY < height / 8; tY++) {
          for (let tX = 0; tX < width / 8; tX++) {
            for (let pixel = 0; pixel < 64; pixel++) {
              const x = this.tileOrder[pixel] % 8;
              const y = (this.tileOrder[pixel] - x) / 8;
              const outputOffset = ((tX * 8) + x + ((tY * 8 + y) * width)) * 4;
              output[outputOffset] = 0xff;
              output[outputOffset + 1] = 0xff;
              output[outputOffset + 2] = 0xff;
              let a: number;
              if (toggle) {
                a = (data[dataOffset++] & 0xf0) >> 4;
              } else {
                a = data[dataOffset] & 0xf;
              }
              toggle = !toggle;
              output[outputOffset + 3] = ((a << 4) | a) & 0xff;
            }
          }
        }
        break;

      case OTextureFormat.etc1:
      case OTextureFormat.etc1a4: {
        const decodedData = this.etc1Decode(data, width, height, format === OTextureFormat.etc1a4);
        const etc1Order = this.etc1Scramble(width, height);

        let i = 0;
        for (let tY = 0; tY < height / 4; tY++) {
          for (let tX = 0; tX < width / 4; tX++) {
            const TX = etc1Order[i] % (width / 4);
            const TY = (etc1Order[i] - TX) / (width / 4);
            for (let y = 0; y < 4; y++) {
              for (let x = 0; x < 4; x++) {
                const srcOff = ((TX * 4) + x + (((TY * 4) + y) * width)) * 4;
                const dstOff = ((tX * 4) + x + (((tY * 4 + y)) * width)) * 4;
                decodedData.copy(output, dstOff, srcOff, srcOff + 4);
              }
            }
            i += 1;
          }
        }
        break;
      }
    }

    return TextureUtils.getBitmap(output, width, height);
  }

  // -----------------------------------------------------------------------
  // encode
  // -----------------------------------------------------------------------
  static encode(bmp: BitmapData, format: OTextureFormat): Buffer {
    const data = TextureUtils.getArray(bmp);
    const output = Buffer.alloc(data.length);
    let outputOffset = 0;

    switch (format) {
      case OTextureFormat.rgba8:
        for (let tY = 0; tY < bmp.height / 8; tY++) {
          for (let tX = 0; tX < bmp.width / 8; tX++) {
            for (let pixel = 0; pixel < 64; pixel++) {
              const x = this.tileOrder[pixel] % 8;
              const y = (this.tileOrder[pixel] - x) / 8;
              const dataOff = ((tX * 8) + x + ((tY * 8 + y) * bmp.width)) * 4;
              data.copy(output, outputOffset + 1, dataOff, dataOff + 3);
              output[outputOffset] = data[dataOff + 3];
              outputOffset += 4;
            }
          }
        }
        break;

      default:
        throw new Error('Texture format encoding not implemented for format ' + format);
    }

    return output;
  }

  // -----------------------------------------------------------------------
  // ETC1 helpers
  // -----------------------------------------------------------------------
  private static etc1Decode(input: Buffer, width: number, height: number, alpha: boolean): Buffer {
    const output = Buffer.alloc(width * height * 4);
    let offset = 0;

    for (let y = 0; y < height / 4; y++) {
      for (let x = 0; x < width / 4; x++) {
        let colorBlock: Buffer = Buffer.alloc(8);
        const alphaBlock = Buffer.alloc(8);

        if (alpha) {
          for (let i = 0; i < 8; i++) {
            colorBlock[7 - i] = input[offset + 8 + i];
            alphaBlock[i] = input[offset + i];
          }
          offset += 16;
        } else {
          for (let i = 0; i < 8; i++) {
            colorBlock[7 - i] = input[offset + i];
            alphaBlock[i] = 0xff;
          }
          offset += 8;
        }

        colorBlock = this.etc1DecodeBlock(colorBlock);

        let toggle = false;
        let alphaOffset = 0;
        for (let tX = 0; tX < 4; tX++) {
          for (let tY = 0; tY < 4; tY++) {
            const outputOffset = (x * 4 + tX + ((y * 4 + tY) * width)) * 4;
            const blockOffset = (tX + (tY * 4)) * 4;
            colorBlock.copy(output, outputOffset, blockOffset, blockOffset + 3);

            let a: number;
            if (toggle) {
              a = (alphaBlock[alphaOffset++] & 0xf0) >> 4;
            } else {
              a = alphaBlock[alphaOffset] & 0xf;
            }
            output[outputOffset + 3] = ((a << 4) | a) & 0xff;
            toggle = !toggle;
          }
        }
      }
    }

    return output;
  }

  private static etc1DecodeBlock(data: Buffer): Buffer {
    const blockTop = data.readUInt32LE(0);
    const blockBottom = data.readUInt32LE(4);

    const flip = (blockTop & 0x1000000) > 0;
    const difference = (blockTop & 0x2000000) > 0;

    let r1: number, g1: number, b1: number;
    let r2: number, g2: number, b2: number;

    if (difference) {
      r1 = blockTop & 0xf8;
      g1 = (blockTop & 0xf800) >> 8;
      b1 = (blockTop & 0xf80000) >> 16;

      // Signed 3-bit deltas -- need sign extension
      r2 = this.toSByte(r1 >> 3) + (this.toSByte((blockTop & 7) << 5) >> 5);
      g2 = this.toSByte(g1 >> 3) + (this.toSByte((blockTop & 0x700) >> 3) >> 5);
      b2 = this.toSByte(b1 >> 3) + (this.toSByte((blockTop & 0x70000) >> 11) >> 5);

      r1 |= r1 >> 5;
      g1 |= g1 >> 5;
      b1 |= b1 >> 5;

      r2 = ((r2 << 3) | (r2 >> 2)) & 0xff;
      g2 = ((g2 << 3) | (g2 >> 2)) & 0xff;
      b2 = ((b2 << 3) | (b2 >> 2)) & 0xff;
    } else {
      r1 = blockTop & 0xf0;
      g1 = (blockTop & 0xf000) >> 8;
      b1 = (blockTop & 0xf00000) >> 16;

      r2 = (blockTop & 0xf) << 4;
      g2 = (blockTop & 0xf00) >> 4;
      b2 = (blockTop & 0xf0000) >> 12;

      r1 |= r1 >> 4;
      g1 |= g1 >> 4;
      b1 |= b1 >> 4;

      r2 |= r2 >> 4;
      g2 |= g2 >> 4;
      b2 |= b2 >> 4;
    }

    const table1 = (blockTop >>> 29) & 7;
    const table2 = (blockTop >>> 26) & 7;

    const output = Buffer.alloc(4 * 4 * 4);

    if (!flip) {
      for (let y = 0; y <= 3; y++) {
        for (let x = 0; x <= 1; x++) {
          const color1 = this.etc1Pixel(r1, g1, b1, x, y, blockBottom, table1);
          const color2 = this.etc1Pixel(r2, g2, b2, x + 2, y, blockBottom, table2);

          const offset1 = (y * 4 + x) * 4;
          output[offset1] = color1.b;
          output[offset1 + 1] = color1.g;
          output[offset1 + 2] = color1.r;

          const offset2 = (y * 4 + x + 2) * 4;
          output[offset2] = color2.b;
          output[offset2 + 1] = color2.g;
          output[offset2 + 2] = color2.r;
        }
      }
    } else {
      for (let y = 0; y <= 1; y++) {
        for (let x = 0; x <= 3; x++) {
          const color1 = this.etc1Pixel(r1, g1, b1, x, y, blockBottom, table1);
          const color2 = this.etc1Pixel(r2, g2, b2, x, y + 2, blockBottom, table2);

          const offset1 = (y * 4 + x) * 4;
          output[offset1] = color1.b;
          output[offset1 + 1] = color1.g;
          output[offset1 + 2] = color1.r;

          const offset2 = ((y + 2) * 4 + x) * 4;
          output[offset2] = color2.b;
          output[offset2 + 1] = color2.g;
          output[offset2 + 2] = color2.r;
        }
      }
    }

    return output;
  }

  private static etc1Pixel(
    r: number, g: number, b: number,
    x: number, y: number,
    block: number, table: number,
  ): { r: number; g: number; b: number } {
    const index = x * 4 + y;
    const MSB = block << 1;

    let pixel: number;
    if (index < 8) {
      pixel = this.etc1LUT[table][((block >>> (index + 24)) & 1) + ((MSB >>> (index + 8)) & 2)];
    } else {
      pixel = this.etc1LUT[table][((block >>> (index + 8)) & 1) + ((MSB >>> (index - 8)) & 2)];
    }

    return {
      r: this.saturate(r + pixel),
      g: this.saturate(g + pixel),
      b: this.saturate(b + pixel),
    };
  }

  private static saturate(value: number): number {
    if (value > 0xff) return 0xff;
    if (value < 0) return 0;
    return value & 0xff;
  }

  /**
   * Convert an unsigned byte value to a signed byte (-128..127),
   * matching C#'s (sbyte) cast.
   */
  private static toSByte(value: number): number {
    value = value & 0xff;
    return value > 127 ? value - 256 : value;
  }

  private static etc1Scramble(width: number, height: number): number[] {
    const tileScramble = new Array<number>((width / 4) * (height / 4)).fill(0);
    let baseAccumulator = 0;
    let rowAccumulator = 0;
    let baseNumber = 0;
    let rowNumber = 0;

    for (let tile = 0; tile < tileScramble.length; tile++) {
      if ((tile % (width / 4) === 0) && tile > 0) {
        if (rowAccumulator < 1) {
          rowAccumulator += 1;
          rowNumber += 2;
          baseNumber = rowNumber;
        } else {
          rowAccumulator = 0;
          baseNumber -= 2;
          rowNumber = baseNumber;
        }
      }

      tileScramble[tile] = baseNumber;

      if (baseAccumulator < 1) {
        baseAccumulator++;
        baseNumber++;
      } else {
        baseAccumulator = 0;
        baseNumber += 3;
      }
    }

    return tileScramble;
  }
}
