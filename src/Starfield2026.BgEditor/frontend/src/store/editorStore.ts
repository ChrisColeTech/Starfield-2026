import { create } from 'zustand'
import type * as THREE from 'three'
import type { LoadedTexture, TextureAdjustment } from '../types/editor'
import { DEFAULT_ADJUSTMENT } from '../types/editor'
import type { SplitManifest } from '../types/animation'
import { loadScene, loadModelOnly, loadBakedClip } from '../services/sceneService'
import type { Manifest } from '../services/sceneService'
import { applyAdjustment, updateThreeTexture } from '../services/textureProcessor'

const API_BASE = 'http://localhost:3001'

// ─────────────────────────── Types ───────────────────────────

interface ManifestListEntry {
  name: string
  dir: string
  assetsPath: string
  modelFile: string
  modelFormat: string
  textures: string[]
  clipCount: number
}

interface EditorState {
  // Scene (shared by Editor + Animations pages)
  sceneName: string | null
  manifest: Manifest | null
  scene: THREE.Group | null
  animations: THREE.AnimationClip[]
  textures: LoadedTexture[]
  selectedTextureIndex: number
  loading: boolean
  error: string | null

  // Animation playback
  animationPlaying: boolean
  activeClipIndex: number

  // Animation editor (folder-based loading, tagging)
  folderPath: string | null
  animManifest: SplitManifest | null
  dirty: boolean
  saving: boolean
  clipLoading: boolean
  activeModelIndex: number

  // Manifest scan browser
  scanDir: string
  manifests: ManifestListEntry[]
  scanning: boolean
  selectedManifestIndex: number

  // Editor actions
  loadManifest: (file: File) => Promise<void>
  loadManifestFromPath: (filePath: string) => Promise<void>
  loadManifestData: (manifest: Manifest) => Promise<void>
  selectTexture: (index: number) => void
  setAdjustment: (index: number, adj: Partial<TextureAdjustment>) => void
  resetTexture: (index: number) => void
  resetAll: () => void
  applyToAll: () => void
  setAnimationPlaying: (playing: boolean) => void
  setActiveClipIndex: (index: number) => void

  // Animation actions
  scanFolder: (dir: string) => Promise<void>
  selectManifest: (index: number) => void
  loadManifestList: (manifests: any[], dir?: string) => void
  loadFolder: (dir: string, manifest?: any) => Promise<void>
  selectClip: (index: number) => Promise<void>
  tagClip: (index: number, semanticName: string | null) => void
  autoTag: () => void
  saveManifest: () => Promise<void>
  resetAnimations: () => void
  clearAll: () => void
}

// ─────────────────────────── Helpers ───────────────────────────

function processTexture(tex: LoadedTexture, adj: TextureAdjustment): LoadedTexture {
  const modifiedDataUrl = applyAdjustment(tex.originalImage, adj)
  updateThreeTexture(tex.threeTexture, modifiedDataUrl)
  return { ...tex, adjustment: adj, modifiedDataUrl }
}

function resetTextureToOriginal(tex: LoadedTexture): LoadedTexture {
  updateThreeTexture(tex.threeTexture, tex.originalDataUrl)
  return {
    ...tex,
    adjustment: { ...DEFAULT_ADJUSTMENT },
    modifiedDataUrl: tex.originalDataUrl,
  }
}

/** Overworld character animation slot map — matches C# OhanaCli MapOverworldSlot */
const OVERWORLD_SLOT_MAP: Record<number, string> = {
  0: 'Idle',
  1: 'Walk',
  2: 'Run',
  4: 'Jump',
  5: 'Land',
  7: 'ShortAction1',
  8: 'LongAction1',
  9: 'ShortAction2',
  17: 'MediumAction',
  20: 'Action',
  23: 'Action2',
  30: 'ShortAction3',
  31: 'ShortAction4',
  52: 'IdleVariant',
  54: 'ShortAction5',
  55: 'LongAction2',
  56: 'ShortAction6',
  59: 'Action3',
  61: 'Action4',
  72: 'Action5',
  123: 'LongAction3',
  124: 'Action6',
  125: 'Action7',
  127: 'Action8',
  128: 'Action9',
}

// ─────────────────────────── Store ───────────────────────────

export const useEditorStore = create<EditorState>()((set, get) => ({
  // Scene state
  sceneName: null,
  manifest: null,
  scene: null,
  animations: [],
  textures: [],
  selectedTextureIndex: 0,
  loading: false,
  error: null,
  animationPlaying: true,
  activeClipIndex: 0,

  // Animation editor state
  folderPath: null,
  animManifest: null,
  dirty: false,
  saving: false,
  clipLoading: false,
  activeModelIndex: 0,

  // Scan browser state
  scanDir: '',
  manifests: [],
  scanning: false,
  selectedManifestIndex: -1,

  // ─────────────── Editor actions ───────────────

  loadManifest: async (file: File) => {
    const filePath = (file as any).path as string | undefined
    if (filePath) {
      return get().loadManifestFromPath(filePath)
    }
    set({ loading: true, error: null })
    try {
      const text = await file.text()
      const manifest: Manifest = JSON.parse(text)
      const result = await loadScene(manifest)
      set({
        scene: result.scene,
        animations: result.animations,
        textures: result.textures,
        sceneName: manifest.name,
        manifest,
        selectedTextureIndex: 0,
        loading: false,
        animationPlaying: true,
        activeClipIndex: 0,
      })
    } catch (err) {
      set({
        error: err instanceof Error ? err.message : 'Failed to load scene',
        loading: false,
      })
    }
  },

  loadManifestFromPath: async (filePath: string) => {
    set({ loading: true, error: null })
    try {
      const dir = filePath.replace(/[\\/][^\\/]+$/, '').replace(/\\/g, '/')
      console.log(`[EditorStore] loadManifestFromPath: dir="${dir}"`)

      const res = await fetch(`${API_BASE}/api/manifests/read?dir=${encodeURIComponent(dir)}`)
      if (!res.ok) throw new Error(`Failed to read manifest: HTTP ${res.status}`)
      const manifest: Manifest = await res.json()
      manifest.dir = dir

      const result = await loadScene(manifest)
      set({
        scene: result.scene,
        animations: result.animations,
        textures: result.textures,
        sceneName: manifest.name,
        manifest,
        selectedTextureIndex: 0,
        loading: false,
        animationPlaying: true,
        activeClipIndex: 0,
      })
    } catch (err) {
      console.error('[EditorStore] loadManifestFromPath failed:', err)
      set({
        error: err instanceof Error ? err.message : 'Failed to load scene',
        loading: false,
      })
    }
  },

  loadManifestData: async (manifest: Manifest) => {
    set({ loading: true, error: null })
    try {
      console.log(`[EditorStore] loadManifestData: name="${manifest.name}" dir="${manifest.dir}"`)
      const result = await loadScene(manifest)
      set({
        scene: result.scene,
        animations: result.animations,
        textures: result.textures,
        sceneName: manifest.name,
        manifest,
        selectedTextureIndex: 0,
        loading: false,
        animationPlaying: true,
        activeClipIndex: 0,
        // Also populate animation page fields
        folderPath: manifest.dir || '',
        animManifest: manifest as any,
        activeModelIndex: 0,
        scanDir: manifest.dir || '',
        manifests: [{ dir: manifest.dir, name: manifest.name || 'Loaded Model', clipCount: (manifest as any).models?.[0]?.clips?.length || 0 }],
        selectedManifestIndex: 0,
      })
    } catch (err) {
      console.error('[EditorStore] loadManifestData failed:', err)
      set({
        error: err instanceof Error ? err.message : 'Failed to load scene',
        loading: false,
      })
    }
  },

  selectTexture: (index: number) => {
    set({ selectedTextureIndex: index })
  },

  setAdjustment: (index: number, adj: Partial<TextureAdjustment>) => {
    const textures = [...get().textures]
    if (!textures[index]) return
    const newAdj = { ...textures[index].adjustment, ...adj }
    textures[index] = processTexture(textures[index], newAdj)
    set({ textures })
  },

  resetTexture: (index: number) => {
    const textures = [...get().textures]
    if (!textures[index]) return
    textures[index] = resetTextureToOriginal(textures[index])
    set({ textures })
  },

  resetAll: () => {
    const textures = get().textures.map(t => resetTextureToOriginal(t))
    set({ textures })
  },

  applyToAll: () => {
    const { textures, selectedTextureIndex } = get()
    const source = textures[selectedTextureIndex]
    if (!source) return
    const adj = source.adjustment
    const updated = textures.map(t => processTexture(t, { ...adj }))
    set({ textures: updated })
  },

  setAnimationPlaying: (playing: boolean) => {
    set({ animationPlaying: playing })
  },

  setActiveClipIndex: (index: number) => {
    set({ activeClipIndex: index })
  },

  // ─────────────── Animation actions ───────────────

  scanFolder: async (dir: string) => {
    if (!dir.trim()) return
    set({ scanning: true, scanDir: dir })
    try {
      const res = await fetch(`${API_BASE}/api/manifests?dir=${encodeURIComponent(dir)}`)
      const data = await res.json()
      set({ manifests: data, selectedManifestIndex: -1, scanning: false })
    } catch {
      set({ scanning: false })
    }
  },

  selectManifest: (index: number) => {
    const { manifests } = get()
    set({ selectedManifestIndex: index })
    const m = manifests[index]
    if (m?.dir) get().loadFolder(m.dir)
  },

  loadManifestList: (manifests: any[], dir?: string) => {
    const existing = get().manifests
    // Dedup by dir — new manifests override existing ones with same dir
    const existingDirs = new Set(manifests.map((m: any) => m.dir))
    const kept = existing.filter((m: any) => !existingDirs.has(m.dir))
    const merged = [...kept, ...manifests]

    set({
      manifests: merged,
      scanDir: dir || manifests[0]?.dir || get().scanDir,
      selectedManifestIndex: kept.length, // select first new one
      scanning: false,
    })
    // Auto-load the first new manifest
    const first = manifests[0]
    if (first?.dir) get().loadFolder(first.dir, first)
  },

  loadFolder: async (dir: string, prefetchedManifest?: any) => {
    set({ loading: true, error: null, folderPath: dir, dirty: false, activeClipIndex: -1 })

    try {
      let manifest: SplitManifest
      if (prefetchedManifest) {
        manifest = prefetchedManifest
      } else {
        const res = await fetch(`${API_BASE}/api/manifests/read?dir=${encodeURIComponent(dir)}`)
        if (!res.ok) {
          const err = await res.json().catch(() => ({ error: res.statusText }))
          throw new Error(err.error || `HTTP ${res.status}`)
        }
        manifest = await res.json()
      }

      if (!manifest.models || manifest.models.length === 0) {
        throw new Error('Manifest has no models')
      }

      const model = manifest.models[0]
      const scene = await loadModelOnly(dir, model.modelFile)

      set({
        scene,
        animations: [],
        sceneName: model.name,
        animationPlaying: false,
        activeClipIndex: 0,
        animManifest: manifest,
        loading: false,
        activeModelIndex: 0,
      })

      // Auto-select first clip
      if (model.clips.length > 0) {
        get().selectClip(0)
      }
    } catch (err) {
      set({
        error: err instanceof Error ? err.message : 'Failed to load folder',
        loading: false,
      })
    }
  },

  selectClip: async (index: number) => {
    const { animManifest, folderPath, activeModelIndex } = get()
    if (!animManifest || !folderPath) return

    const model = animManifest.models[activeModelIndex]
    if (!model || index < 0 || index >= model.clips.length) return

    set({ activeClipIndex: index, clipLoading: true })

    try {
      const clip = model.clips[index]
      console.log(`[EditorStore] Loading baked clip ${index}: "${clip.name}" file="${clip.file}"`)

      const { scene, animations } = await loadBakedClip(folderPath, model.modelFile, clip.file)

      set({
        scene,
        animations,
        sceneName: model.name,
        activeClipIndex: 0,
        animationPlaying: true,
        clipLoading: false,
      })
    } catch (err) {
      console.warn('[EditorStore] Failed to load baked clip:', err)
      set({ clipLoading: false })
    }
  },

  tagClip: (index: number, semanticName: string | null) => {
    const { animManifest, activeModelIndex } = get()
    if (!animManifest) return

    const updated = structuredClone(animManifest)
    const clip = updated.models[activeModelIndex]?.clips[index]
    if (!clip) return

    clip.semanticName = semanticName
    clip.semanticSource = semanticName ? 'manual' : null

    set({ animManifest: updated, dirty: true })
  },

  autoTag: () => {
    const { animManifest, activeModelIndex } = get()
    if (!animManifest) return

    const updated = structuredClone(animManifest)
    const model = updated.models[activeModelIndex]
    if (!model) return

    for (const clip of model.clips) {
      if (clip.semanticName) continue
      const tag = OVERWORLD_SLOT_MAP[clip.index]
      if (tag) {
        clip.semanticName = tag
        clip.semanticSource = 'auto-index'
      }
    }

    set({ animManifest: updated, dirty: true })
  },

  saveManifest: async () => {
    const { animManifest, folderPath } = get()
    if (!animManifest || !folderPath) return

    set({ saving: true })
    try {
      const res = await fetch(`${API_BASE}/api/manifests/save`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ dir: folderPath, manifest: animManifest }),
      })
      if (!res.ok) {
        const err = await res.json().catch(() => ({ error: res.statusText }))
        throw new Error(err.error || `HTTP ${res.status}`)
      }
      set({ saving: false, dirty: false })
    } catch (err) {
      console.error('[EditorStore] Save failed:', err)
      set({ saving: false })
    }
  },

  resetAnimations: () => {
    set({
      folderPath: null,
      animManifest: null,
      dirty: false,
      saving: false,
      clipLoading: false,
      activeModelIndex: 0,
      activeClipIndex: -1,
    })
  },

  clearAll: () => {
    set({
      sceneName: null,
      manifest: null,
      scene: null,
      animations: [],
      textures: [],
      selectedTextureIndex: 0,
      loading: false,
      error: null,
      animationPlaying: true,
      activeClipIndex: 0,
      folderPath: null,
      animManifest: null,
      dirty: false,
      saving: false,
      clipLoading: false,
      activeModelIndex: 0,
      scanDir: '',
      manifests: [],
      scanning: false,
      selectedManifestIndex: -1,
    })
  },
}))
