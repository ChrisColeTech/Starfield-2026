import { BinaryDataReader } from './BinaryDataReader.js';
import { IResData } from './types.js';

export interface ResNode {
    reference: number;
    idxLeft: number;
    idxRight: number;
    key: string;
}

export class ResDict implements IResData {
    public nodes: ResNode[] = [];

    get count(): number {
        return this.nodes.length - 1;
    }

    load(reader: BinaryDataReader): void {
        const sectionSize = reader.readUInt32();
        let nodeCount = reader.readInt32();

        this.nodes = [];
        // ResDict nodes count is entry count + 1 (root node)
        while (nodeCount >= 0) {
            this.nodes.push(this.readNode(reader));
            nodeCount--;
        }
    }

    private readNode(reader: BinaryDataReader): ResNode {
        return {
            reference: reader.readUInt32(),
            idxLeft: reader.readUInt16(),
            idxRight: reader.readUInt16(),
            key: "" // Key is loaded by the BntxFileLoader later via offset
        };
    }

    getKey(index: number): string {
        if (index >= 0 && index < this.count) {
            return this.nodes[index + 1].key;
        }
        throw new Error(`Index ${index} out of range`);
    }
}
