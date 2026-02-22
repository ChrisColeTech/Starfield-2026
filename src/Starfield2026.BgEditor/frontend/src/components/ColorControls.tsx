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
    <div className="mb-[12px]">
      <div className="flex justify-between items-center mb-[4px]">
        <span className="text-[11px] text-text-secondary">{label}</span>
        <span className="text-[11px] text-accent font-mono min-w-[36px] text-right">
          {value}
        </span>
      </div>
      <input
        type="range"
        min={min}
        max={max}
        value={value}
        onChange={e => onChange(Number(e.target.value))}
        className="w-full h-[4px] cursor-pointer"
        style={{ accentColor: 'var(--color-accent)' }}
      />
    </div>
  )
}

export default function ColorControls() {
  const textures = useEditorStore(s => s.textures)
  const selectedIndex = useEditorStore(s => s.selectedTextureIndex)
  const setAdjustment = useEditorStore(s => s.setAdjustment)
  const resetTexture = useEditorStore(s => s.resetTexture)
  const resetAll = useEditorStore(s => s.resetAll)
  const applyToAll = useEditorStore(s => s.applyToAll)

  const selected = textures[selectedIndex]
  if (!selected) return null

  const adj = selected.adjustment

  const handleChange = (field: keyof TextureAdjustment, value: number | string) => {
    setAdjustment(selectedIndex, { [field]: value })
  }

  return (
    <div className="flex flex-col overflow-hidden border-t border-border">
      {/* Header */}
      <div className="p-[10px_14px] border-b border-border text-[12px] font-semibold text-text shrink-0">
        Color Adjustments
      </div>

      {/* Scrollable content */}
      <div className="flex-1 overflow-auto p-[12px_14px]">
        {/* Thumbnails: original vs modified */}
        <div className="flex gap-[10px] mb-[14px] justify-center">
          <div className="text-center">
            <img
              src={selected.originalDataUrl}
              alt="Original"
              className="w-[64px] h-[64px] rounded border border-border"
              style={{ imageRendering: 'pixelated', background: '#000' }}
            />
            <div className="text-[9px] text-text-disabled mt-[3px]">Original</div>
          </div>
          <div className="text-center">
            <img
              src={selected.modifiedDataUrl}
              alt="Modified"
              className="w-[64px] h-[64px] rounded border border-border"
              style={{ imageRendering: 'pixelated', background: '#000' }}
            />
            <div className="text-[9px] text-text-disabled mt-[3px]">Modified</div>
          </div>
        </div>

        {/* Texture name */}
        <div className="text-[11px] text-text-secondary mb-[14px] text-center whitespace-nowrap overflow-hidden text-ellipsis">
          {selected.name}
        </div>

        {/* Sliders */}
        <SliderRow label="Hue Shift" value={adj.hueShift} min={-180} max={180} onChange={v => handleChange('hueShift', v)} />
        <SliderRow label="Saturation" value={adj.saturation} min={-100} max={100} onChange={v => handleChange('saturation', v)} />
        <SliderRow label="Brightness" value={adj.brightness} min={-100} max={100} onChange={v => handleChange('brightness', v)} />

        {/* Tint Color */}
        <div className="mb-[12px]">
          <div className="flex justify-between items-center mb-[4px]">
            <span className="text-[11px] text-text-secondary">Tint Color</span>
            <input
              type="color"
              value={adj.tintColor}
              onChange={e => handleChange('tintColor', e.target.value)}
              className="w-[28px] h-[20px] p-0 border border-border rounded-[3px] bg-transparent cursor-pointer"
            />
          </div>
        </div>

        <SliderRow label="Tint Strength" value={adj.tintStrength} min={0} max={100} onChange={v => handleChange('tintStrength', v)} />

        {/* Action buttons */}
        <div className="flex gap-[6px] mt-[8px] mb-[4px]">
          <button
            onClick={() => resetTexture(selectedIndex)}
            className="flex-1 py-[6px] bg-input border border-border rounded text-text text-[11px] cursor-pointer hover:bg-hover"
          >
            Reset
          </button>
          <button
            onClick={applyToAll}
            className="flex-1 py-[6px] bg-input border border-border rounded text-text text-[11px] cursor-pointer hover:bg-hover"
          >
            Apply to All
          </button>
        </div>
        <button
          onClick={resetAll}
          className="w-full py-[6px] mt-[2px] bg-input border border-border rounded text-text text-[11px] cursor-pointer hover:bg-hover"
        >
          Reset All
        </button>
      </div>
    </div>
  )
}
