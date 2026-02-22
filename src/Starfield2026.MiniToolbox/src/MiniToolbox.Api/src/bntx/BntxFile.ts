import { BinaryDataReader, ByteOrder, SeekTask } from './BinaryDataReader.js';
import { RelocationTable } from './RelocationTable.js';
import { ResDict } from './ResDict.js';
import { StringTable } from './StringTable.js';
import { Texture } from './Texture.js';
import { IResData } from './types.js';

export class BntxFile implements IResData {
    public name: string = "";
    public target: string = "";
    public versionMajor: number = 0;
    public versionMajor2: number = 0;
    public versionMinor: number = 0;
    public versionMinor2: number = 0;
    public byteOrder: ByteOrder = ByteOrder.LittleEndian;
    public alignment: number = 0;
    public targetAddressSize: number = 0;
    public flag: number = 0;
    public blockOffset: number = 0;

    public relocationTable: RelocationTable = new RelocationTable();
    public textures: Texture[] = [];
    public textureDict: ResDict = new ResDict();
    public stringTable: StringTable = new StringTable();

    get platformTarget(): string {
        return this.target;
    }

    get versionFull(): string {
        return `${this.versionMajor}.${this.versionMajor2}.${this.versionMinor}.${this.versionMinor2}`;
    }

    load(reader: BinaryDataReader): void {
        const loader = new BntxLoader(this, reader);
        loader.execute();
    }

    static fromBuffer(buffer: Buffer): BntxFile {
        const file = new BntxFile();
        const reader = new BinaryDataReader(buffer);
        file.load(reader);
        return file;
    }
}

/**
 * Helper class to mirror C# BntxFileLoader logic
 */
class BntxLoader {
    private file: BntxFile;
    private reader: BinaryDataReader;

    constructor(file: BntxFile, reader: BinaryDataReader) {
        this.file = file;
        this.reader = reader;
    }

    execute(): void {
        const reader = this.reader;
        const file = this.file;

        // Signature
        const sig = reader.readStringWithLength(4);
        if (sig !== 'BNTX') {
            throw new Error(`Invalid BNTX signature: ${sig}`);
        }

        reader.readUInt32(); // Skip padding/reserved
        const versionInfo = reader.readUInt32();
        this.setVersionInfo(versionInfo);

        file.byteOrder = reader.readUInt16() as ByteOrder;
        reader.byteOrder = file.byteOrder; // Sync reader byte order

        file.alignment = reader.readByte();
        file.targetAddressSize = reader.readByte();

        const fileNameOffset = Number(reader.readUInt32());
        file.flag = reader.readUInt16();
        file.blockOffset = reader.readUInt16();

        const relocationTableOffset = reader.readUInt32();
        const fileSizeToRLT = reader.readUInt32();

        file.target = reader.readStringWithLength(4);

        const textureCount = reader.readInt32();
        const infoPtrArrayOffset = Number(reader.readInt64());
        const dataBlockOffset = reader.readInt64();

        // Load Textures
        if (infoPtrArrayOffset !== 0) {
            SeekTask.run(reader, infoPtrArrayOffset, 'begin', () => {
                file.textures = [];
                const textureOffsets: number[] = [];
                for (let i = 0; i < textureCount; i++) {
                    textureOffsets.push(Number(reader.readInt64()));
                }

                for (const offset of textureOffsets) {
                    SeekTask.run(reader, offset, 'begin', () => {
                        const tex = new Texture();
                        tex.load(reader);
                        file.textures.push(tex);
                    });
                }
            });
        }

        // Texture Dictionary
        const textureDictOffset = Number(reader.readInt64());
        if (textureDictOffset !== 0) {
            SeekTask.run(reader, textureDictOffset, 'begin', () => {
                file.textureDict.load(reader);

                // Populate node keys from string table (loaded later or inline)
                // In BNTX, ResDict nodes have string offsets that we need to resolve
                const nodeReader = new BinaryDataReader(reader.buffer);
                nodeReader.byteOrder = reader.byteOrder;
                for (let i = 0; i < file.textureDict.nodes.length; i++) {
                    const node = file.textureDict.nodes[i];
                    // Re-read node to get key offset (IdxLeft/Right are at +4, +6. Key offset is at +8)
                    // Each node is 16 bytes.
                    const nodeOffset = textureDictOffset + 8 + (i * 16);
                    nodeReader.position = nodeOffset;
                    const keyOffset = Number(nodeReader.readInt64());
                    if (keyOffset !== 0) {
                        nodeReader.position = keyOffset;
                        const len = nodeReader.readUInt16();
                        node.key = nodeReader.readStringWithLength(len);
                    }
                }
            });
        }

        // Name
        if (fileNameOffset !== 0) {
            SeekTask.run(reader, fileNameOffset - 2, 'begin', () => {
                const len = reader.readUInt16();
                file.name = reader.readStringWithLength(len);
            });
        }

        // Relocation Table
        if (relocationTableOffset !== 0) {
            SeekTask.run(reader, relocationTableOffset, 'begin', () => {
                file.relocationTable.load(reader);
            });
        }

        // String Table — position after texture info pointer array, aligned
        reader.position = infoPtrArrayOffset + (textureCount * 8);
        reader.position = (reader.position + 7) & ~7;

        try {
            file.stringTable.load(reader);
        } catch (e) {
            // String table is sometimes missing or at different offset, failing silently like some parsers
            console.warn("Could not load string table:", e);
        }
    }

    private setVersionInfo(version: number): void {
        this.file.versionMajor = version >>> 24;
        this.file.versionMajor2 = (version >>> 16) & 0xFF;
        this.file.versionMinor = (version >>> 8) & 0xFF;
        this.file.versionMinor2 = version & 0xFF;
    }
}
