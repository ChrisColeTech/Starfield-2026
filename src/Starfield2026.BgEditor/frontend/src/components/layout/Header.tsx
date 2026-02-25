import React, { useRef, useCallback, useState, useEffect } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import type { MenuDefinition, MenuItem as MenuItemType } from '../../types';
import { HeaderMenuBar } from './HeaderMenuBar';
import { useEditorStore } from '../../store/editorStore';

export function Header() {
    const navigate = useNavigate();
    const location = useLocation();
    const fileInputRef = useRef<HTMLInputElement>(null);

    // ── Store ──
    const loadManifest = useEditorStore(s => s.loadManifest);
    const sceneName = useEditorStore(s => s.sceneName);
    const resetAll = useEditorStore(s => s.resetAll);
    const applyToAll = useEditorStore(s => s.applyToAll);
    const textures = useEditorStore(s => s.textures);
    const selectedTextureIndex = useEditorStore(s => s.selectedTextureIndex);
    const resetTexture = useEditorStore(s => s.resetTexture);
    const clearAll = useEditorStore(s => s.clearAll);

    const animManifest = useEditorStore(s => s.animManifest);
    const animDirty = useEditorStore(s => s.dirty);
    const animSaving = useEditorStore(s => s.saving);
    const animSave = useEditorStore(s => s.saveManifest);
    const animAutoTag = useEditorStore(s => s.autoTag);

    const hasScene = !!sceneName;
    const hasModifications = textures.some(t => t.modifiedDataUrl !== t.originalDataUrl);
    const hasAnimModel = !!animManifest;

    // ── Recent paths ──
    const [recentPaths, setRecentPaths] = useState<string[]>([]);

    useEffect(() => {
        (window as any).electronAPI?.storeGet?.('recentPaths').then((paths: string[] | null) => {
            if (paths) setRecentPaths(paths);
        });
    }, []);

    const addRecent = useCallback(async (filePath: string) => {
        const normalized = filePath.replace(/\\/g, '/');
        const updated = [normalized, ...recentPaths.filter(p => p !== normalized)].slice(0, 10);
        setRecentPaths(updated);
        await (window as any).electronAPI?.storeSet?.('recentPaths', updated);
    }, [recentPaths]);

    const handleFileInput = (e: React.ChangeEvent<HTMLInputElement>) => {
        const file = e.target.files?.[0];
        if (file) loadManifest(file);
        e.target.value = '';
    };

    // ── Browse folder helper ──
    const browseFolder = useCallback(async (defaultPath?: string) => {
        return (window as any).electronAPI?.browseFolder?.(defaultPath ?? '') ?? null;
    }, []);

    // ── Menus ──
    const recentItems: MenuItemType[] = recentPaths.length > 0
        ? [
            ...recentPaths.map((p) => ({
                label: p.split('/').pop() || p,
                onClick: async () => {
                    navigate('/');
                    useEditorStore.getState().loadManifestFromPath(p);
                },
            })),
            { separator: true, label: '' },
            {
                label: 'Clear Recent',
                onClick: async () => {
                    setRecentPaths([]);
                    await (window as any).electronAPI?.storeSet?.('recentPaths', []);
                },
            },
        ]
        : [{ label: 'No Recent Files', disabled: true }];

    const menus: MenuDefinition[] = [
        {
            label: 'File',
            active: true,
            items: [
                {
                    label: 'New',
                    shortcut: 'Ctrl+N',
                    onClick: () => {
                        clearAll();
                        navigate('/');
                    },
                },
                {
                    label: 'New Window',
                    onClick: () => {
                        // Open a new Electron window at the same URL
                        window.open(window.location.origin, '_blank');
                    },
                },
                { separator: true, label: '' },
                {
                    label: 'Open File...',
                    shortcut: 'Ctrl+O',
                    onClick: async () => {
                        navigate('/');
                        const filePath = await (window as any).electronAPI?.browseFile?.();
                        if (filePath) {
                            addRecent(filePath);
                            useEditorStore.getState().loadManifestFromPath(filePath);
                        }
                    },
                },
                {
                    label: 'Open Folder...',
                    shortcut: 'Ctrl+Shift+O',
                    onClick: async () => {
                        navigate('/');
                        const picked = await browseFolder();
                        if (picked) {
                            addRecent(picked);
                            useEditorStore.getState().scanFolder(picked);
                        }
                    },
                },
                { separator: true, label: '' },
                { label: 'Open Recent', children: recentItems },
                { separator: true, label: '' },
                {
                    label: 'Exit',
                    onClick: () => window.close(),
                },
            ],
        },
        {
            label: 'Editor',
            active: location.pathname === '/',
            items: [
                {
                    label: 'Open Manifest...',
                    onClick: async () => {
                        navigate('/');
                        const filePath = await (window as any).electronAPI?.browseFile?.();
                        if (filePath) useEditorStore.getState().loadManifestFromPath(filePath);
                    },
                },
                { separator: true, label: '' },
                {
                    label: 'Save Textures',
                    shortcut: 'Ctrl+S',
                    disabled: !hasScene || !hasModifications,
                },
                {
                    label: 'Export Textures...',
                    disabled: !hasScene || !hasModifications,
                },
                { separator: true, label: '' },
                {
                    label: 'Reset Selected',
                    disabled: !hasScene,
                    onClick: () => resetTexture(selectedTextureIndex),
                },
                {
                    label: 'Apply to All',
                    disabled: !hasScene,
                    onClick: () => applyToAll(),
                },
                {
                    label: 'Reset All',
                    disabled: !hasScene,
                    onClick: () => resetAll(),
                },
            ],
        },
        {
            label: 'Animations',
            active: location.pathname === '/animations',
            items: [
                {
                    label: 'Open Folder...',
                    onClick: async () => {
                        navigate('/animations');
                        const picked = await browseFolder();
                        if (picked) {
                            // Dispatch a custom event so AnimationsPage can pick it up
                            window.dispatchEvent(new CustomEvent('animations:browse', { detail: picked }));
                        }
                    },
                },
                { separator: true, label: '' },
                {
                    label: 'Save Tags',
                    shortcut: 'Ctrl+S',
                    disabled: !hasAnimModel || !animDirty || animSaving,
                    onClick: () => animSave(),
                },
                {
                    label: 'Auto-tag All',
                    disabled: !hasAnimModel,
                    onClick: () => animAutoTag(),
                },
                { separator: true, label: '' },
                {
                    label: 'Render Angles...',
                    disabled: !hasAnimModel && !hasScene,
                    onClick: async () => {
                        const state = useEditorStore.getState();
                        const manifestDir = state.manifest?.dir || state.folderPath;
                        if (!manifestDir) {
                            alert('No model loaded. Open a manifest first.');
                            return;
                        }
                        const outputDir = await browseFolder();
                        if (!outputDir) return;
                        try {
                            const manifestPath = `${manifestDir}/manifest.json`.replace(/\\/g, '/');
                            const res = await fetch('http://localhost:3001/api/render-angles', {
                                method: 'POST',
                                headers: { 'Content-Type': 'application/json' },
                                body: JSON.stringify({ manifestPath, outputDir }),
                            });
                            const data = await res.json();
                            if (!res.ok) throw new Error(data.error || 'Render failed');
                            alert(`Saved ${data.saved.length} renders to:\n${outputDir}`);
                        } catch (err: any) {
                            alert(`Render failed: ${err.message}`);
                        }
                    },
                },
            ],
        },
        {
            label: 'Tools',
            active: location.pathname === '/tools',
            items: [
                {
                    label: 'Browse Input Folder...',
                    onClick: async () => {
                        navigate('/tools');
                        const picked = await browseFolder();
                        if (picked) {
                            window.dispatchEvent(new CustomEvent('tools:browseInput', { detail: picked }));
                        }
                    },
                },
                {
                    label: 'Browse Output Folder...',
                    onClick: async () => {
                        navigate('/tools');
                        const picked = await browseFolder();
                        if (picked) {
                            window.dispatchEvent(new CustomEvent('tools:browseOutput', { detail: picked }));
                        }
                    },
                },
                { separator: true, label: '' },
                {
                    label: 'Generate Manifests',
                    disabled: true,  // page handles this directly
                    onClick: () => {
                        navigate('/tools');
                        window.dispatchEvent(new CustomEvent('tools:generate'));
                    },
                },
                {
                    label: 'Refresh Manifest List',
                    onClick: () => {
                        navigate('/tools');
                        window.dispatchEvent(new CustomEvent('tools:refresh'));
                    },
                },
            ],
        },
        {
            label: 'Extraction',
            active: location.pathname === '/extraction',
            items: [
                {
                    label: 'Browse RomFS Folder...',
                    onClick: async () => {
                        navigate('/extraction');
                        const picked = await browseFolder();
                        if (picked) {
                            window.dispatchEvent(new CustomEvent('extraction:browseRomfs', { detail: picked }));
                        }
                    },
                },
                {
                    label: 'Browse Output Folder...',
                    onClick: async () => {
                        navigate('/extraction');
                        const picked = await browseFolder();
                        if (picked) {
                            window.dispatchEvent(new CustomEvent('extraction:browseOutput', { detail: picked }));
                        }
                    },
                },
                { separator: true, label: '' },
                {
                    label: 'Start Extraction',
                    disabled: true,  // page handles enable/disable
                    onClick: () => {
                        navigate('/extraction');
                        window.dispatchEvent(new CustomEvent('extraction:start'));
                    },
                },
                {
                    label: 'Stop Extraction',
                    disabled: true,  // page handles enable/disable
                    onClick: () => {
                        navigate('/extraction');
                        window.dispatchEvent(new CustomEvent('extraction:stop'));
                    },
                },
            ],
        },
    ];

    return (
        <div
            className="h-[30px] bg-background border-b border-border flex items-center select-none"
            style={{ fontSize: 13, WebkitAppRegion: 'drag' } as React.CSSProperties}
        >
            <span
                className="px-2.5 text-[13px] font-semibold text-foreground border-r border-border h-full flex items-center"
                style={{ WebkitAppRegion: 'no-drag' } as React.CSSProperties}
            >
                BgEditor
            </span>

            <HeaderMenuBar menus={menus} />

            {/* Hidden file input for Open Manifest */}
            <input
                ref={fileInputRef}
                type="file"
                accept=".json"
                onChange={handleFileInput}
                style={{ display: 'none' }}
            />

            <div className="flex-1" />
        </div>
    );
}
