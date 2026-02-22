/**
 * 1:1 port of BinaryObjectAttribute.cs
 * In C# this is a [BinaryObject] attribute for decorating classes/structs.
 * In TS, we represent it as a plain config object.
 */
export class BinaryObjectAttribute {
    inherit: boolean = false;
    explicit: boolean = false;
}
