/**
 * Loads a MM overworld chibi character model from Pokemon.
 * Ported from OhanaCli.Formats.Models.PocketMonsters.MM (C#).
 */

import { BinaryReader } from '../../Core/BinaryReader.js';
import { PkmnContainer } from '../../Containers/PkmnContainer.js';
import { BCH } from '../BCH/BCH.js';
import { OModelGroup } from '../../Core/RenderBase.js';

export class MM {
  static load(data: BinaryReader): OModelGroup {
    const container = PkmnContainer.load(data);
    return BCH.load(BinaryReader.fromBuffer(container.content[0].data!));
  }
}
