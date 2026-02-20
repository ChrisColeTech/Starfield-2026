import { useState, useEffect, useRef } from 'react';
import { useStore } from '../../store';
import { api } from '../../services/apiClient';

interface MenuItem {
  label: string;
  shortcut?: string;
  disabled?: boolean;
  danger?: boolean;
  separator?: boolean;
  onClick?: () => void;
}

interface MenuDefinition {
  label: string;
  items: MenuItem[];
}

export function Header() {
  const [openIndex, setOpenIndex] = useState<number | null>(null);
  const menuRef = useRef<HTMLDivElement>(null);

  const randomizeSeed = useStore((s) => s.randomizeSeed);
  const setFrames = useStore((s) => s.setFrames);
  const selectedType = useStore((s) => s.selectedType);
  const selectedVariant = useStore((s) => s.selectedVariant);
  const seed = useStore((s) => s.seed);
  const setLoading = useStore((s) => s.setLoading);
  const setError = useStore((s) => s.setError);
  const setGalleryItems = useStore((s) => s.setGalleryItems);
  const toggleGrid = useStore((s) => s.toggleGrid);

  useEffect(() => {
    const handleClick = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setOpenIndex(null);
      }
    };
    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, []);

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
      ref={menuRef}
      className="h-[30px] bg-bg border-b border-border flex items-center select-none"
      style={{ fontSize: 13 }}
    >
      <span className="px-[10px] text-[13px] font-semibold text-text border-r border-border">
        SpriteGen
      </span>

      {menus.map((menu, i) => (
        <div key={menu.label} className="relative">
          <button
            className="h-[30px] px-[10px] bg-transparent border-none text-text cursor-pointer hover:bg-hover text-[13px]"
            style={openIndex === i ? { background: '#2d2d2d' } : undefined}
            onClick={() => setOpenIndex(openIndex === i ? null : i)}
            onMouseEnter={() => { if (openIndex !== null) setOpenIndex(i); }}
          >
            {menu.label}
          </button>

          {openIndex === i && (
            <div className="absolute top-[30px] left-0 bg-bg border border-border shadow-xl min-w-[220px] py-[4px] z-50">
              {menu.items.map((item, j) =>
                item.separator ? (
                  <div key={j} className="h-[1px] bg-border my-[4px] mx-[10px]" />
                ) : (
                  <button
                    key={j}
                    disabled={item.disabled}
                    className="w-full h-[28px] px-[20px] bg-transparent border-none text-left text-[13px] flex items-center justify-between cursor-pointer hover:bg-hover disabled:opacity-40 disabled:cursor-default disabled:hover:bg-transparent"
                    style={{ color: item.danger ? '#c74e4e' : item.disabled ? '#555555' : '#e0e0e0' }}
                    onClick={() => {
                      item.onClick?.();
                      setOpenIndex(null);
                    }}
                  >
                    <span>{item.label}</span>
                    {item.shortcut && (
                      <span className="text-text-disabled text-[12px] ml-[30px]">{item.shortcut}</span>
                    )}
                  </button>
                ),
              )}
            </div>
          )}
        </div>
      ))}

      <div className="flex-1" />
    </div>
  );
}
