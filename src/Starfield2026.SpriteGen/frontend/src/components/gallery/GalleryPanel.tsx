import { useState, useEffect } from 'react';
import { PanelRightClose, PanelRightOpen, Search, Package } from 'lucide-react';
import { useStore } from '../../store';
import { SectionHeader } from '../common/SectionHeader';
import { api } from '../../services/apiClient';

export function GalleryPanel() {
  const collapsed = useStore((s) => s.galleryCollapsed);
  const toggle = useStore((s) => s.toggleGallery);
  const items = useStore((s) => s.galleryItems);
  const filter = useStore((s) => s.galleryFilter);
  const setFilter = useStore((s) => s.setGalleryFilter);
  const setItems = useStore((s) => s.setGalleryItems);
  const setFrames = useStore((s) => s.setFrames);
  const setError = useStore((s) => s.setError);
  const [expanded, setExpanded] = useState(true);

  useEffect(() => {
    api.getGallery(filter || undefined)
      .then((res) => setItems(res.items))
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load gallery'));
  }, [filter, setItems, setError]);

  const handleItemClick = async (filename: string) => {
    try {
      const res = await fetch(`/api/sprites/${encodeURIComponent(filename)}`);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const svg = await res.text();
      setFrames([svg]);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load sprite');
    }
  };

  return (
    <aside
      className="flex flex-col h-full bg-bg border-l border-border overflow-hidden"
      style={{ width: collapsed ? 36 : 240 }}
    >
      {/* Collapse toggle */}
      <div className="flex items-center justify-between h-[22px] px-[6px] bg-surface border-b border-border">
        {!collapsed && (
          <span className="text-[11px] font-bold uppercase tracking-[0.5px] text-text-secondary">
            Gallery
          </span>
        )}
        <button onClick={toggle} className="text-text-secondary hover:text-text cursor-pointer ml-auto">
          {collapsed ? <PanelRightOpen size={14} /> : <PanelRightClose size={14} />}
        </button>
      </div>

      {!collapsed && (
        <>
          {/* Search */}
          <div className="p-[6px] border-b border-border">
            <div className="flex items-center gap-[4px] px-[6px] py-[3px] rounded-[3px]" style={{ background: '#3c3c3c', border: '1px solid #2d2d2d' }}>
              <Search size={12} className="text-text-disabled" />
              <input
                type="text"
                value={filter}
                onChange={(e) => setFilter(e.target.value)}
                placeholder="Filter sprites..."
                className="flex-1 bg-transparent text-[12px] text-text placeholder-text-disabled outline-none"
              />
            </div>
          </div>

          {/* Sprites list */}
          <SectionHeader
            label="Sprites"
            expanded={expanded}
            onToggle={() => setExpanded(!expanded)}
            badge={items.length}
          />
          {expanded && (
            <div className="flex-1 min-h-0 overflow-y-auto">
              {items.length > 0 ? (
                <div className="grid grid-cols-3 gap-[2px] p-[6px]">
                  {items.map((item) => (
                    <div
                      key={item.filename}
                      className="aspect-square flex items-center justify-center rounded-[2px] cursor-pointer hover:bg-hover"
                      style={{ background: '#0f0f23', border: '1px solid #2d2d2d' }}
                      title={item.filename}
                      onClick={() => handleItemClick(item.filename)}
                    >
                      {item.thumbnail ? (
                        <div className="sprite-render" dangerouslySetInnerHTML={{ __html: item.thumbnail }} />
                      ) : (
                        <span className="text-[8px] text-text-disabled truncate px-[2px]">
                          {item.filename.replace('.svg', '')}
                        </span>
                      )}
                    </div>
                  ))}
                </div>
              ) : (
                <div className="flex flex-col items-center justify-center h-[120px] text-text-disabled">
                  <Package size={24} strokeWidth={1} />
                  <span className="text-[10px] mt-[6px]">No sprites saved</span>
                </div>
              )}
            </div>
          )}
        </>
      )}
    </aside>
  );
}
