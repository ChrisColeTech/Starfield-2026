import { useEffect, useRef } from 'react';
import * as THREE from 'three';
import { ColladaLoader } from 'three/examples/jsm/loaders/ColladaLoader.js';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import type { ModelInfo } from '../../types';

interface DaeViewportProps {
    folderPath: string;
    clipName: string;
    showWireframe: boolean;
    showSkeleton: boolean;
    showGrid: boolean;
    showTextures: boolean;
    lightIntensity: number;
    onModelLoaded?: (info: ModelInfo) => void;
}

export function DaeViewport({
    folderPath,
    clipName,
    showWireframe,
    showSkeleton,
    showGrid,
    showTextures,
    lightIntensity,
    onModelLoaded,
}: DaeViewportProps) {
    const containerRef = useRef<HTMLDivElement>(null);
    const stateRef = useRef<{
        renderer: THREE.WebGLRenderer | null;
        scene: THREE.Scene;
        camera: THREE.PerspectiveCamera;
        controls: OrbitControls | null;
        mixer: THREE.AnimationMixer | null;
        clock: THREE.Clock;
        animId: number;
        model: THREE.Object3D | null;
        gridHelper: THREE.GridHelper | null;
        skeletonHelper: THREE.SkeletonHelper | null;
    }>({
        renderer: null,
        scene: new THREE.Scene(),
        camera: new THREE.PerspectiveCamera(45, 1, 0.1, 1000),
        controls: null,
        mixer: null,
        clock: new THREE.Clock(),
        animId: 0,
        model: null,
        gridHelper: null,
        skeletonHelper: null,
    });

    // Initialize renderer, controls, lights, grid
    useEffect(() => {
        const container = containerRef.current;
        if (!container) return;

        const s = stateRef.current;

        // Renderer
        const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
        renderer.setPixelRatio(window.devicePixelRatio);
        renderer.setClearColor(0x1e1e1e, 1);
        renderer.setSize(container.clientWidth, container.clientHeight);
        renderer.toneMapping = THREE.ACESFilmicToneMapping;
        renderer.toneMappingExposure = 1.2;
        container.appendChild(renderer.domElement);
        s.renderer = renderer;

        // Camera
        s.camera.position.set(3, 3, 5);
        s.camera.lookAt(0, 1, 0);

        // Controls
        const controls = new OrbitControls(s.camera, renderer.domElement);
        controls.target.set(0, 1, 0);
        controls.enableDamping = true;
        controls.dampingFactor = 0.1;
        controls.update();
        s.controls = controls;

        // Lights — three-point setup + hemisphere for natural ambient
        const hemi = new THREE.HemisphereLight(0x8899bb, 0x443322, 0.8); // sky blue / warm ground
        s.scene.add(hemi);

        const keyLight = new THREE.DirectionalLight(0xffffff, 1.2); // main light
        keyLight.position.set(5, 8, 5);
        s.scene.add(keyLight);

        const fillLight = new THREE.DirectionalLight(0x8888cc, 0.4); // soft cool fill
        fillLight.position.set(-4, 4, -3);
        s.scene.add(fillLight);

        const rimLight = new THREE.DirectionalLight(0xffffff, 0.3); // edge highlight
        rimLight.position.set(0, 6, -8);
        s.scene.add(rimLight);

        // Grid
        const grid = new THREE.GridHelper(20, 20, 0x444444, 0x333333);
        s.scene.add(grid);
        s.gridHelper = grid;

        // Animate loop
        const animate = () => {
            s.animId = requestAnimationFrame(animate);
            const delta = s.clock.getDelta();
            if (s.mixer) s.mixer.update(delta);
            if (s.controls) s.controls.update();
            renderer.render(s.scene, s.camera);
        };
        animate();

        // Resize observer
        const ro = new ResizeObserver(() => {
            const w = container.clientWidth;
            const h = container.clientHeight;
            if (w && h) {
                renderer.setSize(w, h);
                s.camera.aspect = w / h;
                s.camera.updateProjectionMatrix();
            }
        });
        ro.observe(container);

        return () => {
            cancelAnimationFrame(s.animId);
            ro.disconnect();
            controls.dispose();
            renderer.dispose();
            if (container.contains(renderer.domElement)) {
                container.removeChild(renderer.domElement);
            }
        };
    }, []);

    // Load DAE model when clip changes
    useEffect(() => {
        if (!clipName || !folderPath) return;

        const s = stateRef.current;

        // Clear previous model
        if (s.model) {
            s.scene.remove(s.model);
            s.model = null;
        }
        if (s.skeletonHelper) {
            s.scene.remove(s.skeletonHelper);
            s.skeletonHelper = null;
        }
        if (s.mixer) {
            s.mixer.stopAllAction();
            s.mixer = null;
        }

        const loader = new ColladaLoader();
        // Load via API proxy
        const daeUrl = `/api/fs/serve-dae?folder=${encodeURIComponent(folderPath)}&file=${encodeURIComponent(clipName)}`;

        loader.load(
            daeUrl,
            (collada) => {
                const model = collada.scene;

                // Auto-center and scale
                const box = new THREE.Box3().setFromObject(model);
                const size = box.getSize(new THREE.Vector3());
                const center = box.getCenter(new THREE.Vector3());
                const maxDim = Math.max(size.x, size.y, size.z);
                const scale = maxDim > 0 ? 3 / maxDim : 1;
                model.scale.setScalar(scale);
                model.position.sub(center.multiplyScalar(scale));
                model.position.y -= box.min.y * scale; // sit on ground

                s.scene.add(model);
                s.model = model;

                // Count model info
                let vertices = 0, faces = 0, bones = 0;
                const materialSet = new Set<string>();
                model.traverse((child) => {
                    if (child instanceof THREE.Mesh) {
                        const geo = child.geometry;
                        if (geo.index) {
                            faces += geo.index.count / 3;
                        } else if (geo.attributes.position) {
                            faces += geo.attributes.position.count / 3;
                        }
                        if (geo.attributes.position) {
                            vertices += geo.attributes.position.count;
                        }
                        const mats = Array.isArray(child.material) ? child.material : [child.material];
                        mats.forEach((m) => materialSet.add(m.name || m.uuid));
                    }
                    if (child instanceof THREE.Bone) bones++;
                });

                // Skeleton helper
                const skeleton = new THREE.SkeletonHelper(model);
                s.scene.add(skeleton);
                s.skeletonHelper = skeleton;
                skeleton.visible = showSkeleton;

                // Animation
                const clips = collada.scene.animations?.length
                    ? collada.scene.animations
                    : (collada as any).animations ?? [];
                const clipNames: string[] = [];

                if (clips.length > 0) {
                    const mixer = new THREE.AnimationMixer(model);
                    s.mixer = mixer;
                    clips.forEach((clip: THREE.AnimationClip) => {
                        clipNames.push(clip.name || 'default');
                        const action = mixer.clipAction(clip);
                        action.play();
                    });
                }

                onModelLoaded?.({
                    vertices,
                    faces: Math.floor(faces),
                    bones,
                    materials: materialSet.size,
                    clips: clipNames,
                });
            },
            undefined,
            (err) => {
                console.error('Failed to load DAE:', err);
            },
        );
    }, [clipName, folderPath]);

    // Toggle grid visibility
    useEffect(() => {
        const s = stateRef.current;
        if (s.gridHelper) s.gridHelper.visible = showGrid;
    }, [showGrid]);

    // Toggle skeleton visibility
    useEffect(() => {
        const s = stateRef.current;
        if (s.skeletonHelper) s.skeletonHelper.visible = showSkeleton;
    }, [showSkeleton]);

    // Toggle wireframe
    useEffect(() => {
        const s = stateRef.current;
        if (!s.model) return;
        s.model.traverse((child) => {
            if (child instanceof THREE.Mesh) {
                const mats = Array.isArray(child.material) ? child.material : [child.material];
                mats.forEach((m) => { m.wireframe = showWireframe; });
            }
        });
    }, [showWireframe]);

    // Toggle textures
    useEffect(() => {
        const s = stateRef.current;
        if (!s.model) return;
        s.model.traverse((child) => {
            if (child instanceof THREE.Mesh) {
                const mats = Array.isArray(child.material) ? child.material : [child.material];
                mats.forEach((m) => {
                    if (m.map) m.map.visible = showTextures;
                    m.needsUpdate = true;
                });
            }
        });
    }, [showTextures]);

    // Lighting intensity
    useEffect(() => {
        const s = stateRef.current;
        if (s.renderer) {
            s.renderer.toneMappingExposure = lightIntensity;
        }
    }, [lightIntensity]);

    return (
        <div
            ref={containerRef}
            className="w-full h-full"
            style={{ minHeight: 200 }}
        />
    );
}
