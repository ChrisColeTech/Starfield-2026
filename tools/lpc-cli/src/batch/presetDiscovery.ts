import { readdir } from "node:fs/promises";
import { extname, resolve } from "node:path";

interface PresetDirectoryEntry {
  name: string;
  isFile(): boolean;
}

export function isPresetJsonFile(entryName: string): boolean {
  return extname(entryName).toLowerCase() === ".json";
}

export function orderPresetFileNames(fileNames: string[]): string[] {
  return [...fileNames].sort((a, b) => {
    const byLowerName = a.toLowerCase().localeCompare(b.toLowerCase());
    if (byLowerName !== 0) {
      return byLowerName;
    }
    return a.localeCompare(b);
  });
}

export function discoverPresetPathsFromEntries(presetDirPath: string, entries: PresetDirectoryEntry[]): string[] {
  const presetFileNames = entries.filter((entry) => entry.isFile() && isPresetJsonFile(entry.name)).map((entry) => entry.name);
  const orderedNames = orderPresetFileNames(presetFileNames);
  return orderedNames.map((entryName) => resolve(presetDirPath, entryName));
}

export async function discoverPresetFiles(presetDirPath: string): Promise<string[]> {
  const entries = await readdir(presetDirPath, { withFileTypes: true });
  return discoverPresetPathsFromEntries(presetDirPath, entries);
}
