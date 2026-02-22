import { Sidebar } from './Sidebar'
import { CanvasContainer } from './CanvasContainer'
import { PropertiesPanel } from './PropertiesPanel'

export function AppShell() {
  return (
    <div className="flex h-full w-full overflow-hidden">
      <Sidebar />
      <CanvasContainer />
      <PropertiesPanel />
    </div>
  )
}
