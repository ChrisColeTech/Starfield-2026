import { useState } from 'react'
import { PanelRightClose, PanelRightOpen } from 'lucide-react'
import { useEditorStore } from '../store/editorStore'
import InfoBar from '../components/InfoBar'
import Viewport from '../components/Viewport'
import DropZone from '../components/DropZone'
import TexturePanel from '../components/TexturePanel'
import ColorControls from '../components/ColorControls'
import ExportPanel from '../components/ExportPanel'

export default function EditorPage() {
  const scene = useEditorStore(s => s.scene)
  const [panelOpen, setPanelOpen] = useState(true)

  return (
    <div className="flex flex-col w-full h-full">
      <InfoBar />
      <div className="flex-1 flex relative overflow-hidden">
        {scene ? (
          <>
            <Viewport />
            <div
              className="bg-surface border-l border-border flex flex-col overflow-hidden shrink-0"
              style={{ width: panelOpen ? 280 : 28 }}
            >
              {/* Panel header with toggle */}
              <div className="h-[28px] flex items-center justify-between px-[6px] bg-bg border-b border-border shrink-0">
                <button
                  onClick={() => setPanelOpen(!panelOpen)}
                  className="text-text-secondary hover:text-text bg-transparent border-none cursor-pointer"
                >
                  {panelOpen ? <PanelRightClose size={14} /> : <PanelRightOpen size={14} />}
                </button>
                {panelOpen && (
                  <span className="text-[11px] font-bold uppercase tracking-[0.5px] text-text-secondary mr-[4px]">
                    Properties
                  </span>
                )}
              </div>

              {panelOpen && (
                <>
                  <div className="flex-none max-h-[30%] overflow-hidden flex">
                    <TexturePanel />
                  </div>
                  <div className="flex-1 overflow-hidden flex flex-col">
                    <ColorControls />
                  </div>
                  <div className="flex-none">
                    <ExportPanel />
                  </div>
                </>
              )}
            </div>
          </>
        ) : (
          <DropZone />
        )}
      </div>
    </div>
  )
}
