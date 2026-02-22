import React from 'react';
import { Menu, MenuButton, MenuItem, MenuItems } from '@headlessui/react';
import type { MenuDefinition } from '../../types';

interface HeaderMenuBarProps {
    menus: MenuDefinition[];
}

export function HeaderMenuBar({ menus }: HeaderMenuBarProps) {
    return (
        <>
            {menus.map((menu) => (
                <Menu as="div" key={menu.label} className="relative" style={{ WebkitAppRegion: 'no-drag' } as React.CSSProperties}>
                    <MenuButton className="h-[30px] px-[10px] bg-transparent border-none text-text cursor-pointer hover:bg-hover text-[13px] focus:outline-none data-[open]:bg-[#2d2d2d]">
                        {menu.label}
                    </MenuButton>

                    <MenuItems className="absolute top-[30px] left-0 bg-bg border border-border shadow-xl min-w-[220px] py-[4px] z-50 whitespace-nowrap focus:outline-none">
                        {menu.items.map((item, j) =>
                            item.separator ? (
                                <div key={j} className="h-[1px] bg-border my-[4px] mx-[10px]" />
                            ) : (
                                <MenuItem key={j} disabled={item.disabled}>
                                    {({ focus }) => (
                                        <button
                                            className={`w-full h-[28px] px-[20px] bg-transparent border-none text-left text-[13px] flex items-center justify-between cursor-pointer disabled:opacity-40 disabled:cursor-default ${focus ? 'bg-hover' : ''}`}
                                            style={{
                                                color: item.danger ? '#c74e4e' : item.disabled ? '#555555' : '#e0e0e0',
                                            }}
                                            onClick={() => item.onClick?.()}
                                        >
                                            <span>{item.label}</span>
                                            {item.shortcut && (
                                                <span className="text-text-disabled text-[12px] ml-[30px]">
                                                    {item.shortcut}
                                                </span>
                                            )}
                                        </button>
                                    )}
                                </MenuItem>
                            ),
                        )}
                    </MenuItems>
                </Menu>
            ))}
        </>
    );
}
