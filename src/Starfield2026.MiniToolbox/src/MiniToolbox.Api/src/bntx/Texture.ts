import { BinaryDataReader, SeekTask } from './BinaryDataReader.js';
import { IResData } from './types.js';
import { SurfaceFormat, TileMode, Dim, SurfaceDim, AccessFlags, ChannelType } from './GFX.js';

export class Texture implements IResData {
    public channelRed: ChannelType = ChannelType.Red;
    public channelGreen: ChannelType = ChannelType.Green;
    public channelBlue: ChannelType = ChannelType.Blue;
    public channelAlpha: ChannelType = ChannelType.Alpha;

    public width: number = 0;
    public height: number = 0;
    public mipCount: number = 0;
    public format: SurfaceFormat = SurfaceFormat.Invalid;
    public name: string = "";
    public depth: number = 0;
    public tileMode: TileMode = TileMode.Default;
    public swizzle: number = 0;
    public alignment: number = 0;
    public dim: Dim = Dim.Undefined;
    public surfaceDim: SurfaceDim = SurfaceDim.Dim2D;
    public mipOffsets: number[] = [];
    public textureData: Buffer[][] = []; // [ArrayLayer][MipLevel]
    public arrayLength: number = 1;
    public flags: number = 0;
    public imageSize: number = 0;
    public sampleCount: number = 0;
    public blockHeightLog2: number = 0;

    load(reader: BinaryDataReader): void {
        const sig = reader.readStringWithLength(4);
        if (sig !== 'BRTI') {
            throw new Error(`Invalid Texture signature: ${sig}`);
        }

        // Header block (size/offset)
        const headerOffset = reader.readUInt32();
        const headerSize = reader.readInt64();

        this.flags = reader.readByte();
        this.dim = reader.readByte() as Dim;
        this.tileMode = reader.readUInt16() as TileMode;
        this.swizzle = reader.readUInt16();
        this.mipCount = reader.readUInt16();
        this.sampleCount = reader.readUInt32();
        this.format = reader.readUInt32() as SurfaceFormat;
        const accessFlags = reader.readUInt32() as AccessFlags;

        this.width = reader.readUInt32();
        this.height = reader.readUInt32();
        this.depth = reader.readUInt32();
        this.arrayLength = reader.readUInt32();

        const textureLayout = reader.readUInt32();
        const textureLayout2 = reader.readUInt32();
        reader.seek(20); // Skip reserved

        this.imageSize = reader.readUInt32();
        this.alignment = reader.readInt32();
        const channelMapping = reader.readUInt32();
        this.surfaceDim = reader.readUInt32() as SurfaceDim;

        // Name offset
        const nameOffset = Number(reader.readInt64());

        // Offsets to sub-resources
        const parentBntxOffset = reader.readInt64();
        const mipOffsetsOffset = reader.readInt64();
        const userDataDictOffset = reader.readInt64();
        const userDataOffset = reader.readInt64();
        const textureDataOffset = reader.readInt64();
        const unknownOffset = reader.readInt64();

        // Map channels
        this.channelRed = (channelMapping & 0xFF) as ChannelType;
        this.channelGreen = ((channelMapping >> 8) & 0xFF) as ChannelType;
        this.channelBlue = ((channelMapping >> 16) & 0xFF) as ChannelType;
        this.channelAlpha = ((channelMapping >> 24) & 0xFF) as ChannelType;

        this.blockHeightLog2 = textureLayout & 7;

        // Load Mip Offsets
        if (mipOffsetsOffset !== 0) {
            SeekTask.run(reader, Number(mipOffsetsOffset), 'begin', () => {
                this.mipOffsets = [];
                for (let i = 0; i < this.mipCount; i++) {
                    this.mipOffsets.push(reader.readInt64());
                }
            });
        }

        // Load Name
        if (nameOffset !== 0) {
            SeekTask.run(reader, nameOffset, 'begin', () => {
                // String in BNTX are WordLengthPrefix
                const len = reader.readUInt16();
                this.name = reader.readStringWithLength(len);
            });
        }

        // Load Texture Data
        this.textureData = [];
        if (this.mipOffsets.length > 0) {
            let currentArrayOffset = 0;
            for (let i = 0; i < this.arrayLength; i++) {
                const layerData: Buffer[] = [];
                for (let j = 0; j < this.mipCount; j++) {
                    // C# Logic for calculating mip size:
                    // int num9 = (int)((MipOffsets[0] + ImageSize - MipOffsets[num8]) / ArrayLength);
                    const firstMipOffset = this.mipOffsets[0];
                    const mipSize = Math.floor((firstMipOffset + this.imageSize - this.mipOffsets[j]) / this.arrayLength);

                    SeekTask.run(reader, currentArrayOffset + this.mipOffsets[j], 'begin', () => {
                        layerData.push(reader.readBytes(mipSize));
                    });
                }
                this.textureData.push(layerData);
                // This is a bit simplified, C# updates num6 based on list[0].length
                currentArrayOffset += layerData[0].length;
            }
        }
    }
}
