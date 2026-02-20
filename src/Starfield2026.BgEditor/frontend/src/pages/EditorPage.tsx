import { useEditorStore } from '../store/editorStore'
import InfoBar from '../components/InfoBar'
import Viewport from '../components/Viewport'
import DropZone from '../components/DropZone'
import TexturePanel from '../components/TexturePanel'
import ColorControls from '../components/ColorControls'
import ExportPanel from '../components/ExportPanel'

export default function EditorPage() {
  const scene = useEditorStore(s => s.scene)

  return (
    <div style={{
      display: 'flex',
      flexDirection: 'column',
      width: '100%',
      height: '100%',
    }}>
      <InfoBar />
      <div style={{
        flex: 1,
        display: 'flex',
        position: 'relative',
        overflow: 'hidden',
      }}>
        {scene ? (
          <>
            <Viewport />
            <div style={{
              width: 280,
              display: 'flex',
              flexDirection: 'column',
              background: '#16162a',
              borderLeft: '1px solid #2a2a4a',
              overflow: 'hidden',
            }}>
              <div style={{ flex: '0 0 auto', maxHeight: '30%', overflow: 'hidden', display: 'flex' }}>
                <TexturePanel />
              </div>
              <div style={{ flex: '1 1 auto', overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>
                <ColorControls />
              </div>
              <div style={{ flex: '0 0 auto' }}>
                <ExportPanel />
              </div>
            </div>
          </>
        ) : (
          <DropZone />
        )}
      </div>
    </div>
  )
}
