import { BrowserRouter, Routes, Route } from 'react-router-dom'
import Sidebar from './components/Sidebar'
import EditorPage from './pages/EditorPage'
import AnimationsPage from './pages/AnimationsPage'
import ToolsPage from './pages/ToolsPage'
import ExtractionPage from './pages/ExtractionPage'

export default function App() {
  return (
    <BrowserRouter>
      <div style={{
        display: 'flex',
        width: '100%',
        height: '100%',
      }}>
        <Sidebar />
        <div style={{
          flex: 1,
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
        }}>
          <Routes>
            <Route path="/" element={<EditorPage />} />
            <Route path="/animations" element={<AnimationsPage />} />
            <Route path="/tools" element={<ToolsPage />} />
            <Route path="/extraction" element={<ExtractionPage />} />
          </Routes>
        </div>
      </div>
    </BrowserRouter>
  )
}
