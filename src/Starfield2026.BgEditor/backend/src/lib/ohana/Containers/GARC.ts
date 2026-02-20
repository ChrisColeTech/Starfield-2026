// Ported from Containers\GARC.cs
// GARC archive reader (Game Archive Container)

import { BinaryReader } from '../Core/BinaryReader.js';
import { OContainer, OFileEntry } from './OContainer.js';
import { FileIO } from '../Core/FileIO.js';

export class GARC {
  /**
   * Reads a GARC archive from a file path.
   */
  static loadFile(fileName: string): OContainer {
    return GARC.load(BinaryReader.fromFile(fileName));
  }

  /**
   * Reads a GARC archive from a BinaryReader.
   */
  static load(input: BinaryReader): OContainer {
    const output = new OContainer();
    output.data = input;

    const garcMagic = input.readMagic(4);
    const garcLength = input.readUInt32();
    const endian = input.readUInt16();
    const version = input.readUInt16(); // 0x400
    const sectionCount = input.readUInt32();
    const dataOffset = input.readUInt32();
    const decompressedLength = input.readUInt32();
    const compressedLength = input.readUInt32();

    input.seek(garcLength); // This is just the header "GARC" blk len, not the entire file

    // File Allocation Table Offsets
    const fatoPosition = input.position;
    const fatoMagic = input.readMagic(4);
    const fatoLength = input.readUInt32();
    const fatoEntries = input.readUInt16();
    input.readUInt16(); // 0xffff = Padding?

    const fatbPosition = fatoPosition + fatoLength;

    for (let i = 0; i < fatoEntries; i++) {
      input.seek(fatoPosition + 0xc + i * 4);
      input.seek(input.readUInt32() + fatbPosition + 0xc);

      const flags = input.readUInt32();

      let folder = '';
      if (flags !== 1) folder = `folder_${String(i).padStart(5, '0')}/`;

      for (let bit = 0; bit < 32; bit++) {
        if ((flags & (1 << bit)) > 0) {
          const startOffset = input.readUInt32();
          const endOffset = input.readUInt32();
          const length = input.readUInt32();

          const position = input.position;

          input.seek(startOffset + dataOffset);

          // Only read the first few bytes needed for magic detection (not the full entry)
          const peekLen = Math.min(length, 16);
          const peek = input.readBytes(peekLen);

          const isCompressed = peek.length > 0 ? peek[0] === 0x11 : false;
          const extension = FileIO.getExtension(peek, isCompressed ? 5 : 0);
          const name = folder + `file_${String(flags === 1 ? i : bit).padStart(5, '0')}${extension}`;

          // Add the file to the container list
          const entry: OFileEntry = {
            name,
            data: null,
            loadFromDisk: true,
            fileOffset: startOffset + dataOffset,
            fileLength: length,
            doDecompression: isCompressed,
          };
          output.content.push(entry);

          input.seek(position);
        }
      }
    }

    return output;
  }
}
