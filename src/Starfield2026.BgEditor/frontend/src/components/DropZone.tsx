import { useCallback, useState } from 'react'
import { useEditorStore } from '../store/editorStore'

export default function DropZone() {
  const [dragging, setDragging] = useState(false)
  const loadManifest = useEditorStore(s => s.loadManifest)
  const loading = useEditorStore(s => s.loading)

  const handleFile = useCallback((file: File) => {
    if (file.name.endsWith('.json')) {
      loadManifest(file)
    }
  }, [loadManifest])

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    setDragging(false)
    const file = e.dataTransfer.files[0]
    if (file) handleFile(file)
  }, [handleFile])

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    setDragging(true)
  }, [])

  const handleDragLeave = useCallback(() => {
    setDragging(false)
  }, [])

  const handleFileInput = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (file) handleFile(file)
    e.target.value = ''
  }, [handleFile])

  return (
    <div
      onDrop={handleDrop}
      onDragOver={handleDragOver}
      onDragLeave={handleDragLeave}
      style={{
        position: 'absolute',
        inset: 0,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        gap: 16,
        border: dragging ? '2px dashed #6c8cff' : '2px dashed #444',
        borderRadius: 12,
        margin: 40,
        transition: 'border-color 0.2s',
        background: dragging ? 'rgba(108, 140, 255, 0.05)' : 'transparent',
      }}
    >
      {loading ? (
        <span style={{ color: '#888', fontSize: 16 }}>Loading...</span>
      ) : (
        <>
          <span style={{ color: '#888', fontSize: 16 }}>
            Drop a manifest.json file here
          </span>
          <span style={{ color: '#555', fontSize: 12 }}>or</span>
          <label
            style={{
              padding: '8px 20px',
              background: '#2a2a4a',
              borderRadius: 6,
              cursor: 'pointer',
              color: '#aaa',
              fontSize: 13,
            }}
          >
            Browse
            <input
              type="file"
              accept=".json"
              onChange={handleFileInput}
              style={{ display: 'none' }}
            />
          </label>
        </>
      )}
    </div>
  )
}
