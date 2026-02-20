import { PanelLeftClose, PanelLeftOpen } from 'lucide-react';
import { useStore } from '../../store';
import { GeneratorControls } from '../controls/GeneratorControls';
import { ImportDropZone } from '../import/ImportDropZone';

export function Sidebar() {
  const collapsed = useStore((s) => s.sidebarCollapsed);
  const toggle = useStore((s) => s.toggleSidebar);

  return (
    <aside
      className="flex flex-col h-full bg-bg border-r border-border overflow-hidden"
      style={{ width: collapsed ? 36 : 260 }}
    >
      {/* Collapse toggle */}
      <div className="flex items-center justify-between h-[22px] px-[6px] bg-surface border-b border-border">
        {!collapsed && (
          <span className="text-[11px] font-bold uppercase tracking-[0.5px] text-text-secondary">
            Explorer
          </span>
        )}
        <button onClick={toggle} className="text-text-secondary hover:text-text cursor-pointer">
          {collapsed ? <PanelLeftOpen size={14} /> : <PanelLeftClose size={14} />}
        </button>
      </div>

      {/* Content */}
      {!collapsed && (
        <div className="flex-1 min-h-0 overflow-y-auto">
          <GeneratorControls />
          <ImportDropZone />
        </div>
      )}
    </aside>
  );
}
