/**
 * Nintendo Tegra X1 swizzle/unswizzle for Switch textures.
 *
 * Calls tegra_swizzle_x64.dll (Rust native library) using koffi FFI.
 * Function signatures reverse-engineered from the C# P/Invoke wrapper in TegraSwizzle.cs.
 */

import * as path from 'path';
import { fileURLToPath } from 'url';
import koffi from 'koffi';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const _dllPath = path.resolve(__dirname, '..', 'tegra_swizzle_x64.dll');

// ---------------------------------------------------------------------------
// koffi FFI bindings
// ---------------------------------------------------------------------------
const _lib = koffi.load(_dllPath);

// ulong = uint64 in koffi
const _deswizzleBlockLinear = _lib.func(
  'deswizzle_block_linear',
  'void',
  ['uint64', 'uint64', 'uint64',   // width, height, depth
    'uint8 *', 'uint64',            // source, sourceLength
    'uint8 *', 'uint64',            // destination, destinationLength
    'uint64', 'uint64']             // blockHeight, bytesPerPixel
);

const _blockHeightMip0 = _lib.func(
  'block_height_mip0',
  'uint64',
  ['uint64']  // height (in blocks)
);

/**
 * Deswizzle a Tegra X1 block-linear or pitch-linear texture surface.
 */
export class TegraSwizzle {
  /**
   * Deswizzle a Tegra X1 block-linear or pitch-linear texture surface.
   * Matches the C# TegraSwizzle.Deswizzle() signature exactly.
   */
  static deswizzle(
    width: number,
    height: number,
    depth: number,
    blkWidth: number,
    blkHeight: number,
    _blkDepth: number,
    _roundPitch: number,
    bpp: number,
    tileMode: number,
    blockHeightLog2: number,
    data: Buffer
  ): Buffer {
    if (tileMode === 1) {
      return this.deswizzlePitchLinear(width, height, depth, blkWidth, blkHeight, 1, _roundPitch, bpp, data);
    } else {
      return this.deswizzleBlockLinearSurface(width, height, depth, blkWidth, blkHeight, 1, bpp, blockHeightLog2, data);
    }
  }

  /**
   * Deswizzle block-linear via the native DLL.
   */
  private static deswizzleBlockLinearSurface(
    width: number,
    height: number,
    depth: number,
    blkWidth: number,
    blkHeight: number,
    _blkDepth: number,
    bpp: number,
    blockHeightLog2: number,
    data: Buffer
  ): Buffer {
    // tegra_swizzle only allows block heights supported by the TRM (1,2,4,8,16,32).
    const blockHeightMip0 = BigInt(1 << Math.max(Math.min(blockHeightLog2, 5), 0));

    // Convert to block dimensions for block compressed formats
    const w = this.divRoundUp(width, blkWidth);
    const h = this.divRoundUp(height, blkHeight);
    const d = this.divRoundUp(depth, 1);

    const outputSize = w * h * d * bpp;
    const output = Buffer.alloc(outputSize);

    _deswizzleBlockLinear(
      BigInt(w), BigInt(h), BigInt(d),
      data, BigInt(data.length),
      output, BigInt(outputSize),
      blockHeightMip0, BigInt(bpp)
    );

    return output;
  }

  /**
   * Deswizzle pitch-linear (pure TS, no native needed).
   */
  private static deswizzlePitchLinear(
    width: number,
    height: number,
    depth: number,
    blkWidth: number,
    blkHeight: number,
    _blkDepth: number,
    roundPitch: number,
    bpp: number,
    data: Buffer
  ): Buffer {
    const w = this.divRoundUp(width, blkWidth);
    const h = this.divRoundUp(height, blkHeight);
    const d = this.divRoundUp(depth, 1);

    let pitch = w * bpp;
    if (roundPitch === 1) {
      pitch = this.roundUp(pitch, 32);
    }

    const surfSize = pitch * h;
    const result = Buffer.alloc(surfSize);

    for (let z = 0; z < d; z++) {
      for (let y = 0; y < h; y++) {
        for (let x = 0; x < w; x++) {
          const pos = y * pitch + x * bpp;
          const srcPos = (z * h * w + y * w + x) * bpp;
          if (pos + bpp <= surfSize && srcPos + bpp <= data.length) {
            data.copy(result, pos, srcPos, srcPos + bpp);
          }
        }
      }
    }

    return result;
  }

  /**
   * Get block height from texture height (in blocks), using native DLL.
   */
  static getBlockHeight(heightInBytes: number): bigint {
    return BigInt(_blockHeightMip0(BigInt(heightInBytes)));
  }

  static divRoundUp(n: number, d: number): number {
    return Math.floor((n + d - 1) / d);
  }

  static pow2RoundUp(x: number): number {
    x -= 1;
    x |= x >> 1; x |= x >> 2; x |= x >> 4; x |= x >> 8; x |= x >> 16;
    return x + 1;
  }

  private static roundUp(x: number, y: number): number {
    return ((x - 1) | (y - 1)) + 1;
  }
}

export default TegraSwizzle;
