import React from 'react';
import { useEditorStore } from '../../store/editorStore';
import { parseCSharpRegistry } from '../../services/registryService';
import { toPascalCase } from '../../services/codeGenService';
import type { MenuDefinition } from '../../types';
import { HeaderMenuBar } from './HeaderMenuBar';

const CS_FILTERS = [{ name: 'C# Files', extensions: ['cs'] }];

export function Header() {
    const mapName = useEditorStore(s => s.mapName);
    const importCSharpMap = useEditorStore(s => s.importCSharpMap);
    const exportCSharp = useEditorStore(s => s.exportCSharp);
    const exportRegistryCSharp = useEditorStore(s => s.exportRegistryCSharp);
    const clear = useEditorStore(s => s.clear);
    const setRegistry = useEditorStore(s => s.setRegistry);

    async function handleImportCSharp() {
        const result = await window.electronAPI.openFile(CS_FILTERS);
        if (result) importCSharpMap(result.content);
    }

    async function handleExportCSharp() {
        const code = exportCSharp();
        const mapId = mapName.toLowerCase().replace(/\s+/g, '_').replace(/[^a-z0-9_]/g, '');
        const className = toPascalCase(mapId) || 'UntitledMap';
        await window.electronAPI.saveFile(`${className}.g.cs`, CS_FILTERS, code);
    }

    async function handleLoadCSharpRegistry() {
        const result = await window.electronAPI.openFile(CS_FILTERS);
        if (!result) return;
        try {
            const registry = parseCSharpRegistry(result.content);
            setRegistry(registry);
        } catch (err) {
            alert(`Invalid C# registry: ${err instanceof Error ? err.message : 'Unknown error'}`);
        }
    }

    async function handleExportRegistryCSharp() {
        const code = exportRegistryCSharp();
        await window.electronAPI.saveFile('TileRegistry.cs', CS_FILTERS, code);
    }

    const menus: MenuDefinition[] = [
        {
            label: 'File',
            items: [
                { label: 'New Map', shortcut: 'Ctrl+N', onClick: clear },
                { separator: true, label: '' },
                { label: 'Import Map (C#)...', onClick: handleImportCSharp },
                { label: 'Export Map (C#)...', onClick: handleExportCSharp },
                { separator: true, label: '' },
                { label: 'Load Registry (C#)...', onClick: handleLoadCSharpRegistry },
                { label: 'Export Registry (C#)...', onClick: handleExportRegistryCSharp },
            ],
        },
        {
            label: 'Edit',
            items: [
                { label: 'Undo', shortcut: 'Ctrl+Z', disabled: true },
                { label: 'Redo', shortcut: 'Ctrl+Y', disabled: true },
                { separator: true, label: '' },
                { label: 'Clear All', onClick: clear },
            ],
        },
        {
            label: 'View',
            items: [
                { label: 'Show Grid' },
                { label: 'Show Trainer Vision' },
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
                MapEditor
            </span>

            <HeaderMenuBar menus={menus} />

            <div className="flex-1" />
        </div>
    );
}
