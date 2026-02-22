# Blender diagnostic script — run from Blender's scripting tab
# Purpose: Import model + animation DAE and print very detailed diagnostics
# about what Blender actually sees (armatures, bones, actions, fcurves)
import bpy
import os

MODEL_PATH = r"D:\Projects\Starfield2026\tools\drp-to-dae\test-output\a038\model.dae"
ANIM_PATH = r"D:\Projects\Starfield2026\tools\drp-to-dae\test-output\a038\animations\a038hi_attack_anim.dae"

def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def import_dae(path, label):
    print(f"\n{'='*60}")
    print(f"IMPORTING {label}: {os.path.basename(path)}")
    print(f"{'='*60}")
    
    before = set(bpy.data.objects.keys())
    before_actions = set(bpy.data.actions.keys())
    before_armatures = set(bpy.data.armatures.keys())
    
    bpy.ops.wm.collada_import(filepath=path)
    
    after = set(bpy.data.objects.keys())
    after_actions = set(bpy.data.actions.keys())
    after_armatures = set(bpy.data.armatures.keys())
    
    new_objects = after - before
    new_actions = after_actions - before_actions
    new_armatures = after_armatures - before_armatures
    
    print(f"\nNew objects: {list(new_objects)}")
    print(f"New actions: {list(new_actions)}")
    print(f"New armatures: {list(new_armatures)}")
    
    return new_objects, new_actions, new_armatures

def dump_armatures():
    print(f"\n{'='*60}")
    print("ALL ARMATURES IN SCENE")
    print(f"{'='*60}")
    for obj in bpy.data.objects:
        if obj.type == 'ARMATURE':
            arm = obj.data
            print(f"\nArmature object: '{obj.name}' (data: '{arm.name}')")
            print(f"  Bones ({len(arm.bones)}):")
            for i, bone in enumerate(arm.bones):
                if i < 20:
                    print(f"    [{i}] '{bone.name}'")
            if len(arm.bones) > 20:
                print(f"    ... and {len(arm.bones) - 20} more")
            
            if obj.animation_data:
                print(f"  animation_data.action: {obj.animation_data.action}")
                if obj.animation_data.action:
                    print(f"  Action name: '{obj.animation_data.action.name}'")
            else:
                print(f"  animation_data: None")

def dump_actions():
    print(f"\n{'='*60}")
    print("ALL ACTIONS IN FILE")
    print(f"{'='*60}")
    for action in bpy.data.actions:
        print(f"\nAction: '{action.name}'")
        print(f"  frame_range: {action.frame_range}")
        print(f"  fcurves count: {len(action.fcurves)}")
        
        if len(action.fcurves) == 0:
            print(f"  WARNING: Action has ZERO fcurves!")
        
        groups = {}
        for fc in action.fcurves:
            dp = fc.data_path
            if dp not in groups:
                groups[dp] = []
            groups[dp].append(fc.array_index)
        
        print(f"  Unique data_paths: {len(groups)}")
        for i, (dp, indices) in enumerate(groups.items()):
            if i < 10:
                print(f"    '{dp}' indices={indices} keyframes={len(fc.keyframe_points)}")
            elif i == 10:
                print(f"    ... and {len(groups) - 10} more data_paths")

def dump_all():
    print(f"\n{'='*60}")
    print("ALL OBJECTS IN SCENE")
    print(f"{'='*60}")
    for obj in bpy.data.objects:
        print(f"  '{obj.name}' type={obj.type}")

def run_diagnostic():
    clear_scene()
    
    # Need to defer after factory reset
    def do_import():
        print("\n\nSTARTING DIAGNOSTIC IMPORT")
        print("="*60)
        
        # Import model
        model_objs, model_actions, model_armatures = import_dae(MODEL_PATH, "MODEL")
        dump_all()
        dump_armatures()
        dump_actions()
        
        # Import animation
        anim_objs, anim_actions, anim_armatures = import_dae(ANIM_PATH, "ANIMATION")
        dump_all()
        dump_armatures()
        dump_actions()
        
        # Try to find model armature and reassign animation
        model_arm_obj = None
        anim_action = None
        
        for obj in bpy.data.objects:
            if obj.type == 'ARMATURE' and obj.name not in anim_objs:
                model_arm_obj = obj
                break
        
        for action in bpy.data.actions:
            if action.name not in model_actions:
                anim_action = action
                break
        
        print(f"\n{'='*60}")
        print("REASSIGNMENT ATTEMPT")
        print(f"{'='*60}")
        print(f"Model armature: {model_arm_obj.name if model_arm_obj else 'NOT FOUND'}")
        print(f"Animation action: {anim_action.name if anim_action else 'NOT FOUND'}")
        
        if model_arm_obj and anim_action:
            if not model_arm_obj.animation_data:
                model_arm_obj.animation_data_create()
            model_arm_obj.animation_data.action = anim_action
            
            # Check if fcurve data_paths match bone names
            bone_names = set(b.name for b in model_arm_obj.data.bones)
            matched = 0
            unmatched = 0
            for fc in anim_action.fcurves:
                # Extract bone name from data_path like pose.bones["BoneName"].location
                dp = fc.data_path
                if 'pose.bones["' in dp:
                    bone_name = dp.split('pose.bones["')[1].split('"]')[0]
                    if bone_name in bone_names:
                        matched += 1
                    else:
                        if unmatched < 5:
                            print(f"  UNMATCHED fcurve bone: '{bone_name}' not in armature")
                        unmatched += 1
                else:
                    if unmatched < 5:
                        print(f"  NON-BONE fcurve: '{dp}'")
                    unmatched += 1
            
            print(f"\nFcurve bones matched: {matched}")
            print(f"Fcurve bones unmatched: {unmatched}")
            
            bpy.context.scene.frame_end = int(anim_action.frame_range[1])
            print(f"Timeline set to frame 0-{int(anim_action.frame_range[1])}")
        
        print(f"\n{'='*60}")
        print("DIAGNOSTIC COMPLETE")
        print(f"{'='*60}")
        return None
    
    bpy.app.timers.register(do_import, first_interval=0.1)

run_diagnostic()
