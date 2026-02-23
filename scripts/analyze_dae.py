"""Analyze bone structure in model.dae and clip DAE files to understand animation targeting."""
import re
import sys
import os

BASE = r"D:\Projects\Starfield-2026\src\Starfield2026.Assets\Models\Characters\PZLA\tr0001_00"

def extract_joints(filepath):
    """Extract JOINT node ids, names, and sids from a DAE file."""
    with open(filepath, encoding='utf-8') as f:
        text = f.read()
    # <node id="chara_root_id" name="chara_root" type="JOINT" sid="chara_root">
    pattern = r'<node\s+id="([^"]*)"[^>]*name="([^"]*)"[^>]*type="JOINT"[^>]*sid="([^"]*)"'
    matches = re.findall(pattern, text)
    if not matches:
        # Try alternate attribute order
        pattern2 = r'<node[^>]*type="JOINT"[^>]*id="([^"]*)"[^>]*name="([^"]*)"[^>]*sid="([^"]*)"'
        matches = re.findall(pattern2, text)
    return matches  # list of (id, name, sid)

def extract_channels(filepath):
    """Extract animation channel targets from a DAE file."""
    with open(filepath, encoding='utf-8') as f:
        text = f.read()
    # <channel source="#anim_source" target="bone_id/transform"/>
    targets = re.findall(r'<channel[^>]*target="([^"]*)"', text)
    return targets

def main():
    model_path = os.path.join(BASE, "model.dae")
    clip_path = os.path.join(BASE, "clips", "clip_000.dae")
    
    print("=" * 80)
    print(f"MODEL: {model_path}")
    print("=" * 80)
    
    model_joints = extract_joints(model_path)
    print(f"Total JOINT nodes: {len(model_joints)}")
    print(f"\nFirst 15 bones (id | name | sid):")
    for id_, name, sid in model_joints[:15]:
        print(f"  {id_:40s} | {name:30s} | {sid}")
    
    print(f"\n{'=' * 80}")
    print(f"CLIP: {clip_path}")
    print("=" * 80)
    
    clip_joints = extract_joints(clip_path)
    print(f"Total JOINT nodes: {len(clip_joints)}")
    print(f"\nFirst 15 bones (id | name | sid):")
    for id_, name, sid in clip_joints[:15]:
        print(f"  {id_:40s} | {name:30s} | {sid}")
    
    # Check for name overlap
    model_names = {name for _, name, _ in model_joints}
    clip_names = {name for _, name, _ in clip_joints}
    model_ids = {id_ for id_, _, _ in model_joints}
    clip_ids = {id_ for id_, _, _ in clip_joints}
    model_sids = {sid for _, _, sid in model_joints}
    clip_sids = {sid for _, _, sid in clip_joints}
    
    print(f"\n{'=' * 80}")
    print("BONE NAME OVERLAP ANALYSIS")
    print("=" * 80)
    print(f"  Model names:  {len(model_names)}")
    print(f"  Clip names:   {len(clip_names)}")
    print(f"  Shared names: {len(model_names & clip_names)}")
    print(f"  Model IDs:    {len(model_ids)}")
    print(f"  Clip IDs:     {len(clip_ids)}")
    print(f"  Shared IDs:   {len(model_ids & clip_ids)}")
    print(f"  Model SIDs:   {len(model_sids)}")
    print(f"  Clip SIDs:    {len(clip_sids)}")
    print(f"  Shared SIDs:  {len(model_sids & clip_sids)}")
    
    if model_names & clip_names:
        shared = sorted(model_names & clip_names)
        print(f"\n  Shared bone names (first 10): {shared[:10]}")
    
    # Animation channels
    print(f"\n{'=' * 80}")
    print("ANIMATION CHANNELS IN CLIP")
    print("=" * 80)
    channels = extract_channels(clip_path)
    print(f"Total channels: {len(channels)}")
    
    # Parse channel targets: "bone_id/property"
    target_bones = set()
    for ch in channels:
        bone = ch.split("/")[0]
        target_bones.add(bone)
    
    print(f"Unique target bones: {len(target_bones)}")
    print(f"\nFirst 15 channel targets:")
    for ch in channels[:15]:
        print(f"  {ch}")
    
    # Check if channel targets use ids, names, or sids
    targets_in_model_ids = target_bones & model_ids
    targets_in_model_names = target_bones & model_names
    targets_in_model_sids = target_bones & model_sids
    targets_in_clip_ids = target_bones & clip_ids
    
    print(f"\n{'=' * 80}")
    print("CHANNEL TARGET → BONE MATCHING")
    print("=" * 80)
    print(f"  Targets matching MODEL node IDs:   {len(targets_in_model_ids)}/{len(target_bones)}")
    print(f"  Targets matching MODEL names:      {len(targets_in_model_names)}/{len(target_bones)}")
    print(f"  Targets matching MODEL SIDs:       {len(targets_in_model_sids)}/{len(target_bones)}")
    print(f"  Targets matching CLIP node IDs:    {len(targets_in_clip_ids)}/{len(target_bones)}")
    
    # Show unmatched targets
    unmatched = target_bones - model_ids - model_names - model_sids - clip_ids
    if unmatched:
        print(f"\n  Unmatched targets (first 10): {sorted(unmatched)[:10]}")
    
    # Key question: what does Three.js ColladaLoader use for bone names?
    print(f"\n{'=' * 80}")
    print("THREE.JS COLLADALOADER BONE NAMING")
    print("=" * 80)
    print("  ColladaLoader uses the 'id' attribute as the Object3D.name")
    print("  (if no 'name' attribute is present, otherwise 'name')")
    print(f"  Model bone names that Three.js would see:")
    for id_, name, sid in model_joints[:5]:
        threejs_name = name if name else id_
        print(f"    {threejs_name}")
    print(f"  Clip bone names that Three.js would see:")
    for id_, name, sid in clip_joints[:5]:
        threejs_name = name if name else id_
        print(f"    {threejs_name}")

if __name__ == "__main__":
    main()
