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
  { id: 'center', label: 'Center', x: 0, y: 0 },
  { id: 'up', label: 'Up', x: 0, y: -1 },
  { id: 'down', label: 'Down', x: 0, y: 1 },
  { id: 'left', label: 'Left', x: -1, y: 0 },
  { id: 'right', label: 'Right', x: 1, y: 0 },
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
      className="shrink-0 flex items-center w-full h-[22px] px-[8px] bg-surface border-none border-b border-border text-text text-[11px] font-bold uppercase tracking-[0.5px] cursor-pointer gap-[4px]"
    >
      {expanded ? <ChevronDown size={12} /> : <ChevronRight size={12} />}
      <span className="flex-1 text-left">{title}</span>
      {badge !== undefined && (
        <span className="text-[10px] text-text-secondary font-normal">
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

  return (
    <div className="p-2 flex flex-col gap-[6px]">
      {/* Tiles */}
      {categories.map(cat => {
        const tiles = tilesForCategory(registry, cat.id)
        if (tiles.length === 0) return null
        return (
          <div key={cat.id}>
            <div className="text-[10px] text-text-secondary mb-[3px] uppercase">
              {cat.label}
            </div>
            <div className="grid grid-cols-2 gap-[4px]">
              {tiles.map(tile => (
                <button
                  key={tile.id}
                  onClick={() => selectTile(tile.id)}
                  className="p-[6px] rounded-[3px] cursor-pointer text-text text-[11px] text-center"
                  style={{
                    background: tile.color,
                    border: selectedTile === tile.id && selectedBuilding === null ? '2px solid #fff' : '2px solid transparent',
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
          <div className="text-[10px] text-text-secondary mt-[4px] uppercase">
            Buildings
          </div>
          <div className="grid grid-cols-2 gap-[4px]">
            {registry.buildings.map((b, idx) => (
              <button
                key={b.id}
                onClick={() => selectBuilding(idx)}
                className="p-[6px] rounded-[3px] cursor-pointer text-text text-[10px] text-center whitespace-pre-line"
                style={{
                  background: selectedBuilding === idx ? 'var(--color-active)' : '#2a4a3a',
                  border: selectedBuilding === idx ? '2px solid #fff' : '2px solid transparent',
                }}
              >
                {b.name}{'\n'}{buildingWidth(b)}x{buildingHeight(b)}
              </button>
            ))}
          </div>
          {selectedBuilding !== null && (
            <div className="flex gap-[4px]">
              <button onClick={() => rotateBuilding(-1)} className="flex-1 flex items-center justify-center gap-[4px] px-[6px] py-[3px] bg-input border border-border rounded-[3px] text-text text-[11px] cursor-pointer hover:bg-hover">
                <RotateCcw size={12} /> Rotate Left
              </button>
              <button onClick={() => rotateBuilding(1)} className="flex-1 flex items-center justify-center gap-[4px] px-[6px] py-[3px] bg-input border border-border rounded-[3px] text-text text-[11px] cursor-pointer hover:bg-hover">
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

  const inputCls = "w-full px-[6px] py-[3px] bg-input border border-border rounded-[3px] text-text text-[12px] outline-none"
  const btnCls = "flex-1 flex items-center justify-center gap-[4px] px-[6px] py-[4px] bg-input border border-border rounded-[3px] text-text text-[11px] cursor-pointer hover:bg-hover"

  return (
    <div className="p-2 flex flex-col gap-[6px]">
      {/* Map name */}
      <label className="text-[10px] text-text-secondary">
        Map Name
        <input type="text" value={mapName} onChange={e => setMapName(e.target.value)} className={`${inputCls} block mt-[2px]`} />
      </label>

      {/* World */}
      <label className="text-[10px] text-text-secondary">
        World ID
        <input type="text" value={worldId} onChange={e => setWorldId(e.target.value)} className={`${inputCls} block mt-[2px]`} />
      </label>
      <label className="text-[10px] text-text-secondary">
        Grid Position
        <select
          value={GRID_POSITIONS.find(p => p.x === worldX && p.y === worldY)?.id ?? 'custom'}
          onChange={e => {
            const pos = GRID_POSITIONS.find(p => p.id === e.target.value)
            if (pos) { setWorldX(pos.x); setWorldY(pos.y) }
          }}
          className={`${inputCls} block mt-[2px]`}
        >
          {GRID_POSITIONS.map(p => (
            <option key={p.id} value={p.id}>{p.label}</option>
          ))}
          {!GRID_POSITIONS.some(p => p.x === worldX && p.y === worldY) && (
            <option value="custom">Custom ({worldX}, {worldY})</option>
          )}
        </select>
      </label>
      <div className="grid grid-cols-2 gap-[4px]">
        <label className="text-[10px] text-text-secondary">
          X
          <input type="number" value={worldX} onChange={e => setWorldX(+e.target.value)} className={inputCls} />
        </label>
        <label className="text-[10px] text-text-secondary">
          Y
          <input type="number" value={worldY} onChange={e => setWorldY(+e.target.value)} className={inputCls} />
        </label>
      </div>

      <div className="text-[10px] text-text-secondary">
        Registry: <span className="text-text">{registry.name} v{registry.version}</span>
      </div>

      {/* Grid size */}
      <div className="grid grid-cols-3 gap-[4px]">
        <label className="text-[10px] text-text-secondary">
          Width
          <input type="number" min={1} max={200} value={w} onChange={e => setW(+e.target.value)} className={inputCls} />
        </label>
        <label className="text-[10px] text-text-secondary">
          Height
          <input type="number" min={1} max={200} value={h} onChange={e => setH(+e.target.value)} className={inputCls} />
        </label>
        <label className="text-[10px] text-text-secondary">
          Cell
          <input type="number" min={8} max={64} value={cellSize} onChange={e => setCellSize(+e.target.value)} className={inputCls} />
        </label>
      </div>
      {/* Base tile */}
      <label className="text-[10px] text-text-secondary">
        Base Tile
        <select value={baseTile} onChange={e => setBaseTile(+e.target.value)} className={`${inputCls} block mt-[2px]`}>
          {registry.tiles
            .filter(t => t.category === 'terrain')
            .map(t => (
              <option key={t.id} value={t.id}>{t.name}</option>
            ))}
        </select>
      </label>

      <div className="flex gap-[4px]">
        <button onClick={() => resize(w, h)} className={btnCls}>Resize</button>
        <button onClick={clear} className={`${btnCls} text-danger`}>Clear All</button>
      </div>
      <div className="flex gap-[4px]">
        <button onClick={() => rotateMap(-1)} className={btnCls}>
          <RotateCcw size={12} /> Rotate CCW
        </button>
        <button onClick={() => rotateMap(1)} className={btnCls}>
          <RotateCw size={12} /> Rotate CW
        </button>
      </div>

      {/* Templates */}
      <div className="text-[10px] text-text-secondary mt-[4px] uppercase">
        Templates
      </div>
      <select
        value={selectedTemplate?.id ?? ''}
        onChange={e => {
          const t = MAP_TEMPLATES.find(t => t.id === e.target.value) ?? null
          setSelectedTemplate(t)
        }}
        className={inputCls}
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
      <div className="flex gap-[4px]">
        <button
          disabled={!selectedTemplate || loadingTemplate}
          onClick={() => selectedTemplate && handleLoadTemplate(selectedTemplate)}
          className="flex-1 flex items-center justify-center gap-[4px] px-[6px] py-[4px] border border-border rounded-[3px] text-[11px] cursor-pointer disabled:opacity-60 disabled:cursor-default"
          style={{
            background: selectedTemplate ? 'var(--color-active)' : 'var(--color-input)',
            color: selectedTemplate ? 'var(--color-text)' : 'var(--color-text-secondary)',
          }}
        >
          <FileDown size={12} /> {loadingTemplate ? 'Loading...' : 'Load Template'}
        </button>
        <button
          disabled={loadingTemplate}
          onClick={handleRandom}
          className={`${btnCls} disabled:opacity-60`}
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
      <div className="w-[36px] h-full shrink-0 bg-surface border-r border-border flex flex-col items-center pt-[8px]">
        <button
          onClick={() => setCollapsed(false)}
          title="Expand Sidebar"
          className="bg-transparent border-none text-text cursor-pointer p-[4px]"
        >
          <PanelLeftOpen size={18} />
        </button>
      </div>
    )
  }

  return (
    <div className="w-[240px] h-full shrink-0 bg-bg border-r border-border flex flex-col overflow-hidden">
      {/* Sidebar top bar */}
      <div className="shrink-0 flex items-center justify-between px-[8px] py-[4px] border-b border-border bg-surface">
        <span className="text-[11px] font-bold text-text uppercase tracking-[0.5px]">
          Explorer
        </span>
        <button
          onClick={() => setCollapsed(true)}
          title="Collapse Sidebar"
          className="bg-transparent border-none text-text-secondary cursor-pointer p-[2px]"
        >
          <PanelLeftClose size={16} />
        </button>
      </div>

      {/* Sections container */}
      <div className="flex flex-col flex-1 min-h-0 overflow-hidden">
        {/* Palette */}
        <SectionHeader title="Palette" sectionId="palette" expanded={sections.palette} onToggle={toggleSection} badge={paletteCount} />
        {sections.palette && (
          <div className="flex-1 min-h-0 overflow-y-auto">
            <PaletteSection />
          </div>
        )}

        {/* Controls */}
        <SectionHeader title="Controls" sectionId="controls" expanded={sections.controls} onToggle={toggleSection} />
        {sections.controls && (
          <div className="flex-1 min-h-0 overflow-y-auto">
            <ControlsSection />
          </div>
        )}
      </div>
    </div>
  )
}
