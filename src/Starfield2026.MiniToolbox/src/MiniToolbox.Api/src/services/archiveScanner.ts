import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';
import { TrpfsLoader, TrpakHashCache } from '../lib/index.js';
import type { ScanResult, FolderGroup } from '../types/index.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

/**
 * Scans a TRPFD/TRPFS archive directory and discovers all .trmdl models,
 * grouped by folder prefix.
 */
export async function scanArchive(arcPath: string): Promise<ScanResult> {
    const startTime = Date.now();

    const hashCache = loadHashCache();
    const loader = new TrpfsLoader(arcPath, hashCache);

    const modelPaths: string[] = [];
    for (const [_hash, name] of loader.FindFilesByExtension('.trmdl')) {
        modelPaths.push(name);
    }

    const groups = groupByPrefix(modelPaths);

    return {
        totalModels: modelPaths.length,
        groups,
        scanTimeMs: Date.now() - startTime,
    };
}

/**
 * Creates a TrpfsLoader for the given archive directory.
 */
export function openArchive(arcPath: string): TrpfsLoader {
    const hashCache = loadHashCache();
    return new TrpfsLoader(arcPath, hashCache);
}

function loadHashCache(): TrpakHashCache {
    const hashCache = new TrpakHashCache();

    // Search for the hash file
    // __dirname = .../SwitchToolboxCli.Api/src/services
    const candidates = [
        path.join(process.cwd(), 'hashes_inside_fd.txt'),
        path.resolve(__dirname, '..', 'lib', 'hashes_inside_fd.txt'),
    ];

    for (const candidate of candidates) {
        if (fs.existsSync(candidate)) {
            const lines = fs.readFileSync(candidate, 'utf8').split('\n');
            hashCache.LoadHashList(lines);
            console.log(`[archiveScanner] Loaded ${lines.length} hashes from ${candidate}`);
            return hashCache;
        }
    }

    console.warn('[archiveScanner] No hash file found — file names will not be resolved');
    return hashCache;
}

function groupByPrefix(paths: string[]): FolderGroup[] {
    const map = new Map<string, string[]>();

    for (const p of paths) {
        const parts = p.split('/');
        const prefix = parts.length >= 2 ? `${parts[0]}/${parts[1]}` : parts[0];
        if (!map.has(prefix)) map.set(prefix, []);
        map.get(prefix)!.push(p);
    }

    return [...map.entries()]
        .map(([prefix, modelPaths]) => ({
            prefix,
            label: `${prefix} (${modelPaths.length})`,
            modelPaths,
            modelCount: modelPaths.length,
        }))
        .sort((a, b) => b.modelCount - a.modelCount);
}
