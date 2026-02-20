import { BinaryReader } from '../../Core/BinaryReader.js';
import { PkmnContainer } from '../../Containers/PkmnContainer.js';
import { FileIO, formatType } from '../../Core/FileIO.js';
import type { OTexture } from './GfTexture.js';

/**
 * Loads all monster textures from a PT Pokemon container.
 * Ported from OhanaCli.Formats.Textures.PocketMonsters.PT (C#).
 */
export class PT {
  static load(input: BinaryReader): OTexture[] {
    const textures: OTexture[] = [];

    const container = PkmnContainer.load(input);
    for (let i = 0; i < container.content.length; i++) {
      const entry = container.content[i];
      if (!entry.data) continue;
      const file = FileIO.load(BinaryReader.fromBuffer(entry.data));
      if (file.type === formatType.model) {
        const modelGroup = file.data as { texture: OTexture[] };
        textures.push(...modelGroup.texture);
      }
    }

    return textures;
  }
}
