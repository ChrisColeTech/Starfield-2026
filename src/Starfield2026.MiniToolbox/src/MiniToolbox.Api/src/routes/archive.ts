import { FastifyPluginAsync } from 'fastify';
import { scanArchive } from '../services/archiveScanner.js';

export const archiveRoutes: FastifyPluginAsync = async (app) => {
    // POST /api/archive/scan
    app.post('/scan', async (request, reply) => {
        const { arcPath } = request.body as { arcPath: string };

        if (!arcPath) {
            return reply.status(400).send({ error: 'Missing "arcPath" in request body' });
        }

        try {
            const result = await scanArchive(arcPath);
            return result;
        } catch (err) {
            console.error('[scan] Error:', err);
            const message = err instanceof Error ? err.message : 'Scan failed';
            return reply.status(500).send({ error: message });
        }
    });
};
