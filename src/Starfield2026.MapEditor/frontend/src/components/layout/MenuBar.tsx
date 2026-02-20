import { useState, useEffect, useRef } from 'react'
import { useEditorStore } from '../../store/editorStore'
import { parseCSharpRegistry } from '../../services/registryService'
import { toPascalCase } from '../../services/codeGenService'

interface MenuItem {
  label: string
  shortcut?: string
  disabled?: boolean
  danger?: boolean
  separator?: boolean
  onClick?: () => void
}

interface MenuDefinition {
  label: string
  items: MenuItem[]
}

const CS_FILTERS = [{ name: 'C# Files', extensions: ['cs'] }]

export function MenuBar() {
  const [openIndex, setOpenIndex] = useState<number | null>(null)
  const menuRef = useRef<HTMLDivElement>(null)

  const mapName = useEditorStore(s => s.mapName)
  const importCSharpMap = useEditorStore(s => s.importCSharpMap)
  const exportCSharp = useEditorStore(s => s.exportCSharp)
  const exportRegistryCSharp = useEditorStore(s => s.exportRegistryCSharp)
  const clear = useEditorStore(s => s.clear)
  const setRegistry = useEditorStore(s => s.setRegistry)

  useEffect(() => {
    const handleClick = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setOpenIndex(null)
      }
    }
    document.addEventListener('mousedown', handleClick)
    return () => document.removeEventListener('mousedown', handleClick)
  }, [])

  async function handleImportCSharp() {
    const result = await window.electronAPI.openFile(CS_FILTERS)
    if (result) importCSharpMap(result.content)
  }

  async function handleExportCSharp() {
    const code = exportCSharp()
    const mapId = mapName.toLowerCase().replace(/\s+/g, '_').replace(/[^a-z0-9_]/g, '')
    const className = toPascalCase(mapId) || 'UntitledMap'
    await window.electronAPI.saveFile(`${className}.g.cs`, CS_FILTERS, code)
  }

  async function handleLoadCSharpRegistry() {
    const result = await window.electronAPI.openFile(CS_FILTERS)
    if (!result) return
    try {
      const registry = parseCSharpRegistry(result.content)
      setRegistry(registry)
    } catch (err) {
      alert(`Invalid C# registry: ${err instanceof Error ? err.message : 'Unknown error'}`)
    }
  }

  async function handleExportRegistryCSharp() {
    const code = exportRegistryCSharp()
    await window.electronAPI.saveFile('TileRegistry.cs', CS_FILTERS, code)
  }

  const menus: MenuDefinition[] = [
    {
      label: 'File',
      items: [
        { label: 'New Map', shortcut: 'Ctrl+N', onClick: clear },
        { separator: true, label: '' },
        { label: 'Import Map (C#)...', onClick: handleImportCSharp },
        { label: 'Export Map (C#)...', onClick: handleExportCSharp },
        { separator: true, label: '' },
        { label: 'Load Registry (C#)...', onClick: handleLoadCSharpRegistry },
        { label: 'Export Registry (C#)...', onClick: handleExportRegistryCSharp },
      ],
    },
    {
      label: 'Edit',
      items: [
        { label: 'Undo', shortcut: 'Ctrl+Z', disabled: true },
        { label: 'Redo', shortcut: 'Ctrl+Y', disabled: true },
        { separator: true, label: '' },
        { label: 'Clear All', onClick: clear },
      ],
    },
    {
      label: 'View',
      items: [
        { label: 'Show Grid' },
        { label: 'Show Trainer Vision' },
      ],
    },
  ]

  return (
    <div
      ref={menuRef}
      className="h-[30px] bg-[#1e1e1e] border-b border-[#2d2d2d] flex items-center select-none"
      style={{ fontSize: '13px', flexShrink: 0 }}
    >
      {menus.map((menu, i) => (
        <div key={menu.label} className="relative">
          <button
            className="h-[30px] px-[10px] bg-transparent border-none text-[#e0e0e0] cursor-pointer hover:bg-[#2d2d2d] text-[13px]"
            style={openIndex === i ? { background: '#2d2d2d' } : undefined}
            onClick={() => setOpenIndex(openIndex === i ? null : i)}
            onMouseEnter={() => { if (openIndex !== null) setOpenIndex(i) }}
          >
            {menu.label}
          </button>

          {openIndex === i && (
            <div className="absolute top-[30px] left-0 bg-[#1e1e1e] border border-[#2d2d2d] shadow-xl min-w-[220px] py-[4px] z-50">
              {menu.items.map((item, j) =>
                item.separator ? (
                  <div key={j} className="h-[1px] bg-[#2d2d2d] my-[4px] mx-[10px]" />
                ) : (
                  <button
                    key={j}
                    disabled={item.disabled}
                    className="w-full h-[28px] px-[20px] bg-transparent border-none text-left text-[13px] flex items-center justify-between cursor-pointer hover:bg-[#2d2d2d] disabled:opacity-40 disabled:cursor-default disabled:hover:bg-transparent"
                    style={{ color: item.danger ? '#c74e4e' : item.disabled ? '#555555' : '#e0e0e0' }}
                    onClick={() => {
                      item.onClick?.()
                      setOpenIndex(null)
                    }}
                  >
                    <span>{item.label}</span>
                    {item.shortcut && (
                      <span className="text-[#555555] text-[12px] ml-[30px]">{item.shortcut}</span>
                    )}
                  </button>
                ),
              )}
            </div>
          )}
        </div>
      ))}

      <div className="flex-1" />
    </div>
  )
}
