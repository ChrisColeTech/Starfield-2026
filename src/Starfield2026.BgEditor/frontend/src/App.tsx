import { useEffect, useState } from 'react'
import { Routes, Route, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { Header } from './components/layout/Header'
import Sidebar from './components/Sidebar'
import EditorPage from './pages/EditorPage'
import AnimationsPage from './pages/AnimationsPage'
import ToolsPage from './pages/ToolsPage'
import ExtractionPage from './pages/ExtractionPage'
import { useBackendEvents } from './hooks/useBackendEvents'
import { ToastContainer } from './components/ToastContainer'

const STORAGE_KEY = 'bgeditor-last-page'
const VALID_PAGES = ['/', '/animations', '/tools', '/extraction']
const DEFAULT_PAGE = '/'

function getElectronAPI(): any | null {
  return (window as any).electronAPI?.storeGet ? (window as any).electronAPI : null
}

async function loadLastPage(): Promise<string> {
  const api = getElectronAPI()
  if (api) {
    try {
      const page = await api.storeGet('lastActivePage')
      if (page && VALID_PAGES.includes(page)) return page
    } catch { /* fall through */ }
  }
  try {
    const page = localStorage.getItem(STORAGE_KEY)
    if (page && VALID_PAGES.includes(page)) return page
  } catch { /* ignore */ }
  return DEFAULT_PAGE
}

function saveLastPage(page: string) {
  const api = getElectronAPI()
  if (api) {
    api.storeSet('lastActivePage', page).catch(() => { })
  }
  try {
    localStorage.setItem(STORAGE_KEY, page)
  } catch { /* ignore */ }
}

function NavigationPersistence() {
  const location = useLocation()
  useEffect(() => {
    if (VALID_PAGES.includes(location.pathname)) {
      saveLastPage(location.pathname)
    }
  }, [location.pathname])
  return null
}

function NavigationListener() {
  const navigate = useNavigate()
  useEffect(() => {
    const handler = (e: Event) => {
      const page = (e as CustomEvent).detail?.page
      if (page && VALID_PAGES.includes(page)) {
        navigate(page)
      }
    }
    window.addEventListener('app:navigate', handler)
    return () => window.removeEventListener('app:navigate', handler)
  }, [navigate])
  return null
}

function InitialRedirect({ lastPage }: { lastPage: string }) {
  const location = useLocation()
  const navigate = useNavigate()

  useEffect(() => {
    // On first render, if we're at root and last page was different, redirect
    if (location.pathname === '/' && lastPage !== '/') {
      navigate(lastPage, { replace: true })
    }
  }, [])

  return null
}

function BackendEventsProvider() {
  useBackendEvents()
  return null
}

export default function App() {
  const [lastPage, setLastPage] = useState<string | null>(null)

  useEffect(() => {
    loadLastPage().then(setLastPage)
  }, [])

  if (lastPage === null) return null // wait for hydration

  return (
    <>
      <NavigationPersistence />
      <NavigationListener />
      <InitialRedirect lastPage={lastPage} />
      <BackendEventsProvider />
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
      <ToastContainer />
    </>
  )
}
