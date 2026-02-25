import { useEditorStore } from '../store/editorStore'
import type { TextureAdjustment } from '../types/editor'

interface SliderRowProps {
  label: string
  value: number
  min: number
  max: number
  onChange: (value: number) => void
}

function SliderRow({ label, value, min, max, onChange }: SliderRowProps) {
  return (
    <div className="mb-3">
      <div className="flex justify-between items-center mb-1">
        <span className="text-[11px] text-muted-foreground">{label}</span>
        <span className="text-[11px] text-primary font-mono min-w-9 text-right">
          {value}
        </span>
      </div>
      <input
        type="range"
        min={min}
        max={max}
        value={value}
        onChange={e => onChange(Number(e.target.value))}
        className="w-full h-1 cursor-pointer accent-primary"
      />
    </div>
  )
}

export default function ColorControls() {
  const textures = useEditorStore(s => s.textures)
  const selectedIndex = useEditorStore(s => s.selectedTextureIndex)
  const setAdjustment = useEditorStore(s => s.setAdjustment)

  const selected = textures[selectedIndex]
  const adj = selected?.adjustment

  const handleChange = (field: keyof TextureAdjustment, value: number | string) => {
    setAdjustment(selectedIndex, { [field]: value })
  }

  return (
    <div className="flex flex-col overflow-hidden border-t border-border">
      {/* Header — always visible */}
      <div className="px-3.5 py-2.5 border-b border-border text-xs font-semibold text-foreground shrink-0">
        Color Adjustments
      </div>

      {selected ? (
        <div className="flex-1 overflow-auto px-3.5 py-3">
          {/* Thumbnails: original vs modified */}
          <div className="flex gap-2.5 mb-3.5 justify-center">
            <div className="text-center">
              <img
                src={selected.originalDataUrl}
                alt="Original"
                className="w-16 h-16 rounded border border-border bg-black"
                style={{ imageRendering: 'pixelated' }}
              />
              <div className="text-[9px] text-muted-foreground/50 mt-0.5">Original</div>
            </div>
            <div className="text-center">
              <img
                src={selected.modifiedDataUrl}
                alt="Modified"
                className="w-16 h-16 rounded border border-border bg-black"
                style={{ imageRendering: 'pixelated' }}
              />
              <div className="text-[9px] text-muted-foreground/50 mt-0.5">Modified</div>
            </div>
          </div>

          {/* Texture name */}
          <div className="text-[11px] text-muted-foreground mb-3.5 text-center whitespace-nowrap overflow-hidden text-ellipsis">
            {selected.name}
          </div>

          {/* Sliders */}
          <SliderRow label="Hue Shift" value={adj.hueShift} min={-180} max={180} onChange={v => handleChange('hueShift', v)} />
          <SliderRow label="Saturation" value={adj.saturation} min={-100} max={100} onChange={v => handleChange('saturation', v)} />
          <SliderRow label="Brightness" value={adj.brightness} min={-100} max={100} onChange={v => handleChange('brightness', v)} />

          {/* Tint Color */}
          <div className="mb-3">
            <div className="flex justify-between items-center mb-1">
              <span className="text-[11px] text-muted-foreground">Tint Color</span>
              <input
                type="color"
                value={adj.tintColor}
                onChange={e => handleChange('tintColor', e.target.value)}
                className="w-7 h-5 p-0 border border-border rounded bg-transparent cursor-pointer"
              />
            </div>
          </div>

          <SliderRow label="Tint Strength" value={adj.tintStrength} min={0} max={100} onChange={v => handleChange('tintStrength', v)} />
        </div>
      ) : (
        <div className="p-3.5 text-[11px] text-muted-foreground/50">
          Select a texture to adjust
        </div>
      )}
    </div>
  )
}
