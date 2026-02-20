/**
 * Loads a CP overworld character model from Pokemon.
 * Ported from OhanaCli.Formats.Models.PocketMonsters.CP (C#).
 */

import { BinaryReader } from '../../Core/BinaryReader.js';
import { PkmnContainer } from '../../Containers/PkmnContainer.js';
import { CM } from './CM.js';
import { OModelGroup } from '../../Core/RenderBase.js';

export class CP {
  static load(data: BinaryReader): OModelGroup {
    const container = PkmnContainer.load(data);
    return CM.load(BinaryReader.fromBuffer(container.content[1].data!));
  }
}
