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
      className={`absolute inset-0 flex flex-col items-center justify-center gap-4 rounded-xl m-10 transition-colors border-2 border-dashed ${dragging ? 'border-primary bg-primary/5' : 'border-border'
        }`}
    >
      {loading ? (
        <span className="text-muted-foreground text-base">Loading...</span>
      ) : (
        <>
          <span className="text-muted-foreground text-base">
            Drop a manifest.json file here
          </span>
          <span className="text-muted-foreground/50 text-xs">or</span>
          <label className="px-5 py-2 bg-input border border-border rounded-md cursor-pointer text-muted-foreground text-sm hover:bg-muted">
            Browse
            <input
              type="file"
              accept=".json"
              onChange={handleFileInput}
              className="hidden"
            />
          </label>
        </>
      )}
    </div>
  )
}
