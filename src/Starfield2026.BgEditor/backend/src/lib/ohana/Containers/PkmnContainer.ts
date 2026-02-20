// Ported from Containers\PkmnContainer.cs
// Generic Pokemon container reader (GR, MM, AD, etc.)

import { BinaryReader } from '../Core/BinaryReader.js';
import { OContainer, OFileEntry } from './OContainer.js';
import { FileIO } from '../Core/FileIO.js';

export class PkmnContainer {
  /**
   * Reads a generic Pokemon container from a file path.
   */
  static loadFile(fileName: string): OContainer {
    return PkmnContainer.load(BinaryReader.fromFile(fileName));
  }

  /**
   * Reads a generic Pokemon container from a BinaryReader.
   * These containers start with "GR", "MM", "AD" and so on.
   */
  static load(input: BinaryReader): OContainer {
    const output = new OContainer();

    const magic = input.readMagic(2); // Magic
    const sectionCount = input.readUInt16();

    for (let i = 0; i < sectionCount; i++) {
      input.seek(4 + (i * 4));
      const startOffset = input.readUInt32();
      const endOffset = input.readUInt32();
      const length = endOffset - startOffset;

      input.seek(startOffset);
      const buffer = input.readBytes(length);

      const isCompressed = buffer.length > 0 ? buffer[0] === 0x11 : false;
      const extension = FileIO.getExtension(buffer, isCompressed ? 5 : 0);
      const name = `file_${String(i).padStart(5, '0')}${extension}`;

      const entry: OFileEntry = {
        name,
        data: buffer,
        loadFromDisk: false,
        fileOffset: 0,
        fileLength: 0,
        doDecompression: false,
      };

      output.content.push(entry);
    }

    return output;
  }
}
