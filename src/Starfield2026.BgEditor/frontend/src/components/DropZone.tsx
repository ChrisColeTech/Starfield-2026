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
      className="absolute inset-0 flex flex-col items-center justify-center gap-[16px] rounded-[12px] m-[40px] transition-colors"
      style={{
        border: dragging ? '2px dashed var(--color-accent)' : '2px dashed var(--color-border)',
        background: dragging ? 'rgba(86,156,214,0.05)' : 'transparent',
      }}
    >
      {loading ? (
        <span className="text-text-secondary text-[16px]">Loading...</span>
      ) : (
        <>
          <span className="text-text-secondary text-[16px]">
            Drop a manifest.json file here
          </span>
          <span className="text-text-disabled text-[12px]">or</span>
          <label className="px-[20px] py-[8px] bg-input border border-border rounded-[6px] cursor-pointer text-text-secondary text-[13px] hover:bg-hover">
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
