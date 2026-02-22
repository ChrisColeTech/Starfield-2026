import type { ProgressEvent } from '../types';

const API_BASE = '/api';

export interface StartExportRequest {
    arcPath: string;
    outputDir: string;
    parallelJobs: number;
    maxModels: number;
    exportMode: 'baked' | 'split';
}

export const api = {
    async startExport(request: StartExportRequest): Promise<{ jobId: string }> {
        const res = await fetch(`${API_BASE}/export/start`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(request),
        });
        if (!res.ok) throw new Error((await res.json()).error ?? 'Export failed to start');
        return res.json();
    },

    async cancelExport(): Promise<{ cancelled: boolean }> {
        const res = await fetch(`${API_BASE}/export/cancel`, { method: 'POST' });
        return res.json();
    },

    async getStatus(): Promise<{ phase: string; total: number; success: number; failed: number; skipped: number }> {
        const res = await fetch(`${API_BASE}/export/status`, { method: 'POST' });
        return res.json();
    },

    async listDaeFiles(folderPath: string): Promise<string[]> {
        const res = await fetch(`${API_BASE}/fs/list-dae`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ path: folderPath }),
        });
        if (!res.ok) throw new Error((await res.json()).error ?? 'List failed');
        const data = await res.json();
        return data.files;
    },

    subscribeProgress(onEvent: (event: ProgressEvent) => void): () => void {
        const source = new EventSource(`${API_BASE}/export/progress`);
        source.onmessage = (e) => {
            try {
                onEvent(JSON.parse(e.data));
            } catch { /* ignore */ }
        };
        return () => source.close();
    },
};
