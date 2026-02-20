import { NavLink } from 'react-router-dom'
import { Hexagon, Play, Settings, Archive, type LucideIcon } from 'lucide-react'

const navItems: { to: string; label: string; Icon: LucideIcon }[] = [
  { to: '/', label: 'Editor', Icon: Hexagon },
  { to: '/animations', label: 'Anim', Icon: Play },
  { to: '/tools', label: 'Tools', Icon: Settings },
  { to: '/extraction', label: 'Extract', Icon: Archive },
]

export default function Sidebar() {
  return (
    <nav style={{
      width: 56,
      minWidth: 56,
      display: 'flex',
      flexDirection: 'column',
      background: '#12122a',
      borderRight: '1px solid #2a2a4a',
      paddingTop: 8,
      gap: 4,
    }}>
      {navItems.map(item => (
        <NavLink
          key={item.to}
          to={item.to}
          end={item.to === '/'}
          style={({ isActive }) => ({
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            gap: 2,
            padding: '10px 4px',
            textDecoration: 'none',
            color: isActive ? '#8c8cff' : '#888',
            background: isActive ? '#1e1e3a' : 'transparent',
            borderLeft: isActive ? '2px solid #8c8cff' : '2px solid transparent',
            fontSize: 10,
            cursor: 'pointer',
            transition: 'color 0.15s',
          })}
        >
          <item.Icon size={20} strokeWidth={1.5} />
          <span>{item.label}</span>
        </NavLink>
      ))}
    </nav>
  )
}
