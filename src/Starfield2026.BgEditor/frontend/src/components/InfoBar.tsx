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
    <div style={{
      height: 40,
      background: '#12122a',
      borderBottom: '1px solid #2a2a4a',
      display: 'flex',
      alignItems: 'center',
      padding: '0 16px',
      gap: 16,
      flexShrink: 0,
    }}>
      <span style={{ fontWeight: 700, fontSize: 14, color: '#8c8cff' }}>
        BG Editor
      </span>

      {sceneName && (
        <span style={{ color: '#888', fontSize: 12 }}>
          {sceneName} &mdash; {textures.length} textures
        </span>
      )}

      {error && (
        <span style={{ color: '#ff6666', fontSize: 12 }}>
          {error}
        </span>
      )}

      <div style={{ flex: 1 }} />

      <label style={{
        padding: '4px 12px',
        background: '#2a2a4a',
        borderRadius: 4,
        cursor: 'pointer',
        color: '#aaa',
        fontSize: 12,
      }}>
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
