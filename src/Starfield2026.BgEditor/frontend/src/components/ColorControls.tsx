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
    <div style={{ marginBottom: 12 }}>
      <div style={{
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: 4,
      }}>
        <span style={{ fontSize: 11, color: '#aaa' }}>{label}</span>
        <span style={{
          fontSize: 11,
          color: '#8c8cff',
          fontFamily: 'monospace',
          minWidth: 36,
          textAlign: 'right',
        }}>
          {value}
        </span>
      </div>
      <input
        type="range"
        min={min}
        max={max}
        value={value}
        onChange={e => onChange(Number(e.target.value))}
        style={{
          width: '100%',
          height: 4,
          accentColor: '#8c8cff',
          cursor: 'pointer',
        }}
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

  const buttonStyle: React.CSSProperties = {
    flex: 1,
    padding: '6px 0',
    background: '#2a2a4a',
    border: '1px solid #3a3a6a',
    borderRadius: 4,
    color: '#ccc',
    fontSize: 11,
    cursor: 'pointer',
  }

  return (
    <div style={{
      display: 'flex',
      flexDirection: 'column',
      overflow: 'hidden',
      borderTop: '1px solid #2a2a4a',
    }}>
      {/* Header */}
      <div style={{
        padding: '10px 14px',
        borderBottom: '1px solid #2a2a4a',
        fontSize: 12,
        fontWeight: 600,
        color: '#ccc',
        flexShrink: 0,
      }}>
        Color Adjustments
      </div>

      {/* Scrollable content */}
      <div style={{
        flex: 1,
        overflow: 'auto',
        padding: '12px 14px',
      }}>
        {/* Thumbnails: original vs modified */}
        <div style={{
          display: 'flex',
          gap: 10,
          marginBottom: 14,
          justifyContent: 'center',
        }}>
          <div style={{ textAlign: 'center' }}>
            <img
              src={selected.originalDataUrl}
              alt="Original"
              style={{
                width: 64,
                height: 64,
                borderRadius: 4,
                border: '1px solid #333',
                imageRendering: 'pixelated',
                background: '#000',
              }}
            />
            <div style={{ fontSize: 9, color: '#666', marginTop: 3 }}>Original</div>
          </div>
          <div style={{ textAlign: 'center' }}>
            <img
              src={selected.modifiedDataUrl}
              alt="Modified"
              style={{
                width: 64,
                height: 64,
                borderRadius: 4,
                border: '1px solid #333',
                imageRendering: 'pixelated',
                background: '#000',
              }}
            />
            <div style={{ fontSize: 9, color: '#666', marginTop: 3 }}>Modified</div>
          </div>
        </div>

        {/* Texture name */}
        <div style={{
          fontSize: 11,
          color: '#888',
          marginBottom: 14,
          textAlign: 'center',
          whiteSpace: 'nowrap',
          overflow: 'hidden',
          textOverflow: 'ellipsis',
        }}>
          {selected.name}
        </div>

        {/* Sliders */}
        <SliderRow
          label="Hue Shift"
          value={adj.hueShift}
          min={-180}
          max={180}
          onChange={v => handleChange('hueShift', v)}
        />
        <SliderRow
          label="Saturation"
          value={adj.saturation}
          min={-100}
          max={100}
          onChange={v => handleChange('saturation', v)}
        />
        <SliderRow
          label="Brightness"
          value={adj.brightness}
          min={-100}
          max={100}
          onChange={v => handleChange('brightness', v)}
        />

        {/* Tint Color */}
        <div style={{ marginBottom: 12 }}>
          <div style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            marginBottom: 4,
          }}>
            <span style={{ fontSize: 11, color: '#aaa' }}>Tint Color</span>
            <input
              type="color"
              value={adj.tintColor}
              onChange={e => handleChange('tintColor', e.target.value)}
              style={{
                width: 28,
                height: 20,
                padding: 0,
                border: '1px solid #3a3a6a',
                borderRadius: 3,
                background: 'transparent',
                cursor: 'pointer',
              }}
            />
          </div>
        </div>

        <SliderRow
          label="Tint Strength"
          value={adj.tintStrength}
          min={0}
          max={100}
          onChange={v => handleChange('tintStrength', v)}
        />

        {/* Action buttons */}
        <div style={{
          display: 'flex',
          gap: 6,
          marginTop: 8,
          marginBottom: 4,
        }}>
          <button
            onClick={() => resetTexture(selectedIndex)}
            style={buttonStyle}
          >
            Reset
          </button>
          <button
            onClick={applyToAll}
            style={buttonStyle}
          >
            Apply to All
          </button>
        </div>
        <button
          onClick={resetAll}
          style={{
            ...buttonStyle,
            width: '100%',
            marginTop: 2,
          }}
        >
          Reset All
        </button>
      </div>
    </div>
  )
}
