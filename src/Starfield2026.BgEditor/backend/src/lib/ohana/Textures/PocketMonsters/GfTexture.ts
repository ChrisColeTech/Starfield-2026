import { BinaryReader } from '../../Core/BinaryReader.js';
import { IOUtils } from '../../Core/IOUtils.js';
import { TextureCodec, OTextureFormat, BitmapData } from '../Codecs/TextureCodec.js';
import { OTexture } from '../../Core/RenderBase.js';

export { OTexture } from '../../Core/RenderBase.js';

/**
 * Loads a Game Freak texture.
 * Ported from OhanaCli.Formats.Textures.PocketMonsters.GfTexture (C#).
 */
export class GfTexture {
  /**
   * Loads a Game Freak texture from a BinaryReader.
   * @param input - BinaryReader positioned at the start of the texture data.
   * @returns The decoded OTexture, or null if the signature does not match.
   */
  static load(input: BinaryReader): OTexture | null {
    const descAddress = input.position;

    input.seekRelative(8);
    if (input.readStringWithLength(7) !== 'texture') return null;

    input.seek(descAddress + 0x18);
    const texLength = input.readInt32();

    input.seek(descAddress + 0x28);
    const texName = input.readStringWithLength(0x40);

    input.seek(descAddress + 0x68);
    const width = input.readUInt16();
    const height = input.readUInt16();
    const texFormat = input.readUInt16();
    const _texMipMaps = input.readUInt16();

    input.seekRelative(0x10);
    const texBuffer = input.readBytes(texLength);

    let fmt: OTextureFormat = OTextureFormat.dontCare;

    switch (texFormat) {
      case 0x02: fmt = OTextureFormat.rgb565; break;
      case 0x03: fmt = OTextureFormat.rgb8; break;
      case 0x04: fmt = OTextureFormat.rgba8; break;
      case 0x16: fmt = OTextureFormat.rgba4; break;
      case 0x17: fmt = OTextureFormat.rgba5551; break;
      case 0x23: fmt = OTextureFormat.la8; break;
      case 0x24: fmt = OTextureFormat.hilo8; break;
      case 0x25: fmt = OTextureFormat.l8; break;
      case 0x26: fmt = OTextureFormat.a8; break;
      case 0x27: fmt = OTextureFormat.la4; break;
      case 0x28: fmt = OTextureFormat.l4; break;
      case 0x29: fmt = OTextureFormat.a4; break;
      case 0x2a: fmt = OTextureFormat.etc1; break;
      case 0x2b: fmt = OTextureFormat.etc1a4; break;
      default:
        console.warn(`GfTexture: Unknown texture format 0x${texFormat.toString(16).padStart(4, '0')} @ ${texName}`);
        break;
    }

    const tex = TextureCodec.decode(texBuffer, width, height, fmt);
    return new OTexture(tex, texName);
  }
}
