import { useEditorStore } from '../store/editorStore'

export default function TexturePanel() {
  const textures = useEditorStore(s => s.textures)
  const selectedIndex = useEditorStore(s => s.selectedTextureIndex)
  const selectTexture = useEditorStore(s => s.selectTexture)
  const sceneName = useEditorStore(s => s.sceneName)

  return (
    <div className="w-full bg-card flex flex-col overflow-hidden">
      {/* Header */}
      <div className="px-3.5 py-3 border-b border-border text-xs text-muted-foreground">
        <div className="font-semibold text-foreground mb-1">
          {sceneName || 'Textures'}
        </div>
        {textures.length > 0
          ? `${textures.length} texture${textures.length !== 1 ? 's' : ''} found`
          : 'No textures loaded'}
      </div>

      {/* Texture list */}
      {textures.length > 0 && (
        <div className="flex-1 overflow-auto p-2">
          {textures.map((tex, i) => (
            <div
              key={tex.name + i}
              onClick={() => selectTexture(i)}
              className={`flex items-center gap-2.5 px-2.5 py-2 mb-1 rounded-md cursor-pointer border transition-colors ${i === selectedIndex
                  ? 'bg-primary/10 border-primary'
                  : 'border-transparent hover:bg-muted'
                }`}
            >
              {/* Thumbnail */}
              <img
                src={tex.modifiedDataUrl}
                alt={tex.name}
                className="w-10 h-10 rounded border border-border bg-black"
                style={{ imageRendering: 'pixelated' }}
              />
              <div className="flex-1 min-w-0">
                <div className="text-xs text-foreground whitespace-nowrap overflow-hidden text-ellipsis">
                  {tex.name}
                </div>
                <div className="text-[10px] text-muted-foreground/50">
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
