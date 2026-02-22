import { Routes, Route } from 'react-router-dom'
import { NavSidebar } from './components/layout/NavSidebar'
import { Header } from './components/layout/Header'
import EditorPage from './pages/EditorPage'
import EncountersPage from './pages/EncountersPage'

function App() {
  return (
    <>
      <Header />
      <div className="flex flex-1 min-h-0">
        <NavSidebar />
        <main className="flex-1 min-w-0 min-h-0 overflow-hidden bg-bg">
          <Routes>
            <Route path="/" element={<EditorPage />} />
            <Route path="/encounters" element={<EncountersPage />} />
          </Routes>
        </main>
      </div>
    </>
  )
}

export default App
