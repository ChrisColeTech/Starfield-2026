import { useEffect, useRef, useCallback } from 'react';
import { useStore } from '../store/exportStore';
import { api } from '../services/apiClient';

export function useExportPage() {
    const {
        arcPath, setArcPath,
        outputDir, setOutputDir,
        parallelJobs, setParallelJobs,
        maxModels, setMaxModels,
        exportMode, setExportMode,
        exportPhase, setExportPhase,
        total, success, failed, skipped,
        updateProgress,
        addResult, clearResults,
        logs, addLog, clearLogs,
    } = useStore();

    // Log auto-scroll
    const logEndRef = useRef<HTMLDivElement>(null);
    useEffect(() => {
        logEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    }, [logs]);

    // Derived
    const processed = success + failed + skipped;
    const pct = total > 0 ? (processed / total) * 100 : 0;

    // Handlers
    const handleBrowseArc = useCallback(() => {
        const electronAPI = (window as any).electronAPI;
        if (electronAPI?.browseFolder) {
            electronAPI.browseFolder('Select Archive Directory').then((path: string | null) => {
                if (path) setArcPath(path);
            });
        }
    }, [setArcPath]);

    const handleBrowseOutput = useCallback(() => {
        const electronAPI = (window as any).electronAPI;
        if (electronAPI?.browseFolder) {
            electronAPI.browseFolder('Select Output Directory').then((path: string | null) => {
                if (path) setOutputDir(path);
            });
        }
    }, [setOutputDir]);

    const handleStart = useCallback(async () => {
        if (!arcPath) return;
        setExportPhase('exporting');
        clearResults();
        clearLogs();
        updateProgress({ total: 0, success: 0, failed: 0, skipped: 0, elapsedMs: 0 });

        // Subscribe to progress BEFORE starting so we don't miss events
        const unsub = api.subscribeProgress((event) => {
            switch (event.type) {
                case 'scanning':
                    addLog('Scanning archive...');
                    break;
                case 'scan-complete':
                    addLog(`Found ${event.total ?? 0} models.`);
                    break;
                case 'started':
                    updateProgress({ total: event.total });
                    break;
                case 'model-done':
                    addResult({ name: event.model!, status: event.success ? 'ok' : 'fail' });
                    if (event.success) {
                        updateProgress({ success: useStore.getState().success + 1 });
                    } else {
                        updateProgress({ failed: useStore.getState().failed + 1 });
                    }
                    break;
                case 'model-skipped':
                    addResult({ name: event.model!, status: 'skipped' });
                    updateProgress({ skipped: useStore.getState().skipped + 1 });
                    break;
                case 'log':
                    addLog(event.message!);
                    break;
                case 'complete':
                    setExportPhase('complete');
                    updateProgress({ elapsedMs: event.elapsed! * 1000 });
                    addLog(`[DONE] ${event.success} OK, ${event.failed} failed, ${event.skipped} skipped (${event.elapsed!.toFixed(1)}s)`);
                    unsub();
                    break;
                case 'error':
                    setExportPhase('error');
                    addLog(`[ERR] ${event.message}`);
                    unsub();
                    break;
            }
        });

        try {
            await api.startExport({
                arcPath,
                outputDir,
                parallelJobs,
                maxModels,
                exportMode,
            });
        } catch (err) {
            setExportPhase('error');
            addLog(`[ERR] Failed: ${err instanceof Error ? err.message : 'Unknown'}`);
            unsub();
        }
    }, [arcPath, outputDir, parallelJobs, maxModels, exportMode, setExportPhase, clearResults, clearLogs, addLog, addResult, updateProgress]);

    const handleStop = useCallback(async () => {
        await api.cancelExport();
        setExportPhase('cancelled');
        addLog('[STOP] Export cancelled.');
    }, [setExportPhase, addLog]);

    const handleReset = useCallback(() => {
        setExportPhase('idle');
        clearResults();
        clearLogs();
        updateProgress({ total: 0, success: 0, failed: 0, skipped: 0, elapsedMs: 0 });
    }, [setExportPhase, clearResults, clearLogs, updateProgress]);

    return {
        arcPath, setArcPath,
        outputDir, setOutputDir,
        parallelJobs, setParallelJobs,
        maxModels, setMaxModels,
        exportMode, setExportMode,
        exportPhase,
        total, success, failed, skipped,
        logs,
        processed,
        pct,
        logEndRef,
        handleBrowseArc,
        handleBrowseOutput,
        handleStart,
        handleStop,
        handleReset,
        clearLogs,
    };
}
