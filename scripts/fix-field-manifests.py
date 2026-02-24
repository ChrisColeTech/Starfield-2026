"""
Fix field character manifests:
  1. Populate empty "clips" arrays from actual clip files on disk
  2. Fix "dir" to point to the actual manifest directory (not old export path)
  3. Fix "assetsPath" relative to the assets root
  4. Add "name" if missing

Usage:
  python fix-field-manifests.py <field-dir>
  python fix-field-manifests.py D:/Projects/Starfield-2026/src/Starfield2026.Assets/Models/Characters/sun-moon/field
  python fix-field-manifests.py --dry-run D:/Projects/Starfield-2026/src/Starfield2026.Assets/Models/Characters/sun-moon/field
"""

import json
import os
import re
import sys
import xml.etree.ElementTree as ET

ASSETS_ROOT = "D:/Projects/Starfield-2026/src/Starfield2026.Assets/Models"


def parse_clip_dae(clip_path: str) -> dict:
    """Extract animation metadata from a clip DAE file."""
    info = {"frameCount": 0, "fps": 30, "sourceName": ""}
    try:
        tree = ET.parse(clip_path)
        root = tree.getroot()
        ns = {"c": "http://www.collada.org/2005/11/COLLADASchema"}

        # Try to get animation name
        anims = root.findall(".//c:animation", ns)
        if anims:
            anim_id = anims[0].get("id", "") or anims[0].get("name", "")
            if anim_id:
                info["sourceName"] = anim_id

        # Try to get frame count and fps from first float_array (time values)
        for accessor in root.findall(".//c:source/c:technique_common/c:accessor", ns):
            param = accessor.find("c:param", ns)
            if param is not None and param.get("name") == "TIME":
                count = int(accessor.get("count", "0"))
                float_array_id = accessor.get("source", "").lstrip("#")
                float_array = root.find(f".//c:float_array[@id='{float_array_id}']", ns)
                if float_array is not None and float_array.text and count > 1:
                    times = [float(t) for t in float_array.text.strip().split()]
                    if len(times) >= 2:
                        duration = times[-1] - times[0]
                        if duration > 0:
                            fps = round((count - 1) / duration)
                            info["fps"] = fps if fps > 0 else 30
                            info["frameCount"] = count
                break
    except Exception:
        pass
    return info


def fix_manifest(manifest_path: str, dry_run: bool = False) -> bool:
    """Fix a single manifest. Returns True if modified."""
    with open(manifest_path, "r", encoding="utf-8") as f:
        data = json.load(f)

    manifest_dir = os.path.dirname(manifest_path).replace("\\", "/")
    folder_name = os.path.basename(manifest_dir)
    changed = False

    # Fix dir to actual location
    if data.get("dir") != manifest_dir:
        data["dir"] = manifest_dir
        changed = True

    # Fix name
    if data.get("name") != folder_name:
        data["name"] = folder_name
        changed = True

    # Fix assetsPath
    rel = os.path.relpath(manifest_dir, ASSETS_ROOT).replace("\\", "/")
    if data.get("assetsPath") != rel:
        data["assetsPath"] = rel
        changed = True

    # Fix modelFormat
    if "modelFormat" not in data:
        model_file = data.get("modelFile", "")
        ext = os.path.splitext(model_file)[1].lstrip(".").lower()
        if not ext:
            ext = data.get("format", "dae")
        data["modelFormat"] = ext
        changed = True

    # Rebuild clips array from disk
    clips_dir = os.path.join(manifest_dir, "clips").replace("\\", "/")

    if os.path.isdir(clips_dir):
        clip_files = sorted(
            f for f in os.listdir(clips_dir)
            if f.endswith(".dae") and f.startswith("clip_")
        )

        clips = []
        for clip_file in clip_files:
            match = re.match(r"clip_(\d+)\.dae", clip_file)
            if not match:
                continue
            idx = int(match.group(1))
            clip_id = f"clip_{idx:03d}"
            clip_path = os.path.join(clips_dir, clip_file)
            rel_file = f"clips/{clip_file}"

            meta = parse_clip_dae(clip_path)

            clips.append({
                "index": idx,
                "id": clip_id,
                "sourceName": meta["sourceName"] or clip_id,
                "file": rel_file,
                "frameCount": meta["frameCount"],
                "fps": meta["fps"],
                "boneCount": 0,
            })

        if clips != data.get("clips", []):
            data["clips"] = clips
            changed = True

    if changed and not dry_run:
        with open(manifest_path, "w", encoding="utf-8") as f:
            json.dump(data, f, indent=2)

    return changed


def main():
    args = sys.argv[1:]
    dry_run = "--dry-run" in args
    if dry_run:
        args.remove("--dry-run")

    if not args:
        print("Usage: python fix-field-manifests.py [--dry-run] <field-dir>")
        sys.exit(1)

    field_dir = os.path.abspath(args[0]).replace("\\", "/")
    if not os.path.isdir(field_dir):
        print(f"Error: {field_dir} is not a directory")
        sys.exit(1)

    print(f"Field dir: {field_dir}")
    print(f"Assets root: {ASSETS_ROOT}")
    print(f"Dry run: {dry_run}")

    total = 0
    fixed = 0
    errors = 0
    total_clips = 0

    for entry in sorted(os.listdir(field_dir)):
        entry_path = os.path.join(field_dir, entry)
        manifest_path = os.path.join(entry_path, "manifest.json")
        if not os.path.isfile(manifest_path):
            continue

        total += 1
        try:
            if fix_manifest(manifest_path, dry_run):
                fixed += 1
                # Count clips added
                with open(manifest_path, "r", encoding="utf-8") as f:
                    data = json.load(f)
                n_clips = len(data.get("clips", []))
                total_clips += n_clips
                action = "WOULD FIX" if dry_run else "FIXED"
                print(f"  {action}: {entry} ({n_clips} clips)")
        except Exception as e:
            errors += 1
            print(f"  ERROR: {entry}: {e}")

    action = "would fix" if dry_run else "fixed"
    print(f"\nDone: {total} manifests found, {fixed} {action}, {total_clips} clips added, {errors} errors")

    if dry_run and fixed > 0:
        print("\nRe-run without --dry-run to apply changes.")


if __name__ == "__main__":
    main()
