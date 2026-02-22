/**
 * 1:1 port of TypeExtensions.cs
 * In C# these are extension methods on Type for checking if a type is enumerable.
 * In TS, we provide equivalent helper functions for checking array-like values.
 */
export class TypeExtensions {
    /**
     * Check if a value is an enumerable (array), excluding strings.
     */
    static isEnumerable(value: any): boolean {
        if (typeof value === 'string') return false;
        return Array.isArray(value);
    }

    /**
     * Get the element type name for an array, or null if not an array.
     */
    static getEnumerableElementType(value: any): string | null {
        if (typeof value === 'string') return null;
        if (Array.isArray(value) && value.length > 0) {
            return typeof value[0];
        }
        return null;
    }

    /**
     * Try to get the element type, returning success flag.
     */
    static tryGetEnumerableElementType(value: any): { success: boolean; elementType: string | null } {
        const elementType = this.getEnumerableElementType(value);
        return { success: elementType !== null, elementType };
    }
}
