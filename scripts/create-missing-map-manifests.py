"""
Create manifest.json for map model folders that are missing one.
Follows the established manifest format used by existing map models.

Usage:
  python create-missing-map-manifests.py <maps-root>
  python create-missing-map-manifests.py D:/Projects/Starfield-2026/src/Starfield2026.Assets/Models/Maps
  python create-missing-map-manifests.py --dry-run D:/Projects/Starfield-2026/src/Starfield2026.Assets/Models/Maps
"""

import json
import os
import sys

ASSETS_ROOT = "D:/Projects/Starfield-2026/src/Starfield2026.Assets/Models"


def find_textures(model_dir):
    """List texture files relative to model_dir."""
    tex_dir = os.path.join(model_dir, "textures")
    if not os.path.isdir(tex_dir):
        return []
    return sorted(
        f"textures/{f}"
        for f in os.listdir(tex_dir)
        if f.lower().endswith((".png", ".jpg", ".jpeg", ".tga", ".bmp"))
    )


def find_clips(model_dir):
    """List clip DAE files relative to model_dir."""
    clips_dir = os.path.join(model_dir, "clips")
    if not os.path.isdir(clips_dir):
        return []
    return sorted(
        f for f in os.listdir(clips_dir)
        if f.lower().endswith(".dae") and f.startswith("clip_")
    )


def create_manifest(model_dir, assets_root, dry_run=False):
    """Create manifest.json for a model folder. Returns True if created."""
    manifest_path = os.path.join(model_dir, "manifest.json")
    if os.path.exists(manifest_path):
        return False

    model_file = None
    for candidate in ("model.dae", "model.obj", "model.fbx"):
        if os.path.exists(os.path.join(model_dir, candidate)):
            model_file = candidate
            break

    if model_file is None:
        return False

    folder_name = os.path.basename(model_dir)
    abs_dir = model_dir.replace("\\", "/")
    rel_path = os.path.relpath(model_dir, assets_root).replace("\\", "/")
    ext = os.path.splitext(model_file)[1].lstrip(".").lower()

    textures = find_textures(model_dir)
    clip_files = find_clips(model_dir)

    clips = []
    for clip_file in clip_files:
        import re
        match = re.match(r"clip_(\d+)\.dae", clip_file)
        if match:
            idx = int(match.group(1))
            clips.append({
                "index": idx,
                "id": f"clip_{idx:03d}",
                "sourceName": f"clip_{idx:03d}",
                "file": f"clips/{clip_file}",
                "frameCount": 0,
                "fps": 30,
                "boneCount": 0,
            })

    manifest = {
        "version": 1,
        "format": ext,
        "animationMode": "split",
        "modelFile": model_file,
        "textures": textures,
        "clips": clips,
        "name": folder_name,
        "dir": abs_dir,
        "modelFormat": ext,
        "assetsPath": rel_path,
    }

    if not dry_run:
        with open(manifest_path, "w", encoding="utf-8") as f:
            json.dump(manifest, f, indent=2)

    return True


def main():
    args = sys.argv[1:]
    dry_run = "--dry-run" in args
    if dry_run:
        args.remove("--dry-run")

    if not args:
        print("Usage: python create-missing-map-manifests.py [--dry-run] <maps-root>")
        sys.exit(1)

    maps_root = os.path.abspath(args[0]).replace("\\", "/")
    if not os.path.isdir(maps_root):
        print(f"Error: {maps_root} is not a directory")
        sys.exit(1)

    print(f"Maps root: {maps_root}")
    print(f"Assets root: {ASSETS_ROOT}")
    print(f"Dry run: {dry_run}")

    created = 0
    scanned = 0

    for root, dirs, files in os.walk(maps_root):
        if "model.dae" in files or "model.obj" in files or "model.fbx" in files:
            scanned += 1
            root_norm = root.replace("\\", "/")
            if create_manifest(root_norm, ASSETS_ROOT, dry_run):
                created += 1
                action = "WOULD CREATE" if dry_run else "CREATED"
                print(f"  {action}: {os.path.basename(root_norm)}")

    action = "would create" if dry_run else "created"
    print(f"\nDone: {scanned} model folders scanned, {created} manifests {action}")

    if dry_run and created > 0:
        print("\nRe-run without --dry-run to apply changes.")


if __name__ == "__main__":
    main()
