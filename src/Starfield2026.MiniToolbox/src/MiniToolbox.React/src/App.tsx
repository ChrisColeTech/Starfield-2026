import { useEffect } from 'react';
import { Routes, Route, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { Header } from './components/layout/Header';
import { Sidebar } from './components/layout/Sidebar';
import { ExportPage } from './pages/ExportPage';
import { ViewerPage } from './pages/ViewerPage';
import { useStore } from './store/exportStore';

function NavigationPersistence() {
    const location = useLocation();
    const setLastActivePage = useStore((s) => s.setLastActivePage);

    useEffect(() => {
        setLastActivePage(location.pathname);
    }, [location.pathname, setLastActivePage]);

    return null;
}

export function App() {
    const hydrate = useStore((s) => s.hydrate);
    const hydrated = useStore((s) => s.hydrated);
    const lastActivePage = useStore((s) => s.lastActivePage);

    useEffect(() => {
        hydrate();
    }, [hydrate]);

    if (!hydrated) {
        return null; // Wait for persisted state to load
    }

    return (
        <>
            <NavigationPersistence />
            <Header />
            <div className="flex flex-1 min-h-0">
                <Sidebar />
                <main className="flex-1 min-w-0 min-h-0 overflow-y-auto bg-bg">
                    <Routes>
                        <Route path="/" element={<Navigate to={lastActivePage || '/export'} replace />} />
                        <Route path="/viewer" element={<ViewerPage />} />
                        <Route path="/export" element={<ExportPage />} />
                    </Routes>
                </main>
            </div>
        </>
    );
}
