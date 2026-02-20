import { useState, useMemo } from 'react'
import { PanelRightClose, PanelRightOpen } from 'lucide-react'
import { useEditorStore } from '../../store/editorStore'
import { UNKNOWN_TILE } from '../../services/registryService'

export function PropertiesPanel() {
  const [collapsed, setCollapsed] = useState(false)
  const mapData = useEditorStore(s => s.mapData)
  const mapWidth = useEditorStore(s => s.mapWidth)
  const mapHeight = useEditorStore(s => s.mapHeight)
  const registry = useEditorStore(s => s.registry)
  const tilesById = useEditorStore(s => s.tilesById)

  const tileCounts = useMemo(() => {
    const counts = new Map<number, number>()
    for (const row of mapData) {
      for (const tileId of row) {
        counts.set(tileId, (counts.get(tileId) || 0) + 1)
      }
    }
    return counts
  }, [mapData])

  const groupedTiles = useMemo(() => {
    const groups: Record<string, { tile: typeof UNKNOWN_TILE; count: number }[]> = {}
    for (const cat of registry.categories) {
      groups[cat.id] = []
    }
    // Collect unknowns separately
    const unknowns: { tile: typeof UNKNOWN_TILE; count: number }[] = []

    for (const [tileId, count] of tileCounts) {
      const tile = tilesById.get(tileId)
      if (!tile) {
        unknowns.push({ tile: { ...UNKNOWN_TILE, id: tileId, name: `Unknown #${tileId}` }, count })
        continue
      }
      if (!groups[tile.category]) groups[tile.category] = []
      groups[tile.category].push({ tile, count })
    }

    // Sort each group by count descending
    for (const cat of Object.keys(groups)) {
      groups[cat].sort((a, b) => b.count - a.count)
    }

    return { groups, unknowns }
  }, [tileCounts, registry, tilesById])

  const totalTiles = mapWidth * mapHeight
  const uniqueCount = tileCounts.size

  if (collapsed) {
    return (
      <div className="w-[36px] bg-[#1e1e1e] border-l border-[#2d2d2d] flex flex-col items-center pt-[6px]">
        <button
          className="w-[28px] h-[28px] border-none rounded-[3px] cursor-pointer text-[#808080] bg-transparent hover:bg-[#2d2d2d] hover:text-[#e0e0e0] flex items-center justify-center"
          onClick={() => setCollapsed(false)}
          title="Expand properties"
        >
          <PanelRightOpen size={16} />
        </button>
      </div>
    )
  }

  return (
    <div className="w-[240px] bg-[#1e1e1e] border-l border-[#2d2d2d] flex flex-col overflow-hidden">
      {/* Header */}
      <div className="flex items-center justify-between px-[10px] h-[30px] border-b border-[#2d2d2d]">
        <button
          className="w-[22px] h-[22px] border-none rounded-[3px] cursor-pointer text-[#808080] bg-transparent hover:bg-[#2d2d2d] hover:text-[#e0e0e0] flex items-center justify-center"
          onClick={() => setCollapsed(true)}
          title="Collapse properties"
        >
          <PanelRightClose size={14} />
        </button>
        <span className="text-[11px] text-[#808080] uppercase tracking-wider">Properties</span>
      </div>

      {/* Map summary */}
      <div className="px-[10px] py-[8px] border-b border-[#2d2d2d] text-[11px] text-[#808080] flex flex-col gap-[2px]">
        <div className="flex justify-between">
          <span>Size</span>
          <span className="text-[#e0e0e0]">{mapWidth} x {mapHeight}</span>
        </div>
        <div className="flex justify-between">
          <span>Total cells</span>
          <span className="text-[#e0e0e0]">{totalTiles}</span>
        </div>
        <div className="flex justify-between">
          <span>Unique tiles</span>
          <span className="text-[#e0e0e0]">{uniqueCount}</span>
        </div>
      </div>

      {/* Tile inventory — dynamic from registry categories */}
      <div className="flex-1 overflow-y-auto p-[10px]">
        {registry.categories.map(cat => {
          const items = groupedTiles.groups[cat.id]
          if (!items || items.length === 0) return null

          const categoryTotal = items.reduce((sum, i) => sum + i.count, 0)

          return (
            <div key={cat.id}>
              <div className="text-[11px] text-[#808080] mt-[8px] mb-[4px] flex justify-between">
                <span>{cat.label}</span>
                <span>{categoryTotal}</span>
              </div>
              {items.map(({ tile, count }) => (
                <div
                  key={tile.id}
                  className="flex items-center gap-[6px] py-[2px] px-[4px] rounded-[2px] hover:bg-[#2d2d2d]"
                >
                  <div
                    className="w-[12px] h-[12px] rounded-[2px] flex-shrink-0"
                    style={{ background: tile.color }}
                  />
                  <span className="text-[12px] text-[#e0e0e0] flex-1 truncate">{tile.name}</span>
                  <span className="text-[11px] text-[#808080] tabular-nums">{count}</span>
                </div>
              ))}
            </div>
          )
        })}

        {/* Unknown tiles (not in registry) */}
        {groupedTiles.unknowns.length > 0 && (
          <div>
            <div className="text-[11px] text-[#808080] mt-[8px] mb-[4px] flex justify-between">
              <span>Unknown</span>
              <span>{groupedTiles.unknowns.reduce((s, i) => s + i.count, 0)}</span>
            </div>
            {groupedTiles.unknowns.map(({ tile, count }) => (
              <div
                key={tile.id}
                className="flex items-center gap-[6px] py-[2px] px-[4px] rounded-[2px] hover:bg-[#2d2d2d]"
              >
                <div
                  className="w-[12px] h-[12px] rounded-[2px] flex-shrink-0"
                  style={{ background: tile.color }}
                />
                <span className="text-[12px] text-[#e0e0e0] flex-1 truncate">{tile.name}</span>
                <span className="text-[11px] text-[#808080] tabular-nums">{count}</span>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
