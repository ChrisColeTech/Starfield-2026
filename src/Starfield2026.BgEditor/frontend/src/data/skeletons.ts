/**
 * Game skeleton definitions.
 *
 * Each skeleton is an array of BoneData tuples:
 *   { name, head: [x,y,z], tail: [x,y,z], roll, parent }
 *
 * Data extracted from game model DAE files via Blender Collada import.
 * Centimeter scale, Y-up (object-level 90° X rotation handles Z-up display).
 */

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

// Body-part bone groups with display colors
export const BONE_COLLECTIONS: BoneCollectionDef[] = [
    { name: 'Root', color: '#888888', bones: ['tr0010_00', 'Origin'] },
    { name: 'Torso', color: '#4488ff', bones: ['Waist', 'Spine2', 'Spine3', 'Hips'] },
    { name: 'Head', color: '#ff8844', bones: ['Neck', 'Head'] },
    { name: 'Left Arm', color: '#44cc44', bones: ['LShoulder', 'LArm', 'LForeArm', 'LHand', 'LArmEX', 'LForeArmEX'] },
    { name: 'Right Arm', color: '#cc4444', bones: ['RShoulder', 'RArm', 'RForeArm', 'RHand', 'RArmEX', 'RForeArmEX', 'EffBall'] },
    { name: 'Left Fingers', color: '#44cc88', bones: ['LFingerA1', 'LFingerA2', 'LFingerA3', 'LFingerB1', 'LFingerB2', 'LFingerB3', 'LFingerC1', 'LFingerC2', 'LFingerC3', 'LFingerD1', 'LFingerD2', 'LFingerD3', 'LFingerE1', 'LFingerE2', 'LFingerE3'] },
    { name: 'Right Fingers', color: '#cc4488', bones: ['RFingerA1', 'RFingerA2', 'RFingerA3', 'RFingerB1', 'RFingerB2', 'RFingerB3', 'RFingerC1', 'RFingerC2', 'RFingerC3', 'RFingerD1', 'RFingerD2', 'RFingerD3', 'RFingerE1', 'RFingerE2', 'RFingerE3'] },
    { name: 'Left Leg', color: '#44aacc', bones: ['LThigh', 'LLeg', 'LFoot', 'LToe'] },
    { name: 'Right Leg', color: '#cc8844', bones: ['RThigh', 'RLeg', 'RFoot', 'RToe'] },
]

export type GameType = 'SUNMOON' | 'SCARLET' | 'PZLA'

export const GAME_LABELS: Record<GameType, string> = {
    SUNMOON: 'Sun/Moon',
    SCARLET: 'Scarlet',
    PZLA: 'PZLA',
}

// Sun/Moon trainer skeleton — tr0010_00/model.dae (59 bones)
export const SUNMOON: BoneData[] = [
    { name: 'tr0010_00', head: [-0.000000, 0.000000, -0.000000], tail: [0.000000, 0.009999, 0.000000], roll: 0.000000, parent: '' },
    { name: 'Origin', head: [-0.000000, 0.000000, -0.000000], tail: [0.000000, 0.009999, 0.000000], roll: 0.000000, parent: 'tr0010_00' },
    { name: 'Waist', head: [0.000000, 90.599998, 0.000000], tail: [0.000000, 90.610001, 0.000000], roll: 0.000000, parent: 'Origin' },
    { name: 'Spine2', head: [0.000004, 104.099998, 0.000000], tail: [0.000004, 104.110001, 0.000000], roll: 0.000000, parent: 'Waist' },
    { name: 'Spine3', head: [0.000008, 117.599998, 0.000001], tail: [0.000008, 117.610001, 0.000001], roll: 0.091436, parent: 'Spine2' },
    { name: 'Neck', head: [0.000014, 134.329987, -1.534000], tail: [0.000014, 134.339981, -1.534000], roll: -0.272693, parent: 'Spine3' },
    { name: 'Head', head: [0.000015, 139.260986, -0.155000], tail: [0.000015, 139.270981, -0.155000], roll: -0.000000, parent: 'Neck' },
    { name: 'LShoulder', head: [4.500012, 131.698990, -1.534001], tail: [4.500012, 131.708984, -1.534001], roll: 0.000000, parent: 'Spine3' },
    { name: 'LArm', head: [15.809010, 126.098984, -1.534004], tail: [15.809010, 126.108986, -1.534004], roll: 0.000000, parent: 'LShoulder' },
    { name: 'LForeArm', head: [38.409008, 126.098969, -1.534009], tail: [38.409008, 126.108971, -1.534009], roll: 0.000000, parent: 'LArm' },
    { name: 'LHand', head: [61.009007, 126.098961, -1.534013], tail: [61.009007, 126.108963, -1.534013], roll: 0.000000, parent: 'LForeArm' },
    { name: 'LFingerA1', head: [64.177895, 125.657959, 2.232842], tail: [64.177895, 125.667961, 2.232842], roll: -0.340824, parent: 'LHand' },
    { name: 'LFingerA2', head: [66.604050, 123.315628, 3.428895], tail: [66.604050, 123.325630, 3.428895], roll: -0.340824, parent: 'LFingerA1' },
    { name: 'LFingerA3', head: [69.030205, 120.973289, 4.624953], tail: [69.030205, 120.983292, 4.624953], roll: -0.340824, parent: 'LFingerA2' },
    { name: 'LFingerB1', head: [69.661705, 126.098953, 1.236580], tail: [69.661705, 126.108955, 1.236580], roll: 0.000000, parent: 'LHand' },
    { name: 'LFingerB2', head: [73.829208, 126.098953, 1.236579], tail: [73.829208, 126.108955, 1.236579], roll: 0.000000, parent: 'LFingerB1' },
    { name: 'LFingerB3', head: [76.716293, 126.098953, 1.236578], tail: [76.716293, 126.108955, 1.236578], roll: 0.000000, parent: 'LFingerB2' },
    { name: 'LFingerC1', head: [69.851105, 126.098953, -1.534015], tail: [69.851105, 126.108955, -1.534015], roll: 0.000000, parent: 'LHand' },
    { name: 'LFingerC2', head: [74.213600, 126.098953, -1.534016], tail: [74.213600, 126.108955, -1.534016], roll: 0.000000, parent: 'LFingerC1' },
    { name: 'LFingerC3', head: [77.235809, 126.098953, -1.534017], tail: [77.235809, 126.108955, -1.534017], roll: 0.000000, parent: 'LFingerC2' },
    { name: 'LFingerD1', head: [69.632195, 126.098953, -4.000994], tail: [69.632195, 126.108955, -4.000994], roll: 0.000000, parent: 'LHand' },
    { name: 'LFingerD2', head: [73.839294, 126.098953, -4.000995], tail: [73.839294, 126.108955, -4.000995], roll: 0.000000, parent: 'LFingerD1' },
    { name: 'LFingerD3', head: [76.753593, 126.098953, -4.000996], tail: [76.753593, 126.108955, -4.000996], roll: 0.000000, parent: 'LFingerD2' },
    { name: 'LFingerE1', head: [69.243896, 126.098953, -6.332159], tail: [69.243896, 126.108955, -6.332159], roll: 0.000000, parent: 'LHand' },
    { name: 'LFingerE2', head: [72.866302, 126.098953, -6.332160], tail: [72.866302, 126.108955, -6.332160], roll: 0.000000, parent: 'LFingerE1' },
    { name: 'LFingerE3', head: [75.639999, 126.098953, -6.332160], tail: [75.639999, 126.108955, -6.332160], roll: 0.000000, parent: 'LFingerE2' },
    { name: 'LForeArmEX', head: [60.999016, 126.098961, -1.534013], tail: [60.999016, 126.108963, -1.534013], roll: 0.000000, parent: 'LForeArm' },
    { name: 'LArmEX', head: [15.819010, 126.098984, -1.534004], tail: [15.819010, 126.108986, -1.534004], roll: 0.000000, parent: 'LArm' },
    { name: 'RShoulder', head: [-4.499988, 131.698990, -1.534001], tail: [-4.499988, 131.708984, -1.534001], roll: -0.000000, parent: 'Spine3' },
    { name: 'RArm', head: [-15.808987, 126.098991, -1.534004], tail: [-15.808987, 126.108994, -1.534004], roll: 0.000000, parent: 'RShoulder' },
    { name: 'RForeArm', head: [-38.408985, 126.098991, -1.534009], tail: [-38.408985, 126.108994, -1.534009], roll: 0.000000, parent: 'RArm' },
    { name: 'RHand', head: [-61.008999, 126.098999, -1.534014], tail: [-61.008999, 126.109001, -1.534014], roll: 0.000000, parent: 'RForeArm' },
    { name: 'RFingerA1', head: [-64.177895, 125.658035, 2.232846], tail: [-64.177895, 125.668037, 2.232846], roll: -0.340841, parent: 'RHand' },
    { name: 'RFingerA2', head: [-66.604218, 123.315880, 3.428954], tail: [-66.604218, 123.325882, 3.428954], roll: -0.340786, parent: 'RFingerA1' },
    { name: 'RFingerA3', head: [-69.030060, 120.973152, 4.624877], tail: [-69.030060, 120.983154, 4.624877], roll: -0.340822, parent: 'RFingerA2' },
    { name: 'RFingerB1', head: [-69.661697, 126.098999, 1.236585], tail: [-69.661697, 126.109001, 1.236585], roll: 0.000000, parent: 'RHand' },
    { name: 'RFingerB2', head: [-73.829201, 126.098999, 1.236584], tail: [-73.829201, 126.109001, 1.236584], roll: 0.000000, parent: 'RFingerB1' },
    { name: 'RFingerB3', head: [-76.716301, 126.098999, 1.236584], tail: [-76.716301, 126.109001, 1.236584], roll: 0.000000, parent: 'RFingerB2' },
    { name: 'RFingerC1', head: [-69.851097, 126.098999, -1.534015], tail: [-69.851097, 126.109001, -1.534015], roll: 0.000000, parent: 'RHand' },
    { name: 'RFingerC2', head: [-74.213600, 126.098999, -1.534016], tail: [-74.213600, 126.109001, -1.534016], roll: 0.000000, parent: 'RFingerC1' },
    { name: 'RFingerC3', head: [-77.235802, 126.098999, -1.534017], tail: [-77.235802, 126.109001, -1.534017], roll: 0.000000, parent: 'RFingerC2' },
    { name: 'RFingerD1', head: [-69.632187, 126.098999, -4.000995], tail: [-69.632187, 126.109001, -4.000995], roll: 0.000000, parent: 'RHand' },
    { name: 'RFingerD2', head: [-73.839287, 126.098999, -4.000996], tail: [-73.839287, 126.109001, -4.000996], roll: 0.000000, parent: 'RFingerD1' },
    { name: 'RFingerD3', head: [-76.753593, 126.098999, -4.000997], tail: [-76.753593, 126.109001, -4.000997], roll: 0.000000, parent: 'RFingerD2' },
    { name: 'RFingerE1', head: [-69.243896, 126.098999, -6.332156], tail: [-69.243896, 126.109001, -6.332156], roll: 0.000000, parent: 'RHand' },
    { name: 'RFingerE2', head: [-72.866287, 126.098999, -6.332156], tail: [-72.866287, 126.109001, -6.332156], roll: 0.000000, parent: 'RFingerE1' },
    { name: 'RFingerE3', head: [-75.639992, 126.098999, -6.332157], tail: [-75.639992, 126.109001, -6.332157], roll: 0.000000, parent: 'RFingerE2' },
    { name: 'EffBall', head: [-68.879829, 120.895973, -1.534015], tail: [-68.879829, 120.905975, -1.534015], roll: -0.000000, parent: 'RHand' },
    { name: 'RForeArmEX', head: [-60.998993, 126.098999, -1.534013], tail: [-60.998993, 126.109001, -1.534013], roll: 0.000000, parent: 'RForeArm' },
    { name: 'RArmEX', head: [-15.818986, 126.098991, -1.534004], tail: [-15.818986, 126.108994, -1.534004], roll: 0.000000, parent: 'RArm' },
    { name: 'Hips', head: [-0.000000, 90.589996, 0.000000], tail: [-0.000000, 90.599998, 0.000000], roll: 0.000000, parent: 'Waist' },
    { name: 'LThigh', head: [7.799993, 79.289978, 0.000000], tail: [7.799993, 79.299980, 0.000000], roll: 0.000000, parent: 'Hips' },
    { name: 'LLeg', head: [7.799971, 44.989986, 0.000000], tail: [7.799971, 44.999985, 0.000000], roll: 0.000000, parent: 'LThigh' },
    { name: 'LFoot', head: [7.799949, 10.690002, -0.000000], tail: [7.799949, 10.700002, -0.000000], roll: -1.570796, parent: 'LLeg' },
    { name: 'LToe', head: [7.799945, 4.454994, 11.099998], tail: [7.799945, 4.464993, 11.099998], roll: -1.570796, parent: 'LFoot' },
    { name: 'RThigh', head: [-7.800007, 79.289993, 0.000000], tail: [-7.800007, 79.299995, 0.000000], roll: 0.000000, parent: 'Hips' },
    { name: 'RLeg', head: [-7.800029, 44.990002, 0.000000], tail: [-7.800029, 45.000000, 0.000000], roll: 0.000000, parent: 'RThigh' },
    { name: 'RFoot', head: [-7.800052, 10.690002, 0.000000], tail: [-7.800052, 10.700002, 0.000000], roll: -1.570796, parent: 'RLeg' },
    { name: 'RToe', head: [-7.800056, 4.454994, 11.099998], tail: [-7.800056, 4.464993, 11.099998], roll: -1.570796, parent: 'RFoot' },
]

/** Look up skeleton by game type */
export const SKELETONS: Record<GameType, BoneData[]> = {
    SUNMOON: SUNMOON,
    SCARLET: [], // TODO: dump from Scarlet trainer DAE
    PZLA: [],    // TODO: dump from PZLA trainer DAE
}
