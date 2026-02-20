import type { FastifyInstance } from 'fastify';
import { getSpritesDir, listSprites, readSprite, deleteSprite, clearSprites, saveSprite } from '../utils/fileUtils.js';
import type { SaveRequest, GeneratorType } from '../types/index.js';
import { getGenerator } from '../services/registry.js';

export async function galleryRoutes(app: FastifyInstance) {
  const spritesDir = getSpritesDir();

  /** List gallery items. */
  app.get<{ Querystring: { filter?: string } }>('/api/gallery', async (request) => {
    const items = await listSprites(spritesDir, request.query.filter);
    return { items, total: items.length };
  });

  /** Get a single sprite file. */
  app.get<{ Params: { filename: string } }>('/api/sprites/:filename', async (request, reply) => {
    try {
      const content = await readSprite(spritesDir, request.params.filename);
      return reply.type('image/svg+xml').send(content);
    } catch {
      return reply.status(404).send({ error: 'Sprite not found' });
    }
  });

  /** Delete a single sprite. */
  app.delete<{ Params: { filename: string } }>('/api/sprites/:filename', async (request, reply) => {
    try {
      await deleteSprite(spritesDir, request.params.filename);
      return { success: true };
    } catch {
      return reply.status(404).send({ error: 'Sprite not found' });
    }
  });

  /** Save generated frames to disk. */
  app.post<{ Body: SaveRequest }>('/api/save', async (request, reply) => {
    const { type, variant, seed, baseName } = request.body;
    const generator = getGenerator(type as GeneratorType);

    if (!generator) {
      return reply.status(400).send({ error: `Unknown generator type: ${type}` });
    }

    const results = generator.generateAll(seed, undefined, variant);
    const saved: string[] = [];
    const prefix = baseName ?? `${type}${variant ? `-${variant}` : ''}-${seed}`;

    for (let i = 0; i < results.length; i++) {
      const filename = `${prefix}-frame${i}.svg`;
      await saveSprite(spritesDir, filename, results[i].svg);
      saved.push(filename);
    }

    return { saved, count: saved.length };
  });

  /** Clear all sprites from gallery. */
  app.post('/api/clear', async () => {
    const deleted = await clearSprites(spritesDir);
    return { deleted };
  });
}
