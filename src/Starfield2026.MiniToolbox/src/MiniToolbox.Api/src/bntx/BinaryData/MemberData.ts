import { BinaryMemberAttribute } from './BinaryMemberAttribute.js';

/**
 * 1:1 port of MemberData.cs
 * Stores metadata about a single member (field/property) of a binary-serializable type.
 */
export class MemberData {
    readonly memberName: string;
    readonly type: string; // TypeScript type name (for debug/logging)
    readonly attribute: BinaryMemberAttribute;
    readonly getValue: (instance: any) => any;
    readonly setValue: (instance: any, value: any) => void;

    constructor(
        memberName: string,
        type: string,
        attribute: BinaryMemberAttribute,
        getValue: (instance: any) => any,
        setValue: (instance: any, value: any) => void
    ) {
        this.memberName = memberName;
        this.type = type;
        this.attribute = attribute;
        this.getValue = getValue;
        this.setValue = setValue;
    }
}
