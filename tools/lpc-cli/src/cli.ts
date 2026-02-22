#!/usr/bin/env node
import { access, mkdir, rm, stat, writeFile } from "node:fs/promises";
import { basename, extname, resolve } from "node:path";
import { Command } from "commander";
import { DEFAULT_METADATA_SOURCE_PATH, loadMetadataFromFile } from "./metadata/loadMetadata";
import { discoverPresetFiles } from "./batch/presetDiscovery";
import { createDeterministicBundleZip } from "./generate/bundleZip";
import { collectCredits, creditsToCsv, creditsToTxt } from "./generate/credits";
import { renderStandardSpritesheet } from "./generate/renderer";
import { resolveStandardCharacter } from "./generate/resolver";
import { exportSplitActionSprites } from "./generate/splitActions";
import { loadPresetFromFile, validatePresetAgainstMetadata, validatePresetShape } from "./validation/validatePreset";

const program = new Command();

interface ValidateAndResolveResult {
  schemaErrors: string[];
  metadataErrors: string[];
  metadataWarnings: string[];
  readinessErrors: string[];
  bodyType?: string;
  selectionCount?: number;
}

interface GeneratePresetOptions {
  presetPath: string;
  assetsRootPath: string;
  metadataPath: string;
  outDir: string;
  bodyTypeOverride?: string;
  outputName?: string;
  splitActions?: boolean;
  bundleZip?: boolean;
}

interface GeneratePresetResult {
  outputBaseName: string;
  spritesheetPath: string;
  characterPath: string;
  creditsCsvPath: string;
  creditsTxtPath: string;
  actionsDirPath: string;
  bundleZipPath: string;
  splitActionPaths: string[];
  resolvedLayerCount: number;
  resolvedCustomLayerCount: number;
  resolvedCustomAnimationCount: number;
  creditsCount: number;
  validationWarnings: string[];
}

function withBodyTypeOverride<T extends { bodyType: string }>(preset: T, bodyTypeOverride?: string): T {
  if (!bodyTypeOverride) {
    return preset;
  }
  return {
    ...preset,
    bodyType: bodyTypeOverride
  };
}

function toIssueMessages(prefix: string, issues: Array<{ code: string; message: string }>): string[] {
  return issues.map((issue) => `[${prefix}.${issue.code}] ${issue.message}`);
}

async function validateAssetsRoot(assetsRootPath: string): Promise<string | null> {
  try {
    const stats = await stat(assetsRootPath);
    if (!stats.isDirectory()) {
      return `Assets root is not a directory: ${assetsRootPath}`;
    }
  } catch {
    return `Assets root does not exist: ${assetsRootPath}`;
  }
  return null;
}

async function validatePresetAndResolution(options: {
  presetPath: string;
  metadataPath: string;
  assetsRootPath: string;
  bodyTypeOverride?: string;
}): Promise<ValidateAndResolveResult> {
  const assetsRootError = await validateAssetsRoot(options.assetsRootPath);
  if (assetsRootError) {
    return {
      schemaErrors: [],
      metadataErrors: [],
      metadataWarnings: [],
      readinessErrors: [assetsRootError]
    };
  }

  const presetRaw = await loadPresetFromFile(options.presetPath);
  const presetShapeResult = validatePresetShape(presetRaw);
  if (!presetShapeResult.preset) {
    return {
      schemaErrors: toIssueMessages("schema", presetShapeResult.errors),
      metadataErrors: [],
      metadataWarnings: [],
      readinessErrors: []
    };
  }

  const preset = withBodyTypeOverride(presetShapeResult.preset, options.bodyTypeOverride);
  const metadata = await loadMetadataFromFile(options.metadataPath);
  const semantic = validatePresetAgainstMetadata(preset, metadata);
  const metadataErrors = toIssueMessages("metadata", semantic.result.errors);
  const metadataWarnings = toIssueMessages("metadata", semantic.result.warnings);

  if (metadataErrors.length > 0) {
    return {
      schemaErrors: [],
      metadataErrors,
      metadataWarnings,
      readinessErrors: [],
      bodyType: semantic.normalizedPreset.bodyType,
      selectionCount: semantic.normalizedPreset.selections.length
    };
  }

  let readinessErrors: string[] = [];
  try {
    const resolved = resolveStandardCharacter(semantic.normalizedPreset, metadata, options.assetsRootPath);
    readinessErrors = await collectMissingSpritePaths(resolved);
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    readinessErrors = [message];
  }

  return {
    schemaErrors: [],
    metadataErrors,
    metadataWarnings,
    readinessErrors,
    bodyType: semantic.normalizedPreset.bodyType,
    selectionCount: semantic.normalizedPreset.selections.length
  };
}

async function collectMissingSpritePaths(resolved: Awaited<ReturnType<typeof resolveStandardCharacter>>): Promise<string[]> {
  const uniquePaths = Array.from(
    new Set(
      resolved.layers
        .flatMap((layer) => layer.animations.map((animation) => animation.absolutePath))
        .concat(resolved.customLayers.map((layer) => layer.absolutePath))
    )
  ).sort((a, b) => a.localeCompare(b));

  const checks = await Promise.all(
    uniquePaths.map(async (absolutePath) => {
      try {
        await access(absolutePath);
        return null;
      } catch {
        return absolutePath;
      }
    })
  );

  return checks.filter((value): value is string => value !== null);
}

function printGroupedValidation(result: ValidateAndResolveResult): void {
  const hasErrors =
    result.schemaErrors.length > 0 || result.metadataErrors.length > 0 || result.readinessErrors.length > 0;

  if (!hasErrors) {
    if (result.metadataWarnings.length > 0) {
      console.log("Preset validation succeeded with warnings:");
      for (const warning of result.metadataWarnings) {
        console.log(`- ${warning}`);
      }
    } else {
      console.log("Preset validation succeeded.");
    }

    console.log(`Body type: ${result.bodyType ?? "<unknown>"}`);
    console.log(`Selections: ${result.selectionCount ?? 0}`);
    return;
  }

  console.error("Preset validation failed:");
  if (result.schemaErrors.length > 0) {
    console.error("Schema:");
    for (const error of result.schemaErrors) {
      console.error(`- ${error}`);
    }
  }
  if (result.metadataErrors.length > 0) {
    console.error("Metadata match:");
    for (const error of result.metadataErrors) {
      console.error(`- ${error}`);
    }
  }
  if (result.readinessErrors.length > 0) {
    console.error("Path resolution readiness:");
    const listed = result.readinessErrors.slice(0, 30);
    for (const error of listed) {
      console.error(`- ${error}`);
    }
    if (result.readinessErrors.length > listed.length) {
      console.error(`- ...and ${result.readinessErrors.length - listed.length} more`);
    }
  }
  if (result.metadataWarnings.length > 0) {
    console.error("Warnings:");
    for (const warning of result.metadataWarnings) {
      console.error(`- ${warning}`);
    }
  }
}

function resolveOutputName(presetPath: string, outputName?: string): string {
  const raw = outputName && outputName.trim().length > 0 ? outputName.trim() : basename(presetPath, extname(presetPath));
  const normalized = raw.replace(/[^a-zA-Z0-9._-]/g, "-").replace(/-+/g, "-").replace(/^-|-$/g, "");
  return normalized.length > 0 ? normalized : "character";
}

function buildSchemaFailureMessage(errors: Array<{ code: string; message: string }>): string {
  const details = errors.map((error) => `- [schema.${error.code}] ${error.message}`).join("\n");
  return `Preset schema validation failed:\n${details}`;
}

function buildMetadataFailureMessage(
  errors: Array<{ code: string; message: string }>,
  warnings: Array<{ code: string; message: string }>
): string {
  const lines = [
    "Preset validation failed:",
    ...errors.map((error) => `- [metadata.${error.code}] ${error.message}`)
  ];

  if (warnings.length > 0) {
    lines.push("Warnings:");
    for (const warning of warnings) {
      lines.push(`- [metadata.${warning.code}] ${warning.message}`);
    }
  }

  return lines.join("\n");
}

async function generatePresetArtifacts(options: GeneratePresetOptions): Promise<GeneratePresetResult> {
  const assetsRootError = await validateAssetsRoot(options.assetsRootPath);
  if (assetsRootError) {
    throw new Error(assetsRootError);
  }

  const presetRaw = await loadPresetFromFile(options.presetPath);
  const presetShapeResult = validatePresetShape(presetRaw);
  if (!presetShapeResult.preset) {
    throw new Error(buildSchemaFailureMessage(presetShapeResult.errors));
  }

  const preset = withBodyTypeOverride(presetShapeResult.preset, options.bodyTypeOverride);
  const metadata = await loadMetadataFromFile(options.metadataPath);
  const semantic = validatePresetAgainstMetadata(preset, metadata);
  if (semantic.result.errors.length > 0) {
    throw new Error(buildMetadataFailureMessage(semantic.result.errors, semantic.result.warnings));
  }

  const validationWarnings = semantic.result.warnings.map(
    (warning) => `[metadata.${warning.code}] ${warning.message}`
  );
  const resolved = resolveStandardCharacter(semantic.normalizedPreset, metadata, options.assetsRootPath);

  await mkdir(options.outDir, { recursive: true });

  const outputBaseName = resolveOutputName(options.presetPath, options.outputName);
  const spritesheetPath = resolve(options.outDir, `${outputBaseName}.spritesheet.png`);
  const characterPath = resolve(options.outDir, `${outputBaseName}.character.json`);
  const creditsCsvPath = resolve(options.outDir, `${outputBaseName}.credits.csv`);
  const creditsTxtPath = resolve(options.outDir, `${outputBaseName}.credits.txt`);
  const actionsDirPath = resolve(options.outDir, `${outputBaseName}.actions`);
  const bundleZipPath = resolve(options.outDir, `${outputBaseName}.bundle.zip`);

  await Promise.all([
    rm(spritesheetPath, { force: true }),
    rm(characterPath, { force: true }),
    rm(creditsCsvPath, { force: true }),
    rm(creditsTxtPath, { force: true })
  ]);

  await renderStandardSpritesheet(resolved, spritesheetPath);

  let splitActionPaths: string[] = [];
  if (options.splitActions) {
    splitActionPaths = await exportSplitActionSprites({
      spritesheetPath,
      outputDir: actionsDirPath,
      character: resolved
    });
  }

  const credits = collectCredits(resolved, metadata);

  const characterJson = {
    bodyType: resolved.bodyType,
    dimensions: resolved.dimensions,
    selections: resolved.selections.map((selection) => ({
      itemId: selection.itemId,
      typeName: selection.typeName,
      variant: selection.variant
    })),
    layers: resolved.layers.map((layer) => ({
      itemId: layer.itemId,
      typeName: layer.typeName,
      variant: layer.variant,
      layerKey: layer.layerKey,
      layerNum: layer.layerNum,
      zPos: layer.zPos,
      animations: layer.animations.map((animation) => ({
        animation: animation.animation,
        yOffset: animation.yOffset,
        path: animation.relativePath
      }))
    })),
    customAnimations: resolved.customAnimations,
    customLayers: resolved.customLayers.map((layer) => ({
      itemId: layer.itemId,
      typeName: layer.typeName,
      variant: layer.variant,
      layerKey: layer.layerKey,
      layerNum: layer.layerNum,
      zPos: layer.zPos,
      customAnimation: layer.customAnimation,
      path: layer.relativePath
    }))
  };

  await writeFile(characterPath, `${JSON.stringify(characterJson, null, 2)}\n`, "utf8");
  await writeFile(creditsCsvPath, creditsToCsv(credits), "utf8");
  await writeFile(creditsTxtPath, creditsToTxt(credits), "utf8");

  if (options.bundleZip) {
    await createDeterministicBundleZip({
      outputBaseName,
      outDir: options.outDir,
      spritesheetPath,
      characterPath,
      creditsCsvPath,
      creditsTxtPath,
      splitActionPaths
    });
  }

  return {
    outputBaseName,
    spritesheetPath,
    characterPath,
    creditsCsvPath,
    creditsTxtPath,
    actionsDirPath,
    bundleZipPath,
    splitActionPaths,
    resolvedLayerCount: resolved.layers.length,
    resolvedCustomLayerCount: resolved.customLayers.length,
    resolvedCustomAnimationCount: resolved.customAnimations.length,
    creditsCount: credits.length,
    validationWarnings
  };
}

function getErrorMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

function summarizeError(error: unknown): string {
  const message = getErrorMessage(error).trim();
  const [firstLine] = message.split(/\r?\n/, 1);
  return firstLine && firstLine.length > 0 ? firstLine : "Unknown error";
}

program
  .name("lpc-cli")
  .description("Slim LPC spritesheet CLI (Phase 3)")
  .version("0.1.0");

const listCommand = program.command("list").description("List metadata resources");

listCommand
  .command("items")
  .description("List available item ids")
  .option("--metadata <path>", "Path to item-metadata.js", DEFAULT_METADATA_SOURCE_PATH)
  .action(async (options: { metadata: string }) => {
    const metadata = await loadMetadataFromFile(options.metadata);
    console.log(`Loaded ${metadata.items.length} items from ${metadata.sourcePath}`);
    for (const item of metadata.items) {
      console.log(`- ${item.id} (type: ${item.typeName}, variants: ${item.variants.length})`);
    }
  });

listCommand
  .command("body-types")
  .description("List discovered body types")
  .option("--metadata <path>", "Path to item-metadata.js", DEFAULT_METADATA_SOURCE_PATH)
  .action(async (options: { metadata: string }) => {
    const metadata = await loadMetadataFromFile(options.metadata);
    console.log(`Body types (${metadata.bodyTypes.length}):`);
    for (const bodyType of metadata.bodyTypes) {
      console.log(`- ${bodyType}`);
    }
  });

program
  .command("validate")
  .description("Validate a preset against metadata")
  .requiredOption("--preset <path>", "Path to preset JSON")
  .requiredOption("--assetsRoot <path>", "Path to assets root")
  .option("--metadata <path>", "Path to item-metadata.js", DEFAULT_METADATA_SOURCE_PATH)
  .option("--bodyType <value>", "Override bodyType from preset")
  .action(async (options: { preset: string; assetsRoot: string; metadata: string; bodyType?: string }) => {
    const result = await validatePresetAndResolution({
      presetPath: options.preset,
      metadataPath: options.metadata,
      assetsRootPath: resolve(options.assetsRoot),
      bodyTypeOverride: options.bodyType
    });
    printGroupedValidation(result);

    if (result.schemaErrors.length > 0 || result.metadataErrors.length > 0 || result.readinessErrors.length > 0) {
      process.exitCode = 1;
    }
  });

program
  .command("generate")
  .description("Generate deterministic LPC spritesheet + credits")
  .requiredOption("--preset <path>", "Path to preset JSON")
  .requiredOption("--assetsRoot <path>", "Path to assets root")
  .option("--metadata <path>", "Path to item-metadata.js", DEFAULT_METADATA_SOURCE_PATH)
  .option("--outDir <path>", "Output directory", "./output")
  .option("--bodyType <value>", "Override bodyType from preset")
  .option("--outputName <name>", "Output file base name (without extension)")
  .option("--splitActions", "Export per-action PNG files")
  .option("--bundleZip", "Write deterministic ZIP bundle for generated outputs")
  .action(
    async (options: {
      preset: string;
      assetsRoot: string;
      metadata: string;
      outDir: string;
      bodyType?: string;
      outputName?: string;
      splitActions?: boolean;
      bundleZip?: boolean;
    }) => {
      try {
        const result = await generatePresetArtifacts({
          presetPath: resolve(options.preset),
          assetsRootPath: resolve(options.assetsRoot),
          metadataPath: options.metadata,
          outDir: resolve(options.outDir),
          bodyTypeOverride: options.bodyType,
          outputName: options.outputName,
          splitActions: options.splitActions,
          bundleZip: options.bundleZip
        });

        if (result.validationWarnings.length > 0) {
          console.log("Preset validation warnings:");
          for (const warning of result.validationWarnings) {
            console.log(`- ${warning}`);
          }
        }

        console.log(`Generated ${result.spritesheetPath}`);
        console.log(`Generated ${result.characterPath}`);
        console.log(`Generated ${result.creditsCsvPath}`);
        console.log(`Generated ${result.creditsTxtPath}`);
        if (options.splitActions) {
          console.log(`Generated split actions in ${result.actionsDirPath} (${result.splitActionPaths.length} files)`);
        }
        if (options.bundleZip) {
          console.log(`Generated ${result.bundleZipPath}`);
        }
        console.log(`Resolved layers: ${result.resolvedLayerCount}`);
        console.log(`Resolved custom layers: ${result.resolvedCustomLayerCount}`);
        console.log(`Resolved custom animations: ${result.resolvedCustomAnimationCount}`);
        console.log(`Credits entries: ${result.creditsCount}`);
      } catch (error) {
        console.error(getErrorMessage(error));
        process.exitCode = 1;
      }
    }
  );

program
  .command("generate-batch")
  .description("Generate outputs for every preset JSON in a folder")
  .requiredOption("--presetDir <path>", "Path to folder containing preset JSON files")
  .requiredOption("--assetsRoot <path>", "Path to assets root")
  .requiredOption("--outDir <path>", "Output directory")
  .option("--metadata <path>", "Path to item-metadata.js", DEFAULT_METADATA_SOURCE_PATH)
  .option("--splitActions", "Export per-action PNG files")
  .option("--bundleZip", "Write deterministic ZIP bundle for generated outputs")
  .option("--bodyType <value>", "Override bodyType from preset")
  .action(
    async (options: {
      presetDir: string;
      assetsRoot: string;
      outDir: string;
      metadata: string;
      splitActions?: boolean;
      bundleZip?: boolean;
      bodyType?: string;
    }) => {
      const presetDirPath = resolve(options.presetDir);
      const outDirPath = resolve(options.outDir);
      const assetsRootPath = resolve(options.assetsRoot);

      const presetDirError = await validateAssetsRoot(presetDirPath);
      if (presetDirError) {
        console.error(`Preset directory error: ${presetDirError.replace("Assets root", "Preset directory")}`);
        process.exitCode = 1;
        return;
      }

      const presetPaths = await discoverPresetFiles(presetDirPath);
      if (presetPaths.length === 0) {
        console.log(`No preset JSON files found in ${presetDirPath}`);
        console.log("Batch summary:");
        console.log("- Total presets: 0");
        console.log("- Successes: 0");
        console.log("- Failures: 0");
        return;
      }

      const failures: Array<{ presetName: string; reason: string }> = [];
      let successCount = 0;

      for (const presetPath of presetPaths) {
        const presetName = basename(presetPath);
        try {
          await generatePresetArtifacts({
            presetPath,
            assetsRootPath,
            metadataPath: options.metadata,
            outDir: outDirPath,
            bodyTypeOverride: options.bodyType,
            splitActions: options.splitActions,
            bundleZip: options.bundleZip
          });
          successCount += 1;
          console.log(`[ok] ${presetName}`);
        } catch (error) {
          const reason = summarizeError(error);
          failures.push({ presetName, reason });
          console.error(`[failed] ${presetName}: ${reason}`);
        }
      }

      console.log("Batch summary:");
      console.log(`- Total presets: ${presetPaths.length}`);
      console.log(`- Successes: ${successCount}`);
      console.log(`- Failures: ${failures.length}`);
      if (failures.length > 0) {
        console.log("- Failed presets:");
        for (const failure of failures) {
          console.log(`  - ${failure.presetName}: ${failure.reason}`);
        }
        process.exitCode = 1;
      }
    }
  );

program.parseAsync(process.argv).catch((error: unknown) => {
  const message = getErrorMessage(error);
  console.error(`Command failed: ${message}`);
  process.exitCode = 1;
});
