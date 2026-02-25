import { useEffect, useRef, useCallback, useState } from 'react'
import * as THREE from 'three'
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js'
import { TransformControls } from 'three/examples/jsm/controls/TransformControls.js'
import { ViewHelper } from 'three/examples/jsm/helpers/ViewHelper.js'
import { useEditorStore } from '../store/editorStore'
import type { ViewportSettings } from '../store/editorStore'
import { DEFAULT_VIEWPORT } from '../store/editorStore'
import { createSkeletonGroup, disposeSkeletonGroup } from '../lib/SkeletonRenderer'
import { detectBoneCollections } from '../data/skeletons'

/** Convert spherical (azimuth/elevation/distance) + pan to camera position */
function sphericalToCartesian(vp: ViewportSettings) {
  const azRad = (vp.azimuth * Math.PI) / 180
  const elRad = (vp.elevation * Math.PI) / 180
  const x = vp.panX + vp.distance * Math.cos(elRad) * Math.sin(azRad)
  const y = vp.panY + vp.distance * Math.sin(elRad)
  const z = vp.panZ + vp.distance * Math.cos(elRad) * Math.cos(azRad)
  return { x, y, z, targetX: vp.panX, targetY: vp.panY, targetZ: vp.panZ }
}

/** Reverse: extract spherical coords from camera position + target */
function cartesianToSpherical(camera: THREE.PerspectiveCamera, target: THREE.Vector3): Partial<ViewportSettings> {
  const dx = camera.position.x - target.x
  const dy = camera.position.y - target.y
  const dz = camera.position.z - target.z
  const distance = Math.sqrt(dx * dx + dy * dy + dz * dz)
  const elevation = Math.asin(dy / distance) * (180 / Math.PI)
  const azimuth = Math.atan2(dx, dz) * (180 / Math.PI)
  return {
    azimuth: Math.round(azimuth * 100) / 100,
    elevation: Math.round(elevation * 100) / 100,
    distance: Math.round(distance * 100) / 100,
    panX: Math.round(target.x * 100) / 100,
    panY: Math.round(target.y * 100) / 100,
    panZ: Math.round(target.z * 100) / 100,
  }
}

export default function Viewport() {
  const containerRef = useRef<HTMLDivElement>(null)
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null)
  const sceneRef = useRef<THREE.Scene>(new THREE.Scene())
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null)
  const controlsRef = useRef<OrbitControls | null>(null)
  const modelGroupRef = useRef<THREE.Group | null>(null)
  const animFrameRef = useRef<number>(0)
  const mixerRef = useRef<THREE.AnimationMixer | null>(null)
  const clockRef = useRef<THREE.Clock>(new THREE.Clock())
  const activeActionRef = useRef<THREE.AnimationAction | null>(null)
  const keyLightRef = useRef<THREE.DirectionalLight | null>(null)
  const fillLightRef = useRef<THREE.DirectionalLight | null>(null)
  const rimLightRef = useRef<THREE.DirectionalLight | null>(null)
  const hemiLightRef = useRef<THREE.HemisphereLight | null>(null)
  const applyingRef = useRef(false)  // guard against feedback loops
  const raycasterRef = useRef(new THREE.Raycaster())
  const pointerRef = useRef(new THREE.Vector2())
  const pointerDownPos = useRef({ x: 0, y: 0 })
  const prevHighlightRef = useRef<Array<{ mesh: THREE.Mesh; origColor: THREE.Color; origScale: number }>>([]);
  const transformControlsRef = useRef<TransformControls | null>(null)
  const viewHelperRef = useRef<ViewHelper | null>(null)

  const storeScene = useEditorStore(s => s.scene)
  const storeAnimations = useEditorStore(s => s.animations)
  const animationPlaying = useEditorStore(s => s.animationPlaying)
  const activeClipIndex = useEditorStore(s => s.activeClipIndex)
  const skeleton = useEditorStore(s => s.skeleton)
  const viewport = useEditorStore(s => s.viewport)
  const selectedBone = useEditorStore(s => s.selectedBone)
  const transformMode = useEditorStore(s => s.transformMode)
  const showGrid = useEditorStore(s => s.showGrid)
  const showAxes = useEditorStore(s => s.showAxes)
  const hiddenCollections = useEditorStore(s => s.hiddenCollections)
  const selectedBones = useEditorStore(s => s.selectedBones)
  const skeletonGroupRef = useRef<THREE.Group | null>(null)
  const gridRef = useRef<THREE.GridHelper | null>(null)
  const axesRef = useRef<THREE.AxesHelper | null>(null)

  // Init renderer + camera once
  useEffect(() => {
    const container = containerRef.current
    if (!container) return

    const renderer = new THREE.WebGLRenderer({ antialias: true, preserveDrawingBuffer: true })
    renderer.setPixelRatio(window.devicePixelRatio)
    renderer.outputColorSpace = THREE.SRGBColorSpace
    renderer.setClearColor(0x1e1e1e)
    renderer.setSize(container.clientWidth, container.clientHeight)
    container.appendChild(renderer.domElement)
    rendererRef.current = renderer

      // Expose viewport capture for MCP screenshot tool
      ; (window as any).__captureViewport = () => {
        if (!rendererRef.current || !cameraRef.current) return null
        rendererRef.current.render(sceneRef.current, cameraRef.current)
        return rendererRef.current.domElement.toDataURL('image/png')
      }

    const camera = new THREE.PerspectiveCamera(
      DEFAULT_VIEWPORT.fov,
      container.clientWidth / container.clientHeight,
      0.1,
      1000,
    )
    const initPos = sphericalToCartesian(DEFAULT_VIEWPORT)
    camera.position.set(initPos.x, initPos.y, initPos.z)
    camera.lookAt(initPos.targetX, initPos.targetY, initPos.targetZ)
    cameraRef.current = camera

    const controls = new OrbitControls(camera, renderer.domElement)
    controls.target.set(initPos.targetX, initPos.targetY, initPos.targetZ)
    controls.enableDamping = true
    controls.dampingFactor = 0.1
    controls.update()
    controlsRef.current = controls

    // TransformControls for bone editing
    const tc = new TransformControls(camera, renderer.domElement)
    tc.setSize(0.6)
    sceneRef.current.add(tc.getHelper())
    transformControlsRef.current = tc
    // Disable orbit while dragging gizmo
    tc.addEventListener('dragging-changed', (event: any) => {
      controls.enabled = !event.value
    })

    // Keyboard shortcuts
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement) return
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'z') {
        e.preventDefault()
        if (e.shiftKey) useEditorStore.getState().redo()
        else useEditorStore.getState().undo()
        return
      }
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'y') {
        e.preventDefault()
        useEditorStore.getState().redo()
        return
      }
      switch (e.key.toLowerCase()) {
        case 'g': useEditorStore.getState().setTransformMode('translate'); break
        case 'r': useEditorStore.getState().setTransformMode('rotate'); break
        case 's': useEditorStore.getState().setTransformMode('scale'); break
        case 'escape': useEditorStore.getState().selectBone(null); break
        case 'delete': useEditorStore.getState().selectBone(null); break
      }
    }
    window.addEventListener('keydown', onKeyDown)

    // Grid + Axes
    const grid = new THREE.GridHelper(20, 20, 0x444444, 0x333333)
    sceneRef.current.add(grid)
    gridRef.current = grid

    const axes = new THREE.AxesHelper(5)
    axes.visible = false
    sceneRef.current.add(axes)
    axesRef.current = axes

    // Lights — three-point setup + hemisphere for natural ambient
    const hemi = new THREE.HemisphereLight(0x8899bb, 0x443322, 0.8)
    sceneRef.current.add(hemi)
    hemiLightRef.current = hemi

    const keyLight = new THREE.DirectionalLight(0xffffff, 1.2)
    keyLight.position.set(5, 8, 5)
    sceneRef.current.add(keyLight)
    keyLightRef.current = keyLight

    const fillLight = new THREE.DirectionalLight(0x8888cc, 0.4)
    fillLight.position.set(-4, 4, -3)
    sceneRef.current.add(fillLight)
    fillLightRef.current = fillLight

    const rimLight = new THREE.DirectionalLight(0xffffff, 0.3)
    rimLight.position.set(0, 6, -8)
    sceneRef.current.add(rimLight)
    rimLightRef.current = rimLight

    // Sync OrbitControls user interaction → store (debounced)
    let syncTimer: ReturnType<typeof setTimeout> | null = null
    controls.addEventListener('change', () => {
      if (applyingRef.current) return  // skip feedback from programmatic changes
      if (syncTimer) clearTimeout(syncTimer)
      syncTimer = setTimeout(() => {
        const spherical = cartesianToSpherical(camera, controls.target)
        applyingRef.current = true
        useEditorStore.getState().updateViewport(spherical)
        // Persist to electron-store
        const vp = useEditorStore.getState().viewport
          ; (window as any).electronAPI?.storeSet?.('viewport', vp)
        applyingRef.current = false
      }, 150)
    })

    // ── Bone click-to-select (raycasting) ──
    const onPointerDown = (e: PointerEvent) => {
      pointerDownPos.current = { x: e.clientX, y: e.clientY }
    }
    const onPointerUp = (e: PointerEvent) => {
      // Only treat as click if pointer didn't move much (avoid selecting on orbit drag)
      const dx = e.clientX - pointerDownPos.current.x
      const dy = e.clientY - pointerDownPos.current.y
      if (Math.sqrt(dx * dx + dy * dy) > 5) return

      const rect = container.getBoundingClientRect()
      pointerRef.current.x = ((e.clientX - rect.left) / rect.width) * 2 - 1
      pointerRef.current.y = -((e.clientY - rect.top) / rect.height) * 2 + 1

      raycasterRef.current.setFromCamera(pointerRef.current, camera)

      const skGroup = skeletonGroupRef.current
      if (!skGroup) {
        useEditorStore.getState().selectBone(null)
        return
      }

      // Raycast against skeleton spheres (Mesh children only)
      const meshes = skGroup.children.filter((c): c is THREE.Mesh => c instanceof THREE.Mesh)
      const hits = raycasterRef.current.intersectObjects(meshes, false)
      if (hits.length > 0) {
        const boneName = hits[0].object.userData.boneName as string
        if (boneName) {
          useEditorStore.getState().selectBone(boneName)
          return
        }
      }
      // Click empty → deselect
      useEditorStore.getState().selectBone(null)
    }
    renderer.domElement.addEventListener('pointerdown', onPointerDown)
    renderer.domElement.addEventListener('pointerup', onPointerUp)

    // ── Box select (shift+drag) ──
    let boxStart: { x: number; y: number } | null = null
    let boxDiv: HTMLDivElement | null = null

    const onBoxDown = (e: PointerEvent) => {
      if (!e.shiftKey) return
      boxStart = { x: e.clientX, y: e.clientY }
      // Create selection rect overlay
      boxDiv = document.createElement('div')
      boxDiv.style.cssText = 'position:fixed;border:1px solid #6366f1;background:rgba(99,102,241,0.1);pointer-events:none;z-index:9999;'
      document.body.appendChild(boxDiv)
      e.preventDefault()
    }
    const onBoxMove = (e: PointerEvent) => {
      if (!boxStart || !boxDiv) return
      const x = Math.min(boxStart.x, e.clientX)
      const y = Math.min(boxStart.y, e.clientY)
      const w = Math.abs(e.clientX - boxStart.x)
      const h = Math.abs(e.clientY - boxStart.y)
      boxDiv.style.left = x + 'px'
      boxDiv.style.top = y + 'px'
      boxDiv.style.width = w + 'px'
      boxDiv.style.height = h + 'px'
    }
    const onBoxUp = (e: PointerEvent) => {
      if (!boxStart || !boxDiv) return
      const rectLeft = Math.min(boxStart.x, e.clientX)
      const rectTop = Math.min(boxStart.y, e.clientY)
      const rectRight = Math.max(boxStart.x, e.clientX)
      const rectBottom = Math.max(boxStart.y, e.clientY)
      boxDiv.remove()
      boxDiv = null

      if (rectRight - rectLeft < 5 && rectBottom - rectTop < 5) {
        boxStart = null
        return  // Too small, not a drag
      }

      // Project bone positions to screen and find those inside the rect
      const skGroup = skeletonGroupRef.current
      if (!skGroup || !camera) { boxStart = null; return }

      const containerRect = container.getBoundingClientRect()
      const selected: string[] = []

      skGroup.children.forEach(child => {
        if (!(child instanceof THREE.Mesh)) return
        const boneName = child.userData.boneName as string
        if (!boneName) return

        const pos = new THREE.Vector3()
        child.getWorldPosition(pos)
        pos.project(camera)

        // Convert NDC to screen coords
        const sx = (pos.x * 0.5 + 0.5) * containerRect.width + containerRect.left
        const sy = (-pos.y * 0.5 + 0.5) * containerRect.height + containerRect.top

        if (sx >= rectLeft && sx <= rectRight && sy >= rectTop && sy <= rectBottom) {
          selected.push(boneName)
        }
      })

      if (selected.length > 0) {
        useEditorStore.getState().addToSelection(selected)
      }
      boxStart = null
    }
    renderer.domElement.addEventListener('pointerdown', onBoxDown)
    window.addEventListener('pointermove', onBoxMove)
    window.addEventListener('pointerup', onBoxUp)

    // ViewHelper (orientation cube) — rendered in corner
    const viewHelper = new ViewHelper(camera, renderer.domElement)
    viewHelperRef.current = viewHelper
    const onViewHelperClick = (e: PointerEvent) => {
      viewHelper.handleClick(e)
    }
    renderer.domElement.addEventListener('pointerup', onViewHelperClick)

    // Render loop
    function animate() {
      animFrameRef.current = requestAnimationFrame(animate)
      const delta = clockRef.current.getDelta()
      if (mixerRef.current) {
        mixerRef.current.update(delta)
      }
      controls.update()
      renderer.render(sceneRef.current, camera)
      // Render orientation cube as overlay (needs separate pass)
      if (viewHelper.animating) viewHelper.update(delta)
      renderer.autoClear = false
      renderer.clearDepth()
      viewHelper.render(renderer)
      renderer.autoClear = true
    }
    animate()

    // Resize handler
    const ro = new ResizeObserver(() => {
      const w = container.clientWidth
      const h = container.clientHeight
      if (w === 0 || h === 0) return
      renderer.setSize(w, h)
      camera.aspect = w / h
      camera.updateProjectionMatrix()
    })
    ro.observe(container)

    return () => {
      cancelAnimationFrame(animFrameRef.current)
      ro.disconnect()
      renderer.domElement.removeEventListener('pointerdown', onPointerDown)
      renderer.domElement.removeEventListener('pointerup', onPointerUp)
      renderer.domElement.removeEventListener('pointerup', onViewHelperClick)
      renderer.domElement.removeEventListener('pointerdown', onBoxDown)
      window.removeEventListener('pointermove', onBoxMove)
      window.removeEventListener('pointerup', onBoxUp)
      window.removeEventListener('keydown', onKeyDown)
      tc.dispose()
      controls.dispose()
      renderer.dispose()
      container.removeChild(renderer.domElement)
    }
  }, [])

  // Hydrate viewport from electron-store on mount
  useEffect(() => {
    ; (window as any).electronAPI?.storeGet?.('viewport').then((saved: any) => {
      if (saved && typeof saved === 'object') {
        useEditorStore.getState().updateViewport(saved)
      }
    })
  }, [])

  // Highlight selected bone(s)
  useEffect(() => {
    // Restore all previous highlights
    for (const { mesh, origColor, origScale } of prevHighlightRef.current) {
      const mat = mesh.material as THREE.MeshBasicMaterial
      mat.color.copy(origColor)
      mesh.scale.setScalar(origScale)
    }
    prevHighlightRef.current = []

    if (selectedBones.size === 0 || !skeletonGroupRef.current) return

    const group = skeletonGroupRef.current

    // Highlight all selected bones
    for (const child of group.children) {
      if (!(child instanceof THREE.Mesh)) continue
      const boneName = child.userData.boneName as string
      if (!boneName || !selectedBones.has(boneName)) continue

      const mat = child.material as THREE.MeshBasicMaterial
      const isPrimary = boneName === selectedBone
      const origColor = mat.color.clone()
      const origScale = child.scale.x

      if (isPrimary) {
        child.scale.setScalar(origScale * 2.0)
        mat.color.set(0xffffff)
      } else {
        child.scale.setScalar(origScale * 1.5)
        mat.color.set(0xddddff)
      }
      prevHighlightRef.current.push({ mesh: child, origColor, origScale })
    }
  }, [selectedBone, selectedBones])

  // Attach/detach TransformControls to selected bone
  useEffect(() => {
    const tc = transformControlsRef.current
    if (!tc) return

    if (!selectedBone || !skeletonGroupRef.current) {
      tc.detach()
      return
    }

    // Find the sphere mesh for the selected bone
    const group = skeletonGroupRef.current
    for (const child of group.children) {
      if (child instanceof THREE.Mesh && child.userData.boneName === selectedBone) {
        tc.attach(child)
        return
      }
    }
    tc.detach()
  }, [selectedBone])

  // Update TransformControls mode
  useEffect(() => {
    const tc = transformControlsRef.current
    if (tc) tc.setMode(transformMode)
  }, [transformMode])

  // Grid / Axes visibility
  useEffect(() => {
    if (gridRef.current) gridRef.current.visible = showGrid
  }, [showGrid])

  useEffect(() => {
    if (axesRef.current) axesRef.current.visible = showAxes
  }, [showAxes])

  // Collection visibility — hide bones belonging to hidden collections
  useEffect(() => {
    const group = skeletonGroupRef.current
    const sk = useEditorStore.getState().skeleton
    if (!group || !sk) return

    // Build bone→collection map
    const collections = detectBoneCollections(sk)
    const boneToCollection = new Map<string, string>()
    for (const col of collections) {
      for (const bname of col.bones) boneToCollection.set(bname, col.name)
    }

    group.children.forEach(child => {
      const boneName = child.userData.boneName as string | undefined
      if (!boneName) return
      const col = boneToCollection.get(boneName)
      child.visible = !col || !hiddenCollections.has(col)
    })
  }, [hiddenCollections, skeleton])

  // Apply viewport state changes to Three.js camera/controls/lights
  useEffect(() => {
    const camera = cameraRef.current
    const controls = controlsRef.current
    const renderer = rendererRef.current
    if (!camera || !controls || !renderer) return

    applyingRef.current = true

    // Camera position from spherical coords
    const { x, y, z, targetX, targetY, targetZ } = sphericalToCartesian(viewport)
    camera.position.set(x, y, z)
    controls.target.set(targetX, targetY, targetZ)

    // FOV
    if (camera.fov !== viewport.fov) {
      camera.fov = viewport.fov
      camera.updateProjectionMatrix()
    }

    controls.update()

    // Lighting
    if (keyLightRef.current) keyLightRef.current.intensity = 1.2 * viewport.lightIntensity
    if (fillLightRef.current) fillLightRef.current.intensity = 0.4 * viewport.lightIntensity
    if (rimLightRef.current) rimLightRef.current.intensity = 0.3 * viewport.lightIntensity
    if (hemiLightRef.current) hemiLightRef.current.intensity = viewport.ambientIntensity

    // Background
    renderer.setClearColor(viewport.bgColor)

      // Persist programmatic changes to electron-store
      ; (window as any).electronAPI?.storeSet?.('viewport', viewport)

    // Release guard after a frame
    requestAnimationFrame(() => { applyingRef.current = false })
  }, [viewport])

  // Update scene when model changes -- auto-fit camera to bounds
  useEffect(() => {
    const threeScene = sceneRef.current
    const camera = cameraRef.current
    const controls = controlsRef.current

    // Clean up previous model
    if (modelGroupRef.current) {
      threeScene.remove(modelGroupRef.current)
      modelGroupRef.current = null
    }

    if (!storeScene) return

    threeScene.add(storeScene)
    modelGroupRef.current = storeScene

    // Debug: log scene hierarchy
    console.log('[BgEditor] Scene loaded. Children:', storeScene.children.length)
    let meshCount = 0
    let texturedCount = 0
    let boneCount = 0
    storeScene.traverse((node) => {
      if (node instanceof THREE.Bone) boneCount++
      if (node instanceof THREE.Mesh) {
        meshCount++
        const mats = Array.isArray(node.material) ? node.material : [node.material]
        for (const mat of mats) {
          // Enable double-sided rendering (game uses CullNone)
          mat.side = THREE.DoubleSide

          if ('map' in mat && mat.map) {
            texturedCount++
            mat.map.minFilter = THREE.NearestFilter
            mat.map.magFilter = THREE.NearestFilter
            if (mat.map.image) mat.map.needsUpdate = true
            console.log(`[BgEditor]   Texture: ${mat.map.name || '(unnamed)'}, image:`, mat.map.image ? `${mat.map.image.width}x${mat.map.image.height}` : 'NULL')
          }
        }
      }
    })
    console.log(`[BgEditor] ${meshCount} meshes, ${texturedCount} textured materials, ${boneCount} bones`)

    // Compute bounding box and auto-fit camera
    storeScene.updateMatrixWorld(true)
    let box: THREE.Box3
    try {
      box = new THREE.Box3().setFromObject(storeScene)
    } catch {
      box = new THREE.Box3()
      storeScene.traverse(n => {
        if (n instanceof THREE.Mesh) {
          n.geometry.computeBoundingBox()
          if (n.geometry.boundingBox) {
            box.expandByPoint(n.geometry.boundingBox.min.clone().applyMatrix4(n.matrixWorld))
            box.expandByPoint(n.geometry.boundingBox.max.clone().applyMatrix4(n.matrixWorld))
          }
        }
      })
    }
    if (!box.isEmpty() && camera && controls) {
      const center = box.getCenter(new THREE.Vector3())
      const size = box.getSize(new THREE.Vector3())
      const maxDim = Math.max(size.x, size.y, size.z)

      const fov = camera.fov * (Math.PI / 180)
      const distance = (maxDim / 2) / Math.tan(fov / 2) * 1.5
      camera.position.set(center.x, center.y + maxDim * 0.3, center.z + distance)
      camera.near = distance * 0.01
      camera.far = distance * 10
      camera.updateProjectionMatrix()

      controls.target.copy(center)
      controls.update()
    }
  }, [storeScene])

  // Set up animation playback when animations change
  useEffect(() => {
    // Clean up previous mixer
    if (mixerRef.current) {
      mixerRef.current.stopAllAction()
      mixerRef.current = null
    }
    activeActionRef.current = null

    if (!storeScene || !storeAnimations || storeAnimations.length === 0) return

    console.log(`[BgEditor] Setting up AnimationMixer with ${storeAnimations.length} clip(s)`)
    const mixer = new THREE.AnimationMixer(storeScene)
    mixerRef.current = mixer
    clockRef.current.start()

    const firstClip = storeAnimations[0]
    console.log(`[BgEditor]   Clip: "${firstClip.name}" (${firstClip.duration.toFixed(2)}s, ${firstClip.tracks.length} tracks)`)

    const clipIdx = Math.min(activeClipIndex, storeAnimations.length - 1)
    const clip = storeAnimations[clipIdx]
    const action = mixer.clipAction(clip)
    action.setLoop(THREE.LoopRepeat, Infinity)
    if (!animationPlaying) {
      action.paused = true
    }
    action.play()
    activeActionRef.current = action
  }, [storeScene, storeAnimations])

  // Respond to play/pause changes
  useEffect(() => {
    const action = activeActionRef.current
    if (!action) return
    action.paused = !animationPlaying
  }, [animationPlaying])

  // Respond to active clip changes
  useEffect(() => {
    const mixer = mixerRef.current
    if (!mixer || !storeAnimations || storeAnimations.length === 0) return

    const clipIdx = Math.min(activeClipIndex, storeAnimations.length - 1)
    const clip = storeAnimations[clipIdx]
    if (!clip) return

    // Stop current action
    if (activeActionRef.current) {
      activeActionRef.current.stop()
    }

    const action = mixer.clipAction(clip)
    action.setLoop(THREE.LoopRepeat, Infinity)
    if (!animationPlaying) {
      action.paused = true
    }
    action.play()
    activeActionRef.current = action
  }, [activeClipIndex])

  // Render skeleton when store.skeleton changes
  useEffect(() => {
    const threeScene = sceneRef.current
    const camera = cameraRef.current
    const controls = controlsRef.current

    // Clean up previous skeleton
    if (skeletonGroupRef.current) {
      disposeSkeletonGroup(skeletonGroupRef.current)
      threeScene.remove(skeletonGroupRef.current)
      skeletonGroupRef.current = null
    }

    if (!skeleton || skeleton.length === 0) return

    const group = createSkeletonGroup(skeleton)
    threeScene.add(group)
    skeletonGroupRef.current = group

    // Auto-fit camera: frame based on rig height, position at front ¾ view
    group.updateMatrixWorld(true)
    const box = new THREE.Box3().setFromObject(group)
    if (!box.isEmpty() && camera && controls) {
      const center = box.getCenter(new THREE.Vector3())
      const size = box.getSize(new THREE.Vector3())
      // Use height (Y) for framing distance instead of max-dim (avoids arm-span distortion)
      const height = size.y
      const fov = camera.fov * (Math.PI / 180)
      const distance = (height / 2) / Math.tan(fov / 2) * 1.8

      // Front ¾ view: slightly right + elevated
      camera.position.set(
        center.x + distance * 0.3,
        center.y + height * 0.15,
        center.z + distance
      )
      camera.near = 0.01
      camera.far = distance * 10
      camera.updateProjectionMatrix()

      controls.target.set(center.x, center.y, center.z)
      controls.update()
    }

    console.log(`[BgEditor] Skeleton rendered: ${skeleton.length} bones`)
  }, [skeleton])

  // Listen for viewport:resetView custom event (from AutoRigPanel)
  useEffect(() => {
    const handler = () => {
      const camera = cameraRef.current
      const controls = controlsRef.current
      const group = skeletonGroupRef.current || modelGroupRef.current
      if (!camera || !controls || !group) return

      group.updateMatrixWorld(true)
      const box = new THREE.Box3().setFromObject(group)
      if (box.isEmpty()) return

      const center = box.getCenter(new THREE.Vector3())
      const size = box.getSize(new THREE.Vector3())
      const maxDim = Math.max(size.x, size.y, size.z)

      const fov = camera.fov * (Math.PI / 180)
      const distance = (maxDim / 2) / Math.tan(fov / 2) * 1.5
      camera.position.set(center.x, center.y, center.z + distance)
      camera.near = distance * 0.01
      camera.far = distance * 10
      camera.updateProjectionMatrix()

      controls.target.copy(center)
      controls.update()
    }
    window.addEventListener('viewport:resetView', handler)
    return () => window.removeEventListener('viewport:resetView', handler)
  }, [])

  return (
    <div
      ref={containerRef}
      className="w-full h-full"
      style={{ minHeight: 200 }}
    />
  )
}
