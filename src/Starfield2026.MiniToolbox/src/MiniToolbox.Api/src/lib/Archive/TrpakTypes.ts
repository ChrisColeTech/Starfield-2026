/**
 * TRPAK Archive Type Definitions
 * Converted from C# TrpakTypes.cs
 */

import * as fs from 'fs';

// ============================================================================
// Binary Structs
// ============================================================================

export enum TrpakCompressionType {
    None = 0,
    Zlib = 1,
    Lz4 = 2,
    Oodle = 3
}

export interface TrpakHeader {
    magic: bigint;
    Version: number;
    Relocated: number;
    FileNumber: number;
    FolderNumber: number;
}

export namespace TrpakHeader {
    export const Magic = 0x4B434150_584C4647n; // "GFLXPACK"
    export const Size = 0x18;
}

export interface TrpakFolderHeader {
    Hash: bigint;
    ContentNumber: number;
    Reserved: number;
}

export namespace TrpakFolderHeader {
    export const Size = 0x10;
}

export interface TrpakFolderIndex {
    Hash: bigint;
    Index: number;
    Reserved: number;
}

export namespace TrpakFolderIndex {
    export const Size = 0x10;
}

export interface TrpakFileHeader {
    Level: number;
    CompressionType: TrpakCompressionType;
    BufferSize: number;
    FileSize: number;
    Reserved: number;
    FilePointer: bigint;
}

export namespace TrpakFileHeader {
    export const Size = 0x18;
}

// ============================================================================
// FNV Hash
// ============================================================================

/**
 * FNV-1a 64-bit hash used by TRPAK for file/folder name lookup.
 * Ported from gftool GFFNV.cs.
 */
export class FnvHash {
    private static readonly FnvPrime = 0x00000100000001b3n;
    private static readonly FnvBasis = 0xCBF29CE484222645n;

    static Hash(str: string): bigint {
        const buf = Buffer.from(str, 'utf8');
        let result = this.FnvBasis;
        for (const b of buf) {
            result ^= BigInt(b);
            result *= this.FnvPrime;
        }
        return result;
    }
}

// ============================================================================
// Hash Cache
// ============================================================================

/**
 * Maps FNV hashes back to human-readable file paths.
 * Ported from gftool GFPakHashCache.cs.
 */
export class TrpakHashCache {
    private _cache: Map<bigint, string> = new Map();

    get count(): number {
        return this._cache.size;
    }

    /**
     * Load a binary hash cache file (GFPAKHashCache.bin format).
     */
    LoadBinaryCache(path: string): void {
        if (!fs.existsSync(path)) return;
        const data = fs.readFileSync(path);
        let offset = 0;
        
        const count = data.readBigUInt64LE(offset);
        offset += 8;
        
        for (let i = 0n; i < count; i++) {
            const hash = data.readBigUInt64LE(offset);
            offset += 8;
            
            // Read .NET BinaryReader string format (7-bit encoded length prefix)
            let length = 0;
            let shift = 0;
            while (true) {
                const b = data[offset++];
                length |= (b & 0x7F) << shift;
                if ((b & 0x80) === 0) break;
                shift += 7;
            }
            
            const name = data.toString('utf8', offset, offset + length);
            offset += length;
            
            this._cache.set(hash, name);
        }
    }

    /**
     * Load a text hash list (one "hash path" per line).
     */
    LoadHashList(lines: string[]): void {
        for (const line of lines) {
            if (!line || line.trim().length === 0) continue;
            const parts = line.trim().split(/\s+/).filter(p => p.length > 0);
            if (parts.length < 2) continue;

            const parseResult = this.TryParseHex(parts[0]);
            if (parseResult.success) {
                this._cache.set(parseResult.value, parts[1]);
            } else {
                this._cache.set(FnvHash.Hash(parts[1]), parts[1]);
            }
        }
    }

    Add(hash: bigint, name: string): void {
        this._cache.set(hash, name);
    }

    GetName(hash: bigint): string | null {
        return this._cache.get(hash) ?? null;
    }

    private TryParseHex(text: string): { success: boolean; value: bigint } {
        if (!text || text.trim().length === 0) {
            return { success: false, value: 0n };
        }
        
        let trimmed = text.trim();
        if (trimmed.toLowerCase().startsWith('0x')) {
            trimmed = trimmed.slice(2);
        }
        
        try {
            const value = BigInt('0x' + trimmed);
            return { success: true, value };
        } catch {
            return { success: false, value: 0n };
        }
    }
}

// ============================================================================
// Output Models
// ============================================================================

export class TrpakArchive {
    Folders: TrpakFolder[] = [];

    /**
     * Find a file by its full path (case-insensitive).
     */
    FindFile(fullPath: string): TrpakFile | null {
        for (const folder of this.Folders) {
            for (const file of folder.Files) {
                if (file.FullName.toLowerCase() === fullPath.toLowerCase()) {
                    return file;
                }
            }
        }
        return null;
    }

    /**
     * Find all files matching a predicate.
     */
    FindFiles(predicate: (file: TrpakFile) => boolean): TrpakFile[] {
        const results: TrpakFile[] = [];
        for (const folder of this.Folders) {
            for (const file of folder.Files) {
                if (predicate(file)) {
                    results.push(file);
                }
            }
        }
        return results;
    }

    /**
     * Find all files with a specific extension.
     */
    FindFilesByExtension(extension: string): TrpakFile[] {
        return this.FindFiles(f => 
            f.FullName.toLowerCase().endsWith(extension.toLowerCase())
        );
    }
}

export class TrpakFolder {
    Path: string = '';
    Files: TrpakFile[] = [];
}

export class TrpakFile {
    Name: string = '';
    FullName: string = '';
    Data: Buffer = Buffer.alloc(0);
}
