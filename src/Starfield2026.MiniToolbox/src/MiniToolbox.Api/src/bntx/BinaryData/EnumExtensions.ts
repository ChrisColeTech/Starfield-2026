/**
 * 1:1 port of EnumExtensions.cs
 * Validates whether a value is a valid member of a TypeScript numeric enum,
 * including flag combinations.
 */
export class EnumExtensions {
    /**
     * Check if a value is valid for the given enum object.
     * @param enumObj The enum object (e.g. SurfaceFormat)
     * @param value The numeric value to validate
     * @param isFlags Whether the enum uses [Flags] semantics
     */
    static isValid(enumObj: Record<string, number | string>, value: number, isFlags: boolean = false): boolean {
        // Get all numeric values from the enum
        const numericValues = Object.values(enumObj).filter((v): v is number => typeof v === 'number');

        // Direct match
        if (numericValues.includes(value)) {
            return true;
        }

        // Flags check: all bits in value must be covered by defined enum values
        if (isFlags) {
            let allFlags = 0;
            for (const v of numericValues) {
                allFlags |= v;
            }
            return (allFlags & value) === value;
        }

        return false;
    }
}
