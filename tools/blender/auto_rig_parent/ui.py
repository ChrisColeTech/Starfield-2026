"""UI panel and operators for Auto Rig Parent."""

import os
import bpy
from bpy.props import EnumProperty, PointerProperty, StringProperty, BoolProperty
from bpy_extras.io_utils import ImportHelper


# -------------------------------------------------------------------
# Operators
# -------------------------------------------------------------------

class AUTORIG_OT_new(bpy.types.Operator):
    """Clear the scene and create a new Rigify human metarig"""
    bl_idname = "autorig.new"
    bl_label = "New Rig"
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        if bpy.data.objects:
            bpy.ops.object.select_all(action='SELECT')
            bpy.ops.object.delete()

        bpy.ops.object.armature_human_metarig_add()
        arm = context.active_object
        _fix_tpose(arm)
        _set_front_view(context)

        self.report({'INFO'}, f"Created metarig '{arm.name}'")
        return {'FINISHED'}


class AUTORIG_OT_reset_view(bpy.types.Operator):
    """Reset the viewport to front orthographic view"""
    bl_idname = "autorig.reset_view"
    bl_label = "Reset View"

    def execute(self, context):
        _set_front_view(context)
        return {'FINISHED'}


class AUTORIG_OT_load_model(bpy.types.Operator, ImportHelper):
    """Import a model file (FBX, DAE, or OBJ) with mesh + armature"""
    bl_idname = "autorig.load_model"
    bl_label = "Load Model"
    bl_options = {'REGISTER', 'UNDO'}

    filename_ext = ".fbx"
    filter_glob: StringProperty(default="*.fbx;*.dae;*.obj", options={'HIDDEN'})

    def execute(self, context):
        ext = os.path.splitext(self.filepath)[1].lower()
        if ext == ".fbx":
            bpy.ops.import_scene.fbx(filepath=self.filepath)
        elif ext == ".obj":
            bpy.ops.wm.obj_import(filepath=self.filepath)
        else:
            bpy.ops.wm.collada_import(filepath=self.filepath)
        name = os.path.splitext(os.path.basename(self.filepath))[0]
        armatures = [o for o in context.scene.objects if o.type == 'ARMATURE']
        meshes = [o for o in context.scene.objects if o.type == 'MESH']
        self.report({'INFO'},
                    f"Loaded '{name}': {len(armatures)} armature(s), "
                    f"{len(meshes)} mesh(es)")
        return {'FINISHED'}


class AUTORIG_OT_load_animation(bpy.types.Operator, ImportHelper):
    """Import a DAE animation clip and apply it to the scene armature"""
    bl_idname = "autorig.load_animation"
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
            self.report({'ERROR'}, "No matching bone names between model and clip")
            return {'CANCELLED'}

        action = clip_arm.animation_data.action
        if model_arm.animation_data is None:
            model_arm.animation_data_create()

        if self.replace_existing and model_arm.animation_data.action:
            old_action = model_arm.animation_data.action
            model_arm.animation_data.action = None
            if old_action.users == 0:
                bpy.data.actions.remove(old_action)

        # Direct transfer — same skeleton, action applies as-is
        model_arm.animation_data.action = action

        frame_start = int(action.frame_range[0])
        frame_end = int(action.frame_range[1])
        context.scene.frame_start = frame_start
        context.scene.frame_end = frame_end
        context.scene.frame_set(frame_start)

        # Clean up imported objects (keep the action on the model)
        clip_arm.animation_data.action = None
        for obj in new_objects:
            bpy.data.objects.remove(obj, do_unlink=True)

        clip_name = os.path.splitext(os.path.basename(self.filepath))[0]
        self.report(
            {'INFO'},
            f"Merged '{clip_name}': {len(matching)}/{len(clip_bones)} bones, "
            f"frames {frame_start}-{frame_end}")
        return {'FINISHED'}


class AUTORIG_OT_unload_animation(bpy.types.Operator):
    """Remove the current animation, reset pose and camera to front"""
    bl_idname = "autorig.unload_animation"
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

        # Remove action
        if arm_obj.animation_data and arm_obj.animation_data.action:
            action = arm_obj.animation_data.action
            arm_obj.animation_data.action = None
            if action.users == 0:
                bpy.data.actions.remove(action)

        # Reset pose
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


class AUTORIG_OT_fit_rig(bpy.types.Operator):
    """Fit the metarig bones to match the mesh proportions"""
    bl_idname = "autorig.fit_rig"
    bl_label = "Fit Rig to Model"
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        from . import fit
        return fit.execute(self, context)


class AUTORIG_OT_generate(bpy.types.Operator):
    """Convert the fitted Rigify rig into a game-specific skeleton"""
    bl_idname = "autorig.generate"
    bl_label = "Generate Game Rig"
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        from . import generate
        game = context.scene.autorig_props.game
        return generate.execute(self, context, game)


# -------------------------------------------------------------------
# Helpers
# -------------------------------------------------------------------


def _rotate_bone(bone, pivot, rotation):
    """Rotate a single bone (head + tail) around a pivot point."""
    bone.head = pivot + rotation @ (bone.head - pivot)
    bone.tail = pivot + rotation @ (bone.tail - pivot)


def _fix_tpose(arm_obj):
    """Straighten the metarig into a proper game T-pose.

    Rotates the entire arm assembly to be horizontal, preserving
    all relative bone positions (fingers, palm bones, etc).
    """
    from mathutils import Vector

    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm_obj.data.edit_bones

    for side, sign in [('.L', 1.0), ('.R', -1.0)]:
        upper = eb.get(f'upper_arm{side}')
        hand = eb.get(f'hand{side}')
        if not upper or not hand:
            continue

        # Overall arm direction from upper_arm head to hand tail
        old_dir = (hand.tail - upper.head).normalized()
        new_dir = Vector((sign, 0.0, 0.0))
        rotation = old_dir.rotation_difference(new_dir)

        pivot = upper.head.copy()

        # Collect all bones in the subtree (upper_arm and everything below it)
        def collect_subtree(bone):
            bones = [bone]
            for child in bone.children:
                bones.extend(collect_subtree(child))
            return bones

        subtree = collect_subtree(upper)

        # Disconnect all bones temporarily so auto-updates don't interfere
        was_connected = {}
        for bone in subtree:
            was_connected[bone.name] = bone.use_connect
            bone.use_connect = False

        # Rotate every bone in the subtree around the pivot
        for bone in subtree:
            _rotate_bone(bone, pivot, rotation)

        # Restore connection flags
        for bone in subtree:
            bone.use_connect = was_connected[bone.name]

    bpy.ops.object.mode_set(mode='OBJECT')


def _set_front_view(context):
    """Set the 3D viewport to front orthographic, framing all objects."""
    for area in context.screen.areas:
        if area.type == 'VIEW_3D':
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

class AUTORIG_SceneProps(bpy.types.PropertyGroup):
    game: EnumProperty(
        name="Game",
        description="Target game skeleton",
        items=[
            ('PZLA', "PZLA", "Pokemon Legends: Arceus"),
            ('SCARLET', "Scarlet", "Pokemon Scarlet/Violet"),
            ('SUNMOON', "Sun/Moon", "Pokemon Sun/Moon"),
        ],
        default='PZLA',
    )


# -------------------------------------------------------------------
# Panel
# -------------------------------------------------------------------

class AUTORIG_PT_panel(bpy.types.Panel):
    bl_label = "Auto Rig"
    bl_idname = "AUTORIG_PT_panel"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = 'Rig'

    def draw(self, context):
        layout = self.layout
        props = context.scene.autorig_props

        col = layout.column(align=True)
        col.scale_y = 1.4
        col.operator("autorig.new", icon='FILE_NEW')
        col.operator("autorig.reset_view", icon='VIEW_CAMERA')

        layout.separator()

        col = layout.column(align=True)
        col.scale_y = 1.4
        col.operator("autorig.load_model", icon='IMPORT')
        col.operator("autorig.load_animation", icon='ACTION')
        col.operator("autorig.unload_animation", icon='CANCEL')

        layout.separator()

        col = layout.column(align=True)
        col.scale_y = 1.4
        col.operator("autorig.fit_rig", icon='FULLSCREEN_ENTER')

        layout.separator()
        layout.prop(props, "game")

        layout.separator()

        col = layout.column(align=True)
        col.scale_y = 1.4
        col.operator("autorig.generate", icon='ARMATURE_DATA')



# -------------------------------------------------------------------
# Registration
# -------------------------------------------------------------------

classes = (
    AUTORIG_SceneProps,
    AUTORIG_OT_new,
    AUTORIG_OT_reset_view,
    AUTORIG_OT_load_model,
    AUTORIG_OT_load_animation,
    AUTORIG_OT_unload_animation,
    AUTORIG_OT_fit_rig,
    AUTORIG_OT_generate,
    AUTORIG_PT_panel,
)


def register():
    for cls in classes:
        bpy.utils.register_class(cls)
    bpy.types.Scene.autorig_props = PointerProperty(type=AUTORIG_SceneProps)


def unregister():
    del bpy.types.Scene.autorig_props
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
