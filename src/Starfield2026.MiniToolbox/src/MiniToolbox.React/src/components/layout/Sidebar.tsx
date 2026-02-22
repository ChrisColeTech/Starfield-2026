import { useNavigate, useLocation } from 'react-router-dom';
import { Clapperboard, FileOutput } from 'lucide-react';

const navItems = [
    { path: '/', label: 'Viewer', icon: Clapperboard },
    { path: '/export', label: 'Export', icon: FileOutput },
] as const;

export function Sidebar() {
    const navigate = useNavigate();
    const location = useLocation();

    return (
        <aside className="flex flex-col h-full w-[36px] bg-bg border-r border-border shrink-0">
            {navItems.map(({ path, label, icon: Icon }) => {
                const active = location.pathname === path;
                return (
                    <button
                        key={path}
                        onClick={() => navigate(path)}
                        title={label}
                        className="w-[36px] h-[36px] flex items-center justify-center bg-transparent border-none cursor-pointer hover:bg-hover"
                        style={{
                            color: active ? '#e0e0e0' : '#555555',
                            borderLeft: active ? '2px solid #569cd6' : '2px solid transparent',
                        }}
                    >
                        <Icon size={18} />
                    </button>
                );
            })}
        </aside>
    );
}
