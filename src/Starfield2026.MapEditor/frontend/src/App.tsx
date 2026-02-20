import { BrowserRouter, Routes, Route } from 'react-router-dom'
import { NavSidebar } from './components/layout/NavSidebar'
import { MenuBar } from './components/layout/MenuBar'
import EditorPage from './pages/EditorPage'
import EncountersPage from './pages/EncountersPage'

function App() {
  return (
    <BrowserRouter>
      <div style={{ display: 'flex', flexDirection: 'column', width: '100%', height: '100%' }}>
        <MenuBar />
        <div style={{ flex: 1, display: 'flex', overflow: 'hidden' }}>
          <NavSidebar />
          <div style={{ flex: 1, display: 'flex', overflow: 'hidden' }}>
            <Routes>
              <Route path="/" element={<EditorPage />} />
              <Route path="/encounters" element={<EncountersPage />} />
            </Routes>
          </div>
        </div>
      </div>
    </BrowserRouter>
  )
}

export default App
