import type { TextureAdjustment } from '../types/editor'
import { DEFAULT_ADJUSTMENT } from '../types/editor'

function rgbToHsl(r: number, g: number, b: number): [number, number, number] {
  r /= 255; g /= 255; b /= 255
  const max = Math.max(r, g, b), min = Math.min(r, g, b)
  const l = (max + min) / 2
  if (max === min) return [0, 0, l]
  const d = max - min
  const s = l > 0.5 ? d / (2 - max - min) : d / (max + min)
  let h = 0
  if (max === r) h = ((g - b) / d + (g < b ? 6 : 0)) / 6
  else if (max === g) h = ((b - r) / d + 2) / 6
  else h = ((r - g) / d + 4) / 6
  return [h, s, l]
}

function hslToRgb(h: number, s: number, l: number): [number, number, number] {
  if (s === 0) {
    const v = Math.round(l * 255)
    return [v, v, v]
  }
  function hue2rgb(p: number, q: number, t: number) {
    if (t < 0) t += 1
    if (t > 1) t -= 1
    if (t < 1 / 6) return p + (q - p) * 6 * t
    if (t < 1 / 2) return q
    if (t < 2 / 3) return p + (q - p) * (2 / 3 - t) * 6
    return p
  }
  const q = l < 0.5 ? l * (1 + s) : l + s - l * s
  const p = 2 * l - q
  return [
    Math.round(hue2rgb(p, q, h + 1 / 3) * 255),
    Math.round(hue2rgb(p, q, h) * 255),
    Math.round(hue2rgb(p, q, h - 1 / 3) * 255),
  ]
}

function hexToRgb(hex: string): [number, number, number] {
  const v = parseInt(hex.slice(1), 16)
  return [(v >> 16) & 255, (v >> 8) & 255, v & 255]
}

function clamp(v: number, min: number, max: number): number {
  return v < min ? min : v > max ? max : v
}

function isDefault(adj: TextureAdjustment): boolean {
  return adj.hueShift === DEFAULT_ADJUSTMENT.hueShift
    && adj.saturation === DEFAULT_ADJUSTMENT.saturation
    && adj.brightness === DEFAULT_ADJUSTMENT.brightness
    && adj.tintStrength === DEFAULT_ADJUSTMENT.tintStrength
}

/**
 * Apply color adjustments to an image and return a new data URL.
 */
export function applyAdjustment(
  originalImage: HTMLImageElement | ImageBitmap,
  adj: TextureAdjustment,
): string {
  const w = originalImage.width
  const h = originalImage.height
  const canvas = document.createElement('canvas')
  canvas.width = w
  canvas.height = h
  const ctx = canvas.getContext('2d')!

  ctx.drawImage(originalImage, 0, 0)

  // Skip pixel manipulation if all defaults
  if (isDefault(adj)) {
    return canvas.toDataURL('image/png')
  }

  const imageData = ctx.getImageData(0, 0, w, h)
  const data = imageData.data

  const hueShift = adj.hueShift / 360
  const satMul = 1 + adj.saturation / 100
  const brightShift = adj.brightness / 100
  const tintRgb = hexToRgb(adj.tintColor)
  const tintStr = adj.tintStrength / 100

  for (let i = 0; i < data.length; i += 4) {
    let r = data[i], g = data[i + 1], b = data[i + 2]
    // Skip fully transparent pixels
    if (data[i + 3] === 0) continue

    let [h, s, l] = rgbToHsl(r, g, b)

    // Hue shift
    h = (h + hueShift + 1) % 1

    // Saturation
    s = clamp(s * satMul, 0, 1)

    // Brightness
    l = clamp(l + brightShift * 0.5, 0, 1)

    ;[r, g, b] = hslToRgb(h, s, l)

    // Tint blend
    if (tintStr > 0) {
      r = Math.round(r * (1 - tintStr) + tintRgb[0] * tintStr)
      g = Math.round(g * (1 - tintStr) + tintRgb[1] * tintStr)
      b = Math.round(b * (1 - tintStr) + tintRgb[2] * tintStr)
    }

    data[i] = r
    data[i + 1] = g
    data[i + 2] = b
  }

  ctx.putImageData(imageData, 0, 0)
  return canvas.toDataURL('image/png')
}

/**
 * Update a THREE.Texture's image from a data URL.
 */
export function updateThreeTexture(
  texture: THREE.Texture,
  dataUrl: string,
): Promise<void> {
  return new Promise((resolve) => {
    const img = new Image()
    img.onload = () => {
      texture.image = img
      texture.needsUpdate = true
      resolve()
    }
    img.src = dataUrl
  })
}
