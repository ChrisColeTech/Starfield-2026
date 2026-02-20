import { Sidebar } from './Sidebar'
import { CanvasContainer } from './CanvasContainer'
import { PropertiesPanel } from './PropertiesPanel'

export function AppShell() {
  return (
    <>
      <Sidebar />
      <CanvasContainer />
      <PropertiesPanel />
    </>
  )
}
