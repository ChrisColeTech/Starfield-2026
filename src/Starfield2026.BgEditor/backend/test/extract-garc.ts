/**
 * CLI to extract a GARC archive end-to-end:
 *   GARC -> decompress -> FileIO.load -> group consecutive entries -> export
 *
 * Groups consecutive GARC entries the same way the C# OhanaCli does:
 *   - An entry with models+meshes starts a new group
 *   - Subsequent entries without meshes (textures, animations) merge into it
 *
 * Usage: npx tsx test/extract-garc.ts <garc-path> <output-dir> [--split-model-anims] [-n <limit>]
 */
import * as fs from 'fs';
import * as path from 'path';
import { GARC } from '../src/lib/ohana/Containers/GARC.js';
import { LZSS_Ninty } from '../src/lib/ohana/Compressions/LZSS_Ninty.js';
import { FileIO, formatType } from '../src/lib/ohana/Core/FileIO.js';
import type { LoadedFile } from '../src/lib/ohana/Core/FileIO.js';
import { BinaryReader } from '../src/lib/ohana/Core/BinaryReader.js';
import { DAE } from '../src/lib/ohana/Models/GenericFormats/DAE.js';
import { OModelGroup, OTexture, OSkeletalAnimation } from '../src/lib/ohana/Core/RenderBase.js';
import type { OContainer } from '../src/lib/ohana/Containers/OContainer.js';

// ---------------------------------------------------------------------------
// Manifest types (matches C# OhanaCli SplitExportManifest)
// ---------------------------------------------------------------------------
interface ManifestClip {
  index: number;
  id: string;
  name: string;
  sourceName: string;
  semanticName: string | null;
  semanticSource: string | null;
  file: string;
  frameCount: number;
  fps: number;
}

interface ManifestModel {
  name: string;
  modelFile: string;
  clips: ManifestClip[];
}

interface Manifest {
  version: number;
  mode: string;
  textures: string[];
  models: ManifestModel[];
}

// ---------------------------------------------------------------------------
// Args
// ---------------------------------------------------------------------------
const args = process.argv.slice(2);
const splitModelAnims = args.includes('--split-model-anims');
const limitIdx = args.indexOf('-n');
const limit = limitIdx >= 0 ? parseInt(args[limitIdx + 1], 10) : undefined;
const positionalArgs = args.filter((a, i) => !a.startsWith('-') && (i === 0 || !args[i - 1].startsWith('-n')));
const [garcPath, outputDir] = positionalArgs;

if (!garcPath || !outputDir) {
  console.error('Usage: npx tsx test/extract-garc.ts <garc-path> <output-dir> [--split-model-anims] [-n <limit>]');
  process.exit(1);
}

if (splitModelAnims) console.log('Mode: split model + animation clips');
if (limit) console.log(`Limit: ${limit} entries`);

fs.mkdirSync(outputDir, { recursive: true });

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Build an OModelGroup from any loaded result, recursing into containers. */
function buildModelGroup(loaded: LoadedFile, depth: number = 0): OModelGroup {
  const maxDepth = 3;
  const output = new OModelGroup();

  if (loaded.type === formatType.model && loaded.data instanceof OModelGroup) {
    output.merge(loaded.data);
    return output;
  }

  if (loaded.type === formatType.image && loaded.data instanceof OTexture) {
    output.texture.push(loaded.data);
    return output;
  }

  if (loaded.type === formatType.texture && Array.isArray(loaded.data)) {
    output.texture.push(...(loaded.data as OTexture[]));
    return output;
  }

  if (loaded.type === formatType.anims) {
    if (loaded.data instanceof OSkeletalAnimation) {
      output.skeletalAnimation.list.push(loaded.data);
    } else if (Array.isArray(loaded.data)) {
      for (const anim of loaded.data) {
        if (anim instanceof OSkeletalAnimation) {
          output.skeletalAnimation.list.push(anim);
        }
      }
    }
    return output;
  }

  if (loaded.type === formatType.container && depth < maxDepth) {
    const container = loaded.data as OContainer;
    for (const entry of container.content) {
      let subData: Buffer;
      if (entry.loadFromDisk && container.data) {
        container.data.seek(entry.fileOffset);
        const raw = container.data.readBytes(entry.fileLength);
        subData = entry.doDecompression ? LZSS_Ninty.decompress(raw) : raw;
      } else if (entry.data) {
        subData = entry.data;
      } else {
        continue;
      }

      try {
        const subLoaded = FileIO.load(BinaryReader.fromBuffer(subData));
        const subGroup = buildModelGroup(subLoaded, depth + 1);
        output.merge(subGroup);
      } catch {
        // Sub-entry failed to parse, skip
      }
    }
  }

  return output;
}

/** Count total meshes across all models in a group. */
function countMeshes(group: OModelGroup): number {
  return group.model.reduce((sum, m) => sum + m.mesh.length, 0);
}

/** Get all texture names referenced by materials in the group. */
function getRequiredTextureNames(group: OModelGroup): Set<string> {
  const required = new Set<string>();
  for (const mdl of group.model) {
    for (const mat of mdl.material) {
      if (mat.name0?.trim()) required.add(mat.name0.toLowerCase());
      if (mat.name1?.trim()) required.add(mat.name1.toLowerCase());
      if (mat.name2?.trim()) required.add(mat.name2.toLowerCase());
    }
  }
  return required;
}

/** Check if the group has all textures its materials reference. */
function hasAllReferencedTextures(group: OModelGroup): boolean {
  const required = getRequiredTextureNames(group);
  if (required.size === 0) return true;
  const available = new Set<string>();
  for (const tex of group.texture) {
    if (tex?.name?.trim()) available.add(tex.name.toLowerCase());
  }
  for (const name of required) {
    if (!available.has(name)) return false;
  }
  return true;
}

/** Deduplicate textures by name (case-insensitive). */
function deduplicateTextures(group: OModelGroup): void {
  const seen = new Set<string>();
  const deduped: OTexture[] = [];
  for (let i = 0; i < group.texture.length; i++) {
    const tex = group.texture[i];
    if (!tex) continue;
    const key = tex.name ? tex.name.toLowerCase() : `__null_${i}`;
    if (!seen.has(key)) {
      seen.add(key);
      deduped.push(tex);
    }
  }
  group.texture = deduped;
}

/** Sanitize a name for use as a file/folder name. */
function sanitizeName(value: string): string {
  if (!value || !value.trim()) return 'unnamed';
  return value.replace(/[<>:"/\\|?*]/g, '_').trim() || 'unnamed';
}

/**
 * Derive a group folder name matching C# OhanaCli convention:
 *   {groupIndex:D4}_{modelName}
 * e.g. 0000_model, 0001_model, 0002_tr0001_00_fi
 */
function deriveGroupName(group: OModelGroup, groupIndex: number, startEntry: number): string {
  const idx = String(groupIndex).padStart(4, '0');

  // Try first model with meshes
  const firstName = group.model.find(m => m.mesh.length > 0)?.name ?? '';
  if (firstName && firstName !== 'model' && firstName !== 'unnamed') {
    return `${idx}_${sanitizeName(firstName)}`;
  }

  // Try first texture name
  const firstTex = group.texture.find(t => t?.name?.trim())?.name ?? '';
  if (firstTex) {
    return `${idx}_${sanitizeName(stripImageExt(firstTex))}`;
  }

  return `${idx}_entry_${String(startEntry).padStart(5, '0')}`;
}

/**
 * Overworld character animation slot map (GARC a/2/0/0).
 * From SPICA-README.md — slot numbers are sparse.
 */
const OVERWORLD_SLOT_MAP: Record<number, string> = {
  0:   'Idle',
  1:   'Walk',
  2:   'Run',
  4:   'Jump',
  5:   'Land',
  7:   'ShortAction1',
  8:   'LongAction1',
  9:   'ShortAction2',
  17:  'MediumAction',
  20:  'Action',
  23:  'Action2',
  30:  'ShortAction3',
  31:  'ShortAction4',
  52:  'IdleVariant',
  54:  'ShortAction5',
  55:  'LongAction2',
  56:  'ShortAction6',
  59:  'Action3',
  61:  'Action4',
  72:  'Action5',
  123: 'LongAction3',
  124: 'Action6',
  125: 'Action7',
  127: 'Action8',
  128: 'Action9',
};

/**
 * Pokemon battle animation slot map (GARC a/0/9/4).
 * Only slot 0 (Idle) is verified.
 */
const POKEMON_SLOT_MAP: Record<number, string> = {
  0: 'Idle',
};

/** Asset type detection — matches C# OhanaCli AnimAssetType enum. */
type AnimAssetType = 'overworld' | 'pokemon' | 'unknown';

function detectAssetType(group: OModelGroup): AnimAssetType {
  for (const tex of group.texture) {
    if (!tex?.name) continue;
    if (tex.name.toLowerCase().startsWith('pm')) return 'pokemon';
    if (tex.name.toLowerCase().startsWith('tr')) return 'overworld';
    if (tex.name.toLowerCase().includes('_fi')) return 'overworld';
  }
  for (const mdl of group.model) {
    if (!mdl?.name) continue;
    if (mdl.name.toLowerCase().startsWith('pm')) return 'pokemon';
    if (mdl.name.toLowerCase().startsWith('tr')) return 'overworld';
  }
  return 'unknown';
}

/** Parse trailing number from source name: "anim_4" → 4, "Motion_17" → 17. */
function parseSourceAnimIndex(sourceName: string, fallback: number): number {
  if (!sourceName) return fallback;
  const lastUnderscore = sourceName.lastIndexOf('_');
  if (lastUnderscore >= 0 && lastUnderscore < sourceName.length - 1) {
    const parsed = parseInt(sourceName.slice(lastUnderscore + 1), 10);
    if (!isNaN(parsed)) return parsed;
  }
  return fallback;
}

function resolveSemanticMetadata(sourceName: string, clipIndex: number, assetType: AnimAssetType): { name: string | null; source: string | null } {
  // Try source name keywords first (rare — Sun/Moon uses numeric names)
  if (sourceName) {
    const lower = sourceName.toLowerCase();
    if (lower.includes('idle')) return { name: 'Idle', source: 'source-name' };
    if (lower.includes('walk')) return { name: 'Walk', source: 'source-name' };
    if (lower.includes('run'))  return { name: 'Run',  source: 'source-name' };
    if (lower.includes('jump')) return { name: 'Jump', source: 'source-name' };
  }

  const sourceIndex = parseSourceAnimIndex(sourceName, clipIndex);

  let mapped: string | undefined;
  switch (assetType) {
    case 'overworld': mapped = OVERWORLD_SLOT_MAP[sourceIndex]; break;
    case 'pokemon':   mapped = POKEMON_SLOT_MAP[sourceIndex]; break;
    default:          mapped = sourceIndex === 0 ? 'Idle' : undefined; break;
  }

  return mapped
    ? { name: mapped, source: 'slot-map-v1' }
    : { name: null, source: null };
}

/** Strip known image extensions to avoid double-extension filenames (.tga.png). */
function stripImageExt(name: string): string {
  return name.replace(/\.(tga|png|bmp|jpg|jpeg)$/i, '');
}

/** Save texture data as PNG (via sharp) or raw RGBA fallback. */
async function saveTexture(tex: OTexture, outDir: string): Promise<string> {
  fs.mkdirSync(outDir, { recursive: true });
  const fileName = sanitizeName(stripImageExt(tex.name)) + '.png';
  const filePath = path.join(outDir, fileName);
  try {
    const sharp = (await import('sharp') as any).default;
    await sharp(tex.texture.data, {
      raw: { width: tex.texture.width, height: tex.texture.height, channels: 4 },
    }).png().toFile(filePath);
  } catch {
    fs.writeFileSync(filePath + '.rgba', tex.texture.data);
  }
  return fileName;
}

// ---------------------------------------------------------------------------
// Grouped entry type
// ---------------------------------------------------------------------------
interface GroupedEntry {
  startEntry: number;
  endEntry: number;
  modelGroup: OModelGroup;
}

// ---------------------------------------------------------------------------
// Main extraction
// ---------------------------------------------------------------------------
async function main() {
  const startTime = performance.now();
  console.log(`Loading GARC: ${garcPath}`);
  const container = GARC.loadFile(garcPath);
  console.log(`Entries: ${container.content.length}`);

  const reader = container.data!;
  const max = limit ? Math.min(container.content.length, limit) : container.content.length;

  // ── Phase 1: Load all entries and build model groups ──
  console.log(`\nPhase 1: Loading and parsing ${max} entries...`);
  const parsedEntries: { group: OModelGroup; hasMeshes: boolean }[] = [];
  let parseErrors = 0;

  for (let i = 0; i < max; i++) {
    const entry = container.content[i];
    try {
      reader.seek(entry.fileOffset);
      const raw = reader.readBytes(entry.fileLength);
      const data = entry.doDecompression ? LZSS_Ninty.decompress(raw) : raw;
      const loaded = FileIO.load(BinaryReader.fromBuffer(data));
      const group = buildModelGroup(loaded);

      if (group.model.length === 0 && group.texture.length === 0 && group.skeletalAnimation.list.length === 0) {
        parsedEntries.push({ group, hasMeshes: false });
        continue;
      }

      const meshCount = countMeshes(group);
      parsedEntries.push({ group, hasMeshes: group.model.length > 0 && meshCount > 0 });
    } catch {
      parsedEntries.push({ group: new OModelGroup(), hasMeshes: false });
      parseErrors++;
    }

    if ((i + 1) % 1000 === 0 || i === max - 1) {
      console.log(`  Parsed ${i + 1}/${max} entries...`);
    }
  }

  // ── Phase 2: Group consecutive entries (C# GroupContainerEntries logic) ──
  console.log(`\nPhase 2: Grouping entries...`);
  const groups: GroupedEntry[] = [];
  let current: GroupedEntry | null = null;
  let pendingPrefix = new OModelGroup();

  for (let i = 0; i < parsedEntries.length; i++) {
    const { group: part, hasMeshes } = parsedEntries[i];
    const hasContent = part.model.length > 0 || part.texture.length > 0 || part.skeletalAnimation.list.length > 0;
    if (!hasContent) continue;

    if (hasMeshes) {
      // Start a new group
      if (current) {
        deduplicateTextures(current.modelGroup);
        groups.push(current);
      }

      // Merge any pending prefix (textures/anims before first model)
      if (pendingPrefix.model.length > 0 || pendingPrefix.texture.length > 0 || pendingPrefix.skeletalAnimation.list.length > 0) {
        part.merge(pendingPrefix);
        pendingPrefix = new OModelGroup();
      }

      current = { startEntry: i, endEntry: i, modelGroup: part };
    } else if (current) {
      // Merge into current group
      current.modelGroup.merge(part);
      current.endEntry = i;
    } else {
      // No group yet, accumulate as prefix
      pendingPrefix.merge(part);
    }
  }

  if (current) {
    deduplicateTextures(current.modelGroup);
    groups.push(current);
  }

  // ── Phase 2b: ExtendTrailingGroupForTextures (when -n limit truncates) ──
  if (limit && max < container.content.length && groups.length > 0) {
    const trailing = groups[groups.length - 1];
    if (!hasAllReferencedTextures(trailing.modelGroup)) {
      console.log(`  Extending trailing group past limit to find missing textures...`);
      for (let i = max; i < container.content.length; i++) {
        const entry = container.content[i];
        try {
          reader.seek(entry.fileOffset);
          const raw = reader.readBytes(entry.fileLength);
          const data = entry.doDecompression ? LZSS_Ninty.decompress(raw) : raw;
          const loaded = FileIO.load(BinaryReader.fromBuffer(data));
          const part = buildModelGroup(loaded);

          const hasContent = part.model.length > 0 || part.texture.length > 0 || part.skeletalAnimation.list.length > 0;
          if (!hasContent) continue;

          // Stop if we hit a new model group (has models with meshes)
          if (part.model.length > 0 && countMeshes(part) > 0) break;

          trailing.modelGroup.merge(part);
          trailing.endEntry = i;

          if (hasAllReferencedTextures(trailing.modelGroup)) break;
        } catch {
          // Skip unparseable entries
        }
      }
      deduplicateTextures(trailing.modelGroup);
    }
  }

  console.log(`  Found ${groups.length} model groups from ${max} entries (${parseErrors} parse errors)`);

  // ── Phase 3: Export each group ──
  console.log(`\nPhase 3: Exporting ${groups.length} groups...`);
  let modelCount = 0;
  let textureCount = 0;
  let clipCount = 0;
  let exportErrors = 0;

  for (let gi = 0; gi < groups.length; gi++) {
    const { startEntry, endEntry, modelGroup } = groups[gi];
    const assetType = detectAssetType(modelGroup);
    const folderName = deriveGroupName(modelGroup, gi, startEntry);
    const groupDir = path.join(outputDir, folderName);
    fs.mkdirSync(groupDir, { recursive: true });

    const skeletalClips = modelGroup.skeletalAnimation.list.filter(
      (a): a is OSkeletalAnimation => a instanceof OSkeletalAnimation
    );

    // Write textures first
    const textureFileNames: string[] = [];
    for (const tex of modelGroup.texture) {
      if (!tex || !tex.texture) continue;
      const fileName = await saveTexture(tex, groupDir);
      textureFileNames.push(fileName);
      textureCount++;
    }

    if (splitModelAnims) {
      // ── Split model + clips mode ──
      const manifestModels: ManifestModel[] = [];
      const usedNames = new Set<string>();

      for (let mi = 0; mi < modelGroup.model.length; mi++) {
        const mdl = modelGroup.model[mi];
        if (mdl.mesh.length === 0) continue;

        let baseName = mdl.name ? sanitizeName(mdl.name) : `model_${mi}`;
        let uniqueName = baseName;
        let suffix = 1;
        while (usedNames.has(uniqueName.toLowerCase())) {
          uniqueName = `${baseName}_${suffix++}`;
        }
        usedNames.add(uniqueName.toLowerCase());

        // Export model-only DAE
        const modelFileName = `${uniqueName}.dae`;
        const modelPath = path.join(groupDir, modelFileName);
        try {
          DAE.exportModelOnly(modelGroup, modelPath, mi);
          modelCount++;
        } catch (e: any) {
          console.error(`  Group ${gi} model[${mi}] DAE export failed: ${e.message}`);
          exportErrors++;
        }

        // Export clip DAEs
        const clipDir = path.join(groupDir, 'clips', uniqueName);
        fs.mkdirSync(clipDir, { recursive: true });
        const clipEntries: ManifestClip[] = [];

        for (let ci = 0; ci < skeletalClips.length; ci++) {
          const clip = skeletalClips[ci];
          const clipFileName = `clip_${String(ci).padStart(3, '0')}.dae`;
          const clipPath = path.join(clipDir, clipFileName);
          const clipRelPath = `clips/${uniqueName}/${clipFileName}`;

          try {
            DAE.exportClipOnly(modelGroup, clipPath, mi, ci);
            clipCount++;
          } catch (e: any) {
            console.error(`  Group ${gi} model[${mi}] clip[${ci}] export failed: ${e.message}`);
            exportErrors++;
          }

          const clipId = `clip_${String(ci).padStart(3, '0')}`;
          const sourceName = clip.name?.trim() || clipId;
          const semantic = resolveSemanticMetadata(sourceName, ci, assetType);
          clipEntries.push({
            index: ci,
            id: clipId,
            name: clipId,
            sourceName,
            semanticName: semantic.name,
            semanticSource: semantic.source,
            file: clipRelPath,
            frameCount: clip.frameSize,
            fps: 30,
          });
        }

        manifestModels.push({
          name: uniqueName,
          modelFile: modelFileName,
          clips: clipEntries,
        });
      }

      // Write manifest
      const manifest: Manifest = {
        version: 1,
        mode: 'split-model-anims',
        textures: textureFileNames,
        models: manifestModels,
      };
      fs.writeFileSync(path.join(groupDir, 'manifest.json'), JSON.stringify(manifest, null, 2));
    } else {
      // ── Default mode: one DAE per model (no animations) ──
      const usedNames = new Set<string>();

      for (let mi = 0; mi < modelGroup.model.length; mi++) {
        const mdl = modelGroup.model[mi];
        if (mdl.mesh.length === 0) continue;

        let baseName = mdl.name ? sanitizeName(mdl.name) : `model_${mi}`;
        let uniqueName = baseName;
        let suffix = 1;
        while (usedNames.has(uniqueName.toLowerCase())) {
          uniqueName = `${baseName}_${suffix++}`;
        }
        usedNames.add(uniqueName.toLowerCase());

        const daePath = path.join(groupDir, `${uniqueName}.dae`);
        try {
          DAE.export(modelGroup, daePath, mi);
          modelCount++;
        } catch (e: any) {
          console.error(`  Group ${gi} model[${mi}] DAE export failed: ${e.message}`);
          exportErrors++;
        }
      }
    }

    if ((gi + 1) % 100 === 0 || gi === groups.length - 1) {
      const meshes = countMeshes(modelGroup);
      console.log(`  Group ${gi + 1}/${groups.length}: entries ${startEntry}-${endEntry}, models=${modelGroup.model.length}, meshes=${meshes}, textures=${modelGroup.texture.length}, clips=${skeletalClips.length}`);
    }
  }

  const elapsed = ((performance.now() - startTime) / 1000).toFixed(1);
  console.log('');
  console.log('=== Extraction complete ===');
  console.log(`  Time:      ${elapsed}s`);
  console.log(`  Entries:   ${max} (of ${container.content.length})`);
  console.log(`  Groups:    ${groups.length}`);
  console.log(`  Models:    ${modelCount} DAE files`);
  console.log(`  Clips:     ${clipCount} clip DAE files`);
  console.log(`  Textures:  ${textureCount} texture files`);
  console.log(`  Errors:    ${exportErrors} export failures, ${parseErrors} parse failures`);
  console.log(`  Output:    ${outputDir}`);
}

main().catch((e) => {
  console.error('Fatal error:', e);
  process.exit(1);
});
