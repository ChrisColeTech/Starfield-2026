import type { FastifyInstance } from 'fastify';
import type { GenerateRequest, GenerateResponse, GeneratorType } from '../types/index.js';
import { getGenerator, getAllGenerators } from '../services/registry.js';

export async function generateRoutes(app: FastifyInstance) {
  /** List available generators and their metadata. */
  app.get('/api/generators', async () => {
    return getAllGenerators().map((g) => ({
      type: g.type,
      label: g.label,
      defaultFrames: g.defaultFrames,
      variants: g.variants,
      parameters: g.parameters,
    }));
  });

  /** Generate sprite frames. */
  app.post<{ Body: GenerateRequest }>('/api/generate', async (request, reply) => {
    const { type, variant, seed, frames } = request.body;
    const generator = getGenerator(type as GeneratorType);

    if (!generator) {
      return reply.status(400).send({ error: `Unknown generator type: ${type}` });
    }

    const results = generator.generateAll(seed, frames, variant);
    const response: GenerateResponse = {
      frames: results.map((r) => r.svg),
      metadata: {
        type: type as GeneratorType,
        variant,
        seed,
        frameCount: results.length,
      },
    };

    return response;
  });
}
