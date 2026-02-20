/**
 * Loads a GR map model from Pokemon.
 * Ported from OhanaCli.Formats.Models.PocketMonsters.GR (C#).
 */

import { BinaryReader } from '../../Core/BinaryReader.js';
import { PkmnContainer } from '../../Containers/PkmnContainer.js';
import { BCH } from '../BCH/BCH.js';
import { OModelGroup } from '../../Core/RenderBase.js';

export class GR {
  static load(data: BinaryReader): OModelGroup {
    const container = PkmnContainer.load(data);
    return BCH.load(BinaryReader.fromBuffer(container.content[1].data!));
  }
}
