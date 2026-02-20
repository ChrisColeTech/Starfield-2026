import { useCallback, useRef, useState } from 'react'
import { useEditorStore } from '../../store/editorStore'

const ARROWS: Record<string, string> = {
  up: '\u25B2',
  down: '\u25BC',
  left: '\u25C0',
  right: '\u25B6',
}

export function MapGrid() {
  const mapData = useEditorStore(s => s.mapData)
  const cellSize = useEditorStore(s => s.cellSize)
  const selectedBuilding = useEditorStore(s => s.selectedBuilding)
  const selectedTile = useEditorStore(s => s.selectedTile)
  const paint = useEditorStore(s => s.paint)
  const placeBuilding = useEditorStore(s => s.placeBuilding)
  const getRotatedBuilding = useEditorStore(s => s.getRotatedBuilding)
  const getTile = useEditorStore(s => s.getTile)
  const isDrawing = useRef(false)
  const [hoverPos, setHoverPos] = useState<{ x: number; y: number } | null>(null)

  const handleMouseDown = useCallback((x: number, y: number, e: React.MouseEvent) => {
    e.preventDefault()
    if (e.button === 2) {
      paint(x, y, 1)
    } else if (selectedBuilding !== null) {
      placeBuilding(x, y)
    } else {
      isDrawing.current = true
      paint(x, y)
    }
  }, [paint, placeBuilding, selectedBuilding])

  const handleMouseEnter = useCallback((x: number, y: number) => {
    if (isDrawing.current && selectedBuilding === null) {
      paint(x, y)
    }
    setHoverPos({ x, y })
  }, [paint, selectedBuilding])

  const handleMouseUp = useCallback(() => {
    isDrawing.current = false
  }, [])

  const handleGridLeave = useCallback(() => {
    isDrawing.current = false
    setHoverPos(null)
  }, [])

  const width = mapData[0].length
  const height = mapData.length
  const gap = 1
  const cellTotal = cellSize + gap
  const gridPx = width * cellSize + (width + 1)

  // Build the preview overlay data
  const rotatedBuilding = selectedBuilding !== null ? getRotatedBuilding() : null
  const previewCells: { x: number; y: number; color: string }[] = []

  if (hoverPos && selectedBuilding !== null && rotatedBuilding) {
    for (let by = 0; by < rotatedBuilding.height; by++) {
      for (let bx = 0; bx < rotatedBuilding.width; bx++) {
        const tx = hoverPos.x + bx
        const ty = hoverPos.y + by
        if (tx < width && ty < height) {
          const tileId = rotatedBuilding.tiles[by][bx]
          if (tileId !== null) {
            const tile = getTile(tileId)
            previewCells.push({ x: tx, y: ty, color: tile.color })
          }
        }
      }
    }
  }

  // Single-tile hover highlight (when painting individual tiles, not buildings)
  const showTileHover = hoverPos && selectedBuilding === null
  const hoverTile = showTileHover ? getTile(selectedTile) : null

  return (
    <div
      className="select-none"
      onMouseUp={handleMouseUp}
      onMouseLeave={handleGridLeave}
      onContextMenu={(e) => e.preventDefault()}
    >
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: `repeat(${width}, ${cellSize}px)`,
          gap: `${gap}px`,
          background: '#0f0f23',
          padding: `${gap}px`,
          width: `${gridPx}px`,
          position: 'relative',
        }}
      >
        {mapData.map((row, y) =>
          row.map((tileId, x) => {
            const tile = getTile(tileId)
            return (
              <div
                key={`${x}-${y}`}
                style={{
                  width: cellSize,
                  height: cellSize,
                  background: tile.color,
                  cursor: 'crosshair',
                  borderRadius: 2,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontSize: 10,
                  color: '#e0e0e0',
                }}
                onMouseDown={(e) => handleMouseDown(x, y, e)}
                onMouseEnter={() => handleMouseEnter(x, y)}
              >
                {tile.encounter && '!'}
                {tile.category === 'trainer' && tile.direction && ARROWS[tile.direction]}
              </div>
            )
          })
        )}

        {/* Building preview overlay */}
        {previewCells.map((cell) => (
          <div
            key={`preview-${cell.x}-${cell.y}`}
            style={{
              position: 'absolute',
              left: cell.x * cellTotal + gap,
              top: cell.y * cellTotal + gap,
              width: cellSize,
              height: cellSize,
              background: cell.color,
              opacity: 0.6,
              border: '2px dashed #fff',
              borderRadius: 2,
              pointerEvents: 'none',
              zIndex: 10,
            }}
          />
        ))}

        {/* Single tile hover outline */}
        {showTileHover && hoverPos && hoverTile && (
          <div
            style={{
              position: 'absolute',
              left: hoverPos.x * cellTotal + gap,
              top: hoverPos.y * cellTotal + gap,
              width: cellSize,
              height: cellSize,
              background: hoverTile.color,
              opacity: 0.5,
              border: '2px solid #fff',
              borderRadius: 2,
              pointerEvents: 'none',
              zIndex: 10,
            }}
          />
        )}
      </div>
    </div>
  )
}
