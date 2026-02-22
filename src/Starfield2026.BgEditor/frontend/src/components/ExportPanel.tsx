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
      <div className="p-[10px_14px] border-b border-border text-[12px] font-semibold text-text shrink-0">
        Export
      </div>
      <div className="p-[10px_14px] flex flex-col gap-[6px]">
        <button
          onClick={handleSaveInPlace}
          disabled={exporting || !hasModifications}
          className="w-full py-[7px] rounded text-[11px] cursor-pointer border border-border disabled:opacity-50 disabled:cursor-default"
          style={{
            background: hasModifications ? 'var(--color-accent)' : 'var(--color-input)',
            color: hasModifications ? '#fff' : 'var(--color-text-disabled)',
            fontWeight: hasModifications ? 600 : 400,
          }}
        >
          {exporting ? 'Saving...' : 'Save Textures'}
        </button>
        <button
          onClick={handleExportTextures}
          disabled={exporting || !hasModifications}
          className="w-full py-[7px] bg-input border border-border rounded text-text text-[11px] cursor-pointer hover:bg-hover disabled:opacity-50 disabled:cursor-default"
        >
          Export Textures To...
        </button>
        {status && (
          <div className="text-[10px] text-center py-[4px]"
            style={{ color: status.includes('failed') ? 'var(--color-danger)' : 'var(--color-accent)' }}
          >
            {status}
          </div>
        )}
        {!hasModifications && (
          <div className="text-[10px] text-text-disabled text-center">
            No modifications to save
          </div>
        )}
      </div>
    </div>
  )
}
