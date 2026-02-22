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
import { Offset } from './Offset.js';
import { SeekTask } from './SeekTask.js';

/**
 * 1:1 port of BinaryDataWriter.cs
 * Full-featured binary writer with endianness support, object serialization,
 * and all the string/boolean/datetime format variants.
 */
export class BinaryDataWriter {
    private _buffer: Buffer;
    private _position: number = 0;
    private _byteOrder: ByteOrder;
    private _needsReversion: boolean = false;
    private _capacity: number;

    get byteOrder(): ByteOrder { return this._byteOrder; }
    set byteOrder(value: ByteOrder) {
        this._byteOrder = value;
        this._needsReversion = this._byteOrder !== ByteOrderHelper.systemByteOrder;
    }

    get needsReversion(): boolean { return this._needsReversion; }

    get position(): number { return this._position; }
    set position(value: number) { this._position = value; }

    get length(): number { return this._position; }

    constructor(initialCapacity: number = 4096) {
        this._capacity = initialCapacity;
        this._buffer = Buffer.alloc(this._capacity);
        this._byteOrder = ByteOrderHelper.systemByteOrder;
    }

    /** Get the written data as a Buffer */
    toBuffer(): Buffer {
        return this._buffer.subarray(0, this._position);
    }

    private ensureCapacity(additional: number): void {
        while (this._position + additional > this._capacity) {
            this._capacity *= 2;
            const newBuf = Buffer.alloc(this._capacity);
            this._buffer.copy(newBuf);
            this._buffer = newBuf;
        }
    }

    // ── Alignment ──

    align(alignment: number): void {
        this.seek((-this._position % alignment + alignment) % alignment);
    }

    reserveOffset(): Offset {
        return new Offset(this);
    }

    reserveOffsets(count: number): Offset[] {
        const offsets: Offset[] = [];
        for (let i = 0; i < count; i++) {
            offsets.push(this.reserveOffset());
        }
        return offsets;
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

    // ── Boolean ──

    writeBoolean(value: boolean, format?: BinaryBooleanFormat): void {
        const fmt = format ?? BinaryBooleanFormat.NonZeroByte;
        switch (fmt) {
            case BinaryBooleanFormat.NonZeroByte:
                this.writeByte(value ? 1 : 0);
                break;
            case BinaryBooleanFormat.NonZeroWord:
                this.writeInt16(value ? 1 : 0);
                break;
            case BinaryBooleanFormat.NonZeroDword:
                this.writeInt32(value ? 1 : 0);
                break;
            default:
                throw new Error('The specified binary boolean format is invalid.');
        }
    }

    writeBooleans(values: boolean[], format?: BinaryBooleanFormat): void {
        for (const v of values) this.writeBoolean(v, format);
    }

    // ── DateTime ──

    writeDateTime(value: Date, format?: BinaryDateTimeFormat): void {
        const fmt = format ?? BinaryDateTimeFormat.NetTicks;
        switch (fmt) {
            case BinaryDateTimeFormat.CTime:
                this.writeUInt32(Math.floor(value.getTime() / 1000));
                break;
            case BinaryDateTimeFormat.NetTicks: {
                const epochTicks = BigInt('621355968000000000');
                const ticks = epochTicks + BigInt(value.getTime()) * BigInt(10000);
                this.writeInt64(Number(ticks));
                break;
            }
            default:
                throw new Error('The specified binary date time format is invalid.');
        }
    }

    writeDateTimes(values: Date[], format?: BinaryDateTimeFormat): void {
        for (const v of values) this.writeDateTime(v, format);
    }

    // ── Primitives ──

    writeByte(value: number): void {
        this.ensureCapacity(1);
        this._buffer.writeUInt8(value, this._position);
        this._position += 1;
    }

    writeBytes(values: Buffer | number[]): void {
        const buf = Buffer.isBuffer(values) ? values : Buffer.from(values);
        this.ensureCapacity(buf.length);
        buf.copy(this._buffer, this._position);
        this._position += buf.length;
    }

    writeSByte(value: number): void {
        this.ensureCapacity(1);
        this._buffer.writeInt8(value, this._position);
        this._position += 1;
    }

    writeInt16(value: number): void {
        this.ensureCapacity(2);
        if (this._needsReversion) {
            this._buffer.writeInt16BE(value, this._position);
        } else {
            this._buffer.writeInt16LE(value, this._position);
        }
        this._position += 2;
    }

    writeInt16s(values: number[]): void {
        for (const v of values) this.writeInt16(v);
    }

    writeUInt16(value: number): void {
        this.ensureCapacity(2);
        if (this._needsReversion) {
            this._buffer.writeUInt16BE(value, this._position);
        } else {
            this._buffer.writeUInt16LE(value, this._position);
        }
        this._position += 2;
    }

    writeUInt16s(values: number[]): void {
        for (const v of values) this.writeUInt16(v);
    }

    writeInt32(value: number): void {
        this.ensureCapacity(4);
        if (this._needsReversion) {
            this._buffer.writeInt32BE(value, this._position);
        } else {
            this._buffer.writeInt32LE(value, this._position);
        }
        this._position += 4;
    }

    writeInt32s(values: number[]): void {
        for (const v of values) this.writeInt32(v);
    }

    writeUInt32(value: number): void {
        this.ensureCapacity(4);
        if (this._needsReversion) {
            this._buffer.writeUInt32BE(value, this._position);
        } else {
            this._buffer.writeUInt32LE(value, this._position);
        }
        this._position += 4;
    }

    writeUInt32s(values: number[]): void {
        for (const v of values) this.writeUInt32(v);
    }

    writeInt64(value: number): void {
        this.ensureCapacity(8);
        if (this._needsReversion) {
            this._buffer.writeBigInt64BE(BigInt(value), this._position);
        } else {
            this._buffer.writeBigInt64LE(BigInt(value), this._position);
        }
        this._position += 8;
    }

    writeInt64s(values: number[]): void {
        for (const v of values) this.writeInt64(v);
    }

    writeUInt64(value: number): void {
        this.ensureCapacity(8);
        if (this._needsReversion) {
            this._buffer.writeBigUInt64BE(BigInt(value), this._position);
        } else {
            this._buffer.writeBigUInt64LE(BigInt(value), this._position);
        }
        this._position += 8;
    }

    writeUInt64s(values: number[]): void {
        for (const v of values) this.writeUInt64(v);
    }

    writeSingle(value: number): void {
        this.ensureCapacity(4);
        if (this._needsReversion) {
            this._buffer.writeFloatBE(value, this._position);
        } else {
            this._buffer.writeFloatLE(value, this._position);
        }
        this._position += 4;
    }

    writeSingles(values: number[]): void {
        for (const v of values) this.writeSingle(v);
    }

    writeDouble(value: number): void {
        this.ensureCapacity(8);
        if (this._needsReversion) {
            this._buffer.writeDoubleBE(value, this._position);
        } else {
            this._buffer.writeDoubleLE(value, this._position);
        }
        this._position += 8;
    }

    writeDoubles(values: number[]): void {
        for (const v of values) this.writeDouble(v);
    }

    // ── String ──

    writeString(value: string, format?: BinaryStringFormat): void {
        const fmt = format ?? BinaryStringFormat.VariableLengthPrefix;
        const encoded = Buffer.from(value, 'utf8');
        switch (fmt) {
            case BinaryStringFormat.ByteLengthPrefix:
                this.writeByte(value.length);
                this.writeBytes(encoded);
                break;
            case BinaryStringFormat.WordLengthPrefix:
                this.writeInt16(value.length);
                this.writeBytes(encoded);
                break;
            case BinaryStringFormat.DwordLengthPrefix:
                this.writeInt32(value.length);
                this.writeBytes(encoded);
                break;
            case BinaryStringFormat.VariableLengthPrefix:
                this.write7BitEncodedInt(value.length);
                this.writeBytes(encoded);
                break;
            case BinaryStringFormat.ZeroTerminated:
                this.writeBytes(encoded);
                this.writeByte(0);
                break;
            case BinaryStringFormat.NoPrefixOrTermination:
                this.writeBytes(encoded);
                break;
            default:
                throw new Error('The specified binary string format is invalid.');
        }
    }

    writeStrings(values: string[], format?: BinaryStringFormat): void {
        for (const v of values) this.writeString(v, format);
    }

    // ── Enum ──

    writeEnum(enumObj: Record<string, number | string>, value: number, strict: boolean = false): void {
        if (strict && !EnumExtensions.isValid(enumObj, value)) {
            throw new Error(`Value ${value} to write is not defined in the given enum type.`);
        }
        this.writeInt32(value);
    }

    // ── Object (reflection-based serialization) ──

    writeObject(value: any, typeName?: string): void {
        if (value === null || value === undefined) return;
        const name = typeName ?? 'object';
        this.writeObjectInternal(null, BinaryMemberAttribute.Default, name, value);
    }

    private writeObjectInternal(instance: any, attribute: BinaryMemberAttribute, typeName: string, value: any): void {
        if (attribute.converter !== null) {
            BinaryConverterCache.getConverter(attribute.converter).write(this, instance, attribute, value);
            return;
        }

        if (value === null || value === undefined) return;

        switch (typeName) {
            case 'string': this.writeString(value, attribute.stringFormat); return;
            case 'boolean': this.writeBoolean(value, attribute.booleanFormat); return;
            case 'byte': this.writeByte(value); return;
            case 'sbyte': this.writeSByte(value); return;
            case 'int16': this.writeInt16(value); return;
            case 'uint16': this.writeUInt16(value); return;
            case 'int32': this.writeInt32(value); return;
            case 'uint32': this.writeUInt32(value); return;
            case 'int64': this.writeInt64(value); return;
            case 'uint64': this.writeUInt64(value); return;
            case 'float': this.writeSingle(value); return;
            case 'double': this.writeDouble(value); return;
            case 'datetime': this.writeDateTime(value, attribute.dateTimeFormat); return;
            default:
                if (Array.isArray(value)) {
                    const elementType = typeName.replace('[]', '');
                    for (const item of value) {
                        this.writeObjectInternal(null, BinaryMemberAttribute.Default, elementType, item);
                    }
                    return;
                }
                this.writeCustomObject(typeName, value, this._position);
        }
    }

    private writeCustomObject(typeName: string, instance: any, startOffset: number): void {
        const typeData = TypeData.getTypeData(typeName);

        for (const member of typeData.members) {
            if (member.attribute.offsetOrigin === OffsetOrigin.Begin) {
                this._position = startOffset + member.attribute.offset;
            } else if (member.attribute.offset !== 0) {
                this._position += member.attribute.offset;
            }

            const value = member.getValue(instance);

            if (member.type.endsWith('[]') && Array.isArray(value)) {
                const elementType = member.type.replace('[]', '');
                for (const item of value) {
                    this.writeObjectInternal(instance, member.attribute, elementType, item);
                }
            } else {
                this.writeObjectInternal(instance, member.attribute, member.type, value);
            }
        }
    }

    // ── Private helpers ──

    private write7BitEncodedInt(value: number): void {
        let v = value;
        while (v >= 0x80) {
            this.writeByte((v & 0x7F) | 0x80);
            v >>= 7;
        }
        this.writeByte(v);
    }
}
