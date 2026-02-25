"""Generate a game-specific rig from a fitted Rigify metarig.

Called from AUTORIG_OT_generate in ui.py.

Converts a Rigify human metarig into a game-specific skeleton by:
1. Renaming bones (Rigify -> game names)
2. Creating game-specific bones not in Rigify
3. Reparenting to match the game hierarchy
4. Deleting Rigify-only bones
"""

import bpy


# ===================================================================
# Sun / Moon
# ===================================================================

# Rigify bone name -> Sun/Moon bone name
SUNMOON_RENAME = {
    # Spine / torso
    'spine':     'Waist',
    'pelvis.L':  'Hips',
    'spine.001': 'Spine2',
    'spine.003': 'Spine3',
    'spine.005': 'Neck',
    'face':      'Head',
    # Left arm
    'shoulder.L':  'LShoulder',
    'upper_arm.L': 'LArm',
    'forearm.L':   'LForeArm',
    'hand.L':      'LHand',
    # Left fingers
    'thumb.01.L':    'LFingerA1',
    'thumb.02.L':    'LFingerA2',
    'thumb.03.L':    'LFingerA3',
    'f_index.01.L':  'LFingerB1',
    'f_index.02.L':  'LFingerB2',
    'f_index.03.L':  'LFingerB3',
    'f_middle.01.L': 'LFingerC1',
    'f_middle.02.L': 'LFingerC2',
    'f_middle.03.L': 'LFingerC3',
    'f_ring.01.L':   'LFingerD1',
    'f_ring.02.L':   'LFingerD2',
    'f_ring.03.L':   'LFingerD3',
    'f_pinky.01.L':  'LFingerE1',
    'f_pinky.02.L':  'LFingerE2',
    'f_pinky.03.L':  'LFingerE3',
    # Right arm
    'shoulder.R':  'RShoulder',
    'upper_arm.R': 'RArm',
    'forearm.R':   'RForeArm',
    'hand.R':      'RHand',
    # Right fingers
    'thumb.01.R':    'RFingerA1',
    'thumb.02.R':    'RFingerA2',
    'thumb.03.R':    'RFingerA3',
    'f_index.01.R':  'RFingerB1',
    'f_index.02.R':  'RFingerB2',
    'f_index.03.R':  'RFingerB3',
    'f_middle.01.R': 'RFingerC1',
    'f_middle.02.R': 'RFingerC2',
    'f_middle.03.R': 'RFingerC3',
    'f_ring.01.R':   'RFingerD1',
    'f_ring.02.R':   'RFingerD2',
    'f_ring.03.R':   'RFingerD3',
    'f_pinky.01.R':  'RFingerE1',
    'f_pinky.02.R':  'RFingerE2',
    'f_pinky.03.R':  'RFingerE3',
    # Left leg
    'thigh.L': 'LThigh',
    'shin.L':  'LLeg',
    'foot.L':  'LFoot',
    'toe.L':   'LToe',
    # Right leg
    'thigh.R': 'RThigh',
    'shin.R':  'RLeg',
    'foot.R':  'RFoot',
    'toe.R':   'RToe',
}

# Bones to create: (name, parent_game_name)
SUNMOON_CREATE = [
    ('tr0010_00',   None),
    ('Origin',      'tr0010_00'),
    ('LArmEX',      'LArm'),
    ('RArmEX',      'RArm'),
    ('LForeArmEX',  'LForeArm'),
    ('RForeArmEX',  'RForeArm'),
    ('EffBall',     'RHand'),
    ('Loc_Head',    'Head'),
    ('Loc_Eye',     'Head'),
    ('Loc_Mouth',   'Head'),
]

# Reparent after rename: game_bone -> new_parent (both game names)
SUNMOON_REPARENT = {
    # Waist to new Origin
    'Waist':  'Origin',
    # Hips under Waist, legs under Hips
    'Hips':   'Waist',
    'LThigh': 'Hips',
    'RThigh': 'Hips',
    # Skip deleted spine segments
    'Spine3': 'Spine2',   # skip spine.002
    'Neck':   'Spine3',   # skip spine.004
    'Head':   'Neck',     # skip spine.006
    # Fingers from palm bones to hand
    'LFingerA1': 'LHand',
    'LFingerB1': 'LHand',
    'LFingerC1': 'LHand',
    'LFingerD1': 'LHand',
    'LFingerE1': 'LHand',
    'RFingerA1': 'RHand',
    'RFingerB1': 'RHand',
    'RFingerC1': 'RHand',
    'RFingerD1': 'RHand',
    'RFingerE1': 'RHand',
}

# Set of game bone names to keep (everything else gets deleted)
SUNMOON_KEEP = (
    set(SUNMOON_RENAME.values())
    | {name for name, _ in SUNMOON_CREATE}
)


def _generate_sunmoon(op, context):
    # Find armature
    arm_obj = context.active_object
    if arm_obj is None or arm_obj.type != 'ARMATURE':
        for obj in context.scene.objects:
            if obj.type == 'ARMATURE':
                arm_obj = obj
                break
    if arm_obj is None or arm_obj.type != 'ARMATURE':
        op.report({'ERROR'}, "No armature found in scene")
        return {'CANCELLED'}

    # Enter edit mode
    if context.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode='EDIT')
    edit_bones = arm_obj.data.edit_bones

    # Step 1: Rename mapped bones
    renamed = 0
    for rigify_name, game_name in SUNMOON_RENAME.items():
        bone = edit_bones.get(rigify_name)
        if bone:
            bone.name = game_name
            renamed += 1

    # Step 2: Create new bones
    created = 0
    for name, parent_name in SUNMOON_CREATE:
        bone = edit_bones.new(name)
        if parent_name:
            parent = edit_bones.get(parent_name)
            if parent:
                bone.head = parent.head.copy()
                direction = (parent.tail - parent.head).normalized()
                bone.tail = parent.head + direction * 0.02
                bone.parent = parent
            else:
                bone.head = (0, 0, 0)
                bone.tail = (0, 0.02, 0)
        else:
            bone.head = (0, 0, 0)
            bone.tail = (0, 0.02, 0)
        created += 1

    # Step 3: Reparent bones
    reparented = 0
    for bone_name, new_parent_name in SUNMOON_REPARENT.items():
        bone = edit_bones.get(bone_name)
        parent = edit_bones.get(new_parent_name)
        if bone and parent:
            bone.use_connect = False
            bone.parent = parent
            reparented += 1

    # Step 4: Delete non-game bones
    to_delete = [b for b in edit_bones if b.name not in SUNMOON_KEEP]
    deleted = len(to_delete)
    for bone in to_delete:
        edit_bones.remove(bone)

    bpy.ops.object.mode_set(mode='OBJECT')

    remaining = len(arm_obj.data.bones)
    op.report(
        {'INFO'},
        f"Sun/Moon rig: {renamed} renamed, {created} created, "
        f"{reparented} reparented, {deleted} deleted. "
        f"{remaining} bones total")
    return {'FINISHED'}


# ===================================================================
# Dispatch
# ===================================================================

def execute(op, context, game):
    if game == 'SUNMOON':
        return _generate_sunmoon(op, context)
    op.report({'WARNING'}, f"Generate {game} rig — not yet implemented")
    return {'CANCELLED'}
