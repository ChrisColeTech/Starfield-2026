import { useState } from 'react'
import { PanelRightClose, PanelRightOpen } from 'lucide-react'
import Viewport from '../components/Viewport'
import AutoRigPanel from '../components/AutoRigPanel'

export default function EditorPage() {
  const [panelOpen, setPanelOpen] = useState(true)

  return (
    <div className="flex flex-col w-full h-full">
      <div className="flex-1 flex relative overflow-hidden">
        {/* Center: Three.js viewport (always visible) */}
        <div className="flex-1 flex flex-col min-w-0">
          <div className="flex-1 bg-background relative">
            <Viewport />
          </div>
        </div>

        {/* Right: Auto-Rig panel with collapse toggle */}
        <div
          className="bg-card border-l border-border flex flex-col overflow-hidden shrink-0"
          style={{ width: panelOpen ? 280 : 28 }}
        >
          {/* Panel header with toggle */}
          <div className="h-7 flex items-center justify-between px-1.5 bg-background border-b border-border shrink-0">
            <button
              onClick={() => setPanelOpen(!panelOpen)}
              className="text-muted-foreground hover:text-foreground bg-transparent border-none cursor-pointer"
            >
              {panelOpen ? <PanelRightClose size={14} /> : <PanelRightOpen size={14} />}
            </button>
            {panelOpen && (
              <span className="text-[11px] font-bold uppercase tracking-wider text-muted-foreground mr-1">
                Auto Rig
              </span>
            )}
          </div>

          {panelOpen && (
            <div className="flex-1 overflow-y-auto">
              <AutoRigPanel />
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
