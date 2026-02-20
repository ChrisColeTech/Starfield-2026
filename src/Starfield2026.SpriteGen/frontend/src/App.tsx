import { Header } from './components/layout/Header';
import { Sidebar } from './components/layout/Sidebar';
import { MainContent } from './components/layout/MainContent';
import { GalleryPanel } from './components/gallery/GalleryPanel';
import { useStore } from './store';

export function App() {
  const error = useStore((s) => s.error);
  const setError = useStore((s) => s.setError);

  return (
    <div className="h-screen flex flex-col overflow-hidden select-none">
      <Header />
      {error && (
        <div
          className="flex items-center justify-between px-[10px] py-[4px] text-[12px] cursor-pointer"
          style={{ background: 'rgba(199,78,78,0.15)', borderBottom: '1px solid #c74e4e', color: '#e0a0a0' }}
          onClick={() => setError(null)}
        >
          <span>{error}</span>
          <span className="text-[10px] text-text-disabled ml-[8px]">click to dismiss</span>
        </div>
      )}
      <div className="flex-1 flex min-h-0">
        <Sidebar />
        <MainContent />
        <GalleryPanel />
      </div>
    </div>
  );
}
