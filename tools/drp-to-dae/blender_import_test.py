"""
Blender Import Test Script — Interactive File Picker
-----------------------------------------------------
Run this in Blender's Scripting tab.
Opens two file picker dialogs (model DAE, then animation DAE),
then merges the animation onto the model armature.

Check Window > Toggle System Console for diagnostic output.
"""

import bpy


def do_import(model_path, anim_path):
    """Deferred import — runs after context is valid."""

    print("\n=== IMPORT TEST ===")
    print(f"Model:     {model_path}")
    print(f"Animation: {anim_path}")

    # ── Clear scene first ─────────────────────────────────────────────────────
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)

    # ── Import model, track what was added ────────────────────────────────────
    before = set(bpy.data.objects.keys())
    bpy.ops.wm.collada_import(filepath=model_path)
    after = set(bpy.data.objects.keys())
    model_objects = [bpy.data.objects[n] for n in (after - before)]
    print(f"Model import added {len(model_objects)} objects: {[o.name for o in model_objects]}")

    model_armatures = [o for o in model_objects if o.type == "ARMATURE"]
    print(f"Model armatures: {[o.name for o in model_armatures]}")

    if not model_armatures:
        print("ERROR: No armature in model DAE!")
        return
    model_arm = model_armatures[0]
    print(f"Using model armature: {model_arm.name} ({len(model_arm.data.bones)} bones)")
    print(f"  First 5 bones: {[b.name for b in list(model_arm.data.bones)[:5]]}")

    # ── Import animation, track what was added ────────────────────────────────
    before2 = set(bpy.data.objects.keys())
    bpy.ops.wm.collada_import(filepath=anim_path)
    after2 = set(bpy.data.objects.keys())
    anim_objects = [bpy.data.objects[n] for n in (after2 - before2)]
    print(f"Anim import added {len(anim_objects)} objects: {[o.name for o in anim_objects]}")

    anim_armatures = [o for o in anim_objects if o.type == "ARMATURE"]
    print(f"Anim armatures: {[o.name for o in anim_armatures]}")

    if not anim_armatures:
        print("ERROR: No armature in animation DAE!")
        return
    anim_arm = anim_armatures[0]

    # ── Grab action ───────────────────────────────────────────────────────────
    anim_action = None
    if anim_arm.animation_data:
        anim_action = anim_arm.animation_data.action
    print(f"Animation action: {anim_action.name if anim_action else 'NONE'}")

    if not anim_action:
        # Check all actions in bpy.data.actions
        print(f"All actions in scene: {[a.name for a in bpy.data.actions]}")
        if bpy.data.actions:
            anim_action = bpy.data.actions[-1]
            print(f"Falling back to last action: {anim_action.name}")
        else:
            print("ERROR: No actions found at all!")
            return

    print(f"Action fcurves: {len(anim_action.fcurves)}")
    print(f"  First 5 channels: {[fc.data_path for fc in list(anim_action.fcurves)[:5]]}")

    # ── Assign action to model armature ───────────────────────────────────────
    if not model_arm.animation_data:
        model_arm.animation_data_create()
    model_arm.animation_data.action = anim_action
    print(f"Assigned action '{anim_action.name}' to '{model_arm.name}'")

    # ── Delete animation armature and its objects ─────────────────────────────
    for obj in anim_objects:
        bpy.data.objects.remove(obj, do_unlink=True)
    print("Removed animation import objects")

    # ── Set timeline ──────────────────────────────────────────────────────────
    bpy.context.scene.frame_start = int(anim_action.frame_range[0])
    bpy.context.scene.frame_end   = int(anim_action.frame_range[1])
    bpy.context.scene.frame_set(bpy.context.scene.frame_start)
    print(f"Timeline: {bpy.context.scene.frame_start} - {bpy.context.scene.frame_end}")

    # ── Select model armature ─────────────────────────────────────────────────
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = model_arm
    model_arm.select_set(True)

    print("=== DONE — Press Space to play ===\n")


class PickAnimOperator(bpy.types.Operator):
    bl_idname = "import_test.pick_anim"
    bl_label = "Select Animation DAE"
    filepath: bpy.props.StringProperty(subtype="FILE_PATH")
    filter_glob: bpy.props.StringProperty(default="*.dae", options={"HIDDEN"})

    def invoke(self, context, event):
        context.window_manager.fileselect_add(self)
        return {"RUNNING_MODAL"}

    def execute(self, context):
        model_path = context.scene.get("_import_model_path", "")
        anim_path = self.filepath
        bpy.app.timers.register(lambda: do_import(model_path, anim_path) or None, first_interval=0.1)
        return {"FINISHED"}


class PickModelOperator(bpy.types.Operator):
    bl_idname = "import_test.pick_model"
    bl_label = "Select Model DAE"
    filepath: bpy.props.StringProperty(subtype="FILE_PATH")
    filter_glob: bpy.props.StringProperty(default="*.dae", options={"HIDDEN"})

    def invoke(self, context, event):
        context.window_manager.fileselect_add(self)
        return {"RUNNING_MODAL"}

    def execute(self, context):
        context.scene["_import_model_path"] = self.filepath
        bpy.ops.import_test.pick_anim("INVOKE_DEFAULT")
        return {"FINISHED"}


for cls in [PickModelOperator, PickAnimOperator]:
    try:
        bpy.utils.unregister_class(cls)
    except Exception:
        pass
    bpy.utils.register_class(cls)

bpy.ops.import_test.pick_model("INVOKE_DEFAULT")
