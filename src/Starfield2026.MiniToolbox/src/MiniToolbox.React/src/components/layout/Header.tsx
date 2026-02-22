import React from 'react';
import { useHeaderMenu } from '../../hooks/useHeaderMenu';
import { HeaderMenuBar } from './HeaderMenuBar';

export function Header() {
    const { menus } = useHeaderMenu();

    return (
        <div
            className="h-[30px] bg-bg border-b border-border flex items-center select-none"
            style={{ fontSize: 13, WebkitAppRegion: 'drag' } as React.CSSProperties}
        >
            <span
                className="px-[10px] text-[13px] font-semibold text-text border-r border-border h-full flex items-center"
                style={{ WebkitAppRegion: 'no-drag' } as React.CSSProperties}
            >
                SwitchToolbox
            </span>

            <HeaderMenuBar menus={menus} />

            <div className="flex-1" />
        </div>
    );
}
