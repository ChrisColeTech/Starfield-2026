/**
 * Loads a CM overworld character model from Pokemon.
 * Ported from OhanaCli.Formats.Models.PocketMonsters.CM (C#).
 */

import { BinaryReader } from '../../Core/BinaryReader.js';
import { PkmnContainer } from '../../Containers/PkmnContainer.js';
import { GfModel } from './GfModel.js';
import { GfMotion } from './GfMotion.js';
import { OModelGroup } from '../../Core/RenderBase.js';

export class CM {
  static load(data: BinaryReader): OModelGroup {
    const container = PkmnContainer.load(data);
    const models = GfModel.load(BinaryReader.fromBuffer(container.content[0].data!));

    if (container.content.length > 1 && container.content[1].data) {
      try {
        const anms = GfMotion.load(BinaryReader.fromBuffer(container.content[1].data));
        for (const anm of anms) models.skeletalAnimation.list.push(anm);
      } catch {
        // Animation data may not be present or valid in all CM containers
      }
    }

    return models;
  }
}
