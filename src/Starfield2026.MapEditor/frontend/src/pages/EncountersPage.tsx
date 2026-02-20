import { useState, useMemo, useRef, useEffect, useCallback } from 'react'
import { Plus, Trash2, Upload } from 'lucide-react'
import { useEditorStore } from '../store/editorStore'
import {
  ENCOUNTER_TYPES,
  PROGRESS_MULTIPLIERS,
  type SpeciesInfo,
  type EncounterEntry,
} from '../types/encounters'

// --- Styles ---

const inputStyle: React.CSSProperties = {
  padding: '3px 6px',
  background: '#3c3c3c',
  border: '1px solid #2d2d2d',
  borderRadius: 3,
  color: '#e0e0e0',
  fontSize: 12,
  outline: 'none',
}

const btnStyle: React.CSSProperties = {
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'center',
  gap: 4,
  padding: '4px 8px',
  background: '#3c3c3c',
  border: '1px solid #2d2d2d',
  borderRadius: 3,
  color: '#e0e0e0',
  fontSize: 11,
  cursor: 'pointer',
}

// --- Species Search Dropdown ---

function SpeciesSearch({
  value,
  species,
  onChange,
}: {
  value: string
  species: SpeciesInfo[]
  onChange: (speciesId: string) => void
}) {
  const [query, setQuery] = useState(value)
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => { setQuery(value) }, [value])

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  const filtered = useMemo(() => {
    if (!query) return species.slice(0, 50)
    const q = query.toLowerCase()
    return species.filter(s => s.name.toLowerCase().includes(q)).slice(0, 50)
  }, [query, species])

  return (
    <div ref={ref} style={{ position: 'relative', flex: 1, minWidth: 120 }}>
      <input
        type="text"
        value={query}
        onChange={e => { setQuery(e.target.value); setOpen(true) }}
        onFocus={() => setOpen(true)}
        placeholder="Search species..."
        style={{ ...inputStyle, width: '100%' }}
      />
      {open && filtered.length > 0 && (
        <div style={{
          position: 'absolute',
          top: '100%',
          left: 0,
          right: 0,
          maxHeight: 200,
          overflowY: 'auto',
          background: '#1e1e1e',
          border: '1px solid #2d2d2d',
          borderRadius: 3,
          zIndex: 100,
        }}>
          {filtered.map(s => (
            <button
              key={s.id}
              onClick={() => { onChange(s.name); setQuery(s.name); setOpen(false) }}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                width: '100%',
                padding: '4px 8px',
                background: s.name === value ? '#094771' : 'transparent',
                border: 'none',
                color: '#e0e0e0',
                fontSize: 12,
                cursor: 'pointer',
                textAlign: 'left',
              }}
              onMouseEnter={e => { if (s.name !== value) (e.target as HTMLElement).style.background = '#2d2d2d' }}
              onMouseLeave={e => { if (s.name !== value) (e.target as HTMLElement).style.background = 'transparent' }}
            >
              <span style={{ color: '#808080', fontSize: 10, minWidth: 28 }}>#{s.id}</span>
              <span>{s.name}</span>
              <span style={{ color: '#808080', fontSize: 10, marginLeft: 'auto' }}>
                {s.type1}{s.type2 ? ` / ${s.type2}` : ''}
              </span>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

// --- Weight Bar ---

function WeightBar({ entries }: { entries: EncounterEntry[] }) {
  const totalWeight = entries.reduce((sum, e) => sum + e.weight, 0)
  if (totalWeight === 0) return null

  const colors = [
    '#4a9eff', '#ff6b6b', '#51cf66', '#ffd43b', '#cc5de8',
    '#ff922b', '#20c997', '#e599f7', '#74c0fc', '#f06595',
  ]

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 3, padding: '4px 0' }}>
      <div style={{ display: 'flex', height: 8, borderRadius: 4, overflow: 'hidden' }}>
        {entries.map((e, i) => {
          const pct = (e.weight / totalWeight) * 100
          if (pct === 0) return null
          return (
            <div
              key={i}
              title={`${e.speciesId || '?'}: ${pct.toFixed(1)}%`}
              style={{
                width: `${pct}%`,
                background: colors[i % colors.length],
                minWidth: 2,
              }}
            />
          )
        })}
      </div>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '2px 10px', fontSize: 10, color: '#808080' }}>
        {entries.map((e, i) => {
          const pct = (e.weight / totalWeight) * 100
          return (
            <span key={i} style={{ display: 'flex', alignItems: 'center', gap: 3 }}>
              <span style={{ width: 8, height: 8, borderRadius: 2, background: colors[i % colors.length], display: 'inline-block' }} />
              {e.speciesId || '?'} {pct.toFixed(1)}%
            </span>
          )
        })}
      </div>
    </div>
  )
}

// --- Entry Row ---

function EntryRow({
  entry,
  groupIndex,
  entryIndex,
  species,
}: {
  entry: EncounterEntry
  groupIndex: number
  entryIndex: number
  species: SpeciesInfo[]
}) {
  const updateEntry = useEditorStore(s => s.updateEncounterEntry)
  const removeEntry = useEditorStore(s => s.removeEncounterEntry)

  const handleUpdate = useCallback((partial: Partial<EncounterEntry>) => {
    updateEntry(groupIndex, entryIndex, partial)
  }, [updateEntry, groupIndex, entryIndex])

  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
      <SpeciesSearch
        value={entry.speciesId}
        species={species}
        onChange={speciesId => handleUpdate({ speciesId })}
      />
      <input
        type="number"
        min={1}
        max={100}
        value={entry.minLevel}
        onChange={e => handleUpdate({ minLevel: +e.target.value })}
        title="Min Level"
        style={{ ...inputStyle, width: 44, textAlign: 'center' }}
      />
      <input
        type="number"
        min={1}
        max={100}
        value={entry.maxLevel}
        onChange={e => handleUpdate({ maxLevel: +e.target.value })}
        title="Max Level"
        style={{ ...inputStyle, width: 44, textAlign: 'center' }}
      />
      <input
        type="number"
        min={1}
        max={255}
        value={entry.weight}
        onChange={e => handleUpdate({ weight: +e.target.value })}
        title="Weight"
        style={{ ...inputStyle, width: 44, textAlign: 'center' }}
      />
      <input
        type="number"
        min={0}
        max={8}
        value={entry.requiredBadges ?? 0}
        onChange={e => handleUpdate({ requiredBadges: +e.target.value || undefined })}
        title="Required Badges"
        style={{ ...inputStyle, width: 36, textAlign: 'center' }}
      />
      <input
        type="text"
        value={(entry.requiredFlags ?? []).join(', ')}
        onChange={e => {
          const flags = e.target.value.split(',').map(f => f.trim()).filter(Boolean)
          handleUpdate({ requiredFlags: flags.length > 0 ? flags : undefined })
        }}
        placeholder="flags"
        title="Required Flags (comma-separated)"
        style={{ ...inputStyle, width: 100 }}
      />
      <button
        onClick={() => removeEntry(groupIndex, entryIndex)}
        title="Remove entry"
        style={{ ...btnStyle, padding: '3px 5px', color: '#f48771', background: 'none', border: 'none' }}
      >
        <Trash2 size={13} />
      </button>
    </div>
  )
}

// --- Encounter Group Card ---

function EncounterGroupCard({
  groupIndex,
  species,
}: {
  groupIndex: number
  species: SpeciesInfo[]
}) {
  const group = useEditorStore(s => s.encounterData.encounterGroups[groupIndex])
  const updateType = useEditorStore(s => s.updateEncounterGroupType)
  const updateRate = useEditorStore(s => s.updateEncounterGroupRate)
  const addEntry = useEditorStore(s => s.addEncounterEntry)
  const removeGroup = useEditorStore(s => s.removeEncounterGroup)

  const ratePct = ((group.baseEncounterRate / 255) * 100).toFixed(1)

  return (
    <div style={{
      background: '#1e1e1e',
      border: '1px solid #2d2d2d',
      borderRadius: 4,
      overflow: 'hidden',
    }}>
      {/* Group header */}
      <div style={{
        display: 'flex',
        alignItems: 'center',
        gap: 8,
        padding: '6px 10px',
        background: '#252526',
        borderBottom: '1px solid #2d2d2d',
      }}>
        <select
          value={group.encounterType}
          onChange={e => updateType(groupIndex, e.target.value)}
          style={{ ...inputStyle, flex: 1 }}
        >
          {ENCOUNTER_TYPES.map(t => (
            <option key={t.id} value={t.id}>{t.label}</option>
          ))}
        </select>
        <div style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: 11, color: '#808080' }}>
          <span>Rate:</span>
          <input
            type="number"
            min={0}
            max={255}
            value={group.baseEncounterRate}
            onChange={e => updateRate(groupIndex, +e.target.value)}
            style={{ ...inputStyle, width: 48, textAlign: 'center' }}
          />
          <span style={{ fontSize: 10 }}>({ratePct}%)</span>
        </div>
        <button
          onClick={() => removeGroup(groupIndex)}
          title="Delete group"
          style={{ ...btnStyle, padding: '3px 5px', color: '#f48771', background: 'none', border: 'none' }}
        >
          <Trash2 size={14} />
        </button>
      </div>

      {/* Column headers */}
      <div style={{
        display: 'flex',
        gap: 4,
        padding: '4px 10px',
        fontSize: 10,
        color: '#808080',
        textTransform: 'uppercase',
        borderBottom: '1px solid #2d2d2d',
      }}>
        <span style={{ flex: 1, minWidth: 120 }}>Species</span>
        <span style={{ width: 44, textAlign: 'center' }}>Min</span>
        <span style={{ width: 44, textAlign: 'center' }}>Max</span>
        <span style={{ width: 44, textAlign: 'center' }}>Wt</span>
        <span style={{ width: 36, textAlign: 'center' }}>Bdg</span>
        <span style={{ width: 100 }}>Flags</span>
        <span style={{ width: 27 }} />
      </div>

      {/* Entries */}
      <div style={{ padding: '6px 10px', display: 'flex', flexDirection: 'column', gap: 4 }}>
        {group.entries.map((entry, i) => (
          <EntryRow
            key={i}
            entry={entry}
            groupIndex={groupIndex}
            entryIndex={i}
            species={species}
          />
        ))}

        {/* Weight bar */}
        {group.entries.length > 0 && <WeightBar entries={group.entries} />}

        {/* Add entry button */}
        <button
          onClick={() => addEntry(groupIndex)}
          style={{ ...btnStyle, alignSelf: 'flex-start', marginTop: 2 }}
        >
          <Plus size={12} /> Add Entry
        </button>
      </div>
    </div>
  )
}

// --- Main Page ---

export default function EncountersPage() {
  const mapName = useEditorStore(s => s.mapName)
  const encounterData = useEditorStore(s => s.encounterData)
  const species = useEditorStore(s => s.species)
  const speciesLoaded = useEditorStore(s => s.speciesLoaded)
  const loadSpecies = useEditorStore(s => s.loadSpecies)
  const setProgressMultiplier = useEditorStore(s => s.setProgressMultiplier)
  const addEncounterGroup = useEditorStore(s => s.addEncounterGroup)

  const mapId = mapName.toLowerCase().replace(/\s+/g, '_').replace(/[^a-z0-9_]/g, '')

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
      {/* Top bar */}
      <div style={{
        display: 'flex',
        alignItems: 'center',
        gap: 12,
        padding: '8px 16px',
        background: '#252526',
        borderBottom: '1px solid #2d2d2d',
        flexShrink: 0,
      }}>
        <span style={{ fontSize: 12, color: '#808080' }}>
          Map: <span style={{ color: '#e0e0e0', fontWeight: 600 }}>{mapId || 'untitled'}</span>
        </span>
        <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
          <span style={{ fontSize: 11, color: '#808080' }}>Scaling:</span>
          <select
            value={encounterData.progressMultiplier}
            onChange={e => setProgressMultiplier(+e.target.value)}
            style={inputStyle}
          >
            {PROGRESS_MULTIPLIERS.map(p => (
              <option key={p.value} value={p.value}>{p.label}</option>
            ))}
          </select>
        </div>
        <div style={{ flex: 1 }} />
        {!speciesLoaded && (
          <button onClick={loadSpecies} style={{ ...btnStyle, background: '#094771' }}>
            <Upload size={12} /> Load Species Data
          </button>
        )}
        {speciesLoaded && (
          <span style={{ fontSize: 10, color: '#808080' }}>{species.length} species loaded</span>
        )}
        <button
          onClick={() => addEncounterGroup()}
          style={{ ...btnStyle, background: '#094771' }}
        >
          <Plus size={12} /> Add Group
        </button>
      </div>

      {/* Content */}
      <div style={{ flex: 1, overflow: 'auto', padding: 16, display: 'flex', flexDirection: 'column', gap: 12 }}>
        {encounterData.encounterGroups.length === 0 ? (
          <div style={{
            flex: 1,
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            color: '#808080',
            fontSize: 13,
            gap: 8,
          }}>
            <span>No encounter groups defined for this map.</span>
            <span style={{ fontSize: 11 }}>Click "Add Group" to create one.</span>
          </div>
        ) : (
          encounterData.encounterGroups.map((_, i) => (
            <EncounterGroupCard key={i} groupIndex={i} species={species} />
          ))
        )}
      </div>
    </div>
  )
}
