import { readdir, readFile, writeFile, unlink, stat, mkdir } from 'node:fs/promises';
import { join, extname } from 'node:path';
import type { GalleryItem } from '../types/index.js';

/** Resolve the sprites output directory. */
export function getSpritesDir(): string {
  // Default to project Assets/Sprites — can be overridden via env
  return process.env.SPRITES_DIR || join(process.cwd(), '..', '..', 'Starfield.Assets', 'Sprites');
}

export async function ensureDir(dir: string): Promise<void> {
  await mkdir(dir, { recursive: true });
}

export async function listSprites(dir: string, filter?: string): Promise<GalleryItem[]> {
  await ensureDir(dir);
  const files = await readdir(dir);
  const svgFiles = files.filter((f) => extname(f).toLowerCase() === '.svg');

  const items: GalleryItem[] = [];
  for (const filename of svgFiles) {
    if (filter && !filename.toLowerCase().includes(filter.toLowerCase())) continue;
    const info = await stat(join(dir, filename));
    items.push({
      filename,
      type: inferType(filename),
      createdAt: info.mtime.toISOString(),
    });
  }

  return items.sort((a, b) => b.createdAt.localeCompare(a.createdAt));
}

export async function readSprite(dir: string, filename: string): Promise<string> {
  return readFile(join(dir, filename), 'utf-8');
}

export async function saveSprite(dir: string, filename: string, content: string): Promise<void> {
  await ensureDir(dir);
  await writeFile(join(dir, filename), content, 'utf-8');
}

export async function deleteSprite(dir: string, filename: string): Promise<void> {
  await unlink(join(dir, filename));
}

export async function clearSprites(dir: string): Promise<number> {
  const files = await readdir(dir);
  const svgFiles = files.filter((f) => extname(f).toLowerCase() === '.svg');
  for (const f of svgFiles) {
    await unlink(join(dir, f));
  }
  return svgFiles.length;
}

function inferType(filename: string): string {
  if (filename.startsWith('grass')) return 'grass';
  if (filename.startsWith('flower')) return 'flower';
  if (filename.startsWith('tree')) return 'tree';
  if (filename.startsWith('bush')) return 'bush';
  return 'unknown';
}
