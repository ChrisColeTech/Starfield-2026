"""UI panel and operators for Game Rig Builder."""

import os
import bpy
from bpy.props import EnumProperty, PointerProperty, StringProperty, BoolProperty
from bpy_extras.io_utils import ImportHelper
from mathutils import Vector


# -------------------------------------------------------------------
# Operators
# -------------------------------------------------------------------

class GAMERIG_OT_generate(bpy.types.Operator):
    """Create a game-specific armature from model bone data"""
    bl_idname = "gamerig.generate"
    bl_label = "Generate Rig"
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        from . import skeletons

        game = context.scene.gamerig_props.game
        if game == 'SUNMOON':
            bone_data = skeletons.SUNMOON
            rig_name = 'SunMoon'
        else:
            self.report({'WARNING'}, f"{game} not yet implemented")
            return {'CANCELLED'}

        # Delete existing armatures
        if context.mode != 'OBJECT':
            bpy.ops.object.mode_set(mode='OBJECT')
        for obj in list(context.scene.objects):
            if obj.type == 'ARMATURE':
                bpy.data.objects.remove(obj, do_unlink=True)

        # Create armature
        arm_data = bpy.data.armatures.new(rig_name)
        arm_obj = bpy.data.objects.new(rig_name, arm_data)
        context.collection.objects.link(arm_obj)
        context.view_layer.objects.active = arm_obj
        arm_obj.select_set(True)

        bpy.ops.object.mode_set(mode='EDIT')
        edit_bones = arm_data.edit_bones

        # Create all bones
        for name, head, tail, roll, _parent in bone_data:
            bone = edit_bones.new(name)
            bone.head = Vector(head)
            bone.tail = Vector(tail)
            bone.roll = roll

        # Set parents
        for name, _head, _tail, _roll, parent_name in bone_data:
            if parent_name:
                bone = edit_bones.get(name)
                parent = edit_bones.get(parent_name)
                if bone and parent:
                    bone.parent = parent

        # Extend stub tails toward first child for visibility
        for bone in edit_bones:
            children = bone.children
            if children:
                child_head = children[0].head
                direction = child_head - bone.head
                if direction.length > 0.01:
                    bone.tail = child_head
            else:
                if bone.parent:
                    direction = bone.head - bone.parent.head
                    if direction.length > 0.01:
                        bone.tail = bone.head + direction.normalized() * 5.0
                    else:
                        bone.tail = bone.head + Vector((0, 5.0, 0))
                else:
                    bone.tail = bone.head + Vector((0, 5.0, 0))

        bpy.ops.object.mode_set(mode='OBJECT')

        # Organize bones into collections by body part
        bone_groups = {
            'Root':        ['tr0010_00', 'Origin'],
            'Torso':       ['Waist', 'Spine2', 'Spine3', 'Hips'],
            'Head':        ['Neck', 'Head'],
            'Left Arm':    ['LShoulder', 'LArm', 'LForeArm', 'LHand', 'LArmEX', 'LForeArmEX'],
            'Right Arm':   ['RShoulder', 'RArm', 'RForeArm', 'RHand', 'RArmEX', 'RForeArmEX', 'EffBall'],
            'Left Fingers': [f'LFinger{l}{n}' for l in 'ABCDE' for n in '123'],
            'Right Fingers': [f'RFinger{l}{n}' for l in 'ABCDE' for n in '123'],
            'Left Leg':    ['LThigh', 'LLeg', 'LFoot', 'LToe'],
            'Right Leg':   ['RThigh', 'RLeg', 'RFoot', 'RToe'],
        }
        assigned = set()
        for group_name, bone_names in bone_groups.items():
            col = arm_data.collections.new(group_name)
            col.is_visible = True
            for bname in bone_names:
                bone = arm_data.bones.get(bname)
                if bone:
                    col.assign(bone)
                    assigned.add(bname)
        # Catch any unassigned bones
        if len(assigned) < len(arm_data.bones):
            col = arm_data.collections.new("Other")
            col.is_visible = True
            for bone in arm_data.bones:
                if bone.name not in assigned:
                    col.assign(bone)

        arm_obj.data.display_type = 'STICK'
        arm_obj.show_in_front = True

        # Rotate to match Collada Y-up -> Blender Z-up
        from math import radians
        arm_obj.rotation_euler[0] = radians(90)

        self.report({'INFO'}, f"{rig_name} rig: {len(arm_data.bones)} bones")
        return {'FINISHED'}


class GAMERIG_OT_load_model(bpy.types.Operator, ImportHelper):
    """Import a DAE model file (mesh + armature)"""
    bl_idname = "gamerig.load_model"
    bl_label = "Load Model"
    bl_options = {'REGISTER', 'UNDO'}

    filename_ext = ".dae"
    filter_glob: StringProperty(default="*.dae", options={'HIDDEN'})

    def execute(self, context):
        bpy.ops.wm.collada_import(filepath=self.filepath)
        name = os.path.splitext(os.path.basename(self.filepath))[0]
        armatures = [o for o in context.scene.objects if o.type == 'ARMATURE']
        meshes = [o for o in context.scene.objects if o.type == 'MESH']
        self.report({'INFO'},
                    f"Loaded '{name}': {len(armatures)} armature(s), "
                    f"{len(meshes)} mesh(es)")
        return {'FINISHED'}


class GAMERIG_OT_fit_rig(bpy.types.Operator):
    """Fit the rig bones to match the mesh proportions"""
    bl_idname = "gamerig.fit_rig"
    bl_label = "Fit Rig to Model"
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        self.report({'WARNING'}, "Fit rig not yet implemented for game rigs")
        return {'CANCELLED'}


class GAMERIG_OT_load_animation(bpy.types.Operator, ImportHelper):
    """Import a DAE animation clip and apply it to the armature"""
    bl_idname = "gamerig.load_animation"
    bl_label = "Load Animation"
    bl_options = {'REGISTER', 'UNDO'}

    filename_ext = ".dae"
    filter_glob: StringProperty(default="*.dae", options={'HIDDEN'})

    replace_existing: BoolProperty(
        name="Replace Existing",
        description="Replace the current action instead of adding a new one",
        default=False,
    )

    def execute(self, context):
        model_arm = None
        for obj in context.scene.objects:
            if obj.type == 'ARMATURE':
                model_arm = obj
                break

        if model_arm is None:
            self.report({'ERROR'}, "No armature in scene")
            return {'CANCELLED'}

        existing_objects = set(obj.name for obj in context.scene.objects)
        bpy.ops.wm.collada_import(filepath=self.filepath)

        clip_arm = None
        new_objects = []
        for obj in context.scene.objects:
            if obj.name not in existing_objects:
                new_objects.append(obj)
                if obj.type == 'ARMATURE':
                    clip_arm = obj

        if clip_arm is None:
            for obj in new_objects:
                bpy.data.objects.remove(obj, do_unlink=True)
            self.report({'ERROR'}, "Imported DAE has no armature")
            return {'CANCELLED'}

        if clip_arm.animation_data is None or clip_arm.animation_data.action is None:
            for obj in new_objects:
                bpy.data.objects.remove(obj, do_unlink=True)
            self.report({'ERROR'}, "Imported armature has no animation")
            return {'CANCELLED'}

        model_bones = {b.name for b in model_arm.data.bones}
        clip_bones = {b.name for b in clip_arm.data.bones}
        matching = model_bones & clip_bones

        if not matching:
            for obj in new_objects:
                bpy.data.objects.remove(obj, do_unlink=True)
            self.report({'ERROR'}, "No matching bone names between rig and clip")
            return {'CANCELLED'}

        action = clip_arm.animation_data.action
        if model_arm.animation_data is None:
            model_arm.animation_data_create()

        if self.replace_existing and model_arm.animation_data.action:
            old_action = model_arm.animation_data.action
            model_arm.animation_data.action = None
            if old_action.users == 0:
                bpy.data.actions.remove(old_action)

        model_arm.animation_data.action = action

        frame_start = int(action.frame_range[0])
        frame_end = int(action.frame_range[1])
        context.scene.frame_start = frame_start
        context.scene.frame_end = frame_end
        context.scene.frame_set(frame_start)

        clip_arm.animation_data.action = None
        for obj in new_objects:
            bpy.data.objects.remove(obj, do_unlink=True)

        clip_name = os.path.splitext(os.path.basename(self.filepath))[0]
        self.report(
            {'INFO'},
            f"Loaded '{clip_name}': {len(matching)}/{len(clip_bones)} bones, "
            f"frames {frame_start}-{frame_end}")
        return {'FINISHED'}


class GAMERIG_OT_unload_animation(bpy.types.Operator):
    """Remove the current animation and reset pose"""
    bl_idname = "gamerig.unload_animation"
    bl_label = "Unload Animation"
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        arm_obj = None
        for obj in context.scene.objects:
            if obj.type == 'ARMATURE':
                arm_obj = obj
                break

        if arm_obj is None:
            self.report({'ERROR'}, "No armature in scene")
            return {'CANCELLED'}

        if arm_obj.animation_data and arm_obj.animation_data.action:
            action = arm_obj.animation_data.action
            arm_obj.animation_data.action = None
            if action.users == 0:
                bpy.data.actions.remove(action)

        if arm_obj.pose:
            for pb in arm_obj.pose.bones:
                pb.location = (0, 0, 0)
                pb.rotation_quaternion = (1, 0, 0, 0)
                pb.rotation_euler = (0, 0, 0)
                pb.scale = (1, 1, 1)

        context.scene.frame_set(0)
        _set_front_view(context)

        self.report({'INFO'}, "Animation unloaded, pose reset")
        return {'FINISHED'}


class GAMERIG_OT_reset_view(bpy.types.Operator):
    """Reset viewport to front orthographic"""
    bl_idname = "gamerig.reset_view"
    bl_label = "Reset View"

    def execute(self, context):
        _set_front_view(context)
        return {'FINISHED'}


# -------------------------------------------------------------------
# Helpers
# -------------------------------------------------------------------

def _set_front_view(context):
    for area in context.screen.areas:
        if area.type == 'VIEW_3D':
            space = area.spaces[0]
            space.clip_end = 10000
            for region in area.regions:
                if region.type == 'WINDOW':
                    with context.temp_override(area=area, region=region):
                        bpy.ops.view3d.view_axis(type='FRONT')
                        bpy.ops.view3d.view_selected()
                    break
            break


# -------------------------------------------------------------------
# Scene properties
# -------------------------------------------------------------------

class GAMERIG_SceneProps(bpy.types.PropertyGroup):
    game: EnumProperty(
        name="Game",
        description="Target game skeleton",
        items=[
            ('SUNMOON', "Sun/Moon", "Pokemon Sun/Moon"),
            ('SCARLET', "Scarlet", "Pokemon Scarlet/Violet"),
            ('PZLA', "PZLA", "Pokemon Legends: Arceus"),
        ],
        default='SUNMOON',
    )


# -------------------------------------------------------------------
# Panel
# -------------------------------------------------------------------

class GAMERIG_PT_panel(bpy.types.Panel):
    bl_label = "Game Rig Builder"
    bl_idname = "GAMERIG_PT_panel"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = 'GameRig'

    def draw(self, context):
        layout = self.layout
        props = context.scene.gamerig_props

        layout.prop(props, "game")

        layout.separator()

        col = layout.column(align=True)
        col.scale_y = 1.4
        col.operator("gamerig.generate", icon='ARMATURE_DATA')
        col.operator("gamerig.load_model", icon='IMPORT')
        col.operator("gamerig.fit_rig", icon='FULLSCREEN_ENTER')

        layout.separator()

        col = layout.column(align=True)
        col.scale_y = 1.4
        col.operator("gamerig.load_animation", icon='ACTION')
        col.operator("gamerig.unload_animation", icon='CANCEL')

        layout.separator()

        col = layout.column(align=True)
        col.scale_y = 1.4
        col.operator("gamerig.reset_view", icon='VIEW_CAMERA')


# -------------------------------------------------------------------
# Registration
# -------------------------------------------------------------------

classes = (
    GAMERIG_SceneProps,
    GAMERIG_OT_generate,
    GAMERIG_OT_load_model,
    GAMERIG_OT_fit_rig,
    GAMERIG_OT_load_animation,
    GAMERIG_OT_unload_animation,
    GAMERIG_OT_reset_view,
    GAMERIG_PT_panel,
)


def register():
    for cls in classes:
        bpy.utils.register_class(cls)
    bpy.types.Scene.gamerig_props = PointerProperty(type=GAMERIG_SceneProps)


def unregister():
    del bpy.types.Scene.gamerig_props
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
