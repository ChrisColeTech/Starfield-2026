import { NavLink } from 'react-router-dom'
import { Map, Swords, type LucideIcon } from 'lucide-react'

const navItems: { to: string; label: string; Icon: LucideIcon }[] = [
  { to: '/', label: 'Editor', Icon: Map },
  { to: '/encounters', label: 'Enctr', Icon: Swords },
]

export function NavSidebar() {
  return (
    <nav style={{
      width: 56,
      minWidth: 56,
      display: 'flex',
      flexDirection: 'column',
      background: '#252526',
      borderRight: '1px solid #2d2d2d',
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
            color: isActive ? '#e0e0e0' : '#808080',
            background: isActive ? '#3c3c3c' : 'transparent',
            borderLeft: isActive ? '2px solid #e0e0e0' : '2px solid transparent',
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
