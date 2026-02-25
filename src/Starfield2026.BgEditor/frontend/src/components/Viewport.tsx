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
    renderer.setClearColor(0x1e1e1e)
    renderer.setSize(container.clientWidth, container.clientHeight)
    container.appendChild(renderer.domElement)
    rendererRef.current = renderer

    const camera = new THREE.PerspectiveCamera(
      45,
      container.clientWidth / container.clientHeight,
      0.1,
      1000,
    )
    camera.position.set(3, 3, 5)
    camera.lookAt(0, 1, 0)
    cameraRef.current = camera

    const controls = new OrbitControls(camera, renderer.domElement)
    controls.target.set(0, 1, 0)
    controls.enableDamping = true
    controls.dampingFactor = 0.1
    controls.update()
    controlsRef.current = controls

    // Grid
    const grid = new THREE.GridHelper(20, 20, 0x444444, 0x333333)
    sceneRef.current.add(grid)

    // Lights — three-point setup + hemisphere for natural ambient
    const hemi = new THREE.HemisphereLight(0x8899bb, 0x443322, 0.8)
    sceneRef.current.add(hemi)

    const keyLight = new THREE.DirectionalLight(0xffffff, 1.2)
    keyLight.position.set(5, 8, 5)
    sceneRef.current.add(keyLight)

    const fillLight = new THREE.DirectionalLight(0x8888cc, 0.4)
    fillLight.position.set(-4, 4, -3)
    sceneRef.current.add(fillLight)

    const rimLight = new THREE.DirectionalLight(0xffffff, 0.3)
    rimLight.position.set(0, 6, -8)
    sceneRef.current.add(rimLight)

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

  return (
    <div
      ref={containerRef}
      className="w-full h-full"
      style={{ minHeight: 200 }}
    />
  )
}
