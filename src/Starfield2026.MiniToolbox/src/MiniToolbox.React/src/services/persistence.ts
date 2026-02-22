/**
 * Unified persistence layer.
 * Uses electron-store (via IPC) when running in Electron,
 * falls back to localStorage in the browser.
 */

const STORAGE_KEY = 'switchtoolbox-settings';

interface PersistedState {
    arcPath: string;
    outputDir: string;
    parallelJobs: number;
    maxModels: number;
    exportMode: 'baked' | 'split';
    sidebarCollapsed: boolean;
    lastActivePage: string;
    viewerFolder: string;
    clipPanelOpen: boolean;
    propsPanelOpen: boolean;
}

const DEFAULTS: PersistedState = {
    arcPath: '',
    outputDir: '',
    parallelJobs: 8,
    maxModels: 0,
    exportMode: 'baked',
    sidebarCollapsed: false,
    lastActivePage: '/export',
    viewerFolder: '',
    clipPanelOpen: true,
    propsPanelOpen: true,
};

function getElectronAPI(): any | null {
    return (window as any).electronAPI?.storeGet ? (window as any).electronAPI : null;
}

export async function loadPersistedState(): Promise<PersistedState> {
    const electronAPI = getElectronAPI();

    if (electronAPI) {
        try {
            const all = await electronAPI.storeGetAll();
            return { ...DEFAULTS, ...all };
        } catch {
            return { ...DEFAULTS };
        }
    }

    // localStorage fallback
    try {
        const raw = localStorage.getItem(STORAGE_KEY);
        if (raw) {
            return { ...DEFAULTS, ...JSON.parse(raw) };
        }
    } catch { /* ignore */ }

    return { ...DEFAULTS };
}

export async function persistValue(key: keyof PersistedState, value: any): Promise<void> {
    const electronAPI = getElectronAPI();

    if (electronAPI) {
        try {
            await electronAPI.storeSet(key, value);
        } catch { /* ignore */ }
        return;
    }

    // localStorage fallback
    try {
        const raw = localStorage.getItem(STORAGE_KEY);
        const current = raw ? JSON.parse(raw) : {};
        current[key] = value;
        localStorage.setItem(STORAGE_KEY, JSON.stringify(current));
    } catch { /* ignore */ }
}

export async function persistMultiple(updates: Partial<PersistedState>): Promise<void> {
    const electronAPI = getElectronAPI();

    if (electronAPI) {
        for (const [key, value] of Object.entries(updates)) {
            try {
                await electronAPI.storeSet(key, value);
            } catch { /* ignore */ }
        }
        return;
    }

    // localStorage fallback
    try {
        const raw = localStorage.getItem(STORAGE_KEY);
        const current = raw ? JSON.parse(raw) : {};
        Object.assign(current, updates);
        localStorage.setItem(STORAGE_KEY, JSON.stringify(current));
    } catch { /* ignore */ }
}

export type { PersistedState };
