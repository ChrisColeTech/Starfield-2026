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
    <div className="h-[40px] bg-surface border-b border-border flex items-center px-[16px] gap-[16px] shrink-0">
      <span className="font-bold text-[14px] text-accent">
        BG Editor
      </span>

      {sceneName && (
        <span className="text-text-secondary text-[12px]">
          {sceneName} &mdash; {textures.length} textures
        </span>
      )}

      {error && (
        <span className="text-danger text-[12px]">
          {error}
        </span>
      )}

      <div className="flex-1" />

      <label className="px-[12px] py-[4px] bg-input border border-border rounded cursor-pointer text-text-secondary text-[12px] hover:bg-hover">
        Load Manifest
        <input
          type="file"
          accept=".json"
          onChange={handleFileInput}
          style={{ display: 'none' }}
        />
      </label>
    </div>
  )
}
