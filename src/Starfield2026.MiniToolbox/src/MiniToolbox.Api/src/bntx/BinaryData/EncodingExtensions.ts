/**
 * 1:1 port of EncodingExtensions.cs
 * In C# this adds an extension method to Encoding.
 * In TS, we just export a helper function.
 */
export function getStringFromBytes(bytes: Buffer): string {
    return bytes.toString('utf8');
}
