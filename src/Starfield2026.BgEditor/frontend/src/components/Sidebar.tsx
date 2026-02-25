import { useNavigate, useLocation } from 'react-router-dom';
import { Hexagon, Play, Settings, Archive } from 'lucide-react';

const navItems = [
  { path: '/', label: 'Editor', icon: Hexagon },
  { path: '/animations', label: 'Anim', icon: Play },
  { path: '/tools', label: 'Tools', icon: Settings },
  { path: '/extraction', label: 'Extract', icon: Archive },
] as const;

export default function Sidebar() {
  const navigate = useNavigate();
  const location = useLocation();

  return (
    <aside className="flex flex-col h-full w-9 bg-background border-r border-border shrink-0">
      {navItems.map(({ path, label, icon: Icon }) => {
        const active = location.pathname === path;
        return (
          <button
            key={path}
            onClick={() => navigate(path)}
            title={label}
            className={`w-9 h-9 flex items-center justify-center bg-transparent cursor-pointer transition-colors border-y-0 border-r-0 border-l-2 border-solid ${active
              ? 'text-foreground border-l-primary'
              : 'text-muted-foreground/50 border-l-transparent hover:bg-muted hover:text-muted-foreground'
              }`}
          >
            <Icon size={18} />
          </button>
        );
      })}
    </aside>
  );
}
