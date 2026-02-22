import { FastifyPluginAsync } from 'fastify';
import { ExportService } from '../services/exportService.js';
import type { ExportRequest } from '../types/index.js';

const exportService = new ExportService();

export const exportRoutes: FastifyPluginAsync = async (app) => {
    // POST /api/export/start
    app.post('/start', async (request, reply) => {
        const body = request.body as ExportRequest;

        if (!body.arcPath) {
            return reply.status(400).send({ error: 'Missing "arcPath"' });
        }
        if (!body.outputDir) {
            return reply.status(400).send({ error: 'Missing "outputDir"' });
        }

        try {
            const job = exportService.start({
                arcPath: body.arcPath,
                outputDir: body.outputDir,
                parallelJobs: body.parallelJobs ?? 4,
                maxModels: body.maxModels ?? 0,
                exportMode: body.exportMode ?? 'baked',
            });
            return { jobId: job.id };
        } catch (err) {
            const message = err instanceof Error ? err.message : 'Export failed to start';
            return reply.status(500).send({ error: message });
        }
    });

    // POST /api/export/cancel
    app.post('/cancel', async () => {
        const cancelled = exportService.cancel();
        return { cancelled };
    });

    // POST /api/export/status
    app.post('/status', async () => {
        return exportService.getStatus();
    });

    // GET /api/export/progress — Server-Sent Events
    app.get('/progress', async (request, reply) => {
        reply.raw.writeHead(200, {
            'Content-Type': 'text/event-stream',
            'Cache-Control': 'no-cache',
            'Connection': 'keep-alive',
        });

        const unsubscribe = exportService.onProgress((event) => {
            reply.raw.write(`data: ${JSON.stringify(event)}\n\n`);
        });

        request.raw.on('close', () => {
            unsubscribe();
        });
    });
};
