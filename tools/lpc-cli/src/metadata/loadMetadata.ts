import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import vm from "node:vm";
import { CategoryTreeNode, LoadedMetadata, MetadataItem, MetadataLayer, MetadataSourceItem } from "../types/metadata";

export const DEFAULT_METADATA_SOURCE_PATH =
  "D:/Projects/Universal-LPC-Spritesheet-Character-Generator/item-metadata.js";

interface MetadataSandbox {
  window: {
    itemMetadata?: unknown;
    categoryTree?: unknown;
  };
  globalThis?: unknown;
}

export async function loadMetadataFromFile(sourcePath = DEFAULT_METADATA_SOURCE_PATH): Promise<LoadedMetadata> {
  const resolvedPath = resolve(sourcePath);
  const sourceText = await readFile(resolvedPath, "utf8");
  const sandbox: MetadataSandbox = { window: {} };
  sandbox.globalThis = sandbox.window;

  try {
    vm.runInNewContext(sourceText, sandbox, {
      filename: resolvedPath,
      timeout: 3000
    });
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    throw new Error(`Unable to evaluate metadata source file: ${message}`);
  }

  const rawItems = sandbox.window.itemMetadata;
  const rawTree = sandbox.window.categoryTree;
  if (!isRecord(rawItems)) {
    throw new Error("Metadata source did not provide window.itemMetadata");
  }

  const itemsById: Record<string, MetadataItem> = {};
  for (const [itemId, rawValue] of Object.entries(rawItems)) {
    if (!isRecord(rawValue)) {
      continue;
    }

    const sourceItem = rawValue as MetadataSourceItem;
    itemsById[itemId] = {
      id: itemId,
      name: asString(sourceItem.name, itemId),
      typeName: asString(sourceItem.type_name, itemId),
      priority: typeof sourceItem.priority === "number" ? sourceItem.priority : null,
      required: asStringArray(sourceItem.required),
      animations: asStringArray(sourceItem.animations),
      tags: asStringArray(sourceItem.tags),
      requiredTags: asStringArray(sourceItem.required_tags),
      excludedTags: asStringArray(sourceItem.excluded_tags),
      path: asStringArray(sourceItem.path),
      variants: asStringArray(sourceItem.variants),
      layers: toLayerMap(sourceItem.layers),
      credits: Array.isArray(sourceItem.credits) ? (sourceItem.credits as []) : [],
      raw: sourceItem
    };
  }

  const items = Object.values(itemsById).sort((a, b) => a.id.localeCompare(b.id));
  const categoryTree = toCategoryTreeNode(rawTree);
  const bodyTypes = inferBodyTypes(itemsById, categoryTree);

  return {
    sourcePath: resolvedPath,
    itemsById,
    items,
    categoryTree,
    bodyTypes
  };
}

function inferBodyTypes(itemsById: Record<string, MetadataItem>, categoryTree: CategoryTreeNode): string[] {
  const ordered: string[] = [];
  const seen = new Set<string>();

  const bodyItem = itemsById.body;
  if (bodyItem) {
    for (const bodyType of bodyItem.required) {
      pushUnique(bodyType, ordered, seen);
    }
    for (const layer of Object.values(bodyItem.layers)) {
      for (const key of Object.keys(layer)) {
        if (key !== "zPos") {
          pushUnique(key, ordered, seen);
        }
      }
    }
  }

  for (const item of Object.values(itemsById).sort((a, b) => a.id.localeCompare(b.id))) {
    for (const bodyType of item.required) {
      pushUnique(bodyType, ordered, seen);
    }
  }

  collectBodyTypesFromTree(categoryTree, ordered, seen);
  return ordered;
}

function collectBodyTypesFromTree(node: CategoryTreeNode, ordered: string[], seen: Set<string>): void {
  for (const value of node.required ?? []) {
    pushUnique(value, ordered, seen);
  }
  for (const child of Object.values(node.children)) {
    collectBodyTypesFromTree(child, ordered, seen);
  }
}

function toCategoryTreeNode(value: unknown): CategoryTreeNode {
  if (!isRecord(value)) {
    return { items: [], children: {} };
  }

  const items = asStringArray(value.items);
  const children: Record<string, CategoryTreeNode> = {};
  if (isRecord(value.children)) {
    for (const [key, child] of Object.entries(value.children)) {
      children[key] = toCategoryTreeNode(child);
    }
  }

  const node: CategoryTreeNode = {
    items,
    children
  };

  if (typeof value.label === "string") {
    node.label = value.label;
  }
  if (typeof value.priority === "number") {
    node.priority = value.priority;
  }
  if (Array.isArray(value.required)) {
    node.required = asStringArray(value.required);
  }
  if (Array.isArray(value.animations)) {
    node.animations = asStringArray(value.animations);
  }

  return node;
}

function toLayerMap(value: unknown): Record<string, MetadataLayer> {
  if (!isRecord(value)) {
    return {};
  }

  const layers: Record<string, MetadataLayer> = {};
  for (const [layerId, layerValue] of Object.entries(value)) {
    if (!isRecord(layerValue)) {
      continue;
    }

    const layer: MetadataLayer = {};
    for (const [key, rawLayerField] of Object.entries(layerValue)) {
      if (typeof rawLayerField === "string" || typeof rawLayerField === "number") {
        layer[key] = rawLayerField;
      }
    }
    layers[layerId] = layer;
  }

  return layers;
}

function asString(value: unknown, fallback: string): string {
  return typeof value === "string" && value.length > 0 ? value : fallback;
}

function asStringArray(value: unknown): string[] {
  if (!Array.isArray(value)) {
    return [];
  }
  return value.filter((entry): entry is string => typeof entry === "string");
}

function pushUnique(value: string, list: string[], seen: Set<string>): void {
  if (!value || seen.has(value)) {
    return;
  }
  seen.add(value);
  list.push(value);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}
