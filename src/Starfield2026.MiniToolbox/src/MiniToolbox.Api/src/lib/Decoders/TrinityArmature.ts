import { Vector3, Vector4, Matrix4, MathQuaternion } from './Math.js';
import type { TRSKL, TRTransformNode, TRJointInfo } from '../Flatbuffers/TR/Model/index.js';
import * as fs from 'fs';
import * as path from 'path';

interface JointInfoJson {
    SegmentScaleCompensate: boolean;
    InfluenceSkinning: boolean;
    HasInverseBind: boolean;
    InverseBind: Matrix4;
}

interface JointInfoParseResult {
    JointInfos: JointInfoJson[];
    NodeJointInfoIds: number[];
    NodeNames: string[];
}

/**
 * Data-only skeleton/armature decoded from TRSKL.
 * Ported from gftool Armature.cs — all GL rendering code removed.
 */
export class TrinityArmature {
    public Bones: Bone[] = [];
    private _parentIndices: number[] = [];
    private _jointInfoToNode: number[] = [];
    private _skinningPaletteOffset: number = 0;

    public get ParentIndices(): readonly number[] {
        return this._parentIndices;
    }

    public get JointInfoCount(): number {
        return this._jointInfoToNode.length;
    }

    public get BoneMetaCount(): number {
        return 0;
    }

    constructor(skel: TRSKL, sourcePath?: string, useTrsklInverseBind: boolean = true) {
        this._skinningPaletteOffset = skel.SkinningPaletteOffset ?? 0;

        for (const transNode of skel.TransformNodes ?? []) {
            const bone = new Bone(transNode, false);
            this.Bones.push(bone);
            this._parentIndices.push(transNode.ParentNodeIndex);
        }

        this.ApplyJointInfoFromTrskl(skel);
        this.ApplyJointInfoFromJson(sourcePath);

        for (let i = 0; i < this.Bones.length; i++) {
            const parentIndex = this.Bones[i].ParentIndex;
            if (parentIndex >= 0 && parentIndex < this.Bones.length && parentIndex !== i) {
                this.Bones[parentIndex].AddChild(this.Bones[i]);
            }
        }

        this.UpdateRestParentMatrices();
        this.ComputeInverseBindMatrices(useTrsklInverseBind);
    }

    private ApplyJointInfoFromTrskl(skel: TRSKL): void {
        if (!skel.JointInfos || skel.JointInfos.length === 0 || !skel.TransformNodes || skel.TransformNodes.length === 0) {
            return;
        }

        this._jointInfoToNode = new Array(skel.JointInfos.length).fill(-1);

        const count = Math.min(this.Bones.length, skel.TransformNodes.length);
        for (let i = 0; i < count; i++) {
            const node = skel.TransformNodes[i];
            const jointId = node.JointInfoIndex;
            if (jointId < 0 || jointId >= skel.JointInfos.length) {
                continue;
            }

            this._jointInfoToNode[jointId] = i;
            TrinityArmature.ApplyTrsklJointInfoToBone(this.Bones[i], skel.JointInfos[jointId]);
        }
    }

    private static ApplyTrsklJointInfoToBone(bone: Bone, joint: TRJointInfo): void {
        bone.UseSegmentScaleCompensate = joint.SegmentScaleCompensate;
        bone.Skinning = joint.InfluenceSkinning;

        if (joint.InverseBindPoseMatrix != null) {
            bone.JointInverseBindWorld = TrinityArmature.CreateMatrixFromAxis(
                new Vector3(joint.InverseBindPoseMatrix.AxisX.X, joint.InverseBindPoseMatrix.AxisX.Y, joint.InverseBindPoseMatrix.AxisX.Z),
                new Vector3(joint.InverseBindPoseMatrix.AxisY.X, joint.InverseBindPoseMatrix.AxisY.Y, joint.InverseBindPoseMatrix.AxisY.Z),
                new Vector3(joint.InverseBindPoseMatrix.AxisZ.X, joint.InverseBindPoseMatrix.AxisZ.Y, joint.InverseBindPoseMatrix.AxisZ.Z),
                new Vector3(joint.InverseBindPoseMatrix.AxisW.X, joint.InverseBindPoseMatrix.AxisW.Y, joint.InverseBindPoseMatrix.AxisW.Z)
            );
            bone.HasJointInverseBind = true;
        }
    }

    private UpdateRestParentMatrices(): void {
        for (let i = 0; i < this.Bones.length; i++) {
            const bone = this.Bones[i];
            if (bone.ParentIndex >= 0 && bone.ParentIndex < this.Bones.length && bone.ParentIndex !== i) {
                bone.RestInvParentMatrix = Matrix4.Invert(this.Bones[bone.ParentIndex].RestLocalMatrix);
            } else {
                bone.RestInvParentMatrix = Matrix4.Identity;
            }
        }
    }

    private ComputeInverseBindMatrices(useTrsklInverseBind: boolean): void {
        if (this.Bones.length === 0) {
            return;
        }

        const bindWorld: Matrix4[] = new Array(this.Bones.length);
        const computed: boolean[] = new Array(this.Bones.length).fill(false);
        for (let i = 0; i < this.Bones.length; i++) {
            bindWorld[i] = this.ComputeBindWorld(i, useTrsklInverseBind, bindWorld, computed);
        }

        for (let i = 0; i < this.Bones.length; i++) {
            if (useTrsklInverseBind && this.Bones[i].HasJointInverseBind) {
                this.Bones[i].InverseBindWorld = this.Bones[i].JointInverseBindWorld;
            } else {
                this.Bones[i].InverseBindWorld = Matrix4.Invert(bindWorld[i]);
            }
        }
    }

    private ComputeBindWorld(index: number, useTrsklInverseBind: boolean, world: Matrix4[], computed: boolean[]): Matrix4 {
        if (computed[index]) {
            return world[index];
        }

        const bone = this.Bones[index];
        let local: Matrix4;
        if (useTrsklInverseBind && bone.HasJointInverseBind) {
            local = Matrix4.Invert(bone.JointInverseBindWorld);
        } else {
            local = bone.RestLocalMatrix;
        }

        if (bone.ParentIndex >= 0 && bone.ParentIndex < this.Bones.length && bone.ParentIndex !== index) {
            if (bone.UseSegmentScaleCompensate) {
                const parent = this.Bones[bone.ParentIndex];
                const invScale = new Vector3(
                    parent.RestScale.x !== 0 ? 1 / parent.RestScale.x : 1,
                    parent.RestScale.y !== 0 ? 1 / parent.RestScale.y : 1,
                    parent.RestScale.z !== 0 ? 1 / parent.RestScale.z : 1
                );
                const invScaleMat = Matrix4.CreateScale(invScale);
                local = Matrix4.Multiply(local, invScaleMat);
            }
            const parentWorld = this.ComputeBindWorld(bone.ParentIndex, useTrsklInverseBind, world, computed);
            world[index] = Matrix4.Multiply(local, parentWorld);
        } else {
            world[index] = local;
        }

        computed[index] = true;
        return world[index];
    }

    public BuildSkinningPalette(): number[] {
        if (this._jointInfoToNode == null || this._jointInfoToNode.length === 0) {
            return [];
        }

        const palette = new Array(this._jointInfoToNode.length);
        for (let i = 0; i < palette.length; i++) {
            const nodeIndex = this._jointInfoToNode[i];
            palette[i] = nodeIndex >= 0 ? nodeIndex : 0;
        }
        return palette;
    }

    public MapJointInfoIndex(jointInfoIndex: number): number {
        if (jointInfoIndex < 0 || jointInfoIndex >= this._jointInfoToNode.length) {
            return 0;
        }
        const mapped = this._jointInfoToNode[jointInfoIndex];
        return mapped >= 0 ? mapped : 0;
    }

    public GetWorldMatrices(): Matrix4[] {
        const world = new Array(this.Bones.length);
        const computed = new Array(this.Bones.length).fill(false);
        for (let i = 0; i < this.Bones.length; i++) {
            world[i] = this.ComputeWorldMatrix(i, computed, world);
        }
        return world;
    }

    private ComputeWorldMatrix(index: number, computed: boolean[], world: Matrix4[]): Matrix4 {
        if (computed[index]) {
            return world[index];
        }

        const bone = this.Bones[index];
        const scaleMat = Matrix4.CreateScale(bone.Scale);
        const rotMat = Matrix4.CreateFromQuaternion(bone.Rotation);
        const transMat = Matrix4.CreateTranslation(bone.Position);
        let local = Matrix4.Multiply(Matrix4.Multiply(scaleMat, rotMat), transMat);

        if (bone.ParentIndex >= 0 && bone.ParentIndex < this.Bones.length && bone.ParentIndex !== index) {
            if (bone.UseSegmentScaleCompensate) {
                const parent = this.Bones[bone.ParentIndex];
                const invScale = new Vector3(
                    parent.Scale.x !== 0 ? 1 / parent.Scale.x : 1,
                    parent.Scale.y !== 0 ? 1 / parent.Scale.y : 1,
                    parent.Scale.z !== 0 ? 1 / parent.Scale.z : 1
                );
                const invScaleMat = Matrix4.CreateScale(invScale);
                local = Matrix4.Multiply(local, invScaleMat);
            }
            const parentWorld = this.ComputeWorldMatrix(bone.ParentIndex, computed, world);
            world[index] = Matrix4.Multiply(local, parentWorld);
        } else {
            world[index] = local;
        }

        computed[index] = true;
        return world[index];
    }

    public MapBoneMetaIndex(_boneMetaIndex: number): number {
        return 0;
    }

    private ApplyJointInfoFromJson(sourcePath?: string): void {
        const parseResult = TrinityArmature.LoadJointInfoFromJson(sourcePath);
        if (!parseResult || parseResult.JointInfos.length === 0) {
            return;
        }

        if (!this._jointInfoToNode || this._jointInfoToNode.length === 0) {
            this._jointInfoToNode = new Array(parseResult.JointInfos.length).fill(-1);
        } else if (parseResult.JointInfos.length > this._jointInfoToNode.length) {
            const oldLen = this._jointInfoToNode.length;
            const newArray = new Array(parseResult.JointInfos.length).fill(-1);
            for (let i = 0; i < oldLen; i++) {
                newArray[i] = this._jointInfoToNode[i];
            }
            this._jointInfoToNode = newArray;
        }

        if (parseResult.NodeNames.length === 0 || parseResult.NodeJointInfoIds.length === 0) {
            return;
        }

        const map = new Map<string, number>();
        const count = Math.min(parseResult.NodeNames.length, parseResult.NodeJointInfoIds.length);
        for (let i = 0; i < count; i++) {
            const name = parseResult.NodeNames[i];
            if (!name || name.trim().length === 0) {
                continue;
            }
            map.set(name.toLowerCase(), parseResult.NodeJointInfoIds[i]);
        }

        for (let i = 0; i < this.Bones.length; i++) {
            const bone = this.Bones[i];
            const jointId = map.get(bone.Name.toLowerCase());
            if (jointId === undefined) {
                continue;
            }

            if (jointId >= 0 && jointId < this._jointInfoToNode.length) {
                this._jointInfoToNode[jointId] = i;
            }

            TrinityArmature.ApplyJointInfoToBone(bone, parseResult, jointId);
        }
    }

    private static ApplyJointInfoToBone(bone: Bone, parseResult: JointInfoParseResult, jointId: number): void {
        if (jointId < 0 || jointId >= parseResult.JointInfos.length) {
            return;
        }

        const joint = parseResult.JointInfos[jointId];
        bone.UseSegmentScaleCompensate = joint.SegmentScaleCompensate;
        if (joint.HasInverseBind) {
            bone.JointInverseBindWorld = joint.InverseBind;
            bone.HasJointInverseBind = true;
        }
        bone.Skinning = joint.InfluenceSkinning;
    }

    private static LoadJointInfoFromJson(sourcePath?: string): JointInfoParseResult | null {
        const jsonPath = TrinityArmature.ResolveTrsklJsonPath(sourcePath);
        if (!jsonPath || !fs.existsSync(jsonPath)) {
            return null;
        }

        try {
            let text = fs.readFileSync(jsonPath, 'utf-8');
            text = text.replace(/\b([A-Za-z_][A-Za-z0-9_]*)\b\s*:/g, '"$1":');
            const root = JSON.parse(text);

            const jointInfos: JointInfoJson[] = [];
            const jointInfoList = root.joint_info_list;
            if (Array.isArray(jointInfoList)) {
                for (const entry of jointInfoList) {
                    const info: JointInfoJson = {
                        SegmentScaleCompensate: entry.segment_scale_compensate === true,
                        InfluenceSkinning: entry.influence_skinning !== false,
                        HasInverseBind: false,
                        InverseBind: Matrix4.Identity
                    };

                    if (entry.inverse_bind_pose_matrix) {
                        if (TrinityArmature.TryParseAxisMatrix(entry.inverse_bind_pose_matrix)) {
                            const axisX = TrinityArmature.ReadVector3Json(entry.inverse_bind_pose_matrix.axis_x);
                            const axisY = TrinityArmature.ReadVector3Json(entry.inverse_bind_pose_matrix.axis_y);
                            const axisZ = TrinityArmature.ReadVector3Json(entry.inverse_bind_pose_matrix.axis_z);
                            const axisW = TrinityArmature.ReadVector3Json(entry.inverse_bind_pose_matrix.axis_w);
                            info.InverseBind = TrinityArmature.CreateMatrixFromAxis(axisX, axisY, axisZ, axisW);
                            info.HasInverseBind = true;
                        }
                    }

                    jointInfos.push(info);
                }
            }

            const nodeJointIds: number[] = [];
            const nodeNames: string[] = [];
            const nodeList = root.node_list;
            if (Array.isArray(nodeList)) {
                const cnt = nodeList.length;
                for (let i = 0; i < cnt; i++) {
                    const node = nodeList[i];
                    nodeNames.push(node.name ?? '');
                    nodeJointIds.push(node.joint_info_id ?? -1);
                }
            }

            return { JointInfos: jointInfos, NodeJointInfoIds: nodeJointIds, NodeNames: nodeNames };
        } catch {
            return null;
        }
    }

    private static TryParseAxisMatrix(matrix: any): boolean {
        return matrix.axis_x !== undefined && matrix.axis_y !== undefined && matrix.axis_z !== undefined && matrix.axis_w !== undefined;
    }

    private static ReadVector3Json(element: any): Vector3 {
        const x = element.x !== undefined ? Number(element.x) : 0;
        const y = element.y !== undefined ? Number(element.y) : 0;
        const z = element.z !== undefined ? Number(element.z) : 0;
        return new Vector3(x, y, z);
    }

    private static ResolveTrsklJsonPath(sourcePath?: string): string | null {
        if (!sourcePath || sourcePath.trim().length === 0) {
            return null;
        }

        const dir = path.dirname(sourcePath);
        const baseName = path.basename(sourcePath, path.extname(sourcePath));
        let candidate = path.join(dir, `${baseName}.trskl.json`);
        if (fs.existsSync(candidate)) {
            return candidate;
        }

        candidate = path.join(dir, `${baseName}.json`);
        if (fs.existsSync(candidate)) {
            return candidate;
        }

        return null;
    }

    private static CreateMatrixFromAxis(axisX: Vector3, axisY: Vector3, axisZ: Vector3, axisW: Vector3): Matrix4 {
        return new Matrix4(new Float32Array([
            axisX.x, axisX.y, axisX.z, 0,
            axisY.x, axisY.y, axisY.z, 0,
            axisZ.x, axisZ.y, axisZ.z, 0,
            axisW.x, axisW.y, axisW.z, 1
        ]));
    }
}

export class Bone {
    public Name: string;
    public RestPosition: Vector3;
    public RestRotation: MathQuaternion;
    public RestScale: Vector3;
    public RestEuler: Vector3;
    public RestLocalMatrix: Matrix4;
    public RestInvParentMatrix: Matrix4 = Matrix4.Identity;
    public InverseBindWorld: Matrix4 = Matrix4.Identity;
    public JointInverseBindWorld: Matrix4 = Matrix4.Identity;
    public HasJointInverseBind: boolean = false;
    public UseSegmentScaleCompensate: boolean = false;
    public ParentIndex: number;
    public Skinning: boolean = false;
    public Parent: Bone | null = null;
    public Children: Bone[] = [];

    // Mutable pose (used by animation)
    public Position: Vector3;
    public Rotation: MathQuaternion;
    public Scale: Vector3;

    constructor(node: TRTransformNode, skinning: boolean) {
        this.Name = node.Name ?? '';
        this.Position = new Vector3(node.Transform.Translate.X, node.Transform.Translate.Y, node.Transform.Translate.Z);
        this.RestEuler = new Vector3(node.Transform.Rotate.X, node.Transform.Rotate.Y, node.Transform.Rotate.Z);
        this.Rotation = Bone.FromEulerXYZ(this.RestEuler);
        this.Scale = new Vector3(node.Transform.Scale.X, node.Transform.Scale.Y, node.Transform.Scale.Z);
        this.RestPosition = this.Position;
        this.RestRotation = this.Rotation;
        this.RestScale = this.Scale;
        const scaleMat = Matrix4.CreateScale(this.RestScale);
        const rotMat = Matrix4.CreateFromQuaternion(this.RestRotation);
        const transMat = Matrix4.CreateTranslation(this.RestPosition);
        this.RestLocalMatrix = Matrix4.Multiply(Matrix4.Multiply(scaleMat, rotMat), transMat);
        this.ParentIndex = node.ParentNodeIndex;
        this.Skinning = skinning;
        this.HasJointInverseBind = false;
        this.UseSegmentScaleCompensate = false;
    }

    public AddChild(bone: Bone): void {
        bone.Parent = this;
        this.Children.push(bone);
    }

    public ResetPose(): void {
        this.Position = this.RestPosition;
        this.Rotation = this.RestRotation;
        this.Scale = this.RestScale;
    }

    private static FromEulerXYZ(euler: Vector3): MathQuaternion {
        const qx = MathQuaternion.FromAxisAngle(Vector3.UnitX, euler.x);
        const qy = MathQuaternion.FromAxisAngle(Vector3.UnitY, euler.y);
        const qz = MathQuaternion.FromAxisAngle(Vector3.UnitZ, euler.z);
        const q = MathQuaternion.Multiply(qz, MathQuaternion.Multiply(qy, qx));
        return q.Normalized();
    }
}
