import { BinaryBooleanFormat } from './BinaryBooleanFormat.js';
import { BinaryDateTimeFormat } from './BinaryDateTimeFormat.js';
import { BinaryStringFormat } from './BinaryStringFormat.js';
import { OffsetOrigin } from './OffsetOrigin.js';

/**
 * 1:1 port of BinaryMemberAttribute.cs
 * In C# this is a [BinaryMember] attribute for decorating fields/properties.
 * In TS, we represent it as a plain config object.
 */
export class BinaryMemberAttribute {
    static readonly Default = new BinaryMemberAttribute();

    offset: number = 0;
    offsetOrigin: OffsetOrigin = OffsetOrigin.Current;
    booleanFormat: BinaryBooleanFormat = BinaryBooleanFormat.NonZeroByte;
    dateTimeFormat: BinaryDateTimeFormat = BinaryDateTimeFormat.NetTicks;
    stringFormat: BinaryStringFormat = BinaryStringFormat.VariableLengthPrefix;
    length: number = 0;
    strict: boolean = false;
    converter: (new () => any) | null = null;
}
