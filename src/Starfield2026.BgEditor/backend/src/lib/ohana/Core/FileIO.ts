// Ported from Core\FileIO.cs
// Format dispatcher: detects file format by magic bytes and routes to the appropriate parser.

import { BinaryReader } from './BinaryReader.js';
import { IOUtils } from './IOUtils.js';
import { LZSS } from '../Compressions/LZSS.js';
import { LZSS_Ninty } from '../Compressions/LZSS_Ninty.js';
import { BLZ } from '../Compressions/BLZ.js';
import { GARC } from '../Containers/GARC.js';
import { PkmnContainer } from '../Containers/PkmnContainer.js';
import { BCH } from '../Models/BCH/BCH.js';
import { GfModel } from '../Models/PocketMonsters/GfModel.js';
import { GfMotion } from '../Models/PocketMonsters/GfMotion.js';
import { GfTexture } from '../Textures/PocketMonsters/GfTexture.js';
import { PT } from '../Textures/PocketMonsters/PT.js';
import { AD } from '../Textures/PocketMonsters/AD.js';
import { GR } from '../Models/PocketMonsters/GR.js';
import { MM } from '../Models/PocketMonsters/MM.js';
import { CM } from '../Models/PocketMonsters/CM.js';
import { CP } from '../Models/PocketMonsters/CP.js';
import { PC } from '../Models/PocketMonsters/PC.js';
import { OModelGroup } from './RenderBase.js';

export enum formatType {
  unsupported = 0,
  compression = 1 << 0,
  container = 1 << 1,
  image = 1 << 2,
  model = 1 << 3,
  texture = 1 << 4,
  anims = 1 << 5,
  all = 0xffffffff,
}

export interface LoadedFile {
  data: unknown;
  type: formatType;
}

export enum fileType {
  none,
  model,
  texture,
  skeletalAnimation,
  materialAnimation,
  visibilityAnimation,
}

export class FileIO {
  /**
   * Load a file from disk by path, dispatching by extension or content.
   */
  static loadFile(fileName: string): LoadedFile {
    // No special extension handling currently active; fall through to content-based detection
    return FileIO.load(BinaryReader.fromFile(fileName));
  }

  /**
   * Load from a BinaryReader, detecting format by magic bytes.
   * This is the main dispatcher.
   */
  static load(data: BinaryReader): LoadedFile {
    if (data.length < 0x10) {
      return { data: null, type: formatType.unsupported };
    }

    const input = data;

    // Check 4-byte peek value for GfModel / GfMotion / GfTexture formats
    switch (FileIO.peek(input)) {
      case 0x00010000: return { data: GfModel.load(data), type: formatType.model };
      case 0x00060000: return { data: GfMotion.loadAnim(input), type: formatType.anims };
      case 0x15041213: return { data: GfTexture.load(data), type: formatType.image };
      case 0x15122117: {
        const mdls = new OModelGroup();
        mdls.model.push(GfModel.loadModel(data));
        return { data: mdls, type: formatType.model };
      }
    }

    // Skip 7-char and 5-char magic checks (commented out parsers)
    FileIO.getMagicFromReader(input, 7);
    FileIO.getMagicFromReader(input, 5);

    switch (FileIO.getMagicFromReader(input, 4)) {
      case 'CRAG':
        return { data: GARC.load(data), type: formatType.container };
      case 'IECP': {
        input.readUInt32(); // skip magic re-read
        const length = input.readUInt32();
        const decompressed = LZSS.decompress(data, length);
        return FileIO.load(BinaryReader.fromBuffer(decompressed));
      }
    }

    switch (FileIO.getMagicFromReader(input, 3)) {
      case 'BCH': {
        const buffer = input.readBytes(data.length);
        return {
          data: BCH.load(BinaryReader.fromBuffer(buffer)),
          type: formatType.model,
        };
      }
    }

    const magic2b = FileIO.getMagicFromReader(input, 2);

    switch (magic2b) {
      case 'AD': return { data: AD.load(data), type: formatType.model };
      case 'BM': return { data: MM.load(data), type: formatType.model };
      case 'CM': return { data: CM.load(data), type: formatType.model };
      case 'CP': return { data: CP.load(data), type: formatType.model };
      case 'GR': return { data: GR.load(data), type: formatType.model };
      case 'MM': return { data: MM.load(data), type: formatType.model };
      case 'PC': return { data: PC.load(data), type: formatType.model };
      case 'PT': return { data: PT.load(data), type: formatType.texture };
    }

    // If magic is two uppercase ASCII letters, treat as PkmnContainer
    if (magic2b.length === 2) {
      if (
        magic2b.charCodeAt(0) >= 0x41 && magic2b.charCodeAt(0) <= 0x5a &&
        magic2b.charCodeAt(1) >= 0x41 && magic2b.charCodeAt(1) <= 0x5a
      ) {
        return { data: PkmnContainer.load(data), type: formatType.container };
      }
    }

    // Compression detection
    data.seek(0);
    let cmp = input.readUInt32();
    if ((cmp & 0xff) === 0x13) cmp = input.readUInt32();
    switch (cmp & 0xff) {
      case 0x11: {
        // LZSS_Ninty: read remaining data after the 4-byte header we already consumed
        data.seek(0);
        const compBuf = input.readBytes(data.length);
        const decompressed = LZSS_Ninty.decompress(compBuf);
        return FileIO.load(BinaryReader.fromBuffer(decompressed));
      }
      case 0x90: {
        data.seek(0);
        const blzReader = BinaryReader.fromBuffer(input.readBytes(data.length));
        const buffer = BLZ.decompress(blzReader);
        const newData = buffer.subarray(1);
        return FileIO.load(BinaryReader.fromBuffer(newData));
      }
    }

    return { data: null, type: formatType.unsupported };
  }

  /**
   * Detect file extension from magic bytes in a raw buffer.
   */
  static getExtension(data: Buffer, startIndex: number = 0): string {
    if (data.length > 3 + startIndex) {
      switch (FileIO.getMagicFromBuffer(data, 4, startIndex)) {
        case 'CGFX': return '.bcres';
      }
    }

    if (data.length > 2 + startIndex) {
      switch (FileIO.getMagicFromBuffer(data, 3, startIndex)) {
        case 'BCH': return '.bch';
      }
    }

    if (data.length > 1 + startIndex) {
      switch (FileIO.getMagicFromBuffer(data, 2, startIndex)) {
        case 'AD': return '.ad';
        case 'BG': return '.bg';
        case 'BM': return '.bm';
        case 'BS': return '.bs';
        case 'CM': return '.cm';
        case 'GR': return '.gr';
        case 'MM': return '.mm';
        case 'PB': return '.pb';
        case 'PC': return '.pc';
        case 'PF': return '.pf';
        case 'PK': return '.pk';
        case 'PO': return '.po';
        case 'PT': return '.pt';
        case 'TM': return '.tm';
      }
    }

    return '.bin';
  }

  /**
   * Peek at a uint32 without advancing the reader position.
   */
  private static peek(input: BinaryReader): number {
    const value = input.readUInt32();
    input.seekRelative(-4);
    return value;
  }

  /**
   * Read a magic string from BinaryReader starting at position 0, then reset to position 0.
   */
  private static getMagicFromReader(input: BinaryReader, length: number): string {
    input.seek(0);
    const magic = input.readMagic(length);
    input.seek(0);
    return magic;
  }

  /**
   * Read ASCII magic bytes from a raw Buffer.
   */
  static getMagicFromBuffer(data: Buffer, length: number, startIndex: number = 0): string {
    return data.toString('ascii', startIndex, startIndex + length);
  }
}
