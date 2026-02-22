import React from 'react';
import type { MenuDefinition } from '../../types';
import { HeaderMenuBar } from './HeaderMenuBar';

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
        label: 'Edit',
        items: [
            { label: 'Undo', shortcut: 'Ctrl+Z', disabled: true },
            { label: 'Redo', shortcut: 'Ctrl+Y', disabled: true },
        ],
    },
    {
        label: 'View',
        items: [
            { label: 'Reset Zoom', disabled: true },
            { label: 'Show Grid', disabled: true },
        ],
    },
];

export function Header() {
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

            <div className="flex-1" />
        </div>
    );
}
