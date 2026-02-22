import { resolve } from "node:path";
import { STANDARD_ANIMATIONS, SPRITESHEET_HEIGHT, SPRITESHEET_WIDTH } from "./constants";
import { CustomAnimationArea, CUSTOM_ANIMATIONS, customAnimationArea } from "./customAnimations";
import { LoadedMetadata, MetadataItem, MetadataLayer } from "../types/metadata";
import { NormalizedPreset } from "../types/selection";

interface SelectionContextEntry {
  itemId: string;
  typeName: string;
  nameToken: string;
  variant: string;
  selectionIndex: number;
}

export interface ResolvedAnimationPath {
  animation: string;
  yOffset: number;
  relativePath: string;
  absolutePath: string;
}

export interface ResolvedLayer {
  itemId: string;
  typeName: string;
  variant: string;
  layerKey: string;
  layerNum: number;
  zPos: number;
  selectionIndex: number;
  animations: ResolvedAnimationPath[];
}

export interface ResolvedCustomLayer {
  itemId: string;
  typeName: string;
  variant: string;
  layerKey: string;
  layerNum: number;
  zPos: number;
  selectionIndex: number;
  customAnimation: string;
  relativePath: string;
  absolutePath: string;
}

export interface ResolvedCharacter {
  bodyType: string;
  dimensions: {
    width: number;
    height: number;
  };
  selections: SelectionContextEntry[];
  layers: ResolvedLayer[];
  customLayers: ResolvedCustomLayer[];
  customAnimations: CustomAnimationArea[];
}

export function resolveStandardCharacter(
  normalizedPreset: NormalizedPreset,
  metadata: LoadedMetadata,
  assetsRootPath: string
): ResolvedCharacter {
  const selectionContext = buildSelectionContext(normalizedPreset, metadata);
  const selectionByType = new Map(selectionContext.map((entry) => [entry.typeName, entry]));

  const layers: ResolvedLayer[] = [];
  const customLayers: ResolvedCustomLayer[] = [];
  for (const selection of selectionContext) {
    const item = metadata.itemsById[selection.itemId];
    if (!item) {
      throw new Error(`Unable to resolve selected item '${selection.itemId}'.`);
    }

    if (item.required.length > 0 && !item.required.includes(normalizedPreset.bodyType)) {
      throw new Error(`Item '${selection.itemId}' does not support body type '${normalizedPreset.bodyType}'.`);
    }

    for (const [layerKey, layer] of getOrderedLayers(item)) {
      const bodyTypePath = layer[normalizedPreset.bodyType];
      if (typeof bodyTypePath !== "string" || bodyTypePath.length === 0) {
        continue;
      }

      if (typeof layer.custom_animation === "string" && layer.custom_animation.length > 0) {
        const resolvedBasePath = resolvePathTemplate(bodyTypePath, item, selectionByType);
        const normalizedBasePath = resolvedBasePath.replaceAll("\\", "/");
        const basePathPrefix = normalizedBasePath.endsWith("/") ? normalizedBasePath : `${normalizedBasePath}/`;
        const relativePath = `spritesheets/${basePathPrefix}${variantToFilename(selection.variant)}.png`;

        customLayers.push({
          itemId: item.id,
          typeName: item.typeName,
          variant: selection.variant,
          layerKey,
          layerNum: parseLayerNum(layerKey),
          zPos: typeof layer.zPos === "number" ? layer.zPos : 100,
          selectionIndex: selection.selectionIndex,
          customAnimation: layer.custom_animation,
          relativePath,
          absolutePath: resolve(assetsRootPath, relativePath)
        });
        continue;
      }

      const supportedAnimations = getSupportedStandardAnimations(item);
      if (supportedAnimations.length === 0) {
        continue;
      }

      const resolvedBasePath = resolvePathTemplate(bodyTypePath, item, selectionByType);
      const normalizedBasePath = resolvedBasePath.replaceAll("\\", "/");
      const basePathPrefix = normalizedBasePath.endsWith("/") ? normalizedBasePath : `${normalizedBasePath}/`;

      const animationPaths = supportedAnimations.map((anim) => {
        const relativePath = `spritesheets/${basePathPrefix}${anim.folderName}/${variantToFilename(selection.variant)}.png`;
        return {
          animation: anim.name,
          yOffset: anim.yOffset,
          relativePath,
          absolutePath: resolve(assetsRootPath, relativePath)
        };
      });

      layers.push({
        itemId: item.id,
        typeName: item.typeName,
        variant: selection.variant,
        layerKey,
        layerNum: parseLayerNum(layerKey),
        zPos: typeof layer.zPos === "number" ? layer.zPos : 100,
        selectionIndex: selection.selectionIndex,
        animations: animationPaths
      });
    }
  }

  layers.sort((a, b) => {
    return (
      a.zPos - b.zPos ||
      a.selectionIndex - b.selectionIndex ||
      a.itemId.localeCompare(b.itemId) ||
      a.layerNum - b.layerNum ||
      a.variant.localeCompare(b.variant)
    );
  });

  customLayers.sort((a, b) => {
    return (
      a.customAnimation.localeCompare(b.customAnimation) ||
      a.zPos - b.zPos ||
      a.selectionIndex - b.selectionIndex ||
      a.itemId.localeCompare(b.itemId) ||
      a.layerNum - b.layerNum ||
      a.variant.localeCompare(b.variant)
    );
  });

  const customAnimations = resolveCustomAnimations(customLayers);
  const customWidth = customAnimations.reduce((max, area) => Math.max(max, area.width), SPRITESHEET_WIDTH);
  const customHeight = customAnimations.reduce((sum, area) => sum + area.height, SPRITESHEET_HEIGHT);

  return {
    bodyType: normalizedPreset.bodyType,
    dimensions: {
      width: customWidth,
      height: customHeight
    },
    selections: selectionContext,
    layers,
    customLayers,
    customAnimations
  };
}

function resolveCustomAnimations(customLayers: ResolvedCustomLayer[]): CustomAnimationArea[] {
  const names = Array.from(new Set(customLayers.map((layer) => layer.customAnimation))).sort((a, b) => a.localeCompare(b));
  const areas: CustomAnimationArea[] = [];
  let yOffset = SPRITESHEET_HEIGHT;

  for (const name of names) {
    const definition = CUSTOM_ANIMATIONS[name];
    if (!definition) {
      throw new Error(`Missing custom animation definition for '${name}'.`);
    }
    const area = customAnimationArea(name, definition, yOffset);
    areas.push(area);
    yOffset += area.height;
  }

  return areas;
}

function buildSelectionContext(preset: NormalizedPreset, metadata: LoadedMetadata): SelectionContextEntry[] {
  const result: SelectionContextEntry[] = [];

  preset.selections.forEach((selection, index) => {
    const item = metadata.itemsById[selection.itemId];
    if (!item) {
      return;
    }

    const variant = resolveVariant(item, selection.variant);
    result.push({
      itemId: item.id,
      typeName: item.typeName,
      nameToken: item.name.replaceAll(" ", "_"),
      variant,
      selectionIndex: index
    });
  });

  return result;
}

function resolveVariant(item: MetadataItem, presetVariant: string | null): string {
  if (presetVariant && presetVariant.length > 0) {
    return presetVariant;
  }

  if (item.variants.length === 0) {
    return "";
  }

  if (item.variants.length === 1) {
    return item.variants[0];
  }

  const inferred = item.id.split("_").at(-1);
  if (inferred) {
    const matched = item.variants.find((variant) => variant.toLowerCase() === inferred.toLowerCase());
    if (matched) {
      return matched;
    }
  }

  throw new Error(
    `Item '${item.id}' requires an explicit variant. Available variants: ${item.variants.join(", ")}.`
  );
}

function getOrderedLayers(item: MetadataItem): Array<[string, MetadataLayer]> {
  return Object.entries(item.layers)
    .filter(([layerKey]) => /^layer_\d+$/.test(layerKey))
    .sort((a, b) => parseLayerNum(a[0]) - parseLayerNum(b[0]));
}

function parseLayerNum(layerKey: string): number {
  const value = Number(layerKey.replace("layer_", ""));
  return Number.isFinite(value) ? value : Number.MAX_SAFE_INTEGER;
}

function getSupportedStandardAnimations(item: MetadataItem) {
  if (!Array.isArray(item.animations) || item.animations.length === 0) {
    return [];
  }

  return STANDARD_ANIMATIONS.filter((anim) => {
    if (anim.name === "combat_idle") {
      return item.animations.includes("combat");
    }
    if (anim.name === "backslash") {
      return item.animations.includes("1h_slash") || item.animations.includes("1h_backslash");
    }
    if (anim.name === "halfslash") {
      return item.animations.includes("1h_halfslash");
    }
    return item.animations.includes(anim.name);
  });
}

function resolvePathTemplate(
  rawPath: string,
  item: MetadataItem,
  selectionByType: Map<string, SelectionContextEntry>
): string {
  if (!rawPath.includes("${")) {
    return rawPath;
  }

  const rawReplaceInPath = item.raw.replace_in_path;
  const replaceInPath = isRecord(rawReplaceInPath) ? rawReplaceInPath : {};

  const resolved = rawPath.replace(/\$\{(.*?)\}/g, (_match, token: string) => {
    const contextSelection = selectionByType.get(token);
    if (!contextSelection) {
      throw new Error(`Unable to resolve path template variable '${token}' for item '${item.id}'.`);
    }

    const valueMap = replaceInPath[token];
    if (!isRecord(valueMap)) {
      throw new Error(`Item '${item.id}' is missing replace_in_path mapping for '${token}'.`);
    }

    const replacement = valueMap[contextSelection.nameToken];
    if (typeof replacement !== "string" || replacement.length === 0) {
      throw new Error(
        `Unable to resolve replacement for '${token}'='${contextSelection.nameToken}' on item '${item.id}'.`
      );
    }

    return replacement;
  });

  if (resolved.includes("${")) {
    throw new Error(`Unresolved template placeholders remain for item '${item.id}' path '${rawPath}'.`);
  }

  return resolved;
}

function variantToFilename(variant: string): string {
  return variant.replaceAll(" ", "_");
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}
