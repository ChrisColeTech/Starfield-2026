// SwitchToolboxCli — Headless Trinity model export tool
//
// Usage:
//   switchtool --arc <arcDir> --model <romfsPath> --output <outDir>
//   switchtool --arc <arcDir> --all --output <outDir>         # export ALL models
//   switchtool --arc <arcDir> --list                           # list available .trmdl files
//
// Output (split model + animation clip pattern):
//   <output>/model.dae          — mesh + skeleton + skin + materials (no animations)
//   <output>/textures/*.png     — BNTX textures as PNG
//   <output>/manifest.json      — links model + textures

import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import * as crypto from 'crypto';
import { TrpfsLoader, TrpakHashCache } from './archive/index.js';
import { TrinityModelDecoder, TrinityAnimationDecoder } from './decoders/index.js';
import { TrinityColladaExporter, EyeTextureBaker } from './exporters/index.js';
import { BntxDecoder, BntxFile } from './bntx/index.js';
import { FlatBufferConverter } from './utils/index.js';
import { TRMDL, TRMSH, TRMTR } from './flatbuffers/TR/Model/index.js';
import { Animation as GFAnimation } from './flatbuffers/GF/Animation/index.js';
import sharp from 'sharp';

export async function main(): Promise<number> {
    // ---------- Parse Args ----------

    const args = process.argv.slice(2);
    let arcDir: string | null = null;
    let modelPath: string | null = null;
    let outputDir: string | null = null;
    let listMode = false;
    let allMode = false;

    for (let i = 0; i < args.length; i++) {
        const arg = args[i];
        if (arg === '--arc' && i + 1 < args.length) {
            arcDir = args[++i];
        } else if (arg === '--model' && i + 1 < args.length) {
            modelPath = args[++i];
        } else if ((arg === '--output' || arg === '-o') && i + 1 < args.length) {
            outputDir = args[++i];
        } else if (arg === '--list') {
            listMode = true;
        } else if (arg === '--all') {
            allMode = true;
        }
    }

    if (!arcDir || arcDir.trim().length === 0) {
        console.error('Usage: switchtool --arc <arcDir> [--model <romfsPath>] [--output <outDir>] [--list]');
        return 1;
    }

    // ---------- Load Hash Cache ----------

    console.log('Loading hash cache...');
    const hashCache = new TrpakHashCache();
    const hashFile = path.join(process.cwd(), 'hashes_inside_fd.txt');
    if (fs.existsSync(hashFile)) {
        const lines = fs.readFileSync(hashFile, 'utf8').split('\n');
        hashCache.LoadHashList(lines);
        console.log(`  ${hashCache.count} entries loaded.`);
    } else {
        console.error(`  WARNING: ${hashFile} not found — will use raw hashes.`);
    }

    // ---------- Open Archive ----------

    console.log(`Opening archive: ${arcDir}`);
    const loader = new TrpfsLoader(arcDir, hashCache);
    console.log(`  ${loader.FileCount} files in descriptor, ${loader.PackNames.length} packs.`);

    // ---------- List Mode ----------

    if (listMode) {
        console.log('\nAvailable .trmdl files:');
        let count = 0;
        for (const [hash, name] of loader.FindFilesByExtension('.trmdl')) {
            console.log(`  ${name}`);
            count++;
        }
        console.log(`\n${count} model(s) found.`);
        return 0;
    }

    // ---------- Batch All Mode ----------

    if (allMode) {
        outputDir = outputDir || path.join(process.cwd(), 'export_all');
        fs.mkdirSync(outputDir, { recursive: true });
        console.log(`\nBatch exporting ALL models to: ${outputDir}`);

        const allModels: [bigint, string][] = Array.from(loader.FindFilesByExtension('.trmdl'));
        console.log(`  Found ${allModels.length} models.`);

        let success = 0, failed = 0;
        for (let mi = 0; mi < allModels.length; mi++) {
            const [_, mpath] = allModels[mi];
            // Use the parent folder name as the output directory name (e.g. pm0025_00_00)
            const modelDirName = path.parse(mpath).name;
            const modelOutDir = path.join(outputDir, modelDirName);

            console.log(`\n[${mi + 1}/${allModels.length}] ${mpath}`);
            try {
                const result = await exportModel(loader, mpath, modelOutDir);
                if (result === 0) success++; else failed++;
            } catch (ex: any) {
                console.log(`  ERROR: ${ex.message}`);
                failed++;
            }
        }

        console.log(`\n=== Batch complete: ${success} succeeded, ${failed} failed out of ${allModels.length} ===`);
        return 0;
    }

    // ---------- Export Mode ----------

    if (!modelPath || modelPath.trim().length === 0) {
        console.error('ERROR: --model <romfsPath> is required for export. Use --list to see available models.');
        return 1;
    }

    outputDir = outputDir || path.join(process.cwd(), path.parse(modelPath).name);
    return await exportModel(loader, modelPath, outputDir);
}

// ---------- Export a single model ----------

export async function exportModel(loader: TrpfsLoader, modelPath: string, outputDir: string): Promise<number> {
    fs.mkdirSync(outputDir, { recursive: true });

    console.log(`Extracting model: ${modelPath}`);
    console.log(`Output: ${outputDir}`);

    // Normalize the model path
    const normalizedModel = modelPath.replace(/\\/g, '/').replace(/^\/+/, '');

    // ---------- Extract Files to Temp ----------

    const tempRoot = path.join(os.tmpdir(), 'SwitchToolboxCli', crypto.randomUUID().replace(/-/g, ''));
    fs.mkdirSync(tempRoot, { recursive: true });

    try {
        // Extract the TRMDL itself
        const trmdlBytes = loader.ExtractFile(normalizedModel);
        if (trmdlBytes === null) {
            console.error(`ERROR: Could not find '${normalizedModel}' in archive.`);
            return 1;
        }

        writeExtractedFile(tempRoot, normalizedModel, trmdlBytes);
        console.log(`  Extracted: ${normalizedModel} (${trmdlBytes.length} bytes)`);

        // Parse TRMDL to find dependencies
        const mdl = FlatBufferConverter.DeserializeFrom(trmdlBytes, TRMDL);
        const modelDir = getDirectoryOrEmpty(normalizedModel);

        const pending: string[] = [];
        const extracted = new Set<string>([normalizedModel.toLowerCase()]);

        // Enqueue mesh files
        if (mdl.Meshes) {
            for (const mesh of mdl.Meshes) {
                if (mesh?.PathName && mesh.PathName.trim().length > 0) {
                    enqueuePath(modelDir, mesh.PathName, pending);
                }
            }
        }

        // Enqueue material files
        if (mdl.Materials) {
            for (const mat of mdl.Materials) {
                if (mat && mat.trim().length > 0) {
                    enqueuePath(modelDir, mat, pending);
                }
            }
        }

        // Enqueue skeleton
        if (mdl.Skeleton?.PathName && mdl.Skeleton.PathName.trim().length > 0) {
            enqueuePath(modelDir, mdl.Skeleton.PathName, pending);
        }

        // BFS: extract dependencies
        while (pending.length > 0) {
            const relPath = normalizePath(pending.shift()!);
            const relPathLower = relPath.toLowerCase();
            if (extracted.has(relPathLower)) continue;
            extracted.add(relPathLower);

            const bytes = loader.ExtractFile(relPath);
            if (bytes === null) {
                console.log(`  SKIP (not found): ${relPath}`);
                continue;
            }

            writeExtractedFile(tempRoot, relPath, bytes);
            console.log(`  Extracted: ${relPath} (${bytes.length} bytes)`);

            const ext = path.extname(relPath).toLowerCase();
            const dir = getDirectoryOrEmpty(relPath);

            if (ext === '.trmsh') {
                // TRMSH → buffer file
                try {
                    const msh = FlatBufferConverter.DeserializeFrom(bytes, TRMSH);
                    if (msh?.bufferFilePath && msh.bufferFilePath.trim().length > 0) {
                        enqueuePath(dir, msh.bufferFilePath, pending);
                    }
                } catch {
                    // skip parse errors
                }
            } else if (ext === '.trmtr') {
                // TRMTR → texture files (BNTX)
                try {
                    const mtr = FlatBufferConverter.DeserializeFrom(bytes, TRMTR);
                    if (mtr?.Materials) {
                        for (const mat of mtr.Materials) {
                            if (!mat?.Textures) continue;
                            for (const tex of mat.Textures) {
                                if (tex?.File && tex.File.trim().length > 0) {
                                    enqueuePath(dir, tex.File, pending);
                                }
                            }
                        }
                    }
                } catch {
                    // skip parse errors
                }
            }
        }

        // ---------- Decode Model ----------

        const trmdlOnDisk = path.join(tempRoot, normalizedModel.replace(/\//g, path.sep));
        console.log('\nDecoding model...');

        const decoder = new TrinityModelDecoder(trmdlOnDisk);
        const exportData = decoder.CreateExportData();
        console.log(`  Name: ${exportData.Name}`);
        console.log(`  Submeshes: ${exportData.Submeshes.length}`);
        console.log(`  Materials: ${exportData.Materials.length}`);
        console.log(`  Armature: ${exportData.Armature ? `${exportData.Armature.Bones.length} bones` : 'none'}`);

        // ---------- Export DAE ----------

        const daeOut = path.join(outputDir, 'model.dae');
        console.log(`\nExporting DAE: ${daeOut}`);
        TrinityColladaExporter.Export(daeOut, exportData);
        console.log('  Done.');

        // ---------- Dump Eye Material Params (diagnostic) ----------
        for (const mat of exportData.Materials) {
            if (!mat.Name.toLowerCase().includes('eye')) continue;
            console.log(`\n=== EYE MATERIAL: ${mat.Name} (shader: ${mat.ShaderName}) ===`);
            console.log('  Textures:');
            for (const tex of mat.Textures) {
                console.log(`    [${tex.Slot}] ${tex.Name} -> ${path.basename(tex.FilePath)}`);
            }
            if (mat.FloatParams && mat.FloatParams.length > 0) {
                console.log('  FloatParams:');
                for (const p of mat.FloatParams) {
                    console.log(`    ${p.Name} = ${p.Value}`);
                }
            }
            if (mat.Vec3Params && mat.Vec3Params.length > 0) {
                console.log('  Vec3Params:');
                for (const p of mat.Vec3Params) {
                    console.log(`    ${p.Name} = (${p.Value.X.toFixed(3)}, ${p.Value.Y.toFixed(3)}, ${p.Value.Z.toFixed(3)})`);
                }
            }
            if (mat.Vec4Params && mat.Vec4Params.length > 0) {
                console.log('  Vec4Params:');
                for (const p of mat.Vec4Params) {
                    console.log(`    ${p.Name} = (${p.Value.W.toFixed(3)}, ${p.Value.X.toFixed(3)}, ${p.Value.Y.toFixed(3)}, ${p.Value.Z.toFixed(3)})`);
                }
            }
            if (mat.ShaderParams && mat.ShaderParams.length > 0) {
                console.log('  ShaderParams:');
                for (const param of mat.ShaderParams) {
                    console.log(`    ${param.Name} = ${param.Value}`);
                }
            }
        }

        // ---------- Decode Textures ----------


        console.log('\nDecoding textures...');
        const texOutDir = path.join(outputDir, 'textures');
        fs.mkdirSync(texOutDir, { recursive: true });

        let texCount = 0;

        function* enumerateBntxFiles(dir: string): Generator<string> {
            const entries = fs.readdirSync(dir, { withFileTypes: true });
            for (const entry of entries) {
                const fullPath = path.join(dir, entry.name);
                if (entry.isDirectory()) {
                    yield* enumerateBntxFiles(fullPath);
                } else if (entry.name.toLowerCase().endsWith('.bntx')) {
                    yield fullPath;
                }
            }
        }

        for (const bntxFile of enumerateBntxFiles(tempRoot)) {
            try {
                const bntxBytes = fs.readFileSync(bntxFile);
                const bntxObj = BntxFile.fromBuffer(bntxBytes);
                const textures = BntxDecoder.decode(bntxObj);
                for (const tex of textures) {
                    const pngPath = path.join(texOutDir, tex.name + '.png');
                    await sharp(tex.rgbaData, { raw: { width: tex.width, height: tex.height, channels: 4 } }).png().toFile(pngPath);
                    console.log(`  ${tex.name}: ${tex.width}x${tex.height} -> ${pngPath}`);
                    texCount++;
                }
            } catch (ex: any) {
                console.log(`  WARNING: Failed to decode ${path.basename(bntxFile)}: ${ex.message}`);
            }
        }
        console.log(`  ${texCount} texture(s) exported.`);

        // ---------- Bake Eye Textures ----------

        for (const mat of exportData.Materials) {
            if (EyeTextureBaker.IsEyeMaterial(mat)) {
                EyeTextureBaker.BakeEyeTexture(mat, tempRoot, texOutDir);
            }
        }

        // ---------- Extract Animations ----------

        console.log('\nExtracting animations...');
        const animOutDir = path.join(outputDir, 'animations');
        fs.mkdirSync(animOutDir, { recursive: true });

        // Find the model's data directory (e.g. "pokemon/data/pm0025/pm0025_00_00/")
        const modelDataDir = getDirectoryOrEmpty(normalizedModel);
        let animCount = 0;

        if (exportData.Armature) {
            const animFiles: [bigint, string][] = Array.from(loader.FindFiles((name: string) =>
                name.toLowerCase().startsWith(modelDataDir.toLowerCase()) &&
                name.toLowerCase().endsWith('.tranm')
            ));
            for (const [hash, animName] of animFiles) {
                try {
                    const animBytes = loader.ExtractFile(hash.toString());
                    if (animBytes === null) continue;

                    const animFb = FlatBufferConverter.DeserializeFrom(animBytes, GFAnimation) as GFAnimation;
                    const clipName = path.parse(animName).name;
                    const animDecoder = new TrinityAnimationDecoder(animFb, clipName);

                    const clipDae = path.join(animOutDir, clipName + '.dae');
                    TrinityColladaExporter.ExportWithAnimation(clipDae, exportData, animDecoder);
                    console.log(`  ${clipName}: ${animDecoder.FrameCount} frames @ ${animDecoder.FrameRate}fps -> ${clipDae}`);
                    animCount++;
                } catch (ex: any) {
                    const shortName = path.parse(animName).name;
                    console.log(`  WARNING: Failed to decode ${shortName}: ${ex.message}`);
                }
            }
        } else {
            console.log('  No armature — skipping animation export.');
        }
        console.log(`  ${animCount} animation(s) exported.`);

        // Copy textures into animations folder so combined DAEs resolve their paths
        if (animCount > 0 && fs.existsSync(texOutDir)) {
            const animTexDir = path.join(animOutDir, 'textures');
            fs.mkdirSync(animTexDir, { recursive: true });
            const texFiles = fs.readdirSync(texOutDir).filter(f => f.toLowerCase().endsWith('.png'));
            for (const texFile of texFiles) {
                fs.copyFileSync(
                    path.join(texOutDir, texFile),
                    path.join(animTexDir, texFile)
                );
            }
            console.log(`  Copied ${fs.readdirSync(animTexDir).length} textures to ${animTexDir}`);
        }

        // ---------- Summary ----------

        console.log(`\n=== Export complete ===`);
        console.log(`  Model: ${daeOut}`);
        console.log(`  Textures: ${texCount} PNGs in ${texOutDir}`);
        console.log(`  Animations: ${animCount} clip DAEs in ${animOutDir}`);
        return 0;
    } finally {
        // Cleanup temp
        try {
            fs.rmSync(tempRoot, { recursive: true, force: true });
        } catch {
            // ignore cleanup errors
        }
    }
}

// ---------- Helpers ----------

function writeExtractedFile(root: string, relPath: string, data: Buffer): void {
    const outPath = path.join(root, relPath.replace(/\//g, path.sep));
    const dir = path.dirname(outPath);
    if (dir && dir !== '.' && !fs.existsSync(dir)) {
        fs.mkdirSync(dir, { recursive: true });
    }
    fs.writeFileSync(outPath, data);
}

function normalizePath(filePath: string): string {
    let p = (filePath || '').replace(/\\/g, '/').trim();
    if (p.toLowerCase().startsWith('romfs://')) {
        p = p.substring('romfs://'.length);
    }
    if (p.toLowerCase().startsWith('trpfs://')) {
        p = p.substring('trpfs://'.length);
    }
    return p.replace(/^\/+/, '');
}

function getDirectoryOrEmpty(romfsRelativePath: string): string {
    const normalized = normalizePath(romfsRelativePath);
    const lastSlash = normalized.lastIndexOf('/');
    return lastSlash < 0 ? '' : normalized.substring(0, lastSlash + 1);
}

function enqueuePath(currentDir: string, relativePath: string, pending: string[]): void {
    const combined = combineAndNormalize(currentDir, relativePath);
    pending.push(combined);
}

function combineAndNormalize(dir: string, rel: string): string {
    const combined = dir + rel.replace(/\\/g, '/');
    // Resolve .. segments
    const parts = combined.split('/').filter(p => p.length > 0);
    const resolved: string[] = [];
    for (const part of parts) {
        if (part === '..') {
            if (resolved.length > 0) {
                resolved.pop();
            }
        } else if (part !== '.') {
            resolved.push(part);
        }
    }
    return resolved.join('/');
}

// Main execution
(async () => {
    const exitCode = await main();
    process.exit(exitCode);
})();
