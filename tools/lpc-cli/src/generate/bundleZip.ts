import { createWriteStream } from "node:fs";
import { readFile, rm } from "node:fs/promises";
import { basename, resolve } from "node:path";
import { pipeline } from "node:stream/promises";
import * as yazl from "yazl";

interface CreateDeterministicBundleZipOptions {
  outputBaseName: string;
  outDir: string;
  spritesheetPath: string;
  characterPath: string;
  creditsCsvPath: string;
  creditsTxtPath: string;
  splitActionPaths: string[];
}

export interface BundleEntry {
  sourcePath: string;
  archivePath: string;
}

const FIXED_MTIME = new Date(1980, 0, 1, 0, 0, 0);
const FILE_MODE = 0o100644;

export async function createDeterministicBundleZip(options: CreateDeterministicBundleZipOptions): Promise<string> {
  const entries = buildBundleEntries(options);
  const bundlePath = resolve(options.outDir, `${options.outputBaseName}.bundle.zip`);

  try {
    await rm(bundlePath, { force: true });
    await writeDeterministicZip(bundlePath, entries);
    return bundlePath;
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    throw new Error(`Failed to create deterministic bundle zip at '${bundlePath}': ${message}`);
  }
}

export function buildBundleEntries(options: CreateDeterministicBundleZipOptions): BundleEntry[] {
  const root = options.outputBaseName;
  const entries: BundleEntry[] = [
    {
      sourcePath: options.spritesheetPath,
      archivePath: `${root}/${root}.spritesheet.png`
    },
    {
      sourcePath: options.characterPath,
      archivePath: `${root}/${root}.character.json`
    },
    {
      sourcePath: options.creditsCsvPath,
      archivePath: `${root}/${root}.credits.csv`
    },
    {
      sourcePath: options.creditsTxtPath,
      archivePath: `${root}/${root}.credits.txt`
    }
  ];

  const sortedActions = [...options.splitActionPaths].sort((a, b) => {
    const byBaseName = basename(a).localeCompare(basename(b));
    if (byBaseName !== 0) {
      return byBaseName;
    }
    return a.localeCompare(b);
  });
  for (const actionPath of sortedActions) {
    entries.push({
      sourcePath: actionPath,
      archivePath: `${root}/actions/${basename(actionPath)}`
    });
  }

  const collisionCheck = new Set<string>();
  for (const entry of entries) {
    if (collisionCheck.has(entry.archivePath)) {
      throw new Error(`Bundle entry collision for '${entry.archivePath}'.`);
    }
    collisionCheck.add(entry.archivePath);
  }

  return entries;
}

async function writeDeterministicZip(zipPath: string, entries: BundleEntry[]): Promise<void> {
  const zipFile = new yazl.ZipFile();

  try {
    for (const entry of entries) {
      const bytes = await readFile(entry.sourcePath);
      zipFile.addBuffer(bytes, entry.archivePath, {
        compress: false,
        mtime: FIXED_MTIME,
        mode: FILE_MODE
      });
    }
  } catch (error) {
    zipFile.end();
    throw error;
  }

  zipFile.end();
  await pipeline(zipFile.outputStream, createWriteStream(zipPath));
}
