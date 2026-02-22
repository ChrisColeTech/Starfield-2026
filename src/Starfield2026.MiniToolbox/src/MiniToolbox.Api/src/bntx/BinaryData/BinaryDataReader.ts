import { ByteOrder } from './ByteOrder.js';
import { ByteOrderHelper } from './ByteOrderHelper.js';
import { BinaryBooleanFormat } from './BinaryBooleanFormat.js';
import { BinaryDateTimeFormat } from './BinaryDateTimeFormat.js';
import { BinaryStringFormat } from './BinaryStringFormat.js';
import { BinaryMemberAttribute } from './BinaryMemberAttribute.js';
import { BinaryConverterCache } from './BinaryConverterCache.js';
import { TypeData } from './TypeData.js';
import { OffsetOrigin } from './OffsetOrigin.js';
import { EnumExtensions } from './EnumExtensions.js';
import { SeekTask } from './SeekTask.js';

/**
 * 1:1 port of BinaryDataReader.cs
 * Full-featured binary reader with endianness support, object deserialization,
 * and all the string/boolean/datetime format variants.
 */
export class BinaryDataReader {
    private _buffer: Buffer;
    private _position: number = 0;
    private _byteOrder: ByteOrder;
    private _needsReversion: boolean = false;

    get byteOrder(): ByteOrder { return this._byteOrder; }
    set byteOrder(value: ByteOrder) {
        this._byteOrder = value;
        this._needsReversion = this._byteOrder !== ByteOrderHelper.systemByteOrder;
    }

    get needsReversion(): boolean { return this._needsReversion; }
    get endOfStream(): boolean { return this._position >= this._buffer.length; }
    get length(): number { return this._buffer.length; }

    get position(): number { return this._position; }
    set position(value: number) { this._position = value; }

    get buffer(): Buffer { return this._buffer; }

    constructor(buffer: Buffer, byteOrder?: ByteOrder) {
        this._buffer = buffer;
        this._byteOrder = byteOrder ?? ByteOrderHelper.systemByteOrder;
        this._needsReversion = this._byteOrder !== ByteOrderHelper.systemByteOrder;
    }

    // ── Alignment ──

    align(alignment: number): void {
        this.seek((-this._position % alignment + alignment) % alignment);
    }

    // ── Boolean ──

    readBoolean(format?: BinaryBooleanFormat): boolean {
        const fmt = format ?? BinaryBooleanFormat.NonZeroByte;
        switch (fmt) {
            case BinaryBooleanFormat.NonZeroByte: return this.readByte() !== 0;
            case BinaryBooleanFormat.NonZeroWord: return this.readInt16() !== 0;
            case BinaryBooleanFormat.NonZeroDword: return this.readInt32() !== 0;
            default: throw new Error('The specified binary boolean format is invalid.');
        }
    }

    readBooleans(count: number, format?: BinaryBooleanFormat): boolean[] {
        return this.readMultiple(count, () => this.readBoolean(format));
    }

    // ── DateTime ──

    readDateTime(format?: BinaryDateTimeFormat): Date {
        const fmt = format ?? BinaryDateTimeFormat.NetTicks;
        switch (fmt) {
            case BinaryDateTimeFormat.CTime: {
                const seconds = this.readUInt32();
                return new Date(seconds * 1000);
            }
            case BinaryDateTimeFormat.NetTicks: {
                const ticks = this.readInt64();
                // .NET ticks are 100-nanosecond intervals since 0001-01-01
                const epochTicks = BigInt('621355968000000000'); // Ticks to Unix epoch
                const msSinceEpoch = Number((BigInt(ticks) - epochTicks) / BigInt(10000));
                return new Date(msSinceEpoch);
            }
            default: throw new Error('The specified binary date time format is invalid.');
        }
    }

    readDateTimes(count: number, format?: BinaryDateTimeFormat): Date[] {
        return this.readMultiple(count, () => this.readDateTime(format));
    }

    // ── Primitives (with endian swap) ──

    readByte(): number {
        const val = this._buffer.readUInt8(this._position);
        this._position += 1;
        return val;
    }

    readBytes(count: number): Buffer {
        const slice = this._buffer.subarray(this._position, this._position + count);
        this._position += count;
        return slice;
    }

    readSByte(): number {
        const val = this._buffer.readInt8(this._position);
        this._position += 1;
        return val;
    }

    readSBytes(count: number): number[] {
        return this.readMultiple(count, () => this.readSByte());
    }

    readInt16(): number {
        const val = this._needsReversion
            ? this._buffer.readInt16BE(this._position)
            : this._buffer.readInt16LE(this._position);
        this._position += 2;
        return val;
    }

    readInt16s(count: number): number[] {
        return this.readMultiple(count, () => this.readInt16());
    }

    readUInt16(): number {
        const val = this._needsReversion
            ? this._buffer.readUInt16BE(this._position)
            : this._buffer.readUInt16LE(this._position);
        this._position += 2;
        return val;
    }

    readUInt16s(count: number): number[] {
        return this.readMultiple(count, () => this.readUInt16());
    }

    readInt32(): number {
        const val = this._needsReversion
            ? this._buffer.readInt32BE(this._position)
            : this._buffer.readInt32LE(this._position);
        this._position += 4;
        return val;
    }

    readInt32s(count: number): number[] {
        return this.readMultiple(count, () => this.readInt32());
    }

    readUInt32(): number {
        const val = this._needsReversion
            ? this._buffer.readUInt32BE(this._position)
            : this._buffer.readUInt32LE(this._position);
        this._position += 4;
        return val;
    }

    readUInt32s(count: number): number[] {
        return this.readMultiple(count, () => this.readUInt32());
    }

    readInt64(): number {
        const val = this._needsReversion
            ? Number(this._buffer.readBigInt64BE(this._position))
            : Number(this._buffer.readBigInt64LE(this._position));
        this._position += 8;
        return val;
    }

    readInt64s(count: number): number[] {
        return this.readMultiple(count, () => this.readInt64());
    }

    readUInt64(): number {
        const val = this._needsReversion
            ? Number(this._buffer.readBigUInt64BE(this._position))
            : Number(this._buffer.readBigUInt64LE(this._position));
        this._position += 8;
        return val;
    }

    readUInt64s(count: number): number[] {
        return this.readMultiple(count, () => this.readUInt64());
    }

    readSingle(): number {
        const val = this._needsReversion
            ? this._buffer.readFloatBE(this._position)
            : this._buffer.readFloatLE(this._position);
        this._position += 4;
        return val;
    }

    readSingles(count: number): number[] {
        return this.readMultiple(count, () => this.readSingle());
    }

    readDouble(): number {
        const val = this._needsReversion
            ? this._buffer.readDoubleBE(this._position)
            : this._buffer.readDoubleLE(this._position);
        this._position += 8;
        return val;
    }

    readDoubles(count: number): number[] {
        return this.readMultiple(count, () => this.readDouble());
    }

    readDecimal(): number {
        // .NET decimal is 128 bits: 4 int32s. bits[0..2] = 96-bit mantissa, bits[3] = sign + scale.
        // Port of C# DecimalFromBytes: new decimal(int[4])
        const bytes = this.readBytes(16);
        let buf = bytes;
        if (this._needsReversion) {
            buf = Buffer.from(bytes).reverse();
        }
        const lo = buf.readInt32LE(0);
        const mid = buf.readInt32LE(4);
        const hi = buf.readInt32LE(8);
        const flags = buf.readInt32LE(12);

        const negative = (flags & 0x80000000) !== 0;
        const scale = (flags >>> 16) & 0xFF;

        // Reconstruct 96-bit mantissa as a JS number (may lose precision beyond 2^53)
        const mantissa = ((hi >>> 0) * 0x100000000 + (mid >>> 0)) * 0x100000000 + (lo >>> 0);
        const value = mantissa / Math.pow(10, scale);
        return negative ? -value : value;
    }

    readDecimals(count: number): number[] {
        return this.readMultiple(count, () => this.readDecimal());
    }

    // ── String ──

    readString(formatOrLength?: BinaryStringFormat | number): string {
        if (typeof formatOrLength === 'number') {
            return this.readStringWithLength(formatOrLength);
        }
        const format: BinaryStringFormat = formatOrLength ?? BinaryStringFormat.VariableLengthPrefix;
        switch (format as BinaryStringFormat) {
            case BinaryStringFormat.ByteLengthPrefix:
                return this.readStringInternal(this.readByte());
            case BinaryStringFormat.WordLengthPrefix:
                return this.readStringInternal(this.readInt16());
            case BinaryStringFormat.DwordLengthPrefix:
                return this.readStringInternal(this.readInt32());
            case BinaryStringFormat.VariableLengthPrefix:
                return this.readStringInternal(this.read7BitEncodedInt());
            case BinaryStringFormat.ZeroTerminated:
                return this.readZeroTerminatedString();
            case BinaryStringFormat.NoPrefixOrTermination:
                throw new Error('NoPrefixOrTermination cannot be used for read operations if no length has been specified.');
            default:
                throw new Error('The specified binary string format is invalid.');
        }
    }

    readStringWithLength(length: number): string {
        const bytes = this.readBytes(length);
        return bytes.toString('utf8');
    }

    readStrings(count: number, formatOrLength?: BinaryStringFormat | number): string[] {
        return this.readMultiple(count, () => this.readString(formatOrLength));
    }

    // ── Enum ──

    readEnum<T extends number>(enumObj: Record<string, number | string>, strict: boolean = false): T {
        // Read the underlying value (assumed to be the same width as the enum's underlying type)
        // For simplicity, read as int32 — callers should use readByte/readUInt16/etc. for smaller enums
        const value = this.readInt32();
        if (strict && !EnumExtensions.isValid(enumObj, value)) {
            throw new Error(`Read value ${value} is not defined in the given enum type.`);
        }
        return value as T;
    }

    // ── Object (reflection-based deserialization) ──

    readObject(typeName: string): any {
        return this.readObjectInternal(null, BinaryMemberAttribute.Default, typeName);
    }

    readObjects(count: number, typeName: string): any[] {
        return this.readMultiple(count, () => this.readObject(typeName));
    }

    private readObjectInternal(instance: any, attribute: BinaryMemberAttribute, typeName: string): any {
        if (attribute.converter !== null) {
            const converter = BinaryConverterCache.getConverter(attribute.converter);
            return converter.read(this, instance, attribute);
        }

        // Primitive type dispatch (matching C# ReadObject)
        switch (typeName) {
            case 'string':
                if (attribute.stringFormat === BinaryStringFormat.NoPrefixOrTermination) {
                    return this.readStringWithLength(attribute.length);
                }
                return this.readString(attribute.stringFormat);
            case 'boolean': return this.readBoolean(attribute.booleanFormat);
            case 'byte': return this.readByte();
            case 'sbyte': return this.readSByte();
            case 'int16': return this.readInt16();
            case 'uint16': return this.readUInt16();
            case 'int32': return this.readInt32();
            case 'uint32': return this.readUInt32();
            case 'int64': return this.readInt64();
            case 'uint64': return this.readUInt64();
            case 'float': return this.readSingle();
            case 'double': return this.readDouble();
            case 'decimal': return this.readDecimal();
            case 'datetime': return this.readDateTime(attribute.dateTimeFormat);
            default:
                // Custom object — use TypeData registry
                return this.readCustomObject(typeName, instance, this._position);
        }
    }

    private readCustomObject(typeName: string, instance: any, startOffset: number): any {
        const typeData = TypeData.getTypeData(typeName);
        instance = instance ?? typeData.getInstance();

        for (const member of typeData.members) {
            if (member.attribute.offsetOrigin === OffsetOrigin.Begin) {
                this._position = startOffset + member.attribute.offset;
            } else if (member.attribute.offset !== 0) {
                this._position += member.attribute.offset;
            }

            let value: any;
            if (member.attribute.length > 0 && member.type.endsWith('[]')) {
                // Array member
                const elementType = member.type.replace('[]', '');
                const arr: any[] = [];
                for (let i = 0; i < member.attribute.length; i++) {
                    arr.push(this.readObjectInternal(instance, member.attribute, elementType));
                }
                value = arr;
            } else {
                value = this.readObjectInternal(instance, member.attribute, member.type);
            }

            member.setValue(instance, value);
        }

        return instance;
    }

    // ── Seek ──

    seek(offset: number, origin?: 'begin' | 'current' | 'end'): number {
        const o = origin ?? 'current';
        switch (o) {
            case 'begin': this._position = offset; break;
            case 'current': this._position += offset; break;
            case 'end': this._position = this._buffer.length + offset; break;
        }
        return this._position;
    }

    temporarySeek(offset?: number, origin?: 'begin' | 'current' | 'end'): SeekTask {
        return new SeekTask(this, offset ?? 0, origin ?? 'current');
    }

    // ── Private helpers ──

    private readMultiple<T>(count: number, readFunc: () => T): T[] {
        const arr = new Array<T>(count);
        for (let i = 0; i < count; i++) {
            arr[i] = readFunc();
        }
        return arr;
    }

    private readStringInternal(length: number): string {
        const bytes = this.readBytes(length);
        return bytes.toString('utf8');
    }

    private readZeroTerminatedString(): string {
        const bytes: number[] = [];
        let b = this.readByte();
        while (b !== 0) {
            bytes.push(b);
            if (this.endOfStream) break;
            b = this.readByte();
        }
        return Buffer.from(bytes).toString('utf8');
    }

    private read7BitEncodedInt(): number {
        let result = 0;
        let shift = 0;
        let b: number;
        do {
            b = this.readByte();
            result |= (b & 0x7F) << shift;
            shift += 7;
        } while (b >= 0x80);
        return result;
    }
}
