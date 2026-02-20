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

  const buttonStyle: React.CSSProperties = {
    width: '100%',
    padding: '7px 0',
    background: '#2a2a4a',
    border: '1px solid #3a3a6a',
    borderRadius: 4,
    color: '#ccc',
    fontSize: 11,
    cursor: 'pointer',
  }

  return (
    <div style={{
      display: 'flex',
      flexDirection: 'column',
      borderTop: '1px solid #2a2a4a',
    }}>
      <div style={{
        padding: '10px 14px',
        borderBottom: '1px solid #2a2a4a',
        fontSize: 12,
        fontWeight: 600,
        color: '#ccc',
        flexShrink: 0,
      }}>
        Export
      </div>
      <div style={{ padding: '10px 14px', display: 'flex', flexDirection: 'column', gap: 6 }}>
        <button
          onClick={handleSaveInPlace}
          disabled={exporting || !hasModifications}
          style={{
            ...buttonStyle,
            background: hasModifications ? '#8c8cff' : '#2a2a4a',
            color: hasModifications ? '#fff' : '#666',
            fontWeight: hasModifications ? 600 : 400,
          }}
        >
          {exporting ? 'Saving...' : 'Save Textures'}
        </button>
        <button
          onClick={handleExportTextures}
          disabled={exporting || !hasModifications}
          style={{
            ...buttonStyle,
            opacity: hasModifications ? 1 : 0.5,
          }}
        >
          Export Textures To...
        </button>
        {status && (
          <div style={{
            fontSize: 10,
            color: status.includes('failed') ? '#ff6666' : '#8c8cff',
            textAlign: 'center',
            padding: '4px 0',
          }}>
            {status}
          </div>
        )}
        {!hasModifications && (
          <div style={{ fontSize: 10, color: '#555', textAlign: 'center' }}>
            No modifications to save
          </div>
        )}
      </div>
    </div>
  )
}
