/**
 * SkeletonRenderer — renders bone data as lines + joint spheres in Three.js.
 *
 * Bones are drawn head→tail with extended tails (matching Blender's stick mode).
 * Joint spheres mark bone heads. Colors come from auto-detected bone collections.
 *
 * Coordinate conversion:
 *   Blender: X=right, Y=forward, Z=up
 *   Three.js: X=right, Y=up, Z=forward
 *   Mapping: three(x, y, z) = blender(x, z, -y)
 */

import * as THREE from 'three'
import type { BoneData } from '../data/skeletons'
import { detectBoneCollections } from '../data/skeletons'

const MAX_TAIL = 0.3  // Blender units (meters)
const LEAF_LENGTH = 0.05

/** Convert Blender Z-up to Three.js Y-up: (x, z, -y) */
function toY(p: [number, number, number]): THREE.Vector3 {
    return new THREE.Vector3(p[0], p[2], -p[1])
}

/** Extend stub tails toward first child (matching Blender's logic) — in Blender coords */
function computeExtendedBones(bones: BoneData[]): Array<BoneData & { displayTail: [number, number, number] }> {
    const byName = new Map(bones.map(b => [b.name, b]))
    const children = new Map<string, BoneData[]>()
    for (const b of bones) {
        if (b.parent) {
            const arr = children.get(b.parent) || []
            arr.push(b)
            children.set(b.parent, arr)
        }
    }

    return bones.map(bone => {
        const kids = children.get(bone.name)
        let displayTail: [number, number, number]

        if (kids && kids.length > 0) {
            const child = kids[0]
            const dx = child.head[0] - bone.head[0]
            const dy = child.head[1] - bone.head[1]
            const dz = child.head[2] - bone.head[2]
            const len = Math.sqrt(dx * dx + dy * dy + dz * dz)
            if (len > 0.001) {
                if (len > MAX_TAIL) {
                    const s = MAX_TAIL / len
                    displayTail = [bone.head[0] + dx * s, bone.head[1] + dy * s, bone.head[2] + dz * s]
                } else {
                    displayTail = [...child.head]
                }
            } else {
                displayTail = [...bone.tail]
            }
        } else {
            const parent = bone.parent ? byName.get(bone.parent) : undefined
            if (parent) {
                const dx = bone.head[0] - parent.head[0]
                const dy = bone.head[1] - parent.head[1]
                const dz = bone.head[2] - parent.head[2]
                const len = Math.sqrt(dx * dx + dy * dy + dz * dz)
                if (len > 0.001) {
                    const s = LEAF_LENGTH / len
                    displayTail = [bone.head[0] + dx * s, bone.head[1] + dy * s, bone.head[2] + dz * s]
                } else {
                    displayTail = [bone.head[0], bone.head[1], bone.head[2] + LEAF_LENGTH]
                }
            } else {
                displayTail = [bone.head[0], bone.head[1], bone.head[2] + LEAF_LENGTH]
            }
        }

        return { ...bone, displayTail }
    })
}

/**
 * Create a Three.js Group containing the skeleton visualization.
 * Blender data (Z-up) is converted to Three.js (Y-up) via toY().
 */
export function createSkeletonGroup(bones: BoneData[]): THREE.Group {
    const group = new THREE.Group()
    group.name = 'SkeletonRig'

    // Auto-detect collections and build color map
    const collections = detectBoneCollections(bones)
    const colorMap = new Map<string, string>()
    for (const col of collections) {
        for (const name of col.bones) {
            colorMap.set(name, col.color)
        }
    }

    const extBones = computeExtendedBones(bones)

    // Compute rig height from Blender Z (= up) for auto-scaling joint radius
    let minZ = Infinity, maxZ = -Infinity
    for (const b of bones) {
        minZ = Math.min(minZ, b.head[2])
        maxZ = Math.max(maxZ, b.head[2])
    }
    const rigHeight = maxZ - minZ
    const jointRadius = Math.max(0.005, rigHeight * 0.008)

    const sphereGeo = new THREE.SphereGeometry(jointRadius, 8, 6)

    for (const bone of extBones) {
        const hex = colorMap.get(bone.name) || '#aaaaaa'
        const color = new THREE.Color(hex)

        // Convert Blender coords → Three.js coords
        const headPos = toY(bone.head)
        const tailPos = toY(bone.displayTail)

        // Joint sphere at head
        const mat = new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.9 })
        const sphere = new THREE.Mesh(sphereGeo, mat)
        sphere.position.copy(headPos)
        sphere.userData.boneName = bone.name
        group.add(sphere)

        // Line from head to display tail
        const lineGeo = new THREE.BufferGeometry().setFromPoints([headPos, tailPos])
        const lineMat = new THREE.LineBasicMaterial({ color })
        const line = new THREE.Line(lineGeo, lineMat)
        line.userData.boneName = bone.name
        group.add(line)
    }

    return group
}

/**
 * Dispose all geometries and materials in a skeleton group.
 */
export function disposeSkeletonGroup(group: THREE.Group) {
    group.traverse(obj => {
        if (obj instanceof THREE.Mesh || obj instanceof THREE.Line) {
            obj.geometry.dispose()
            if (Array.isArray(obj.material)) {
                obj.material.forEach(m => m.dispose())
            } else {
                obj.material.dispose()
            }
        }
    })
    group.removeFromParent()
}
