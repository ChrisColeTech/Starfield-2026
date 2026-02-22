import * as path from 'path';
import * as fs from 'fs';
import { randomUUID } from 'crypto';
import { exportModel } from '../lib/Program.js';
import { openArchive } from './archiveScanner.js';
import type { ExportRequest, ExportConfig, ExportEvent, ExportPhase, ExportStatus } from '../types/index.js';

type ProgressCallback = (event: ExportEvent) => void;

/**
 * Manages the lifecycle of a batch export job.
 * Thin orchestration layer — delegates actual work to lib/exportModel.
 */
export class ExportService {
    private jobId: string | null = null;
    private phase: ExportPhase = 'idle';
    private total = 0;
    private success = 0;
    private failed = 0;
    private skipped = 0;
    private startTime = 0;
    private cancelled = false;
    private listeners: Set<ProgressCallback> = new Set();

    /**
     * Start an export job. Scans the archive, discovers models, then exports.
     */
    start(request: ExportRequest): { id: string } {
        if (this.phase === 'exporting' || this.phase === 'scanning') {
            throw new Error('Export already in progress');
        }

        this.jobId = randomUUID();
        this.cancelled = false;
        this.success = 0;
        this.failed = 0;
        this.skipped = 0;
        this.startTime = Date.now();

        // Run in background
        this.runJob(request).catch((err) => {
            this.phase = 'error';
            this.emit({ type: 'error', message: err.message });
        });

        return { id: this.jobId };
    }

    cancel(): boolean {
        if (this.phase !== 'exporting' && this.phase !== 'scanning') return false;
        this.cancelled = true;
        this.phase = 'cancelled';
        return true;
    }

    getStatus(): ExportStatus {
        return {
            jobId: this.jobId ?? '',
            phase: this.phase,
            total: this.total,
            success: this.success,
            failed: this.failed,
            skipped: this.skipped,
            elapsedMs: this.startTime > 0 ? Date.now() - this.startTime : 0,
        };
    }

    onProgress(cb: ProgressCallback): () => void {
        this.listeners.add(cb);
        return () => this.listeners.delete(cb);
    }

    private emit(event: ExportEvent) {
        for (const cb of this.listeners) {
            try { cb(event); } catch { /* ignore */ }
        }
    }

    private async runJob(request: ExportRequest) {
        const { arcPath, outputDir, parallelJobs, maxModels, exportMode } = request;

        // Phase 1: Scan
        this.phase = 'scanning';
        this.emit({ type: 'scanning' });
        this.emit({ type: 'log', message: `Opening archive: ${arcPath}` });

        const loader = openArchive(arcPath);
        const allModels: string[] = [];
        for (const [_hash, name] of loader.FindFilesByExtension('.trmdl')) {
            allModels.push(name);
        }

        let modelPaths = allModels;
        if (maxModels > 0) {
            modelPaths = allModels.slice(0, maxModels);
        }

        this.total = modelPaths.length;
        const scanTime = Date.now() - this.startTime;
        this.emit({ type: 'scan-complete', totalModels: this.total, scanTimeMs: scanTime });
        this.emit({ type: 'log', message: `Found ${allModels.length} models (${(scanTime / 1000).toFixed(1)}s)` });

        if (modelPaths.length === 0) {
            this.phase = 'error';
            this.emit({ type: 'error', message: 'No models found in archive' });
            return;
        }

        // Phase 2: Export
        this.phase = 'exporting';
        this.emit({ type: 'started', total: this.total });
        fs.mkdirSync(outputDir, { recursive: true });

        const concurrency = Math.max(1, Math.min(parallelJobs, 16));
        const queue = [...modelPaths];
        let index = 0;

        const runNext = async (): Promise<void> => {
            while (queue.length > 0 && !this.cancelled) {
                const modelPath = queue.shift()!;
                const currentIndex = index++;
                const modelName = path.parse(modelPath).name;
                const modelOutDir = path.join(outputDir, modelName);

                // Skip if already exported
                if (fs.existsSync(path.join(modelOutDir, 'model.dae'))) {
                    this.skipped++;
                    this.emit({ type: 'model-skipped', model: modelName, index: currentIndex });
                    this.emit({ type: 'log', message: `[SKIP] ${modelName} (already exported)` });
                    continue;
                }

                try {
                    const result = await exportModel(loader, modelPath, modelOutDir);
                    if (result === 0) {
                        this.success++;
                        this.emit({ type: 'model-done', model: modelName, success: true, index: currentIndex });
                        this.emit({ type: 'log', message: `[OK] ${modelName}` });
                    } else {
                        this.failed++;
                        this.emit({ type: 'model-done', model: modelName, success: false, index: currentIndex });
                        this.emit({ type: 'log', message: `[FAIL] ${modelName}` });
                    }
                } catch (err: any) {
                    this.failed++;
                    this.emit({ type: 'model-done', model: modelName, success: false, index: currentIndex });
                    this.emit({ type: 'log', message: `[ERR] ${modelName}: ${err.message}` });
                }
            }
        };

        const workers = Array.from({ length: concurrency }, () => runNext());
        await Promise.all(workers);

        if (!this.cancelled) {
            this.phase = 'complete';
            const elapsed = (Date.now() - this.startTime) / 1000;
            this.emit({
                type: 'complete',
                success: this.success,
                failed: this.failed,
                skipped: this.skipped,
                elapsed,
            });
        }
    }
}
