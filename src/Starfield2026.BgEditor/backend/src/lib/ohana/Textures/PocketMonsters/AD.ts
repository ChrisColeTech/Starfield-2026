import { BinaryReader } from '../../Core/BinaryReader.js';
import { PkmnContainer } from '../../Containers/PkmnContainer.js';
import { FileIO, formatType } from '../../Core/FileIO.js';
import { OModelGroup } from '../../Core/RenderBase.js';

/**
 * Loads all map textures (and other data) from an AD Pokemon container.
 * Ported from OhanaCli.Formats.Textures.PocketMonsters.AD (C#).
 */
export class AD {
  static load(input: BinaryReader): OModelGroup {
    const models = new OModelGroup();

    const container = PkmnContainer.load(input);
    // Note: starts at index 1 per original C# code
    for (let i = 1; i < container.content.length; i++) {
      const entry = container.content[i];
      if (!entry.data) continue;
      const file = FileIO.load(BinaryReader.fromBuffer(entry.data));
      if (file.type === formatType.model) {
        models.merge(file.data as OModelGroup);
      }
    }

    return models;
  }
}
