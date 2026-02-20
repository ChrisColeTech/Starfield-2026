import { create } from 'zustand'
import type * as THREE from 'three'
import type { LoadedTexture, TextureAdjustment } from '../types/editor'
import { DEFAULT_ADJUSTMENT } from '../types/editor'
import { loadScene } from '../services/sceneService'
import type { Manifest } from '../services/sceneService'
import { applyAdjustment, updateThreeTexture } from '../services/textureProcessor'

interface EditorState {
  // Scene
  sceneName: string | null
  manifest: Manifest | null
  scene: THREE.Group | null
  animations: THREE.AnimationClip[]
  textures: LoadedTexture[]
  selectedTextureIndex: number
  loading: boolean
  error: string | null

  // Animation playback state
  animationPlaying: boolean
  activeClipIndex: number

  // Actions
  loadManifest: (file: File) => Promise<void>
  selectTexture: (index: number) => void
  setAdjustment: (index: number, adj: Partial<TextureAdjustment>) => void
  resetTexture: (index: number) => void
  resetAll: () => void
  applyToAll: () => void
  setAnimationPlaying: (playing: boolean) => void
  setActiveClipIndex: (index: number) => void
}

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

export const useEditorStore = create<EditorState>()((set, get) => ({
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

  loadManifest: async (file: File) => {
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
}))
