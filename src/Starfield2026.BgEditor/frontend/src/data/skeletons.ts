/**
 * Game skeleton definitions — Rigify metarig templates.
 *
 * Each skeleton is an array of BoneData:
 *   { name, head: [x,y,z], tail: [x,y,z], roll, parent }
 *
 * Exported from Blender Rigify via MCP.
 * Blender units (meters), Z-up coordinate system.
 */

// JSON imports (Vite handles these natively)
import humanBones from '../assets/rigs/human.json'
import birdBones from '../assets/rigs/bird.json'
import catBones from '../assets/rigs/cat.json'
import horseBones from '../assets/rigs/horse.json'
import sharkBones from '../assets/rigs/shark.json'
import wolfBones from '../assets/rigs/wolf.json'
import basicHumanBones from '../assets/rigs/basic_human.json'
import basicQuadrupedBones from '../assets/rigs/basic_quadruped.json'

export interface BoneData {
    name: string
    head: [number, number, number]
    tail: [number, number, number]
    roll: number
    parent: string
}

export interface BoneCollectionDef {
    name: string
    color: string
    bones: string[]
}

/** All available rig template types */
export type RigTemplate =
    | 'human'
    | 'bird'
    | 'cat'
    | 'horse'
    | 'shark'
    | 'wolf'
    | 'basic_human'
    | 'basic_quadruped'

export const RIG_TEMPLATE_LABELS: Record<RigTemplate, string> = {
    human: 'Human',
    bird: 'Bird',
    cat: 'Cat',
    horse: 'Horse',
    shark: 'Shark',
    wolf: 'Wolf',
    basic_human: 'Basic Human',
    basic_quadruped: 'Basic Quadruped',
}

/** Game types (for game rig generation — separate from Rigify templates) */
export type GameType = 'SUNMOON' | 'SCARLET' | 'PZLA'

export const GAME_LABELS: Record<GameType, string> = {
    SUNMOON: 'Sun/Moon',
    SCARLET: 'Scarlet',
    PZLA: 'PZLA',
}

/** Look up rig template bone data */
export const RIG_TEMPLATES: Record<RigTemplate, BoneData[]> = {
    human: humanBones as BoneData[],
    bird: birdBones as BoneData[],
    cat: catBones as BoneData[],
    horse: horseBones as BoneData[],
    shark: sharkBones as BoneData[],
    wolf: wolfBones as BoneData[],
    basic_human: basicHumanBones as BoneData[],
    basic_quadruped: basicQuadrupedBones as BoneData[],
}

/**
 * Auto-detect bone collection groups from the skeleton.
 * Groups bones by common prefixes (L/R for limbs, spine, finger, etc.)
 */
export function detectBoneCollections(bones: BoneData[]): BoneCollectionDef[] {
    const groups: Record<string, string[]> = {}

    const PATTERNS: [RegExp, string, string][] = [
        [/^spine/i, 'Spine', '#4488ff'],
        [/^(pelvis|hips|waist|torso)/i, 'Torso', '#4488ff'],
        [/^(head|jaw|eye|ear|nose|brow|lip|tongue|tooth|chin|cheek|forehead)/i, 'Head', '#ff8844'],
        [/^(neck)/i, 'Neck', '#ff8844'],
        [/^(shoulder|upper_arm|forearm|hand)\.L/i, 'Left Arm', '#44cc44'],
        [/^(shoulder|upper_arm|forearm|hand)\.R/i, 'Right Arm', '#cc4444'],
        [/^(f_index|f_middle|f_ring|f_pinky|thumb)\.L/i, 'Left Fingers', '#44cc88'],
        [/^(f_index|f_middle|f_ring|f_pinky|thumb)\.R/i, 'Right Fingers', '#cc4488'],
        [/^(thigh|shin|foot|toe|heel)\.L/i, 'Left Leg', '#44aacc'],
        [/^(thigh|shin|foot|toe|heel)\.R/i, 'Right Leg', '#cc8844'],
        [/^breast/i, 'Torso', '#4488ff'],
        [/^palm/i, 'Hands', '#88cc44'],
        [/^tail/i, 'Tail', '#cc88ff'],
        [/^(wing|feather)/i, 'Wings', '#88ccff'],
    ]

    const assigned = new Set<string>()

    for (const bone of bones) {
        let matched = false
        for (const [pattern, group, color] of PATTERNS) {
            if (pattern.test(bone.name)) {
                if (!groups[group]) groups[group] = []
                groups[group].push(bone.name)
                if (!groups[`_color_${group}`]) (groups as any)[`_color_${group}`] = color
                assigned.add(bone.name)
                matched = true
                break
            }
        }
    }

    // Catch unassigned
    const unassigned = bones.filter(b => !assigned.has(b.name)).map(b => b.name)
    if (unassigned.length > 0) {
        groups['Other'] = unassigned
            ; (groups as any)['_color_Other'] = '#888888'
    }

    const collections: BoneCollectionDef[] = []
    for (const [name, boneNames] of Object.entries(groups)) {
        if (name.startsWith('_color_')) continue
        collections.push({
            name,
            color: (groups as any)[`_color_${name}`] || '#aaaaaa',
            bones: boneNames,
        })
    }

    return collections
}
