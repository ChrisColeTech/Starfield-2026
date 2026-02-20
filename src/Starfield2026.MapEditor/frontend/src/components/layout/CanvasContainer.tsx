import { MapGrid } from '../canvas/MapGrid'

export function CanvasContainer() {
  return (
    <div className="flex-1 overflow-auto bg-[#161616] p-[10px]">
      <MapGrid />
    </div>
  )
}
