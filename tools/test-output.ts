// Auto-converted from TrpakTypes.cs
// Manual review required

import { InteropServices } from 'System/Runtime/InteropServices'
import { Text } from 'System/Text'


// // namespace SwitchToolboxCli.Core.Archive {

 export enum TrpakCompressionType {
 None = 0,
 Zlib = 1,
 Lz4 = 2,
 Oodle = 3,
}

 export class TrpakHeader {
 Magic: number = 0x4B434150_584C4647 // "GFLXPACK"
 Size: number = 0x18
 magic: number = 0
 Version: number = 0
 Relocated: number = 0
 FileNumber: number = 0
 FolderNumber: number = 0
 }

 export class TrpakFolderHeader {
 Size: number = 0x10
 Hash: number = 0
 ContentNumber: number = 0
 Reserved: number = 0
 }

 export class TrpakFolderIndex {
 Size: number = 0x10
 Hash: number = 0
 Index: number = 0
 Reserved: number = 0
 }

 export class TrpakFileHeader {
 Size: number = 0x18
 Level: number = 0
 CompressionType: TrpakCompressionType
 BufferSize: number = 0
 FileSize: number = 0
 Reserved: number = 0
 FilePointer: number = 0
 }

 /**

 * FNV-1a 64-bit hash used by TRPAK for file/folder name lookup.

 * Ported from gftool GFFNV.cs.

 */
 export class FnvHash {
 FnvPrime: number = 0x00000100000001b3
 FnvBasis: number = 0xCBF29CE484222645

 Hash(str: string): number
 {
 Buffer buf = Encoding.UTF8.GetBytes(str);
 let result = FnvBasis;
 for (const b of buf)
 {
 result ^= b;
 result *= FnvPrime;
 }
 return result;
 }
 }

 /**

 * Maps FNV hashes back to human-readable file paths.

 * Ported from gftool GFPakHashCache.cs.

 */
 export class TrpakHashCache {
 _cache: Map<number, string> = new Map()

 Count: number = > _cache.size

 /** Load a binary hash cache file (GFPAKHashCache.bin format). */
 LoadBinaryCache(path: string): void
 {
 if (!fs.existsSync(path)) return;
 readonly br = File.OpenRead(path);
 let count = br.ReadUInt64();
 for (let i = 0; i < count; i++)
 {
 let hash = br.ReadUInt64();
 let name = br.readString();
 _cache[hash] = name;
 }
 }

 /** Load a text hash list (one "hash path" per line). */
 LoadHashList(lines: string[]): void
 {
 for (const line of lines)
 {
 if (!line?.trim()) continue;
 let parts = line.trim().split((string[]?)null, StringSplitOptions.RemoveEmptyEntries);
 if (parts.length < 2) continue;

 if (TryParseHex(parts[0], ulong hash))
 _cache[hash] = parts[1];
 else
 _cache[FnvHash.Hash(parts[1])] = parts[1];
 }
 }

 Add(hash: number, name: string): void { return _cache[hash] = name }

 GetName(hash: number): string | null { return _cache.TryGetValue(hash, var name) ? name : null }

 TryParseHex(text: string, value: number): boolean
 {
 value = 0;
 if (!text?.trim()) return false;
 text = text.trim();
 if (text.startsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text.slice(text);
 return ulong.TryParse(text, System.Globalization.NumberStyles.HexNumber,
 System.Globalization.CultureInfo.InvariantCulture, value);
 }
 }

 export class TrpakArchive {
 Folders: TrpakFolder[] = {};

 /** Find a file by its full path (case-insensitive). */
 FindFile(fullPath: string): TrpakFile | null
 {
 for (const folder of Folders)
 for (const file of folder.Files)
 if (file.FullName.toLowerCase() === fullPath.toLowerCase())
 return file;
 return null;
 }

 /** Find all files matching a predicate. */
 FindFiles(Func<TrpakFile, predicate: bool>): TrpakFile[]
 {
 return Folders.SelectMany((f) => f.Files).filter(predicate);
 }

 /** Find all files with a specific extension. */
 FindFilesByExtension(extension: string): TrpakFile[]
 {
 return FindFiles((f) => f.FullName.endsWith(extension));
 }
 }

 export class TrpakFolder {
 Path: string = '';
 Files: TrpakFile[] = {};
 }

 export class TrpakFile {
 Name: string = '';
 FullName: string = '';
 Data: number[] = [] as Buffer;
 }

}
