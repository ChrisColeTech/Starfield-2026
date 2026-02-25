"""Fit metarig bones to match mesh proportions.

Called from AUTORIG_OT_fit_rig in ui.py.
"""

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


# -------------------------------------------------------------------
# BVH helpers
# -------------------------------------------------------------------

def _build_bvh(mesh_obj):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    eval_obj = mesh_obj.evaluated_get(depsgraph)
    eval_mesh = eval_obj.to_mesh()
    mat = mesh_obj.matrix_world
    verts = [mat @ v.co for v in eval_mesh.vertices]
    polys = [p.vertices for p in eval_mesh.polygons]
    bvh = BVHTree.FromPolygons(verts, polys)
    eval_obj.to_mesh_clear()
    return bvh


def _ray_pair(bvh, origin, direction, distance=50):
    d = direction.normalized()
    hit_a, _, _, _ = bvh.ray_cast(origin + d * distance, -d)
    hit_b, _, _, _ = bvh.ray_cast(origin - d * distance, d)
    if hit_a is not None and hit_b is not None:
        return (hit_a, hit_b)
    return (None, None)


def _center_axis(bvh, point, axis_idx, distance=50):
    d = Vector((0, 0, 0))
    d[axis_idx] = 1.0
    hit_a, hit_b = _ray_pair(bvh, point, d, distance)
    if hit_a is not None:
        return (hit_a[axis_idx] + hit_b[axis_idx]) / 2
    return point[axis_idx]


def _mesh_extent(bvh, point, axis_idx, distance=50):
    d = Vector((0, 0, 0))
    d[axis_idx] = 1.0
    hit_a, hit_b = _ray_pair(bvh, point, d, distance)
    if hit_a is not None:
        lo = min(hit_a[axis_idx], hit_b[axis_idx])
        hi = max(hit_a[axis_idx], hit_b[axis_idx])
        return (lo, hi)
    return None


# -------------------------------------------------------------------
# Crotch / joint detection
# -------------------------------------------------------------------

def _find_crotch_z(verts, mesh_min_z, mesh_height, mesh_center_x):
    for pct in range(50, 20, -1):
        z = mesh_min_z + mesh_height * (pct / 100)
        slice_v = [v for v in verts if abs(v.z - z) < mesh_height * 0.02]
        if len(slice_v) < 5:
            continue
        center_v = [v for v in slice_v if abs(v.x - mesh_center_x) < 0.3]
        side_v = [v for v in slice_v if abs(v.x - mesh_center_x) > 0.3]
        if len(side_v) > len(center_v) * 2 and len(center_v) < 5:
            return z
    return mesh_min_z + mesh_height * 0.45


def _find_narrowest_z(verts, x_start, x_end, x_sign, step=0.05, min_verts=2):
    best_x = (x_start + x_end) / 2
    best_spread = 999
    x = x_start
    while x < x_end:
        x += step
        sl = [v for v in verts if abs(abs(v.x) - x) < step * 1.5]
        if len(sl) >= min_verts:
            spread = max(v.z for v in sl) - min(v.z for v in sl)
            if spread < best_spread:
                best_spread = spread
                best_x = x
    return best_x


# -------------------------------------------------------------------
# Placement functions
# -------------------------------------------------------------------

def _place_spine(ebones, bvh, crotch_z, mesh_max_z, mesh_center_x):
    spine_names = ['spine', 'spine.001', 'spine.002', 'spine.003',
                   'spine.004', 'spine.005', 'spine.006']
    existing = [ebones.get(n) for n in spine_names if ebones.get(n)]
    if not existing:
        return

    n = len(existing)
    spine_top = mesh_max_z * 0.98
    span = spine_top - crotch_z

    for i, bone in enumerate(existing):
        z_head = crotch_z + span * (i / n)
        z_tail = crotch_z + span * ((i + 1) / n)
        frac = 0.45 if z_head < crotch_z + span * 0.80 else 0.50
        ext = _mesh_extent(bvh, Vector((mesh_center_x, 0, z_head)), 1)
        y = ext[0] + (ext[1] - ext[0]) * frac if ext else 0
        bone.head = Vector((mesh_center_x, y, z_head))
        bone.tail = Vector((mesh_center_x, y, z_tail))

    for i in range(1, len(existing)):
        existing[i].head = existing[i - 1].tail.copy()


def _place_arms(ebones, bvh, verts, mesh_min_z, mesh_height, mesh_center_x):
    torso_z = mesh_min_z + mesh_height * 0.55
    ext = _mesh_extent(bvh, Vector((0, 0, torso_z)), 0)
    torso_half_x = max(abs(ext[0]), abs(ext[1])) if ext else max(abs(v.x) for v in verts) * 0.4

    for side, x_sign in [('.L', 1), ('.R', -1)]:
        shoulder_b = ebones.get(f'shoulder{side}')
        upper_arm_b = ebones.get(f'upper_arm{side}')
        forearm_b = ebones.get(f'forearm{side}')
        hand_b = ebones.get(f'hand{side}')
        if not all([shoulder_b, upper_arm_b, forearm_b, hand_b]):
            continue

        arm_verts = [v for v in verts
                     if v.x * x_sign > torso_half_x * 0.7
                     and v.z > mesh_min_z + mesh_height * 0.45]
        if len(arm_verts) < 5:
            continue

        arm_min_x = min(abs(v.x) for v in arm_verts)
        arm_max_x = max(abs(v.x) for v in arm_verts)

        spine003 = ebones.get('spine.003')
        shoulder_z = spine003.tail.z if spine003 else mesh_min_z + mesh_height * 0.75
        shoulder_y = spine003.tail.y if spine003 else 0
        shoulder_ext = _mesh_extent(bvh, Vector((0, shoulder_y, shoulder_z)), 0)
        if shoulder_ext:
            shoulder_x = max(abs(shoulder_ext[0]), abs(shoulder_ext[1])) * 0.45
        else:
            shoulder_x = torso_half_x * 0.5
        shoulder_x = min(shoulder_x, arm_min_x + (arm_max_x - arm_min_x) * 0.15)

        wrist_x = _find_narrowest_z(
            arm_verts,
            shoulder_x + (arm_max_x - shoulder_x) * 0.35,
            arm_max_x - (arm_max_x - shoulder_x) * 0.10,
            x_sign)
        elbow_x = shoulder_x + (wrist_x - shoulder_x) * 0.57

        def _arm_center(target_x):
            nearby = [v for v in arm_verts if abs(abs(v.x) - target_x) < 0.2]
            est_y = sum(v.y for v in nearby) / len(nearby) if nearby else shoulder_y
            est_z = sum(v.z for v in nearby) / len(nearby) if nearby else shoulder_z
            p = Vector((target_x * x_sign, est_y, est_z))
            cy = _center_axis(bvh, p, 1)
            return Vector((target_x * x_sign, cy, est_z))

        p_shoulder = _arm_center(shoulder_x)
        p_elbow = _arm_center(elbow_x)
        p_wrist = _arm_center(wrist_x)
        p_hand_tip = _arm_center(arm_max_x)

        shoulder_b.head = spine003.tail.copy() if spine003 else p_shoulder
        shoulder_b.tail = p_shoulder
        upper_arm_b.head = p_shoulder
        upper_arm_b.tail = p_elbow
        forearm_b.head = p_elbow
        forearm_b.tail = p_wrist
        hand_b.head = p_wrist
        hand_b.tail = p_hand_tip

        chain = [shoulder_b, upper_arm_b, forearm_b, hand_b]
        for i in range(1, len(chain)):
            chain[i].head = chain[i - 1].tail.copy()


def _place_legs(ebones, bvh, verts, mesh_min_z, mesh_height, mesh_center_x,
                crotch_z):
    for side, x_sign in [('.L', 1), ('.R', -1)]:
        thigh_b = ebones.get(f'thigh{side}')
        shin_b = ebones.get(f'shin{side}')
        foot_b = ebones.get(f'foot{side}')
        toe_b = ebones.get(f'toe{side}')
        heel_b = ebones.get(f'heel.02{side}')
        if not all([thigh_b, shin_b, foot_b]):
            continue

        mid_z = mesh_min_z + mesh_height * 0.20
        leg_verts = [v for v in verts
                     if v.x * x_sign > 0
                     and v.z < crotch_z + mesh_height * 0.02
                     and abs(v.z - mid_z) < mesh_height * 0.1]
        if not leg_verts:
            continue
        leg_x = sum(v.x for v in leg_verts) / len(leg_verts)

        all_leg = [v for v in verts
                   if v.x * x_sign > 0
                   and v.z < crotch_z
                   and abs(v.x - leg_x) < 0.8]
        if len(all_leg) < 5:
            continue

        leg_min_z = min(v.z for v in all_leg)
        leg_height = crotch_z - leg_min_z
        if leg_height <= 0:
            continue

        knee_z = leg_min_z + leg_height * 0.50
        ankle_z = leg_min_z + leg_height * 0.15
        hip_x = mesh_center_x + (leg_x - mesh_center_x) * 0.35

        def _leg_center(x, z):
            p = Vector((x, 0, z))
            cy = _center_axis(bvh, p, 1)
            return Vector((x, cy, z))

        p_hip = _leg_center(hip_x, crotch_z)
        p_knee = _leg_center(leg_x, knee_z)
        p_ankle = _leg_center(leg_x, ankle_z)

        foot_verts = [v for v in all_leg if v.z < ankle_z]
        toe_y = min(v.y for v in foot_verts) if foot_verts else p_ankle.y - 0.3

        thigh_b.head = p_hip
        thigh_b.tail = p_knee
        shin_b.head = p_knee
        shin_b.tail = p_ankle
        foot_b.head = p_ankle
        foot_b.tail = Vector((leg_x, toe_y, leg_min_z))

        shin_b.head = thigh_b.tail.copy()
        foot_b.head = shin_b.tail.copy()

        if toe_b:
            toe_b.head = foot_b.tail.copy()
            toe_b.tail = foot_b.tail + Vector((0, -0.15, 0))
        if heel_b:
            heel_b.head = foot_b.tail.copy()
            heel_b.tail = foot_b.tail + Vector((0, 0.1, 0))


def _place_secondary(ebones, bvh, mesh_center_x):
    spine = ebones.get('spine')
    spine003 = ebones.get('spine.003')
    spine006 = ebones.get('spine.006')

    for side, x_sign in [('.L', 1), ('.R', -1)]:
        b = ebones.get(f'pelvis{side}')
        if b and spine:
            b.head = spine.head.copy()
            thigh = ebones.get(f'thigh{side}')
            if thigh:
                b.tail = spine.head + (thigh.head - spine.head) * 0.5
            else:
                b.tail = spine.head + Vector((0.3 * x_sign, 0, -0.2))
            b.parent = spine

    for side, x_sign in [('.L', 1), ('.R', -1)]:
        b = ebones.get(f'breast{side}')
        if b and spine003:
            chest_y = spine003.head.y
            ext = _mesh_extent(bvh, Vector((0.3 * x_sign, chest_y, spine003.head.z)), 1)
            front_y = ext[0] if ext else chest_y - 0.3
            b.head = Vector((0.3 * x_sign, chest_y, spine003.head.z))
            b.tail = Vector((0.3 * x_sign, front_y, spine003.head.z - 0.1))
            b.parent = spine003

    for side, x_sign in [('.L', 1), ('.R', -1)]:
        b = ebones.get(f'eye{side}')
        if b and spine006:
            head_pos = spine006.tail
            eye_x = 0.15 * x_sign
            eye_z = head_pos.z - 0.1
            ext = _mesh_extent(bvh, Vector((eye_x, 0, eye_z)), 1)
            eye_y = ext[0] + (ext[1] - ext[0]) * 0.3 if ext else head_pos.y - 0.2
            b.head = Vector((eye_x, eye_y, eye_z))
            b.tail = Vector((eye_x, eye_y - 0.2, eye_z))
            b.parent = spine006

    for jaw_name in ['upper_jaw', 'lower_jaw']:
        b = ebones.get(jaw_name)
        if b and spine006:
            head_pos = spine006.tail
            ext = _mesh_extent(bvh, Vector((0, 0, head_pos.z - 0.25)), 1)
            jaw_y = ext[0] + (ext[1] - ext[0]) * 0.4 if ext else head_pos.y - 0.15
            z_off = -0.2 if jaw_name == 'upper_jaw' else -0.3
            b.head = Vector((0, jaw_y, head_pos.z + z_off))
            b.tail = Vector((0, jaw_y - 0.15, head_pos.z + z_off - 0.05))
            b.parent = spine006


# -------------------------------------------------------------------
# Bone cleanup
# -------------------------------------------------------------------

_FACE_BONE_PREFIXES = (
    'face', 'nose', 'lip', 'ear', 'brow', 'lid', 'cheek',
    'temple', 'forehead', 'jaw', 'chin', 'teeth', 'tongue',
)

_FINGER_BONE_PREFIXES = (
    'palm.01', 'palm.02', 'palm.03', 'palm.04',
    'f_index.01', 'f_index.02', 'f_index.03',
    'f_middle.01', 'f_middle.02', 'f_middle.03',
    'f_ring.01', 'f_ring.02', 'f_ring.03',
    'f_pinky.01', 'f_pinky.02', 'f_pinky.03',
    'thumb.01', 'thumb.02', 'thumb.03',
)


def _remove_face_bones(ebones):
    to_remove = []
    for bone in ebones:
        bname = bone.name.split('.')[0].lower()
        if any(bname.startswith(p) for p in _FACE_BONE_PREFIXES):
            to_remove.append(bone.name)
    for name in to_remove:
        bone = ebones.get(name)
        if bone:
            ebones.remove(bone)
    return len(to_remove)


def _detect_mitten_hands(ebones, verts):
    hand_bone = ebones.get('hand.L')
    if not hand_bone:
        return False
    wrist_x = abs(hand_bone.head.x)
    hand_tip_x = abs(hand_bone.tail.x)
    hand_z = hand_bone.head.z
    hand_region = [v for v in verts
                   if abs(v.x) > wrist_x
                   and abs(v.z - hand_z) < 0.5]
    far_verts = [v for v in hand_region if abs(v.x) > hand_tip_x + 0.1]
    return len(far_verts) < 10


def _remove_finger_bones(ebones):
    removed = 0
    for side in ['.L', '.R']:
        for prefix in _FINGER_BONE_PREFIXES:
            bone = ebones.get(f'{prefix}{side}')
            if bone:
                ebones.remove(bone)
                removed += 1
    return removed


# -------------------------------------------------------------------
# Main execute (called from AUTORIG_OT_fit_rig)
# -------------------------------------------------------------------

def execute(op, context):
    armature = None
    mesh = None
    for obj in context.scene.objects:
        if obj.type == 'ARMATURE' and armature is None:
            armature = obj
        elif obj.type == 'MESH' and obj.visible_get() and mesh is None:
            mesh = obj

    if armature is None:
        op.report({'ERROR'}, "No armature found in scene")
        return {'CANCELLED'}
    if mesh is None:
        op.report({'ERROR'}, "No visible mesh found in scene")
        return {'CANCELLED'}

    verts = [mesh.matrix_world @ v.co for v in mesh.data.vertices]
    mesh_min_x = min(v.x for v in verts)
    mesh_max_x = max(v.x for v in verts)
    mesh_min_z = min(v.z for v in verts)
    mesh_max_z = max(v.z for v in verts)
    mesh_height = mesh_max_z - mesh_min_z
    mesh_center_x = (mesh_min_x + mesh_max_x) / 2

    if mesh_height <= 0:
        op.report({'ERROR'}, "Mesh has zero height")
        return {'CANCELLED'}

    bvh = _build_bvh(mesh)

    mid_z = mesh_min_z + mesh_height * 0.5
    ext_y = _mesh_extent(bvh, Vector((mesh_center_x, 0, mid_z)), 1)
    mesh_center_y = (ext_y[0] + ext_y[1]) / 2 if ext_y else 0

    bpy.ops.object.select_all(action='DESELECT')
    armature.select_set(True)
    context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode='EDIT')

    arm_min_z = min(b.head.z for b in armature.data.edit_bones)
    arm_max_z = max(max(b.head.z, b.tail.z) for b in armature.data.edit_bones)
    arm_height = arm_max_z - arm_min_z

    bpy.ops.object.mode_set(mode='OBJECT')

    if arm_height <= 0:
        op.report({'ERROR'}, "Armature has zero height")
        return {'CANCELLED'}

    scale_factor = mesh_height / arm_height
    armature.scale = (scale_factor, scale_factor, scale_factor)
    armature.location = (mesh_center_x, mesh_center_y, mesh_min_z)

    bpy.ops.object.select_all(action='DESELECT')
    armature.select_set(True)
    context.view_layer.objects.active = armature
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    crotch_z = _find_crotch_z(verts, mesh_min_z, mesh_height, mesh_center_x)

    bpy.ops.object.mode_set(mode='EDIT')
    ebones = armature.data.edit_bones

    _place_spine(ebones, bvh, crotch_z, mesh_max_z, mesh_center_x)
    _place_arms(ebones, bvh, verts, mesh_min_z, mesh_height, mesh_center_x)
    _place_legs(ebones, bvh, verts, mesh_min_z, mesh_height, mesh_center_x, crotch_z)
    _place_secondary(ebones, bvh, mesh_center_x)

    face_removed = _remove_face_bones(ebones)
    fingers_removed = 0
    if _detect_mitten_hands(ebones, verts):
        fingers_removed = _remove_finger_bones(ebones)

    bpy.ops.object.mode_set(mode='OBJECT')

    msg = f"Fit '{armature.name}' to '{mesh.name}' (height {scale_factor:.2f}x)"
    if fingers_removed:
        msg += f", {fingers_removed} finger bones removed"
    if face_removed:
        msg += f", {face_removed} face bones removed"
    op.report({'INFO'}, msg)
    return {'FINISHED'}
