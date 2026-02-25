import { useEditorStore } from '../store/editorStore'

export default function InfoBar() {
  const sceneName = useEditorStore(s => s.sceneName)
  const textures = useEditorStore(s => s.textures)
  const error = useEditorStore(s => s.error)
  const loadManifest = useEditorStore(s => s.loadManifest)

  const handleFileInput = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (file) loadManifest(file)
    e.target.value = ''
  }

  return (
    <div className="h-10 bg-card border-b border-border flex items-center px-4 gap-4 shrink-0">
      <span className="font-bold text-sm text-primary">
        BG Editor
      </span>

      {sceneName && (
        <span className="text-muted-foreground text-xs">
          {sceneName} &mdash; {textures.length} textures
        </span>
      )}

      {error && (
        <span className="text-destructive text-xs">
          {error}
        </span>
      )}

      <div className="flex-1" />

      <label className="px-3 py-1 bg-input border border-border rounded cursor-pointer text-muted-foreground text-xs hover:bg-muted">
        Load Manifest
        <input
          type="file"
          accept=".json"
          onChange={handleFileInput}
          className="hidden"
        />
      </label>
    </div>
  )
}
