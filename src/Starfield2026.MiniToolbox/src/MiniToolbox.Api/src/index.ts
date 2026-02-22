import Fastify from 'fastify';
import cors from '@fastify/cors';
import { archiveRoutes } from './routes/archive.js';
import { exportRoutes } from './routes/export.js';
import { fsRoutes } from './routes/filesystem.js';

const PORT = 3100;

async function main() {
    const app = Fastify({ logger: true });

    await app.register(cors, { origin: true });

    // Health check
    app.get('/api/health', async () => ({ status: 'ok', timestamp: new Date().toISOString() }));

    // Feature routes
    await app.register(archiveRoutes, { prefix: '/api/archive' });
    await app.register(exportRoutes, { prefix: '/api/export' });
    await app.register(fsRoutes, { prefix: '/api/fs' });

    try {
        await app.listen({ port: PORT, host: '0.0.0.0' });
        console.log(`SwitchToolbox API listening on http://localhost:${PORT}`);
    } catch (err) {
        app.log.error(err);
        process.exit(1);
    }
}

main();
