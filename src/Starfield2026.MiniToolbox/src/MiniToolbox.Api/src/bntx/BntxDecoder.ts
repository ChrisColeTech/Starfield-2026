import { BntxFile } from './BntxFile.js';
import { Texture as BntxTexture } from './Texture.js';
import { TegraSwizzle } from './TegraSwizzle.js';
import { SurfaceFormat } from './GFX.js';
import { decodeBC1, decodeBC2, decodeBC3, decodeBC4, decodeBC5, decodeBC7 } from '@bis-toolkit/bcn';
import * as fs from 'fs';

export interface DecodedTexture {
    name: string;
    width: number;
    height: number;
    rgbaData: Buffer;
}

/**
 * Decodes BNTX (Binary NX Texture) files to RGBA pixel data.
 */
export class BntxDecoder {
    /**
     * Helper to load and decode a BNTX file from disk.
     */
    static decodeFile(filePath: string): DecodedTexture[] {
        const buffer = fs.readFileSync(filePath);
        const bntx = BntxFile.fromBuffer(buffer);
        return this.decode(bntx);
    }

    /**
     * Decode all textures from a BntxFile object.
     */
    static decode(bntx: BntxFile): DecodedTexture[] {
        const results: DecodedTexture[] = [];

        for (const tex of bntx.textures) {
            try {
                const decoded = this.decodeTexture(tex, bntx.platformTarget);
                results.push(decoded);
            } catch (err) {
                console.error(`[BntxDecoder] Failed to decode texture '${tex.name}':`, err);
            }
        }

        return results;
    }

    /**
     * Decode a single Texture resource to RGBA.
     */
    static decodeTexture(tex: BntxTexture, platformTarget: string): DecodedTexture {
        const target = platformTarget === 'NX  ' ? 1 : 0;
        const format = tex.format;
        const info = this.getFormatInfo(format);

        const width = tex.width;
        const height = tex.height;
        const depth = Math.max(1, tex.depth);

        if (tex.textureData.length === 0 || tex.textureData[0].length === 0) {
            throw new Error(`Texture '${tex.name}' has no image data`);
        }

        // Deswizzle
        const swizzledData = tex.textureData[0][0]; // Array 0, Mip 0
        let deswizzled = TegraSwizzle.deswizzle(
            width, height, depth,
            info.blkWidth, info.blkHeight, 1,
            0, // roundPitch (C# uses 0 for block-linear)
            info.bpp,
            tex.tileMode,
            tex.blockHeightLog2,
            swizzledData
        );

        // Trim to exact size needed
        const expectedSize = this.divRoundUp(width, info.blkWidth) *
            this.divRoundUp(height, info.blkHeight) *
            info.bpp;
        if (deswizzled.length > expectedSize) {
            deswizzled = deswizzled.subarray(0, expectedSize);
        }

        // Decode to RGBA
        const rgba = this.decodeFormatToRgba(deswizzled, width, height, format, info);

        return {
            name: tex.name,
            width: width,
            height: height,
            rgbaData: rgba
        };
    }

    private static decodeFormatToRgba(data: Buffer, width: number, height: number, format: SurfaceFormat, info: any): Buffer {
        const dataView = new DataView(data.buffer, data.byteOffset, data.byteLength);

        switch (format) {
            // Uncompressed formats
            case SurfaceFormat.R8_G8_B8_A8_UNORM:
            case SurfaceFormat.R8_G8_B8_A8_SRGB:
                return data;
            case SurfaceFormat.B8_G8_R8_A8_UNORM:
            case SurfaceFormat.B8_G8_R8_A8_SRGB:
                return this.convertBgraToRgba(data);
            case SurfaceFormat.R8_UNORM:
                return this.expandR8(data, width, height);
            case SurfaceFormat.R8_G8_UNORM:
                return this.expandRg8(data, width, height);

            // Block-compressed formats
            case SurfaceFormat.BC1_UNORM:
            case SurfaceFormat.BC1_SRGB:
                return Buffer.from(decodeBC1(dataView, width, height));
            case SurfaceFormat.BC2_UNORM:
            case SurfaceFormat.BC2_SRGB:
                return Buffer.from(decodeBC2(dataView, width, height));
            case SurfaceFormat.BC3_UNORM:
            case SurfaceFormat.BC3_SRGB:
                return Buffer.from(decodeBC3(dataView, width, height));
            case SurfaceFormat.BC4_UNORM:
            case SurfaceFormat.BC4_SNORM:
                return Buffer.from(decodeBC4(dataView, width, height));
            case SurfaceFormat.BC5_UNORM:
            case SurfaceFormat.BC5_SNORM:
                return Buffer.from(decodeBC5(dataView, width, height));
            case SurfaceFormat.BC7_UNORM:
            case SurfaceFormat.BC7_SRGB:
                return Buffer.from(decodeBC7(dataView, width, height));

            // ASTC formats — throw with descriptive error (matches C# DecodeAstcManaged behavior)
            case SurfaceFormat.ASTC_4x4_UNORM:
            case SurfaceFormat.ASTC_4x4_SRGB:
                throw new Error(`ASTC 4x4 decode requires native astc-encoder library`);
            case SurfaceFormat.ASTC_5x4_UNORM:
            case SurfaceFormat.ASTC_5x4_SRGB:
                throw new Error(`ASTC 5x4 decode requires native astc-encoder library`);
            case SurfaceFormat.ASTC_5x5_UNORM:
            case SurfaceFormat.ASTC_5x5_SRGB:
                throw new Error(`ASTC 5x5 decode requires native astc-encoder library`);
            case SurfaceFormat.ASTC_6x5_UNORM:
            case SurfaceFormat.ASTC_6x5_SRGB:
                throw new Error(`ASTC 6x5 decode requires native astc-encoder library`);
            case SurfaceFormat.ASTC_6x6_UNORM:
            case SurfaceFormat.ASTC_6x6_SRGB:
                throw new Error(`ASTC 6x6 decode requires native astc-encoder library`);
            case SurfaceFormat.ASTC_8x5_UNORM:
            case SurfaceFormat.ASTC_8x5_SRGB:
                throw new Error(`ASTC 8x5 decode requires native astc-encoder library`);
            case SurfaceFormat.ASTC_8x6_UNORM:
            case SurfaceFormat.ASTC_8x6_SRGB:
                throw new Error(`ASTC 8x6 decode requires native astc-encoder library`);
            case SurfaceFormat.ASTC_8x8_UNORM:
            case SurfaceFormat.ASTC_8x8_SRGB:
                throw new Error(`ASTC 8x8 decode requires native astc-encoder library`);
            case SurfaceFormat.ASTC_10x5_UNORM:
            case SurfaceFormat.ASTC_10x5_SRGB:
                throw new Error(`ASTC 10x5 decode requires native astc-encoder library`);
            case SurfaceFormat.ASTC_10x6_UNORM:
            case SurfaceFormat.ASTC_10x6_SRGB:
                throw new Error(`ASTC 10x6 decode requires native astc-encoder library`);
            case SurfaceFormat.ASTC_10x8_UNORM:
            case SurfaceFormat.ASTC_10x8_SRGB:
                throw new Error(`ASTC 10x8 decode requires native astc-encoder library`);
            case SurfaceFormat.ASTC_10x10_UNORM:
            case SurfaceFormat.ASTC_10x10_SRGB:
                throw new Error(`ASTC 10x10 decode requires native astc-encoder library`);
            case SurfaceFormat.ASTC_12x10_UNORM:
            case SurfaceFormat.ASTC_12x10_SRGB:
                throw new Error(`ASTC 12x10 decode requires native astc-encoder library`);
            case SurfaceFormat.ASTC_12x12_UNORM:
            case SurfaceFormat.ASTC_12x12_SRGB:
                throw new Error(`ASTC 12x12 decode requires native astc-encoder library`);

            default:
                throw new Error(`Unsupported texture format: ${format}`);
        }
    }

    private static convertBgraToRgba(data: Buffer): Buffer {
        const rgba = Buffer.alloc(data.length);
        for (let i = 0; i < data.length; i += 4) {
            rgba[i + 0] = data[i + 2]; // R
            rgba[i + 1] = data[i + 1]; // G
            rgba[i + 2] = data[i + 0]; // B
            rgba[i + 3] = data[i + 3]; // A
        }
        return rgba;
    }

    private static expandR8(data: Buffer, width: number, height: number): Buffer {
        const rgba = Buffer.alloc(width * height * 4);
        for (let i = 0; i < data.length && i < width * height; i++) {
            const idx = i * 4;
            rgba[idx + 0] = data[i];
            rgba[idx + 1] = data[i];
            rgba[idx + 2] = data[i];
            rgba[idx + 3] = 255;
        }
        return rgba;
    }

    private static expandRg8(data: Buffer, width: number, height: number): Buffer {
        const rgba = Buffer.alloc(width * height * 4);
        const pixels = Math.min(data.length / 2, width * height);
        for (let i = 0; i < pixels; i++) {
            const idx = i * 4;
            rgba[idx + 0] = data[i * 2 + 0]; // R
            rgba[idx + 1] = data[i * 2 + 1]; // G
            rgba[idx + 2] = 0;                // B
            rgba[idx + 3] = 255;              // A
        }
        return rgba;
    }

    private static getFormatInfo(format: SurfaceFormat): { bpp: number; blkWidth: number; blkHeight: number } {
        switch (format) {
            case SurfaceFormat.R8_UNORM: return { bpp: 1, blkWidth: 1, blkHeight: 1 };
            case SurfaceFormat.R8_G8_UNORM: return { bpp: 2, blkWidth: 1, blkHeight: 1 };
            case SurfaceFormat.R8_G8_B8_A8_UNORM:
            case SurfaceFormat.R8_G8_B8_A8_SRGB:
            case SurfaceFormat.B8_G8_R8_A8_UNORM:
            case SurfaceFormat.B8_G8_R8_A8_SRGB:
                return { bpp: 4, blkWidth: 1, blkHeight: 1 };

            case SurfaceFormat.BC1_UNORM:
            case SurfaceFormat.BC1_SRGB:
                return { bpp: 8, blkWidth: 4, blkHeight: 4 };
            case SurfaceFormat.BC2_UNORM:
            case SurfaceFormat.BC2_SRGB:
            case SurfaceFormat.BC3_UNORM:
            case SurfaceFormat.BC3_SRGB:
            case SurfaceFormat.BC7_UNORM:
            case SurfaceFormat.BC7_SRGB:
                return { bpp: 16, blkWidth: 4, blkHeight: 4 };
            case SurfaceFormat.BC4_UNORM:
            case SurfaceFormat.BC4_SNORM:
                return { bpp: 8, blkWidth: 4, blkHeight: 4 };
            case SurfaceFormat.BC5_UNORM:
            case SurfaceFormat.BC5_SNORM:
            case SurfaceFormat.BC6_FLOAT:
            case SurfaceFormat.BC6_UFLOAT:
                return { bpp: 16, blkWidth: 4, blkHeight: 4 };

            case SurfaceFormat.ASTC_4x4_UNORM:
            case SurfaceFormat.ASTC_4x4_SRGB:
                return { bpp: 16, blkWidth: 4, blkHeight: 4 };
            case SurfaceFormat.ASTC_5x4_UNORM:
            case SurfaceFormat.ASTC_5x4_SRGB:
                return { bpp: 16, blkWidth: 5, blkHeight: 4 };
            case SurfaceFormat.ASTC_5x5_UNORM:
            case SurfaceFormat.ASTC_5x5_SRGB:
                return { bpp: 16, blkWidth: 5, blkHeight: 5 };
            case SurfaceFormat.ASTC_6x5_UNORM:
            case SurfaceFormat.ASTC_6x5_SRGB:
                return { bpp: 16, blkWidth: 6, blkHeight: 5 };
            case SurfaceFormat.ASTC_6x6_UNORM:
            case SurfaceFormat.ASTC_6x6_SRGB:
                return { bpp: 16, blkWidth: 6, blkHeight: 6 };
            case SurfaceFormat.ASTC_8x5_UNORM:
            case SurfaceFormat.ASTC_8x5_SRGB:
                return { bpp: 16, blkWidth: 8, blkHeight: 5 };
            case SurfaceFormat.ASTC_8x6_UNORM:
            case SurfaceFormat.ASTC_8x6_SRGB:
                return { bpp: 16, blkWidth: 8, blkHeight: 6 };
            case SurfaceFormat.ASTC_8x8_UNORM:
            case SurfaceFormat.ASTC_8x8_SRGB:
                return { bpp: 16, blkWidth: 8, blkHeight: 8 };
            case SurfaceFormat.ASTC_10x5_UNORM:
            case SurfaceFormat.ASTC_10x5_SRGB:
                return { bpp: 16, blkWidth: 10, blkHeight: 5 };
            case SurfaceFormat.ASTC_10x6_UNORM:
            case SurfaceFormat.ASTC_10x6_SRGB:
                return { bpp: 16, blkWidth: 10, blkHeight: 6 };
            case SurfaceFormat.ASTC_10x8_UNORM:
            case SurfaceFormat.ASTC_10x8_SRGB:
                return { bpp: 16, blkWidth: 10, blkHeight: 8 };
            case SurfaceFormat.ASTC_10x10_UNORM:
            case SurfaceFormat.ASTC_10x10_SRGB:
                return { bpp: 16, blkWidth: 10, blkHeight: 10 };
            case SurfaceFormat.ASTC_12x10_UNORM:
            case SurfaceFormat.ASTC_12x10_SRGB:
                return { bpp: 16, blkWidth: 12, blkHeight: 10 };
            case SurfaceFormat.ASTC_12x12_UNORM:
            case SurfaceFormat.ASTC_12x12_SRGB:
                return { bpp: 16, blkWidth: 12, blkHeight: 12 };

            default:
                // Fallback: assume RGBA8 (matches C# behavior)
                return { bpp: 4, blkWidth: 1, blkHeight: 1 };
        }
    }

    private static divRoundUp(n: number, d: number): number {
        return Math.floor((n + d - 1) / d);
    }
}
