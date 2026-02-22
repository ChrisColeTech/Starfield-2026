"""
Blender script to import animation clips onto an existing model armature.

Usage (inside Blender):
  1. Import model.dae (File > Import > COLLADA)
  2. Select the model's armature in the outliner
  3. Run this script (Text Editor > Open > Run Script)
     OR paste into Blender's Python console

The script will:
  - Prompt for the clips/ folder (or read manifest.json)
  - Import each clip DAE
  - Transfer the Action from the temp armature to the selected armature
  - Delete the temp armature
  - Result: one armature with all clips as Actions in the Action Editor

Requirements:
  - Blender 3.x+ with COLLADA import enabled
  - Model armature must be selected before running
"""

import bpy
import os
import json


def import_clips(clips_dir, manifest_path=None, armature_name=None):
    """Import all clip DAEs from clips_dir and attach Actions to the target armature."""

    # Find target armature
    if armature_name:
        target_arm = bpy.data.objects.get(armature_name)
    else:
        target_arm = bpy.context.active_object
        if target_arm and target_arm.type != 'ARMATURE':
            # Try to find the first armature in the scene
            for obj in bpy.context.scene.objects:
                if obj.type == 'ARMATURE':
                    target_arm = obj
                    break

    if not target_arm or target_arm.type != 'ARMATURE':
        print("ERROR: No armature selected or found. Select the model armature first.")
        return

    print(f"Target armature: {target_arm.name}")

    # Gather clip files
    clip_files = []
    clip_names = {}

    if manifest_path and os.path.exists(manifest_path):
        with open(manifest_path, 'r') as f:
            manifest = json.load(f)
        for clip in manifest.get('clips', []):
            clip_file = clip['file']
            # manifest paths are relative to the pokemon dir (parent of clips/)
            full_path = os.path.normpath(os.path.join(os.path.dirname(manifest_path), clip_file))
            if os.path.exists(full_path):
                clip_files.append(full_path)
                clip_names[full_path] = clip.get('name', os.path.splitext(os.path.basename(clip_file))[0])
    else:
        # Scan clips directory
        for f in sorted(os.listdir(clips_dir)):
            if f.endswith('.dae'):
                full_path = os.path.join(clips_dir, f)
                clip_files.append(full_path)
                clip_names[full_path] = os.path.splitext(f)[0]

    if not clip_files:
        print(f"No clip DAE files found in {clips_dir}")
        return

    print(f"Found {len(clip_files)} clips to import")

    # Track existing armatures and actions before import
    existing_armatures = set(obj.name for obj in bpy.data.objects if obj.type == 'ARMATURE')
    existing_actions = set(act.name for act in bpy.data.actions)

    imported = 0
    for clip_path in clip_files:
        clip_name = clip_names.get(clip_path, f"clip_{imported}")
        print(f"  Importing: {os.path.basename(clip_path)} as '{clip_name}'...")

        # Import the clip DAE
        bpy.ops.wm.collada_import(filepath=clip_path)

        # Find new armature(s) created by import
        new_armatures = [
            obj for obj in bpy.data.objects
            if obj.type == 'ARMATURE' and obj.name not in existing_armatures
        ]

        # Find new action(s) created by import
        new_actions = [
            act for act in bpy.data.actions
            if act.name not in existing_actions
        ]

        if new_actions:
            # Take the first new action, rename it, and assign to target
            action = new_actions[0]
            action.name = clip_name

            # Ensure target armature has animation data
            if not target_arm.animation_data:
                target_arm.animation_data_create()

            # Push to NLA as a strip (preserves all clips)
            # First assign as active action to verify it works
            target_arm.animation_data.action = action
            print(f"    Action '{clip_name}' assigned ({action.frame_range[0]:.0f}-{action.frame_range[1]:.0f})")

            # Push to NLA track so it doesn't get overwritten by next clip
            track = target_arm.animation_data.nla_tracks.new()
            track.name = clip_name
            strip = track.strips.new(clip_name, int(action.frame_range[0]), action)
            strip.name = clip_name
            track.mute = True  # Mute so only active action plays

            # Clear active action so next import doesn't conflict
            target_arm.animation_data.action = None

            existing_actions.add(action.name)
        else:
            print(f"    WARNING: No new action found after importing {os.path.basename(clip_path)}")

        # Delete temp armature(s) and their meshes
        for arm_obj in new_armatures:
            # Delete any child objects first
            for child in arm_obj.children:
                bpy.data.objects.remove(child, do_unlink=True)
            bpy.data.objects.remove(arm_obj, do_unlink=True)

        existing_armatures = set(obj.name for obj in bpy.data.objects if obj.type == 'ARMATURE')
        imported += 1

    print(f"Done. Imported {imported} clips onto '{target_arm.name}'")
    print(f"Switch clips in Dope Sheet > Action Editor, or use NLA Editor")


# --- Entry point ---
# Auto-detect paths: look for manifest.json or clips/ next to the blend file or active model

if __name__ == "__main__":
    # Try to auto-detect from file browser or manual path
    # Users can also call import_clips() directly from Blender's Python console:
    #   import blender_import_clips
    #   blender_import_clips.import_clips("/path/to/clips/", manifest_path="/path/to/manifest.json")

    print("=" * 60)
    print("Pokemon Clip Importer")
    print("=" * 60)
    print()
    print("To use this script:")
    print("  1. Import model.dae first (File > Import > COLLADA)")
    print("  2. Select the model armature")
    print("  3. In Blender's Python console, run:")
    print()
    print('     import sys')
    print('     sys.path.append("/path/to/Starfield/tools")')
    print('     import blender_import_clips')
    print('     blender_import_clips.import_clips(')
    print('         "/path/to/pm0001_00/clips",')
    print('         manifest_path="/path/to/pm0001_00/manifest.json"')
    print('     )')
    print()
    print("  Or just provide the clips directory:")
    print('     blender_import_clips.import_clips("/path/to/pm0001_00/clips")')
