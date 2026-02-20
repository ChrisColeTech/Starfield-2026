import type { FastifyInstance } from 'fastify';
import { getSpritesDir, saveSprite, ensureDir } from '../utils/fileUtils.js';

interface ImportBody {
  baseName: string;
  frames: { index: number; content: string; filename: string }[];
}

export async function importRoutes(app: FastifyInstance) {
  const spritesDir = getSpritesDir();

  /** Import uploaded frames as named sprite files. */
  app.post<{ Body: ImportBody }>('/api/import', async (request, reply) => {
    const { baseName, frames } = request.body;

    if (!baseName || !frames?.length) {
      return reply.status(400).send({ error: 'baseName and frames[] are required' });
    }

    // Validate baseName (alphanumeric, underscores, hyphens only)
    if (!/^[a-zA-Z0-9_-]+$/.test(baseName)) {
      return reply.status(400).send({ error: 'baseName must be alphanumeric with underscores/hyphens only' });
    }

    await ensureDir(spritesDir);

    const saved: string[] = [];

    // Save base file (first frame = the default/base sprite)
    if (frames.length > 0) {
      const baseFilename = `${baseName}.svg`;
      await saveSprite(spritesDir, baseFilename, frames[0].content);
      saved.push(baseFilename);
    }

    // Save individual frame files
    for (let i = 0; i < frames.length; i++) {
      const frameFilename = `${baseName}_${i}.svg`;
      await saveSprite(spritesDir, frameFilename, frames[i].content);
      saved.push(frameFilename);
    }

    return { saved, count: saved.length };
  });
}
