/**
 * GARC extraction pipeline — reusable library extracted from test/extract-garc.ts.
 *
 * Provides the core extraction logic with progress callbacks so it can be
 * driven from API routes (or any other caller).
 */
import * as fs from 'fs';
import * as path from 'path';
import { GARC } from './ohana/Containers/GARC.js';
import { LZSS_Ninty } from './ohana/Compressions/LZSS_Ninty.js';
import { FileIO, formatType } from './ohana/Core/FileIO.js';
import type { LoadedFile } from './ohana/Core/FileIO.js';
import { BinaryReader } from './ohana/Core/BinaryReader.js';
import { DAE } from './ohana/Models/GenericFormats/DAE.js';
import { OModelGroup, OTexture, OSkeletalAnimation } from './ohana/Core/RenderBase.js';
import type { OContainer } from './ohana/Containers/OContainer.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export type ExtractionPhase = 'idle' | 'parsing' | 'grouping' | 'exporting' | 'done' | 'error' | 'stopped';

export interface ExtractionStats {
  totalEntries: number;
  processedEntries: number;
  groupsFound: number;
  modelsExported: number;
  texturesExported: number;
  clipsExported: number;
  parseErrors: number;
  exportErrors: number;
}

export interface ExtractionProgress {
  phase: ExtractionPhase;
  stats: ExtractionStats;
  logLines: string[];
  elapsedSeconds: number;
}

export interface ExtractedGroupResult {
  folderName: string;
  modelCount: number;
  textureCount: number;
  clipCount: number;
  files: string[];
}

export interface ExtractionConfig {
  garcPath: string;
  outputDir: string;
  splitModelAnims: boolean;
  entryLimit?: number;
  deriveFolderNames?: boolean;
}

export type ProgressCallback = (progress: ExtractionProgress) => void;

/** Callback that the runner checks periodically; return true to abort. */
export type CancelCheck = () => boolean;

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
// Grouped entry type
// ---------------------------------------------------------------------------
interface GroupedEntry {
  startEntry: number;
  endEntry: number;
  modelGroup: OModelGroup;
}

// ---------------------------------------------------------------------------
// Helpers (ported from test/extract-garc.ts)
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

/** Strip known image extensions to avoid double-extension filenames. */
function stripImageExt(name: string): string {
  return name.replace(/\.(tga|png|bmp|jpg|jpeg)$/i, '');
}

/**
 * Derive a group folder name matching the C# OhanaCli convention:
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
 * Resolve semantic animation metadata from source name or clip index.
 * Matches C# OhanaCli ResolveSemanticMetadata.
 */
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
 * Sequential index — OhanaCli names all clips "anim_0", so clipIndex is used.
 * Only slot 0 (Idle) is verified; others are tentative.
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

/** Parse trailing number from source name: "anim_4" → 4, "Motion_17" → 17, "clip_003" → 3. */
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

  // Parse original animation number from source name (anim_4 → 4, Motion_17 → 17).
  // OhanaCli Pokemon exports name ALL clips "anim_0", so fall back to clipIndex.
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
// Main extraction runner
// ---------------------------------------------------------------------------

export async function runExtraction(
  config: ExtractionConfig,
  onProgress: ProgressCallback,
  isCancelled: CancelCheck,
): Promise<ExtractedGroupResult[]> {
  const startTime = performance.now();
  const { garcPath, outputDir, splitModelAnims, entryLimit, deriveFolderNames } = config;

  const stats: ExtractionStats = {
    totalEntries: 0,
    processedEntries: 0,
    groupsFound: 0,
    modelsExported: 0,
    texturesExported: 0,
    clipsExported: 0,
    parseErrors: 0,
    exportErrors: 0,
  };

  const logLines: string[] = [];

  function elapsed(): number {
    return (performance.now() - startTime) / 1000;
  }

  function log(line: string) {
    logLines.push(line);
  }

  function emit(phase: ExtractionPhase) {
    onProgress({
      phase,
      stats: { ...stats },
      logLines: [...logLines],
      elapsedSeconds: parseFloat(elapsed().toFixed(1)),
    });
  }

  // -- Load GARC --
  log(`Loading GARC: ${garcPath}`);
  emit('parsing');

  fs.mkdirSync(outputDir, { recursive: true });

  const container = GARC.loadFile(garcPath);
  const reader = container.data!;
  const max = entryLimit ? Math.min(container.content.length, entryLimit) : container.content.length;
  stats.totalEntries = max;

  log(`Entries: ${container.content.length}`);
  if (entryLimit) log(`Limit: ${entryLimit} entries`);
  log('');
  log(`Phase 1: Loading and parsing ${max} entries...`);
  emit('parsing');

  if (isCancelled()) return [];

  // -- Helper: export a single group to disk and free its memory --
  async function exportGroup(
    modelGroup: OModelGroup,
    gi: number,
    startEntry: number,
    endEntry: number,
  ): Promise<ExtractedGroupResult> {
    const assetType = detectAssetType(modelGroup);
    const folderName = (deriveFolderNames !== false)
      ? deriveGroupName(modelGroup, gi, startEntry)
      : `group_${String(gi).padStart(4, '0')}_entry_${String(startEntry).padStart(5, '0')}`;
    const groupDir = path.join(outputDir, folderName);
    fs.mkdirSync(groupDir, { recursive: true });

    const groupFiles: string[] = [];

    const skeletalClips = modelGroup.skeletalAnimation.list.filter(
      (a): a is OSkeletalAnimation => a instanceof OSkeletalAnimation,
    );

    // Write textures
    const textureFileNames: string[] = [];
    for (const tex of modelGroup.texture) {
      if (!tex || !tex.texture) continue;
      const fileName = await saveTexture(tex, groupDir);
      textureFileNames.push(fileName);
      groupFiles.push(fileName);
      stats.texturesExported++;
    }

    if (splitModelAnims) {
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

        const modelFileName = `${uniqueName}.dae`;
        const modelPath = path.join(groupDir, modelFileName);
        try {
          DAE.exportModelOnly(modelGroup, modelPath, mi);
          stats.modelsExported++;
          groupFiles.push(modelFileName);
        } catch (e: any) {
          log(`  Error: Group ${gi} model[${mi}] DAE export failed: ${e.message}`);
          stats.exportErrors++;
        }

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
            stats.clipsExported++;
            groupFiles.push(clipRelPath);
          } catch (e: any) {
            log(`  Error: Group ${gi} model[${mi}] clip[${ci}] export failed: ${e.message}`);
            stats.exportErrors++;
          }
        }

        for (let ci = 0; ci < skeletalClips.length; ci++) {
          const clip = skeletalClips[ci];
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
            file: `clips/${uniqueName}/clip_${String(ci).padStart(3, '0')}.dae`,
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

      const manifest: Manifest = {
        version: 1,
        mode: 'split-model-anims',
        textures: textureFileNames,
        models: manifestModels,
      };
      fs.writeFileSync(path.join(groupDir, 'manifest.json'), JSON.stringify(manifest, null, 2));
      groupFiles.push('manifest.json');
    } else {
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
          stats.modelsExported++;
          groupFiles.push(`${uniqueName}.dae`);
        } catch (e: any) {
          log(`  Error: Group ${gi} model[${mi}] DAE export failed: ${e.message}`);
          stats.exportErrors++;
        }
      }
    }

    const modelCountInGroup = modelGroup.model.filter(m => m.mesh.length > 0).length;
    return {
      folderName,
      modelCount: modelCountInGroup,
      textureCount: textureFileNames.length,
      clipCount: skeletalClips.length,
      files: groupFiles,
    };
  }

  // -- Streaming parse → group → export in a single pass --
  // Each entry is parsed, grouped, and when a new model group starts,
  // the previous group is exported and freed from memory immediately.

  log('');
  log('Streaming: parse, group, and export in one pass...');
  emit('exporting');

  const results: ExtractedGroupResult[] = [];
  let currentGroup: GroupedEntry | null = null;
  let pendingPrefix = new OModelGroup();
  let groupIndex = 0;

  for (let i = 0; i < max; i++) {
    if (isCancelled()) return results;

    const entry = container.content[i];
    let part: OModelGroup;
    let hasMeshes = false;

    try {
      reader.seek(entry.fileOffset);
      const raw = reader.readBytes(entry.fileLength);
      const data = entry.doDecompression ? LZSS_Ninty.decompress(raw) : raw;
      const loaded = FileIO.load(BinaryReader.fromBuffer(data));
      part = buildModelGroup(loaded);

      const hasContent = part.model.length > 0 || part.texture.length > 0 || part.skeletalAnimation.list.length > 0;
      if (!hasContent) {
        stats.processedEntries = i + 1;
        continue;
      }

      hasMeshes = part.model.length > 0 && countMeshes(part) > 0;
    } catch {
      stats.parseErrors++;
      stats.processedEntries = i + 1;
      continue;
    }

    if (hasMeshes) {
      // New model group starting — export the previous one
      if (currentGroup) {
        deduplicateTextures(currentGroup.modelGroup);
        const result = await exportGroup(currentGroup.modelGroup, groupIndex, currentGroup.startEntry, currentGroup.endEntry);
        results.push(result);
        stats.groupsFound = groupIndex + 1;
        log(`  Group ${groupIndex + 1}: entries ${currentGroup.startEntry}-${currentGroup.endEntry}, exported`);
        emit('exporting');
        groupIndex++;
        // Free previous group memory
        currentGroup = null;
      }

      // Merge any pending prefix
      if (pendingPrefix.model.length > 0 || pendingPrefix.texture.length > 0 || pendingPrefix.skeletalAnimation.list.length > 0) {
        part.merge(pendingPrefix);
        pendingPrefix = new OModelGroup();
      }

      currentGroup = { startEntry: i, endEntry: i, modelGroup: part };
    } else if (currentGroup) {
      currentGroup.modelGroup.merge(part);
      currentGroup.endEntry = i;
    } else {
      pendingPrefix.merge(part);
    }

    stats.processedEntries = i + 1;

    if ((i + 1) % 500 === 0) {
      log(`  Processed ${i + 1}/${max} entries...`);
      emit('exporting');
    }
  }

  // Export the final group
  if (currentGroup) {
    deduplicateTextures(currentGroup.modelGroup);
    const result = await exportGroup(currentGroup.modelGroup, groupIndex, currentGroup.startEntry, currentGroup.endEntry);
    results.push(result);
    stats.groupsFound = groupIndex + 1;
    log(`  Group ${groupIndex + 1}: entries ${currentGroup.startEntry}-${currentGroup.endEntry}, exported`);
    currentGroup = null;
  }

  // -- Done --
  const elapsedSec = elapsed().toFixed(1);
  log('');
  log('=== Extraction complete ===');
  log(`  Time:      ${elapsedSec}s`);
  log(`  Entries:   ${max} (of ${container.content.length})`);
  log(`  Groups:    ${stats.groupsFound}`);
  log(`  Models:    ${stats.modelsExported} DAE files`);
  log(`  Clips:     ${stats.clipsExported} clip DAE files`);
  log(`  Textures:  ${stats.texturesExported} texture files`);
  log(`  Errors:    ${stats.exportErrors} export failures, ${stats.parseErrors} parse failures`);
  log(`  Output:    ${outputDir}`);
  emit('done');

  return results;
}
