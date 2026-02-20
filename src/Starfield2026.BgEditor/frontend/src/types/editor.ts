import type * as THREE from 'three'

export interface TextureAdjustment {
  hueShift: number       // -180 to 180
  saturation: number     // -100 to 100
  brightness: number     // -100 to 100
  tintColor: string      // hex color
  tintStrength: number   // 0 to 100
}

export interface LoadedTexture {
  name: string
  originalImage: HTMLImageElement
  originalDataUrl: string
  modifiedDataUrl: string
  threeTexture: THREE.Texture
  adjustment: TextureAdjustment
}

export const DEFAULT_ADJUSTMENT: TextureAdjustment = {
  hueShift: 0,
  saturation: 0,
  brightness: 0,
  tintColor: '#ff0000',
  tintStrength: 0,
}
