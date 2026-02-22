import { BinaryObjectAttribute } from './BinaryObjectAttribute.js';
import { BinaryMemberAttribute } from './BinaryMemberAttribute.js';
import { MemberData } from './MemberData.js';

/**
 * 1:1 port of TypeData.cs
 * Caches reflection metadata about binary-serializable types.
 * In C# this uses System.Reflection. In TS, we use a registration-based approach.
 */
export class TypeData {
    private static _cache = new Map<string, TypeData>();

    readonly typeName: string;
    readonly attribute: BinaryObjectAttribute;
    readonly members: MemberData[];
    readonly createInstance: () => any;

    private constructor(
        typeName: string,
        attribute: BinaryObjectAttribute,
        members: MemberData[],
        createInstance: () => any
    ) {
        this.typeName = typeName;
        this.attribute = attribute;
        this.members = members;
        this.createInstance = createInstance;
    }

    getInstance(): any {
        return this.createInstance();
    }

    /**
     * Register a type for binary serialization.
     * Call this to describe how a type's members should be read/written.
     */
    static register(
        typeName: string,
        attribute: BinaryObjectAttribute,
        members: MemberData[],
        createInstance: () => any
    ): void {
        this._cache.set(typeName, new TypeData(typeName, attribute, members, createInstance));
    }

    static getTypeData(typeName: string): TypeData {
        const data = this._cache.get(typeName);
        if (!data) {
            throw new Error(`No TypeData registered for type '${typeName}'.`);
        }
        return data;
    }

    static hasTypeData(typeName: string): boolean {
        return this._cache.has(typeName);
    }
}
