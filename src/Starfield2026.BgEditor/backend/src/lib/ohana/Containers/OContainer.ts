import { BinaryReader } from '../Core/BinaryReader.js';

/**
 * Container for file entries, ported from OhanaCli.Formats.Containers.OContainer (C#).
 * Represents an archive that holds multiple sub-files (e.g. GARC, DARC).
 */
export interface OFileEntry {
  name: string;
  data: Buffer | null;
  loadFromDisk: boolean;
  fileOffset: number;
  fileLength: number;
  doDecompression: boolean;
}

export function createFileEntry(): OFileEntry {
  return {
    name: '',
    data: null,
    loadFromDisk: false,
    fileOffset: 0,
    fileLength: 0,
    doDecompression: false,
  };
}

export class OContainer {
  data: BinaryReader | null = null;
  content: OFileEntry[] = [];
}
