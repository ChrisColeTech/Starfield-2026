import { create } from 'zustand';
import { subscribeWithSelector } from 'zustand/middleware';
import type { ExportPhase, ModelResult } from '../types';
import { loadPersistedState, persistValue } from '../services/persistence';

interface ExportStore {
    // Hydration
    hydrated: boolean;
    hydrate: () => Promise<void>;

    // Sidebar
    sidebarCollapsed: boolean;
    toggleSidebar: () => void;

    // Navigation
    lastActivePage: string;
    setLastActivePage: (page: string) => void;

    // Viewer
    viewerFolder: string;
    setViewerFolder: (path: string) => void;
    clipList: string[];
    setClipList: (clips: string[]) => void;
    selectedClip: string;
    setSelectedClip: (clip: string) => void;
    clipPanelOpen: boolean;
    toggleClipPanel: () => void;
    propsPanelOpen: boolean;
    togglePropsPanel: () => void;

    // Archive settings
    arcPath: string;
    setArcPath: (path: string) => void;
    outputDir: string;
    setOutputDir: (path: string) => void;
    parallelJobs: number;
    setParallelJobs: (n: number) => void;
    maxModels: number;
    setMaxModels: (n: number) => void;
    exportMode: 'baked' | 'split';
    setExportMode: (mode: 'baked' | 'split') => void;

    // Export state (not persisted — ephemeral)
    exportPhase: ExportPhase;
    setExportPhase: (phase: ExportPhase) => void;
    total: number;
    success: number;
    failed: number;
    skipped: number;
    elapsedMs: number;
    updateProgress: (update: Partial<{ total: number; success: number; failed: number; skipped: number; elapsedMs: number }>) => void;

    // Results (not persisted)
    results: ModelResult[];
    addResult: (result: ModelResult) => void;
    clearResults: () => void;

    // Log (not persisted)
    logs: string[];
    addLog: (message: string) => void;
    clearLogs: () => void;
}

export const useStore = create<ExportStore>()(
    subscribeWithSelector((set) => ({
        // Hydration
        hydrated: false,
        hydrate: async () => {
            const saved = await loadPersistedState();
            set({
                hydrated: true,
                arcPath: saved.arcPath,
                outputDir: saved.outputDir,
                parallelJobs: saved.parallelJobs,
                maxModels: saved.maxModels,
                exportMode: saved.exportMode,
                sidebarCollapsed: saved.sidebarCollapsed,
                lastActivePage: saved.lastActivePage,
                viewerFolder: saved.viewerFolder,
                clipPanelOpen: saved.clipPanelOpen,
                propsPanelOpen: saved.propsPanelOpen,
            });
        },

        // Sidebar
        sidebarCollapsed: false,
        toggleSidebar: () => set((s) => ({ sidebarCollapsed: !s.sidebarCollapsed })),

        // Navigation
        lastActivePage: '/export',
        setLastActivePage: (lastActivePage) => set({ lastActivePage }),

        // Viewer
        viewerFolder: '',
        setViewerFolder: (viewerFolder) => set({ viewerFolder }),
        clipList: [],
        setClipList: (clipList) => set({ clipList }),
        selectedClip: '',
        setSelectedClip: (selectedClip) => set({ selectedClip }),
        clipPanelOpen: true,
        toggleClipPanel: () => set((s) => ({ clipPanelOpen: !s.clipPanelOpen })),
        propsPanelOpen: true,
        togglePropsPanel: () => set((s) => ({ propsPanelOpen: !s.propsPanelOpen })),

        // Archive settings
        arcPath: '',
        setArcPath: (arcPath) => set({ arcPath }),
        outputDir: '',
        setOutputDir: (outputDir) => set({ outputDir }),
        parallelJobs: 8,
        setParallelJobs: (parallelJobs) => set({ parallelJobs }),
        maxModels: 0,
        setMaxModels: (maxModels) => set({ maxModels }),
        exportMode: 'baked',
        setExportMode: (exportMode) => set({ exportMode }),

        // Export state
        exportPhase: 'idle',
        setExportPhase: (exportPhase) => set({ exportPhase }),
        total: 0,
        success: 0,
        failed: 0,
        skipped: 0,
        elapsedMs: 0,
        updateProgress: (update) => set((s) => ({ ...s, ...update })),

        // Results
        results: [],
        addResult: (result) => set((s) => ({ results: [...s.results, result] })),
        clearResults: () => set({ results: [] }),

        // Log
        logs: [],
        addLog: (message) => set((s) => ({ logs: [...s.logs, message] })),
        clearLogs: () => set({ logs: [] }),
    }))
);

// Auto-persist on change — only the keys that should survive restart
const PERSISTED_KEYS = [
    'arcPath', 'outputDir', 'parallelJobs', 'maxModels', 'exportMode',
    'sidebarCollapsed', 'lastActivePage', 'viewerFolder', 'clipPanelOpen', 'propsPanelOpen',
] as const;

for (const key of PERSISTED_KEYS) {
    useStore.subscribe(
        (s) => s[key],
        (value) => {
            if (useStore.getState().hydrated) {
                persistValue(key, value);
            }
        }
    );
}
