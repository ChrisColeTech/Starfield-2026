import React from 'react';
import { useStore } from '../../store';
import { api } from '../../services/apiClient';
import type { MenuDefinition } from '../../types';
import { HeaderMenuBar } from './HeaderMenuBar';

export function Header() {
  const randomizeSeed = useStore((s) => s.randomizeSeed);
  const setFrames = useStore((s) => s.setFrames);
  const selectedType = useStore((s) => s.selectedType);
  const selectedVariant = useStore((s) => s.selectedVariant);
  const seed = useStore((s) => s.seed);
  const setLoading = useStore((s) => s.setLoading);
  const setError = useStore((s) => s.setError);
  const setGalleryItems = useStore((s) => s.setGalleryItems);
  const toggleGrid = useStore((s) => s.toggleGrid);

  const menus: MenuDefinition[] = [
    {
      label: 'File',
      items: [
        {
          label: 'New Sprite',
          shortcut: 'Ctrl+N',
          onClick: () => {
            randomizeSeed();
            setFrames([]);
          },
        },
        { separator: true, label: '' },
        {
          label: 'Save Frames',
          shortcut: 'Ctrl+S',
          onClick: async () => {
            setLoading(true);
            setError(null);
            try {
              await api.save(selectedType, seed, selectedVariant ?? undefined);
              const gallery = await api.getGallery();
              setGalleryItems(gallery.items);
            } catch (e) {
              setError(e instanceof Error ? e.message : 'Save failed');
            } finally {
              setLoading(false);
            }
          },
        },
        { label: 'Export PNG...', disabled: true },
        { separator: true, label: '' },
        {
          label: 'Clear All Sprites',
          danger: true,
          onClick: async () => {
            setLoading(true);
            setError(null);
            try {
              await api.clearAll();
              setGalleryItems([]);
            } catch (e) {
              setError(e instanceof Error ? e.message : 'Clear failed');
            } finally {
              setLoading(false);
            }
          },
        },
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
        { label: 'Show Pixel Grid', onClick: toggleGrid },
        { label: 'Reset Zoom', disabled: true },
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
        SpriteGen
      </span>

      <HeaderMenuBar menus={menus} />

      <div className="flex-1" />
    </div>
  );
}
