import { IResData } from './types.js';
import { BinaryDataReader } from './BinaryDataReader.js';

export class RelocationTable implements IResData {
    private static readonly SIGNATURE = '_RLT';
    public position: number = 0;

    load(reader: BinaryDataReader): void {
        this.position = reader.position;
        const sig = reader.readStringWithLength(4);
        if (sig !== RelocationTable.SIGNATURE) {
            throw new Error(`Invalid RelocationTable signature: ${sig}`);
        }

        // Read and skip the RLT header
        const sectionSize = reader.readUInt32();
        const entryCount = reader.readUInt32();
        reader.seek(4); // Skip reserved
    }
}
