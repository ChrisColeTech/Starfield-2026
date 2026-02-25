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
                    <MenuButton
                        className={`h-[30px] px-2.5 bg-transparent border-none cursor-pointer text-[13px] focus:outline-none hover:bg-muted data-[open]:bg-muted ${menu.active ? 'text-foreground' : 'text-muted-foreground'}`}
                    >
                        {menu.label}
                    </MenuButton>

                    <MenuItems className="absolute top-[30px] left-0 bg-background border border-border shadow-xl min-w-[220px] py-1 z-50 whitespace-nowrap focus:outline-none">
                        {menu.items.map((item, j) =>
                            item.separator ? (
                                <div key={j} className="h-px bg-border my-1 mx-2.5" />
                            ) : (
                                <MenuItem key={j} disabled={item.disabled}>
                                    {({ focus }) => (
                                        <button
                                            className={`w-full h-7 px-5 bg-transparent border-none text-left text-[13px] flex items-center justify-between cursor-pointer disabled:opacity-40 disabled:cursor-default ${focus ? 'bg-muted' : ''
                                                } ${item.danger ? 'text-destructive'
                                                    : item.disabled ? 'text-muted-foreground/50'
                                                        : 'text-foreground'
                                                }`}
                                            onClick={() => item.onClick?.()}
                                        >
                                            <span>{item.label}</span>
                                            {item.shortcut && (
                                                <span className="text-muted-foreground/50 text-xs ml-8">
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
