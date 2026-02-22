// ── Viewer types ──

export interface ModelInfo {
    vertices: number;
    faces: number;
    bones: number;
    materials: number;
    clips: string[];
}

export interface RenderSettings {
    showWireframe: boolean;
    showSkeleton: boolean;
    showGrid: boolean;
    showTextures: boolean;
    lightIntensity: number;
}

// ── Menu types ──

export interface MenuItem {
    label: string;
    shortcut?: string;
    disabled?: boolean;
    danger?: boolean;
    separator?: boolean;
    onClick?: () => void;
}

export interface MenuDefinition {
    label: string;
    items: MenuItem[];
}

// ── Export types ──

export type ExportPhase = 'idle' | 'exporting' | 'complete' | 'cancelled' | 'error';

export interface ProgressEvent {
    type: 'scanning' | 'scan-complete' | 'started' | 'model-done' | 'model-skipped' | 'complete' | 'error' | 'log';
    total?: number;
    model?: string;
    success?: boolean | number;
    failed?: number;
    skipped?: number;
    elapsed?: number;
    message?: string;
}

export interface ModelResult {
    name: string;
    status: 'ok' | 'fail' | 'skipped';
}
