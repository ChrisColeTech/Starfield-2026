import { BitmapData } from './Codecs/TextureCodec.js';

/**
 * Texture utility helpers ported from OhanaCli.Formats.TextureUtils (C#).
 *
 * In the original C# code these worked with System.Drawing.Bitmap and
 * GDI+ LockBits / Marshal.Copy.  Here we operate directly on our
 * BitmapData RGBA buffer, so the "conversion" is essentially a no-op.
 */
export class TextureUtils {
  /**
   * Creates a BitmapData from a raw RGBA8 buffer.
   * The C# version did Marshal.Copy into a Bitmap -- here we just wrap.
   */
  static getBitmap(array: Buffer, width: number, height: number): BitmapData {
    const bmp = new BitmapData(width, height);
    const len = Math.min(array.length, bmp.data.length);
    // The C# decode writes pixels in BGRA order (for GDI+ Format32bppArgb).
    // Our BitmapData uses RGBA order, so swap B and R channels.
    for (let i = 0; i < len; i += 4) {
      bmp.data[i]     = array[i + 2]; // R <- B position
      bmp.data[i + 1] = array[i + 1]; // G
      bmp.data[i + 2] = array[i];     // B <- R position
      bmp.data[i + 3] = array[i + 3]; // A
    }
    return bmp;
  }

  /**
   * Returns the raw RGBA8 pixel buffer from a BitmapData.
   * The C# version did LockBits + Marshal.Copy -- here we just return
   * a copy of the underlying buffer.
   */
  static getArray(bmp: BitmapData): Buffer {
    // The C# encoder expects BGRA byte order (matching GDI+ Format32bppArgb).
    // Our BitmapData stores RGBA, so swap R and B channels on the way out.
    const buf = Buffer.alloc(bmp.data.length);
    for (let i = 0; i < bmp.data.length; i += 4) {
      buf[i]     = bmp.data[i + 2]; // B <- R position
      buf[i + 1] = bmp.data[i + 1]; // G
      buf[i + 2] = bmp.data[i];     // R <- B position
      buf[i + 3] = bmp.data[i + 3]; // A
    }
    return buf;
  }
}
