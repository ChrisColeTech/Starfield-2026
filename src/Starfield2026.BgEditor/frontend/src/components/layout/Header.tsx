import React, { useRef, useCallback } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import type { MenuDefinition } from '../../types';
import { HeaderMenuBar } from './HeaderMenuBar';
import { useEditorStore } from '../../store/editorStore';
import { useAnimationEditorStore } from '../../store/animationEditorStore';

export function Header() {
    const navigate = useNavigate();
    const location = useLocation();
    const fileInputRef = useRef<HTMLInputElement>(null);

    // ── Editor store ──
    const loadManifest = useEditorStore(s => s.loadManifest);
    const sceneName = useEditorStore(s => s.sceneName);
    const resetAll = useEditorStore(s => s.resetAll);
    const applyToAll = useEditorStore(s => s.applyToAll);
    const textures = useEditorStore(s => s.textures);
    const selectedTextureIndex = useEditorStore(s => s.selectedTextureIndex);
    const resetTexture = useEditorStore(s => s.resetTexture);

    const hasScene = !!sceneName;
    const hasModifications = textures.some(t => t.modifiedDataUrl !== t.originalDataUrl);

    const handleFileInput = (e: React.ChangeEvent<HTMLInputElement>) => {
        const file = e.target.files?.[0];
        if (file) loadManifest(file);
        e.target.value = '';
    };

    // ── Animation store ──
    const animManifest = useAnimationEditorStore(s => s.manifest);
    const animDirty = useAnimationEditorStore(s => s.dirty);
    const animSaving = useAnimationEditorStore(s => s.saving);
    const animSave = useAnimationEditorStore(s => s.save);
    const animAutoTag = useAnimationEditorStore(s => s.autoTag);

    const hasAnimModel = !!animManifest;

    // ── Browse folder helper ──
    const browseFolder = useCallback(async (defaultPath?: string) => {
        return (window as any).electronAPI?.browseFolder?.(defaultPath ?? '') ?? null;
    }, []);

    // ── Menus ──
    const menus: MenuDefinition[] = [
        {
            label: 'File',
            items: [
                { label: 'New', shortcut: 'Ctrl+N', disabled: true },
                { separator: true, label: '' },
                { label: 'Save', shortcut: 'Ctrl+S', disabled: true },
                { label: 'Export PNG...', disabled: true },
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
            className="h-[30px] bg-bg border-b border-border flex items-center select-none"
            style={{ fontSize: 13, WebkitAppRegion: 'drag' } as React.CSSProperties}
        >
            <span
                className="px-[10px] text-[13px] font-semibold text-text border-r border-border h-full flex items-center"
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
