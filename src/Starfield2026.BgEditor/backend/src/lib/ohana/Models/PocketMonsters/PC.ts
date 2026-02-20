/**
 * Loads a PC monster model from Pokemon.
 * Ported from OhanaCli.Formats.Models.PocketMonsters.PC (C#).
 */

import { BinaryReader } from '../../Core/BinaryReader.js';
import { PkmnContainer } from '../../Containers/PkmnContainer.js';
import { FileIO, formatType } from '../../Core/FileIO.js';
import { OModelGroup, OSkeletalAnimation, OTexture } from '../../Core/RenderBase.js';

export class PC {
  static load(data: BinaryReader): OModelGroup {
    const models = new OModelGroup();
    const container = PkmnContainer.load(data);

    for (const file of container.content) {
      if (!file.data) continue;

      let loaded;
      try {
        loaded = FileIO.load(BinaryReader.fromBuffer(file.data));
      } catch {
        continue;
      }

      if (loaded.data == null) continue;

      switch (loaded.type) {
        case formatType.model:
          models.merge(loaded.data as OModelGroup);
          break;
        case formatType.anims:
          models.skeletalAnimation.list.push(loaded.data as OSkeletalAnimation);
          break;
        case formatType.image:
          models.texture.push(loaded.data as OTexture);
          break;
      }
    }

    return models;
  }
}
