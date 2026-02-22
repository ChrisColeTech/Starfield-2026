import { FastifyPluginAsync } from 'fastify';
import fs from 'fs';
import path from 'path';

export const fsRoutes: FastifyPluginAsync = async (app) => {
    // POST /api/fs/list-dae
    // Lists all .dae files in the given directory (non-recursive, one level deep)
    app.post('/list-dae', async (request, reply) => {
        const { path: folderPath } = request.body as { path: string };

        if (!folderPath) {
            return reply.status(400).send({ error: 'Missing "path" in request body' });
        }

        if (!fs.existsSync(folderPath)) {
            return reply.status(404).send({ error: 'Directory not found' });
        }

        try {
            const files: string[] = [];

            const entries = fs.readdirSync(folderPath, { withFileTypes: true });
            for (const entry of entries) {
                if (entry.isFile() && entry.name.endsWith('.dae')) {
                    files.push(entry.name);
                }
                if (entry.isDirectory()) {
                    const subDir = path.join(folderPath, entry.name);
                    try {
                        const subEntries = fs.readdirSync(subDir, { withFileTypes: true });
                        for (const sub of subEntries) {
                            if (sub.isFile() && sub.name.endsWith('.dae')) {
                                files.push(`${entry.name}/${sub.name}`);
                            }
                        }
                    } catch { /* skip unreadable dirs */ }
                }
            }

            return { files: files.sort(), count: files.length, path: folderPath };
        } catch (err) {
            const message = err instanceof Error ? err.message : 'Failed to list files';
            return reply.status(500).send({ error: message });
        }
    });

    // GET /api/fs/serve-dae?folder=<folderPath>&file=<relativeDaePath>
    // Serves a DAE file from disk so Three.js ColladaLoader can load it
    // (Stays GET — this is a static file server, not an action)
    app.get<{ Querystring: { folder: string; file: string } }>('/serve-dae', async (request, reply) => {
        const { folder, file } = request.query;

        if (!folder || !file) {
            return reply.status(400).send({ error: 'Missing "folder" or "file" query parameter' });
        }

        const normalized = path.normalize(file);
        if (normalized.includes('..')) {
            return reply.status(400).send({ error: 'Invalid file path' });
        }

        const filePath = path.join(folder, normalized);

        if (!fs.existsSync(filePath)) {
            return reply.status(404).send({ error: 'File not found' });
        }

        const ext = path.extname(filePath).toLowerCase();
        const mimeTypes: Record<string, string> = {
            '.dae': 'application/xml',
            '.png': 'image/png',
            '.jpg': 'image/jpeg',
            '.jpeg': 'image/jpeg',
            '.tga': 'image/x-tga',
            '.bmp': 'image/bmp',
        };

        reply.header('Content-Type', mimeTypes[ext] || 'application/octet-stream');
        reply.header('Access-Control-Allow-Origin', '*');
        return reply.send(fs.createReadStream(filePath));
    });
};
