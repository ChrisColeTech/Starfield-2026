import { useEditorStore } from '../store/editorStore'

export default function TexturePanel() {
  const textures = useEditorStore(s => s.textures)
  const selectedIndex = useEditorStore(s => s.selectedTextureIndex)
  const selectTexture = useEditorStore(s => s.selectTexture)
  const sceneName = useEditorStore(s => s.sceneName)

  if (textures.length === 0) return null

  return (
    <div style={{
      width: '100%',
      background: '#16162a',
      display: 'flex',
      flexDirection: 'column',
      overflow: 'hidden',
    }}>
      {/* Header */}
      <div style={{
        padding: '12px 14px',
        borderBottom: '1px solid #2a2a4a',
        fontSize: 12,
        color: '#888',
      }}>
        <div style={{ fontWeight: 600, color: '#ccc', marginBottom: 4 }}>
          {sceneName}
        </div>
        {textures.length} texture{textures.length !== 1 ? 's' : ''} found
      </div>

      {/* Texture list */}
      <div style={{ flex: 1, overflow: 'auto', padding: 8 }}>
        {textures.map((tex, i) => (
          <div
            key={tex.name + i}
            onClick={() => selectTexture(i)}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 10,
              padding: '8px 10px',
              marginBottom: 4,
              borderRadius: 6,
              cursor: 'pointer',
              background: i === selectedIndex ? '#2a2a5a' : 'transparent',
              border: i === selectedIndex ? '1px solid #4a4a8a' : '1px solid transparent',
            }}
          >
            {/* Thumbnail */}
            <img
              src={tex.modifiedDataUrl}
              alt={tex.name}
              style={{
                width: 40,
                height: 40,
                borderRadius: 4,
                border: '1px solid #333',
                imageRendering: 'pixelated',
                background: '#000',
              }}
            />
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{
                fontSize: 12,
                color: '#ddd',
                whiteSpace: 'nowrap',
                overflow: 'hidden',
                textOverflow: 'ellipsis',
              }}>
                {tex.name}
              </div>
              <div style={{ fontSize: 10, color: '#666' }}>
                {tex.originalImage.naturalWidth}x{tex.originalImage.naturalHeight}
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
