import { useEditorStore } from '../store/editorStore'

export default function TexturePanel() {
  const textures = useEditorStore(s => s.textures)
  const selectedIndex = useEditorStore(s => s.selectedTextureIndex)
  const selectTexture = useEditorStore(s => s.selectTexture)
  const sceneName = useEditorStore(s => s.sceneName)

  return (
    <div className="w-full bg-surface flex flex-col overflow-hidden">
      {/* Header */}
      <div className="p-[12px_14px] border-b border-border text-[12px] text-text-secondary">
        <div className="font-semibold text-text mb-[4px]">
          {sceneName || 'Textures'}
        </div>
        {textures.length > 0
          ? `${textures.length} texture${textures.length !== 1 ? 's' : ''} found`
          : 'No textures loaded'}
      </div>

      {/* Texture list */}
      {textures.length > 0 && (
        <div className="flex-1 overflow-auto p-[8px]">
          {textures.map((tex, i) => (
            <div
              key={tex.name + i}
              onClick={() => selectTexture(i)}
              className="flex items-center gap-[10px] p-[8px_10px] mb-[4px] rounded-[6px] cursor-pointer"
              style={{
                background: i === selectedIndex ? 'var(--color-active)' : 'transparent',
                border: i === selectedIndex ? '1px solid var(--color-accent)' : '1px solid transparent',
              }}
            >
              {/* Thumbnail */}
              <img
                src={tex.modifiedDataUrl}
                alt={tex.name}
                className="w-[40px] h-[40px] rounded border border-border"
                style={{ imageRendering: 'pixelated', background: '#000' }}
              />
              <div className="flex-1 min-w-0">
                <div className="text-[12px] text-text whitespace-nowrap overflow-hidden text-ellipsis">
                  {tex.name}
                </div>
                <div className="text-[10px] text-text-disabled">
                  {tex.originalImage.naturalWidth}x{tex.originalImage.naturalHeight}
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
