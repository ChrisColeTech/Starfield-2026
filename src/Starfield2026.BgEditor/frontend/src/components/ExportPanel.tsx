import { useState } from 'react'
import { useEditorStore } from '../store/editorStore'

const API_BASE = 'http://localhost:3001'

export default function ExportPanel() {
  const sceneName = useEditorStore(s => s.sceneName)
  const manifest = useEditorStore(s => s.manifest)
  const textures = useEditorStore(s => s.textures)
  const [exporting, setExporting] = useState(false)
  const [status, setStatus] = useState<string | null>(null)

  if (!sceneName) return null

  const hasModifications = textures.some(t => t.modifiedDataUrl !== t.originalDataUrl)

  const handleExportTextures = async () => {
    setExporting(true)
    setStatus(null)
    try {
      const outputDir = await window.electronAPI.browseFolder()
      if (!outputDir) { setExporting(false); return }

      const modified = textures.filter(t => t.modifiedDataUrl !== t.originalDataUrl)
      const payload = modified.map(t => ({
        name: t.name,
        dataUrl: t.modifiedDataUrl,
      }))

      const res = await fetch(`${API_BASE}/api/textures/export`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ outputDir, textures: payload }),
      })
      const data = await res.json()
      setStatus(`Exported ${data.count} textures`)
    } catch (err) {
      setStatus(err instanceof Error ? err.message : 'Export failed')
    } finally {
      setExporting(false)
    }
  }

  const handleSaveInPlace = async () => {
    setExporting(true)
    setStatus(null)
    try {
      const modified = textures.filter(t => t.modifiedDataUrl !== t.originalDataUrl)
      const payload = modified.map(t => ({
        name: t.name,
        dataUrl: t.modifiedDataUrl,
      }))

      const res = await fetch(`${API_BASE}/api/textures/save`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ dir: manifest?.dir, textures: payload }),
      })
      const data = await res.json()
      setStatus(`Saved ${data.count} textures`)
    } catch (err) {
      setStatus(err instanceof Error ? err.message : 'Save failed')
    } finally {
      setExporting(false)
    }
  }

  return (
    <div className="flex flex-col border-t border-border">
      <div className="px-3.5 py-2.5 border-b border-border text-xs font-semibold text-foreground shrink-0">
        Export
      </div>
      <div className="px-3.5 py-2.5 flex flex-col gap-1.5">
        <button
          onClick={handleSaveInPlace}
          disabled={exporting || !hasModifications}
          className={`w-full py-2 rounded text-[11px] cursor-pointer border transition-colors disabled:opacity-50 disabled:cursor-default ${hasModifications
              ? 'bg-primary border-primary text-primary-foreground font-semibold'
              : 'bg-input border-border text-muted-foreground'
            }`}
        >
          {exporting ? 'Saving...' : 'Save Textures'}
        </button>
        <button
          onClick={handleExportTextures}
          disabled={exporting || !hasModifications}
          className="w-full py-2 bg-input border border-border rounded text-foreground text-[11px] cursor-pointer hover:bg-muted disabled:opacity-50 disabled:cursor-default"
        >
          Export Textures To...
        </button>
        {status && (
          <div className={`text-[10px] text-center py-1 ${status.includes('failed') ? 'text-destructive' : 'text-primary'
            }`}>
            {status}
          </div>
        )}
        {!hasModifications && (
          <div className="text-[10px] text-muted-foreground/50 text-center">
            No modifications to save
          </div>
        )}
      </div>
    </div>
  )
}
