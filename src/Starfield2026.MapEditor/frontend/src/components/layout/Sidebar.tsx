import { useState, useCallback, useMemo } from 'react'
import {
  PanelLeftClose,
  PanelLeftOpen,
  RotateCcw,
  RotateCw,
  Shuffle,
  FileDown,
  ChevronRight,
  ChevronDown,
} from 'lucide-react'
import { useEditorStore } from '../../store/editorStore'
import { paletteCategories, tilesForCategory, buildingWidth, buildingHeight } from '../../services/registryService'
import { MAP_TEMPLATES, TEMPLATE_CATEGORIES, loadTemplate, type MapTemplate } from '../../data/templates'

const GRID_POSITIONS = [
  { id: 'center', label: 'Center',  x:  0, y:  0 },
  { id: 'up',     label: 'Up',      x:  0, y: -1 },
  { id: 'down',   label: 'Down',    x:  0, y:  1 },
  { id: 'left',   label: 'Left',    x: -1, y:  0 },
  { id: 'right',  label: 'Right',   x:  1, y:  0 },
]

type SectionId = 'palette' | 'controls'

function SectionHeader({
  title,
  sectionId,
  expanded,
  onToggle,
  badge,
}: {
  title: string
  sectionId: SectionId
  expanded: boolean
  onToggle: (id: SectionId) => void
  badge?: number
}) {
  return (
    <button
      onClick={() => onToggle(sectionId)}
      style={{
        flexShrink: 0,
        display: 'flex',
        alignItems: 'center',
        width: '100%',
        height: 22,
        padding: '0 8px',
        background: '#252526',
        border: 'none',
        borderBottom: '1px solid #2d2d2d',
        color: '#e0e0e0',
        fontSize: 11,
        fontWeight: 700,
        textTransform: 'uppercase',
        letterSpacing: '0.5px',
        cursor: 'pointer',
        gap: 4,
      }}
    >
      {expanded ? <ChevronDown size={12} /> : <ChevronRight size={12} />}
      <span style={{ flex: 1, textAlign: 'left' }}>{title}</span>
      {badge !== undefined && (
        <span style={{ fontSize: 10, color: '#808080', fontWeight: 400 }}>
          {badge}
        </span>
      )}
    </button>
  )
}


function PaletteSection() {
  const registry = useEditorStore(s => s.registry)
  const selectedTile = useEditorStore(s => s.selectedTile)
  const selectedBuilding = useEditorStore(s => s.selectedBuilding)
  const selectTile = useEditorStore(s => s.selectTile)
  const selectBuilding = useEditorStore(s => s.selectBuilding)
  const rotateBuilding = useEditorStore(s => s.rotateBuilding)
  const categories = useMemo(() => paletteCategories(registry), [registry])

  const btnStyle: React.CSSProperties = {
    flex: 1,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 4,
    padding: '3px 6px',
    background: '#3c3c3c',
    border: '1px solid #2d2d2d',
    borderRadius: 3,
    color: '#e0e0e0',
    fontSize: 11,
    cursor: 'pointer',
  }

  return (
    <div className="p-2" style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
      {/* Tiles */}
      {categories.map(cat => {
        const tiles = tilesForCategory(registry, cat.id)
        if (tiles.length === 0) return null
        return (
          <div key={cat.id}>
            <div style={{ fontSize: 10, color: '#808080', marginBottom: 3, textTransform: 'uppercase' }}>
              {cat.label}
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 4 }}>
              {tiles.map(tile => (
                <button
                  key={tile.id}
                  onClick={() => selectTile(tile.id)}
                  style={{
                    padding: 6,
                    background: tile.color,
                    border: selectedTile === tile.id && selectedBuilding === null ? '2px solid #fff' : '2px solid transparent',
                    borderRadius: 3,
                    cursor: 'pointer',
                    color: '#e0e0e0',
                    fontSize: 11,
                    textAlign: 'center',
                  }}
                >
                  {tile.name}
                </button>
              ))}
            </div>
          </div>
        )
      })}

      {/* Buildings */}
      {registry.buildings.length > 0 && (
        <>
          <div style={{ fontSize: 10, color: '#808080', marginTop: 4, textTransform: 'uppercase' }}>
            Buildings
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 4 }}>
            {registry.buildings.map((b, idx) => (
              <button
                key={b.id}
                onClick={() => selectBuilding(idx)}
                style={{
                  padding: 6,
                  background: selectedBuilding === idx ? '#094771' : '#2a4a3a',
                  border: selectedBuilding === idx ? '2px solid #fff' : '2px solid transparent',
                  borderRadius: 3,
                  color: '#e0e0e0',
                  fontSize: 10,
                  cursor: 'pointer',
                  textAlign: 'center',
                  whiteSpace: 'pre-line',
                }}
              >
                {b.name}{'\n'}{buildingWidth(b)}x{buildingHeight(b)}
              </button>
            ))}
          </div>
          {selectedBuilding !== null && (
            <div style={{ display: 'flex', gap: 4 }}>
              <button onClick={() => rotateBuilding(-1)} style={btnStyle}>
                <RotateCcw size={12} /> Rotate Left
              </button>
              <button onClick={() => rotateBuilding(1)} style={btnStyle}>
                <RotateCw size={12} /> Rotate Right
              </button>
            </div>
          )}
        </>
      )}
    </div>
  )
}

function ControlsSection() {
  const mapName = useEditorStore(s => s.mapName)
  const setMapName = useEditorStore(s => s.setMapName)
  const worldId = useEditorStore(s => s.worldId)
  const setWorldId = useEditorStore(s => s.setWorldId)
  const worldX = useEditorStore(s => s.worldX)
  const setWorldX = useEditorStore(s => s.setWorldX)
  const worldY = useEditorStore(s => s.worldY)
  const setWorldY = useEditorStore(s => s.setWorldY)
  const registry = useEditorStore(s => s.registry)
  const baseTile = useEditorStore(s => s.baseTile)
  const setBaseTile = useEditorStore(s => s.setBaseTile)
  const mapWidth = useEditorStore(s => s.mapWidth)
  const mapHeight = useEditorStore(s => s.mapHeight)
  const cellSize = useEditorStore(s => s.cellSize)
  const resize = useEditorStore(s => s.resize)
  const clear = useEditorStore(s => s.clear)
  const rotateMap = useEditorStore(s => s.rotateMap)
  const setCellSize = useEditorStore(s => s.setCellSize)
  const importJson = useEditorStore(s => s.importJson)

  const [w, setW] = useState(mapWidth)
  const [h, setH] = useState(mapHeight)
  const [selectedTemplate, setSelectedTemplate] = useState<MapTemplate | null>(null)
  const [loadingTemplate, setLoadingTemplate] = useState(false)

  const handleLoadTemplate = useCallback(async (template: MapTemplate) => {
    setLoadingTemplate(true)
    try {
      const json = await loadTemplate(template)
      importJson(json)
    } catch {
      alert(`Failed to load template: ${template.name}`)
    } finally {
      setLoadingTemplate(false)
    }
  }, [importJson])

  const handleRandom = useCallback(() => {
    const t = MAP_TEMPLATES[Math.floor(Math.random() * MAP_TEMPLATES.length)]
    setSelectedTemplate(t)
    handleLoadTemplate(t)
  }, [handleLoadTemplate])

  const inputStyle: React.CSSProperties = {
    width: '100%',
    padding: '3px 6px',
    background: '#3c3c3c',
    border: '1px solid #2d2d2d',
    borderRadius: 3,
    color: '#e0e0e0',
    fontSize: 12,
    outline: 'none',
  }

  const btnStyle: React.CSSProperties = {
    flex: 1,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 4,
    padding: '4px 6px',
    background: '#3c3c3c',
    border: '1px solid #2d2d2d',
    borderRadius: 3,
    color: '#e0e0e0',
    fontSize: 11,
    cursor: 'pointer',
  }

  return (
    <div className="p-2" style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
      {/* Map name */}
      <label style={{ fontSize: 10, color: '#808080' }}>
        Map Name
        <input
          type="text"
          value={mapName}
          onChange={e => setMapName(e.target.value)}
          style={{ ...inputStyle, display: 'block', marginTop: 2 }}
        />
      </label>

      {/* World */}
      <label style={{ fontSize: 10, color: '#808080' }}>
        World ID
        <input
          type="text"
          value={worldId}
          onChange={e => setWorldId(e.target.value)}
          style={{ ...inputStyle, display: 'block', marginTop: 2 }}
        />
      </label>
      <label style={{ fontSize: 10, color: '#808080' }}>
        Grid Position
        <select
          value={GRID_POSITIONS.find(p => p.x === worldX && p.y === worldY)?.id ?? 'custom'}
          onChange={e => {
            const pos = GRID_POSITIONS.find(p => p.id === e.target.value)
            if (pos) { setWorldX(pos.x); setWorldY(pos.y) }
          }}
          style={{ ...inputStyle, display: 'block', marginTop: 2 }}
        >
          {GRID_POSITIONS.map(p => (
            <option key={p.id} value={p.id}>{p.label}</option>
          ))}
          {!GRID_POSITIONS.some(p => p.x === worldX && p.y === worldY) && (
            <option value="custom">Custom ({worldX}, {worldY})</option>
          )}
        </select>
      </label>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 4 }}>
        <label style={{ fontSize: 10, color: '#808080' }}>
          X
          <input type="number" value={worldX} onChange={e => setWorldX(+e.target.value)} style={inputStyle} />
        </label>
        <label style={{ fontSize: 10, color: '#808080' }}>
          Y
          <input type="number" value={worldY} onChange={e => setWorldY(+e.target.value)} style={inputStyle} />
        </label>
      </div>

      <div style={{ fontSize: 10, color: '#808080' }}>
        Registry: <span style={{ color: '#e0e0e0' }}>{registry.name} v{registry.version}</span>
      </div>

      {/* Grid size */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 4 }}>
        <label style={{ fontSize: 10, color: '#808080' }}>
          Width
          <input type="number" min={1} max={200} value={w} onChange={e => setW(+e.target.value)} style={inputStyle} />
        </label>
        <label style={{ fontSize: 10, color: '#808080' }}>
          Height
          <input type="number" min={1} max={200} value={h} onChange={e => setH(+e.target.value)} style={inputStyle} />
        </label>
        <label style={{ fontSize: 10, color: '#808080' }}>
          Cell
          <input type="number" min={8} max={64} value={cellSize} onChange={e => setCellSize(+e.target.value)} style={inputStyle} />
        </label>
      </div>
      {/* Base tile */}
      <label style={{ fontSize: 10, color: '#808080' }}>
        Base Tile
        <select
          value={baseTile}
          onChange={e => setBaseTile(+e.target.value)}
          style={{ ...inputStyle, display: 'block', marginTop: 2 }}
        >
          {registry.tiles
            .filter(t => t.category === 'terrain')
            .map(t => (
              <option key={t.id} value={t.id}>{t.name}</option>
            ))}
        </select>
      </label>

      <div style={{ display: 'flex', gap: 4 }}>
        <button onClick={() => resize(w, h)} style={btnStyle}>Resize</button>
        <button onClick={clear} style={{ ...btnStyle, color: '#f48771' }}>Clear All</button>
      </div>
      <div style={{ display: 'flex', gap: 4 }}>
        <button onClick={() => rotateMap(-1)} style={btnStyle}>
          <RotateCcw size={12} /> Rotate CCW
        </button>
        <button onClick={() => rotateMap(1)} style={btnStyle}>
          <RotateCw size={12} /> Rotate CW
        </button>
      </div>

      {/* Templates */}
      <div style={{ fontSize: 10, color: '#808080', marginTop: 4, textTransform: 'uppercase' }}>
        Templates
      </div>
      <select
        value={selectedTemplate?.id ?? ''}
        onChange={e => {
          const t = MAP_TEMPLATES.find(t => t.id === e.target.value) ?? null
          setSelectedTemplate(t)
        }}
        style={{
          width: '100%',
          padding: '3px 6px',
          background: '#3c3c3c',
          border: '1px solid #2d2d2d',
          borderRadius: 3,
          color: '#e0e0e0',
          fontSize: 12,
          outline: 'none',
        }}
      >
        <option value="">Select template...</option>
        {TEMPLATE_CATEGORIES.map(cat => (
          <optgroup key={cat.id} label={cat.label}>
            {MAP_TEMPLATES.filter(t => t.category === cat.id).map(t => (
              <option key={t.id} value={t.id}>{t.name} ({t.size})</option>
            ))}
          </optgroup>
        ))}
      </select>
      <div style={{ display: 'flex', gap: 4 }}>
        <button
          disabled={!selectedTemplate || loadingTemplate}
          onClick={() => selectedTemplate && handleLoadTemplate(selectedTemplate)}
          style={{
            ...btnStyle,
            background: selectedTemplate ? '#094771' : '#3c3c3c',
            color: selectedTemplate ? '#e0e0e0' : '#808080',
            cursor: selectedTemplate ? 'pointer' : 'default',
            opacity: loadingTemplate ? 0.6 : 1,
          }}
        >
          <FileDown size={12} /> {loadingTemplate ? 'Loading...' : 'Load Template'}
        </button>
        <button
          disabled={loadingTemplate}
          onClick={handleRandom}
          style={{ ...btnStyle, opacity: loadingTemplate ? 0.6 : 1 }}
        >
          <Shuffle size={12} /> Random
        </button>
      </div>
    </div>
  )
}

export function Sidebar() {
  const [collapsed, setCollapsed] = useState(false)
  const [sections, setSections] = useState<Record<SectionId, boolean>>({
    palette: true,
    controls: true,
  })

  const registry = useEditorStore(s => s.registry)

  const toggleSection = useCallback((id: SectionId) => {
    setSections(prev => ({ ...prev, [id]: !prev[id] }))
  }, [])

  const paletteCount = registry.tiles.length + registry.buildings.length

  if (collapsed) {
    return (
      <div
        style={{
          width: 36,
          height: '100%',
          flexShrink: 0,
          background: '#252526',
          borderRight: '1px solid #2d2d2d',
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          paddingTop: 8,
        }}
      >
        <button
          onClick={() => setCollapsed(false)}
          title="Expand Sidebar"
          style={{
            background: 'none',
            border: 'none',
            color: '#e0e0e0',
            cursor: 'pointer',
            padding: 4,
          }}
        >
          <PanelLeftOpen size={18} />
        </button>
      </div>
    )
  }

  return (
    <div
      style={{
        width: 240,
        height: '100%',
        flexShrink: 0,
        background: '#1e1e1e',
        borderRight: '1px solid #2d2d2d',
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
      }}
    >
      {/* Sidebar top bar */}
      <div
        style={{
          flexShrink: 0,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '4px 8px',
          borderBottom: '1px solid #2d2d2d',
          background: '#252526',
        }}
      >
        <span style={{ fontSize: 11, fontWeight: 700, color: '#e0e0e0', textTransform: 'uppercase', letterSpacing: '0.5px' }}>
          Explorer
        </span>
        <button
          onClick={() => setCollapsed(true)}
          title="Collapse Sidebar"
          style={{ background: 'none', border: 'none', color: '#808080', cursor: 'pointer', padding: 2 }}
        >
          <PanelLeftClose size={16} />
        </button>
      </div>

      {/* Sections container - fills remaining space, no scrollbar */}
      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          flex: 1,
          minHeight: 0,
          overflow: 'hidden',
        }}
      >
        {/* Palette — Tiles & Buildings */}
        <SectionHeader title="Palette" sectionId="palette" expanded={sections.palette} onToggle={toggleSection} badge={paletteCount} />
        {sections.palette && (
          <div style={{ flex: 1, minHeight: 0, overflowY: 'auto' }}>
            <PaletteSection />
          </div>
        )}

        {/* Controls — Grid, Rotate & Templates */}
        <SectionHeader title="Controls" sectionId="controls" expanded={sections.controls} onToggle={toggleSection} />
        {sections.controls && (
          <div style={{ flex: 1, minHeight: 0, overflowY: 'auto' }}>
            <ControlsSection />
          </div>
        )}
      </div>
    </div>
  )
}
