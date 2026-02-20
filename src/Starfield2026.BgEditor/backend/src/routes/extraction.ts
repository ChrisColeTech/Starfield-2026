import { FastifyInstance } from 'fastify';
import { randomUUID } from 'crypto';
import fs from 'fs';
import path from 'path';
import {
  runExtraction,
  type ExtractionConfig,
  type ExtractionProgress,
  type ExtractedGroupResult,
  type ExtractionPhase,
} from '../lib/extraction.js';

// ---------------------------------------------------------------------------
// In-memory job tracker
// ---------------------------------------------------------------------------

interface ExtractionJob {
  id: string;
  config: ExtractionConfig;
  progress: ExtractionProgress;
  results: ExtractedGroupResult[];
  cancelled: boolean;
  /** Resolves when the extraction finishes (or fails/cancels). */
  promise: Promise<void> | null;
}

const jobs = new Map<string, ExtractionJob>();

// ---------------------------------------------------------------------------
// RomFS scanner — find all non-empty leaf files (GARC archives)
// ---------------------------------------------------------------------------

interface ScannedArchive {
  subpath: string;
  sizeBytes: number;
  sizeLabel: string;
}

function formatSize(bytes: number): string {
  if (bytes >= 1_073_741_824) return `${(bytes / 1_073_741_824).toFixed(1)} GB`;
  if (bytes >= 1_048_576) return `${(bytes / 1_048_576).toFixed(1)} MB`;
  if (bytes >= 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${bytes} B`;
}

/** Check if a file starts with the GARC magic bytes ("CRAG"). */
function isGarc(filePath: string): boolean {
  const fd = fs.openSync(filePath, 'r');
  try {
    const buf = Buffer.alloc(4);
    const bytesRead = fs.readSync(fd, buf, 0, 4, 0);
    if (bytesRead < 4) return false;
    return buf.toString('ascii', 0, 4) === 'CRAG';
  } finally {
    fs.closeSync(fd);
  }
}

function scanRomFS(rootDir: string): ScannedArchive[] {
  const results: ScannedArchive[] = [];

  function walk(dir: string) {
    let entries: fs.Dirent[];
    try {
      entries = fs.readdirSync(dir, { withFileTypes: true });
    } catch {
      return;
    }

    for (const entry of entries) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        walk(full);
      } else if (entry.isFile()) {
        try {
          const stat = fs.statSync(full);
          if (stat.size >= 4 && isGarc(full)) {
            const subpath = path.relative(rootDir, full).replace(/\\/g, '/');
            results.push({
              subpath,
              sizeBytes: stat.size,
              sizeLabel: formatSize(stat.size),
            });
          }
        } catch {
          // skip unreadable files
        }
      }
    }
  }

  walk(rootDir);
  results.sort((a, b) => a.subpath.localeCompare(b.subpath));
  return results;
}

// ---------------------------------------------------------------------------
// Request / response schemas
// ---------------------------------------------------------------------------

interface StartBody {
  garcPath: string;
  outputDir: string;
  splitModelAnims: boolean;
  entryLimit?: number;
  deriveFolderNames?: boolean;
}

interface StatusParams {
  jobId: string;
}

interface CancelParams {
  jobId: string;
}

interface ResultsParams {
  jobId: string;
}

// ---------------------------------------------------------------------------
// Route plugin
// ---------------------------------------------------------------------------

export default async function extractionRoutes(app: FastifyInstance) {

  // ── POST /api/extraction/start ──────────────────────────────────────
  app.post<{ Body: StartBody }>('/api/extraction/start', async (request, reply) => {
    const { garcPath, outputDir, splitModelAnims, entryLimit, deriveFolderNames } = request.body;

    if (!garcPath || !outputDir) {
      return reply.status(400).send({ error: 'garcPath and outputDir are required' });
    }

    const jobId = randomUUID();

    const initialProgress: ExtractionProgress = {
      phase: 'idle',
      stats: {
        totalEntries: 0,
        processedEntries: 0,
        groupsFound: 0,
        modelsExported: 0,
        texturesExported: 0,
        clipsExported: 0,
        parseErrors: 0,
        exportErrors: 0,
      },
      logLines: [],
      elapsedSeconds: 0,
    };

    const job: ExtractionJob = {
      id: jobId,
      config: {
        garcPath,
        outputDir,
        splitModelAnims,
        entryLimit: entryLimit ?? undefined,
        deriveFolderNames: deriveFolderNames ?? true,
      },
      progress: initialProgress,
      results: [],
      cancelled: false,
      promise: null,
    };

    jobs.set(jobId, job);

    // Start extraction in background
    job.promise = (async () => {
      try {
        const results = await runExtraction(
          job.config,
          (progress) => {
            job.progress = progress;
          },
          () => job.cancelled,
        );

        if (job.cancelled) {
          job.progress = {
            ...job.progress,
            phase: 'stopped',
            logLines: [...job.progress.logLines, '', '--- Extraction stopped by user ---'],
          };
        } else {
          job.results = results;
          // The extraction lib already set phase to 'done' via the callback
        }
      } catch (err: any) {
        job.progress = {
          ...job.progress,
          phase: 'error',
          logLines: [...job.progress.logLines, '', `Fatal error: ${err.message ?? String(err)}`],
        };
      }
    })();

    return { jobId };
  });

  // ── GET /api/extraction/status/:jobId ───────────────────────────────
  app.get<{ Params: StatusParams }>('/api/extraction/status/:jobId', async (request, reply) => {
    const { jobId } = request.params;
    const job = jobs.get(jobId);
    if (!job) {
      return reply.status(404).send({ error: `Job not found: ${jobId}` });
    }

    const { phase, stats, logLines, elapsedSeconds } = job.progress;

    return {
      jobId,
      phase,
      stats,
      logLines,
      elapsedSeconds,
      complete: phase === 'done' || phase === 'error' || phase === 'stopped',
    };
  });

  // ── POST /api/extraction/cancel/:jobId ──────────────────────────────
  app.post<{ Params: CancelParams }>('/api/extraction/cancel/:jobId', async (request, reply) => {
    const { jobId } = request.params;
    const job = jobs.get(jobId);
    if (!job) {
      return reply.status(404).send({ error: `Job not found: ${jobId}` });
    }

    job.cancelled = true;

    return { jobId, cancelled: true };
  });

  // ── GET /api/extraction/results/:jobId ──────────────────────────────
  app.get<{ Params: ResultsParams }>('/api/extraction/results/:jobId', async (request, reply) => {
    const { jobId } = request.params;
    const job = jobs.get(jobId);
    if (!job) {
      return reply.status(404).send({ error: `Job not found: ${jobId}` });
    }

    return {
      jobId,
      phase: job.progress.phase,
      groups: job.results,
    };
  });

  // ── POST /api/extraction/scan ───────────────────────────────────────
  app.post<{ Body: { romfsPath: string } }>('/api/extraction/scan', async (request, reply) => {
    const { romfsPath } = request.body;
    if (!romfsPath) {
      return reply.status(400).send({ error: 'romfsPath is required' });
    }
    if (!fs.existsSync(romfsPath) || !fs.statSync(romfsPath).isDirectory()) {
      return reply.status(400).send({ error: `Not a directory: ${romfsPath}` });
    }

    const archives = scanRomFS(romfsPath);
    return { romfsPath, archives };
  });
}
