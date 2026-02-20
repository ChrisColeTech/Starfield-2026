import Fastify from 'fastify';
import cors from '@fastify/cors';
import { generateRoutes } from './routes/generate.js';
import { galleryRoutes } from './routes/gallery.js';
import { importRoutes } from './routes/import.js';

const PORT = Number(process.env.PORT) || 3001;
const HOST = process.env.HOST || '0.0.0.0';

async function main() {
  const app = Fastify({ logger: true });

  await app.register(cors, { origin: true });

  // Register route groups
  await app.register(generateRoutes);
  await app.register(galleryRoutes);
  await app.register(importRoutes);

  // Health check
  app.get('/api/health', async () => ({ status: 'ok' }));

  await app.listen({ port: PORT, host: HOST });
  console.log(`SpriteGen API running on http://localhost:${PORT}`);
}

main().catch((err) => {
  console.error('Failed to start server:', err);
  process.exit(1);
});
