import { useEffect, useRef } from 'react'
import * as THREE from 'three'
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js'
import { useEditorStore } from '../store/editorStore'

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

  const storeScene = useEditorStore(s => s.scene)
  const storeAnimations = useEditorStore(s => s.animations)
  const animationPlaying = useEditorStore(s => s.animationPlaying)
  const activeClipIndex = useEditorStore(s => s.activeClipIndex)

  // Init renderer + camera once
  useEffect(() => {
    const container = containerRef.current
    if (!container) return

    const renderer = new THREE.WebGLRenderer({ antialias: true })
    renderer.setPixelRatio(window.devicePixelRatio)
    renderer.outputColorSpace = THREE.SRGBColorSpace
    renderer.setClearColor(0x1a1a2e)
    renderer.setSize(container.clientWidth, container.clientHeight)
    container.appendChild(renderer.domElement)
    rendererRef.current = renderer

    const camera = new THREE.PerspectiveCamera(
      50, // wider FOV for initial view; we'll fit to model
      container.clientWidth / container.clientHeight,
      0.1,
      10000,
    )
    camera.position.set(0, 40, 80)
    cameraRef.current = camera

    const controls = new OrbitControls(camera, renderer.domElement)
    controls.target.set(0, 0, 0)
    controls.enableDamping = true
    controls.dampingFactor = 0.1
    controls.update()
    controlsRef.current = controls

    // Lighting
    const ambientLight = new THREE.AmbientLight(0xffffff, 1.5)
    sceneRef.current.add(ambientLight)
    const dirLight = new THREE.DirectionalLight(0xffffff, 0.5)
    dirLight.position.set(10, 20, 10)
    sceneRef.current.add(dirLight)

    // Render loop
    function animate() {
      animFrameRef.current = requestAnimationFrame(animate)
      const delta = clockRef.current.getDelta()
      if (mixerRef.current) {
        mixerRef.current.update(delta)
      }
      controls.update()
      renderer.render(sceneRef.current, camera)
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
      controls.dispose()
      renderer.dispose()
      container.removeChild(renderer.domElement)
    }
  }, [])

  // Update scene when model changes -- auto-fit camera to bounds
  useEffect(() => {
    const threeScene = sceneRef.current
    const camera = cameraRef.current
    const controls = controlsRef.current

    // Clean up previous model and animation mixer
    if (modelGroupRef.current) {
      threeScene.remove(modelGroupRef.current)
      modelGroupRef.current = null
    }
    if (mixerRef.current) {
      mixerRef.current.stopAllAction()
      mixerRef.current = null
    }
    activeActionRef.current = null

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
            mat.map.needsUpdate = true
            console.log(`[BgEditor]   Texture: ${mat.map.name || '(unnamed)'}, image:`, mat.map.image ? `${mat.map.image.width}x${mat.map.image.height}` : 'NULL')
          } else {
            console.log(`[BgEditor]   Material without texture:`, mat.type, mat)
          }
        }
      }
    })
    console.log(`[BgEditor] ${meshCount} meshes, ${texturedCount} textured materials, ${boneCount} bones`)

    // Compute bounding box and auto-fit camera
    const box = new THREE.Box3().setFromObject(storeScene)
    if (!box.isEmpty() && camera && controls) {
      const center = box.getCenter(new THREE.Vector3())
      const size = box.getSize(new THREE.Vector3())
      const maxDim = Math.max(size.x, size.y, size.z)

      console.log(`[BgEditor] Bounds: min(${box.min.x.toFixed(1)}, ${box.min.y.toFixed(1)}, ${box.min.z.toFixed(1)}) max(${box.max.x.toFixed(1)}, ${box.max.y.toFixed(1)}, ${box.max.z.toFixed(1)})`)
      console.log(`[BgEditor] Center: (${center.x.toFixed(1)}, ${center.y.toFixed(1)}, ${center.z.toFixed(1)}), maxDim: ${maxDim.toFixed(1)}`)

      // Position camera to see the whole model
      const fov = camera.fov * (Math.PI / 180)
      const distance = (maxDim / 2) / Math.tan(fov / 2) * 1.5
      camera.position.set(center.x, center.y + maxDim * 0.3, center.z + distance)
      camera.near = distance * 0.01
      camera.far = distance * 10
      camera.updateProjectionMatrix()

      controls.target.copy(center)
      controls.update()
    }

    // Set up animation playback
    if (storeAnimations && storeAnimations.length > 0) {
      console.log(`[BgEditor] Setting up AnimationMixer with ${storeAnimations.length} clip(s)`)
      const mixer = new THREE.AnimationMixer(storeScene)
      mixerRef.current = mixer
      clockRef.current.start()

      // Debug: log track names for first clip
      const firstClip = storeAnimations[0]
      console.log(`[BgEditor]   First clip: "${firstClip.name}" (${firstClip.duration.toFixed(2)}s, ${firstClip.tracks.length} tracks)`)
      for (const track of firstClip.tracks.slice(0, 10)) {
        console.log(`[BgEditor]     Track: ${track.name} (${track.times.length} keyframes)`)
      }
      if (firstClip.tracks.length > 10) {
        console.log(`[BgEditor]     ... and ${firstClip.tracks.length - 10} more tracks`)
      }

      // Play the active clip
      const clipIdx = Math.min(activeClipIndex, storeAnimations.length - 1)
      const clip = storeAnimations[clipIdx]
      const action = mixer.clipAction(clip)
      action.setLoop(THREE.LoopRepeat, Infinity)
      if (!animationPlaying) {
        action.paused = true
      }
      action.play()
      activeActionRef.current = action
    }
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

  return (
    <div
      ref={containerRef}
      style={{
        flex: 1,
        position: 'relative',
        overflow: 'hidden',
      }}
    />
  )
}
