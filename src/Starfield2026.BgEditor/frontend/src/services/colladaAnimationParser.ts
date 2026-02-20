/**
 * Custom Collada animation parser for DAE files that use per-axis
 * rotation/translation channels (e.g., "BoneId/rotation.X").
 *
 * Three.js ColladaLoader only handles 'matrix' type animation transforms.
 * These Pokemon 3D DAE files export per-axis Euler angle rotations and
 * per-axis translations, which the built-in parser silently skips.
 *
 * This parser:
 * 1. Fetches the raw DAE XML
 * 2. Parses all <animation> elements to extract per-bone per-axis keyframes
 * 3. Groups channels by bone ID
 * 4. For each bone, merges time arrays and builds quaternion + position tracks
 * 5. Maps Collada node IDs to Three.js bone names (which use the Collada 'sid')
 * 6. Returns an AnimationClip ready for use with AnimationMixer
 */

import * as THREE from 'three'

const COLLADA_NS = 'http://www.collada.org/2005/11/COLLADASchema'

interface ChannelData {
  times: number[]
  values: number[]
}

interface BoneChannels {
  rotationX?: ChannelData
  rotationY?: ChannelData
  rotationZ?: ChannelData
  translationX?: ChannelData
  translationY?: ChannelData
  translationZ?: ChannelData
}

/** Helper: getElementsByTagNameNS with fallback to non-NS query */
function getElements(parent: Element | Document, tagName: string): Element[] {
  // Try namespace-aware first
  let elems = parent.getElementsByTagNameNS(COLLADA_NS, tagName)
  if (elems.length === 0) {
    // Fallback: try without namespace (in case the DAE has no xmlns)
    elems = parent.getElementsByTagName(tagName)
  }
  return Array.from(elems)
}

/** Helper: find first child element with given tag name */
function getFirstChild(parent: Element, tagName: string): Element | null {
  for (let i = 0; i < parent.children.length; i++) {
    const child = parent.children[i]
    if (child.localName === tagName) return child
  }
  return null
}

/** Helper: find all direct child elements with given tag name */
function getDirectChildren(parent: Element, tagName: string): Element[] {
  const result: Element[] = []
  for (let i = 0; i < parent.children.length; i++) {
    const child = parent.children[i]
    if (child.localName === tagName) result.push(child)
  }
  return result
}

/**
 * Fetch the DAE XML and parse animations into Three.js AnimationClips.
 * The scene is needed to map Collada IDs to Three.js bone names.
 */
export async function parseColladaAnimations(
  daeUrl: string,
  scene: THREE.Group,
): Promise<THREE.AnimationClip[]> {
  const response = await fetch(daeUrl)
  const xmlText = await response.text()
  const parser = new DOMParser()
  const doc = parser.parseFromString(xmlText, 'text/xml')

  // Check for parse errors
  const parseError = doc.querySelector('parsererror')
  if (parseError) {
    console.error('[ColladaAnimParser] XML parse error:', parseError.textContent)
    return []
  }

  // Build map from Collada node ID -> Three.js bone name (sid)
  const idToName = buildIdToNameMap(doc)
  console.log(`[ColladaAnimParser] ID-to-name map: ${Object.keys(idToName).length} entries`)

  // Parse all animation channels, grouped by bone ID
  const boneChannelsMap = parseAnimationChannels(doc)
  const boneIds = Object.keys(boneChannelsMap)
  console.log(`[ColladaAnimParser] Found animation channels for ${boneIds.length} bone(s)`)

  if (boneIds.length === 0) return []

  // Build keyframe tracks for each bone
  const tracks: THREE.KeyframeTrack[] = []

  // Also find the Three.js bone objects by name for debug
  const boneByName = new Map<string, THREE.Bone>()
  scene.traverse((node) => {
    if (node instanceof THREE.Bone) {
      boneByName.set(node.name, node)
    }
  })
  console.log(`[ColladaAnimParser] Scene has ${boneByName.size} bone(s): ${Array.from(boneByName.keys()).join(', ')}`)

  for (const boneId of boneIds) {
    const boneName = idToName[boneId]
    if (!boneName) {
      console.warn(`[ColladaAnimParser] No name mapping for bone ID: ${boneId}`)
      continue
    }

    if (!boneByName.has(boneName)) {
      console.warn(`[ColladaAnimParser] Bone "${boneName}" (id: ${boneId}) not found in scene`)
    }

    const channels = boneChannelsMap[boneId]
    const boneTracks = buildBoneTracks(boneName, channels)
    tracks.push(...boneTracks)
  }

  if (tracks.length === 0) return []

  const clip = new THREE.AnimationClip('idle', -1, tracks)
  console.log(`[ColladaAnimParser] Created clip: "${clip.name}" (${clip.duration.toFixed(2)}s, ${clip.tracks.length} tracks)`)
  return [clip]
}

/**
 * Build a mapping from Collada node IDs to the name/sid used by Three.js.
 * In Collada: <node id="Waist_bone_id" name="Waist" sid="Waist" type="JOINT">
 * Three.js ColladaLoader sets bone.name = sid for JOINTs, or name otherwise.
 */
function buildIdToNameMap(doc: Document): Record<string, string> {
  const map: Record<string, string> = {}
  const nodes = getElements(doc, 'node')
  for (const node of nodes) {
    const id = node.getAttribute('id')
    const sid = node.getAttribute('sid')
    const name = node.getAttribute('name')
    const type = node.getAttribute('type')
    if (id) {
      // ColladaLoader uses sid for JOINTs, name for others
      if (type === 'JOINT' && sid) {
        map[id] = sid
      } else if (name) {
        map[id] = name
      }
    }
  }
  return map
}

/**
 * Parse all <animation> elements and group channels by bone ID.
 */
function parseAnimationChannels(doc: Document): Record<string, BoneChannels> {
  const result: Record<string, BoneChannels> = {}

  // Find the library_animations element
  const libAnims = getElements(doc, 'library_animations')
  if (libAnims.length === 0) {
    console.log('[ColladaAnimParser] No library_animations found')
    return result
  }

  // Get direct <animation> children of library_animations
  const animations = getDirectChildren(libAnims[0], 'animation')
  console.log(`[ColladaAnimParser] Found ${animations.length} <animation> elements`)

  for (const anim of animations) {
    // Find the channel element (direct child)
    const channel = getFirstChild(anim, 'channel')
    if (!channel) continue

    const target = channel.getAttribute('target')
    if (!target) continue

    // Parse target: "Waist_bone_id/rotation.X"
    const slashIdx = target.indexOf('/')
    if (slashIdx === -1) continue

    const boneId = target.substring(0, slashIdx)
    const property = target.substring(slashIdx + 1) // e.g., "rotation.X"

    // Parse the sampler to find input/output sources
    const sampler = getFirstChild(anim, 'sampler')
    if (!sampler) continue

    const inputs = getDirectChildren(sampler, 'input')
    let inputSourceId = ''
    let outputSourceId = ''
    for (const input of inputs) {
      const semantic = input.getAttribute('semantic')
      const source = input.getAttribute('source')
      if (!source) continue
      const sourceId = source.replace('#', '')
      if (semantic === 'INPUT') inputSourceId = sourceId
      else if (semantic === 'OUTPUT') outputSourceId = sourceId
    }

    if (!inputSourceId || !outputSourceId) continue

    // Get the float arrays from <source> elements within this <animation>
    const times = getFloatArrayById(anim, inputSourceId)
    const values = getFloatArrayById(anim, outputSourceId)
    if (!times || !values || times.length === 0) continue

    // Store channel data
    if (!result[boneId]) result[boneId] = {}
    const channels = result[boneId]

    switch (property) {
      case 'rotation.X': channels.rotationX = { times, values }; break
      case 'rotation.Y': channels.rotationY = { times, values }; break
      case 'rotation.Z': channels.rotationZ = { times, values }; break
      case 'translation.X': channels.translationX = { times, values }; break
      case 'translation.Y': channels.translationY = { times, values }; break
      case 'translation.Z': channels.translationZ = { times, values }; break
    }
  }

  return result
}

/**
 * Get the float array data from a <source> element by ID,
 * searching within the given context element.
 */
function getFloatArrayById(context: Element, sourceId: string): number[] | null {
  // Search for the <source> with matching id attribute
  const sources = getDirectChildren(context, 'source')
  let source: Element | null = null
  for (const s of sources) {
    if (s.getAttribute('id') === sourceId) {
      source = s
      break
    }
  }
  if (!source) return null

  const floatArray = getFirstChild(source, 'float_array')
  if (!floatArray?.textContent) return null

  return floatArray.textContent.trim().split(/\s+/).map(Number)
}

/**
 * Build Three.js keyframe tracks for a single bone.
 * Merges all per-axis channels into unified position + quaternion tracks.
 */
function buildBoneTracks(
  boneName: string,
  channels: BoneChannels,
): THREE.KeyframeTrack[] {
  const tracks: THREE.KeyframeTrack[] = []

  // Collect all unique time values across all channels for this bone
  const timeSet = new Set<number>()
  const allChannels = [
    channels.rotationX, channels.rotationY, channels.rotationZ,
    channels.translationX, channels.translationY, channels.translationZ,
  ]
  for (const ch of allChannels) {
    if (ch) ch.times.forEach(t => timeSet.add(t))
  }

  const times = Array.from(timeSet).sort((a, b) => a - b)
  if (times.length === 0) return tracks

  const hasRotation = channels.rotationX || channels.rotationY || channels.rotationZ
  const hasTranslation = channels.translationX || channels.translationY || channels.translationZ

  // Build quaternion track from Euler angles
  if (hasRotation) {
    const quaternionData: number[] = []
    const euler = new THREE.Euler(0, 0, 0, 'XYZ')
    const quat = new THREE.Quaternion()

    for (const t of times) {
      const rx = interpolateAt(channels.rotationX, t)
      const ry = interpolateAt(channels.rotationY, t)
      const rz = interpolateAt(channels.rotationZ, t)

      // The DAE files store Euler angles in radians
      euler.set(rx, ry, rz)
      quat.setFromEuler(euler)

      quaternionData.push(quat.x, quat.y, quat.z, quat.w)
    }

    tracks.push(new THREE.QuaternionKeyframeTrack(
      `${boneName}.quaternion`,
      times,
      quaternionData,
    ))
  }

  // Build position track from translation channels
  if (hasTranslation) {
    const positionData: number[] = []

    for (const t of times) {
      const tx = interpolateAt(channels.translationX, t)
      const ty = interpolateAt(channels.translationY, t)
      const tz = interpolateAt(channels.translationZ, t)
      positionData.push(tx, ty, tz)
    }

    tracks.push(new THREE.VectorKeyframeTrack(
      `${boneName}.position`,
      times,
      positionData,
    ))
  }

  return tracks
}

/**
 * Interpolate a channel value at a given time.
 * If the channel doesn't exist, returns 0.
 * Uses linear interpolation between keyframes.
 */
function interpolateAt(channel: ChannelData | undefined, t: number): number {
  if (!channel || channel.times.length === 0) return 0

  const { times, values } = channel

  // Before first keyframe
  if (t <= times[0]) return values[0]
  // After last keyframe
  if (t >= times[times.length - 1]) return values[values.length - 1]

  // Find bracketing keyframes
  for (let i = 0; i < times.length - 1; i++) {
    if (t >= times[i] && t <= times[i + 1]) {
      const frac = (t - times[i]) / (times[i + 1] - times[i])
      return values[i] + frac * (values[i + 1] - values[i])
    }
  }

  return values[values.length - 1]
}
