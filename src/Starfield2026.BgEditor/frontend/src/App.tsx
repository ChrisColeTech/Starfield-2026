import { Routes, Route } from 'react-router-dom'
import { Header } from './components/layout/Header'
import Sidebar from './components/Sidebar'
import EditorPage from './pages/EditorPage'
import AnimationsPage from './pages/AnimationsPage'
import ToolsPage from './pages/ToolsPage'
import ExtractionPage from './pages/ExtractionPage'

export default function App() {
  return (
    <>
      <Header />
      <div className="flex flex-1 min-h-0">
        <Sidebar />
        <main className="flex-1 min-w-0 min-h-0 overflow-y-auto bg-bg">
          <Routes>
            <Route path="/" element={<EditorPage />} />
            <Route path="/animations" element={<AnimationsPage />} />
            <Route path="/tools" element={<ToolsPage />} />
            <Route path="/extraction" element={<ExtractionPage />} />
          </Routes>
        </main>
      </div>
    </>
  )
}
