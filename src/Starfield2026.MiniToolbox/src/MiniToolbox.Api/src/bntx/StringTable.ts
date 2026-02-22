import { BinaryDataReader } from './BinaryDataReader.js';

export class StringTable {
    private static readonly SIGNATURE = '_STR';
    public strings: Map<number, string> = new Map();

    load(reader: BinaryDataReader): void {
        const sig = reader.readStringWithLength(4);
        if (sig !== StringTable.SIGNATURE) {
            throw new Error(`Invalid StringTable signature: ${sig}`);
        }

        // Read and skip the STR header
        const sectionSize = reader.readUInt32();
        const dataSize = reader.readUInt64();
        const stringCount = reader.readUInt32();
    }
}
