/**
 * TRPAK Archive Loader
 * Ported from C# TrpakLoader.cs
 * 
 * Usage:
 *   const cache = new TrpakHashCache();
 *   cache.LoadHashList(fs.readFileSync('hashlist.txt', 'utf8').split('\n'));
 *   const archive = TrpakLoader.Load('model.trpak', cache);
 *   const trmdl = archive.FindFilesByExtension('.trmdl')[0];
 */

import * as fs from 'fs';
import {
    TrpakHeader,
    TrpakFolderHeader,
    TrpakFolderIndex,
    TrpakFileHeader,
    TrpakCompressionType,
    TrpakArchive,
    TrpakFolder,
    TrpakFile,
    TrpakHashCache
} from './TrpakTypes.js';
import { OodleDecompressor, Lz4Decompressor } from './Decompressors.js';

export class TrpakLoader {
    static Load(path: string, hashCache?: TrpakHashCache): TrpakArchive {
        const data = fs.readFileSync(path);
        return this.LoadFromBuffer(data, hashCache);
    }

    static LoadFromBuffer(data: Buffer, hashCache?: TrpakHashCache): TrpakArchive {
        hashCache ??= new TrpakHashCache();

        let offset = 0;

        // Read header
        const header = this.ReadStructTrpakHeader(data, offset);
        offset = TrpakHeader.Size;

        // Embedded file offset + hash offset
        const embeddedFileOff = data.readBigUInt64LE(offset);
        offset += 8;
        const embeddedFileHashOff = data.readBigUInt64LE(offset);
        offset += 8;

        // Folder offsets
        const folderOffsets: number[] = [];
        for (let i = 0; i < header.FolderNumber; i++) {
            folderOffsets.push(Number(data.readBigInt64LE(offset)));
            offset += 8;
        }

        // File hashes (absolute path hashes)
        offset = Number(embeddedFileHashOff);
        const fileHashes: bigint[] = [];
        for (let i = 0; i < header.FileNumber; i++) {
            fileHashes.push(data.readBigUInt64LE(offset));
            offset += 8;
        }

        // File headers
        offset = Number(embeddedFileOff);
        const fileHeaders: TrpakFileHeader[] = [];
        for (let i = 0; i < header.FileNumber; i++) {
            fileHeaders.push(this.ReadStructTrpakFileHeader(data, offset));
            offset += TrpakFileHeader.Size;
        }

        // Decompress files
        const files: Buffer[] = [];
        for (let i = 0; i < header.FileNumber; i++) {
            const fh = fileHeaders[i];
            const rawBytes = data.subarray(offset, offset + fh.FileSize);
            offset += fh.FileSize;

            let decompressed: Buffer;
            switch (fh.CompressionType) {
                case TrpakCompressionType.Lz4:
                    decompressed = Lz4Decompressor.Decompress(rawBytes, fh.BufferSize);
                    break;
                case TrpakCompressionType.Oodle:
                    const oodleResult = OodleDecompressor.Decompress(rawBytes, fh.BufferSize);
                    decompressed = oodleResult ?? rawBytes;
                    break;
                default:
                    decompressed = Buffer.from(rawBytes);
            }
            files.push(decompressed);
        }

        // Build folder/file structure
        const archive = new TrpakArchive();
        for (let i = 0; i < header.FolderNumber; i++) {
            offset = folderOffsets[i];
            const folderHeader = this.ReadStructTrpakFolderHeader(data, offset);
            offset += TrpakFolderHeader.Size;

            const folderName = hashCache.GetName(folderHeader.Hash) ?? folderHeader.Hash.toString(16).toUpperCase().padStart(16, '0');

            const folder = new TrpakFolder();
            folder.Path = folderName;

            for (let j = 0; j < folderHeader.ContentNumber; j++) {
                const content = this.ReadStructTrpakFolderIndex(data, offset);
                offset += TrpakFolderIndex.Size;

                const fileIndex = content.Index;
                const fileName = hashCache.GetName(content.Hash) ?? content.Hash.toString(16).toUpperCase().padStart(16, '0');

                // Try to resolve the full path from the file hash
                const fullPath = hashCache.GetName(fileHashes[fileIndex]) ?? (folderName + fileName);

                // Auto-register resolved paths
                if (hashCache.GetName(fileHashes[fileIndex]) === null) {
                    hashCache.Add(fileHashes[fileIndex], fullPath);
                }

                const trpakFile = new TrpakFile();
                trpakFile.Name = fileName;
                trpakFile.FullName = fullPath;
                trpakFile.Data = fileIndex < files.length ? files[fileIndex] : Buffer.alloc(0);

                folder.Files.push(trpakFile);
            }

            archive.Folders.push(folder);
        }

        return archive;
    }

    private static ReadStructTrpakHeader(data: Buffer, offset: number): TrpakHeader {
        return {
            magic: data.readBigUInt64LE(offset),
            Version: data.readUInt32LE(offset + 8),
            Relocated: data.readUInt32LE(offset + 12),
            FileNumber: data.readUInt32LE(offset + 16),
            FolderNumber: data.readUInt32LE(offset + 20)
        };
    }

    private static ReadStructTrpakFolderHeader(data: Buffer, offset: number): TrpakFolderHeader {
        return {
            Hash: data.readBigUInt64LE(offset),
            ContentNumber: data.readUInt32LE(offset + 8),
            Reserved: data.readUInt32LE(offset + 12)
        };
    }

    private static ReadStructTrpakFolderIndex(data: Buffer, offset: number): TrpakFolderIndex {
        return {
            Hash: data.readBigUInt64LE(offset),
            Index: data.readUInt32LE(offset + 8),
            Reserved: data.readUInt32LE(offset + 12)
        };
    }

    private static ReadStructTrpakFileHeader(data: Buffer, offset: number): TrpakFileHeader {
        return {
            Level: data.readUInt16LE(offset),
            CompressionType: data.readUInt16LE(offset + 2) as TrpakCompressionType,
            BufferSize: data.readUInt32LE(offset + 4),
            FileSize: data.readUInt32LE(offset + 8),
            Reserved: data.readUInt32LE(offset + 12),
            FilePointer: data.readBigUInt64LE(offset + 16)
        };
    }
}
