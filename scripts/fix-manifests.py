"""
Fix existing manifests to include fields required by BgEditor:
  - name: inferred from parent folder name
  - dir: absolute path to manifest directory (forward slashes)
  - assetsPath: relative path from assets root to manifest dir (forward slashes)
  - modelFormat: file extension of modelFile (e.g. "dae")

Preserves all existing fields (version, format, animationMode, modelFile, textures, clips, etc.)

Usage:
  python fix-manifests.py <assets-root>
  python fix-manifests.py D:/Projects/Starfield-2026/src/Starfield2026.Assets/Models
  python fix-manifests.py --dry-run D:/Projects/Starfield-2026/src/Starfield2026.Assets/Models
"""

import json
import os
import sys

def fix_manifest(manifest_path: str, assets_root: str, dry_run: bool = False) -> bool:
    """Fix a single manifest. Returns True if modified."""
    with open(manifest_path, "r", encoding="utf-8") as f:
        data = json.load(f)

    manifest_dir = os.path.dirname(manifest_path).replace("\\", "/")
    folder_name = os.path.basename(manifest_dir)

    changed = False

    # modelFile: if missing but "models" array exists, promote models[0].file
    if "modelFile" not in data:
        models = data.get("models")
        if models and len(models) > 0 and "file" in models[0]:
            data["modelFile"] = models[0]["file"]
            changed = True

    # name: use parent folder name
    if "name" not in data:
        data["name"] = folder_name
        changed = True

    # dir: absolute path with forward slashes
    if "dir" not in data:
        data["dir"] = manifest_dir
        changed = True

    # modelFormat: extension of modelFile
    if "modelFormat" not in data:
        model_file = data.get("modelFile", "")
        ext = os.path.splitext(model_file)[1].lstrip(".").lower()
        if not ext:
            # Fall back to "format" field
            ext = data.get("format", "dae")
        data["modelFormat"] = ext
        changed = True

    # assetsPath: relative path from assets root
    if "assetsPath" not in data:
        rel = os.path.relpath(manifest_dir, assets_root).replace("\\", "/")
        data["assetsPath"] = rel
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
        print("Usage: python fix-manifests.py [--dry-run] <assets-root>")
        sys.exit(1)

    assets_root = os.path.abspath(args[0]).replace("\\", "/")
    if not os.path.isdir(assets_root):
        print(f"Error: {assets_root} is not a directory")
        sys.exit(1)

    print(f"Assets root: {assets_root}")
    print(f"Dry run: {dry_run}")

    total = 0
    fixed = 0
    errors = 0

    for root, dirs, files in os.walk(assets_root):
        for f in files:
            if f == "manifest.json" or (f.startswith("manifest.") and f.endswith(".json")):
                total += 1
                path = os.path.join(root, f)
                try:
                    if fix_manifest(path, assets_root, dry_run):
                        fixed += 1
                        if dry_run:
                            print(f"  WOULD FIX: {path}")
                except Exception as e:
                    errors += 1
                    print(f"  ERROR: {path}: {e}")

    action = "would fix" if dry_run else "fixed"
    print(f"\nDone: {total} manifests found, {fixed} {action}, {errors} errors")

    if dry_run and fixed > 0:
        print("\nRe-run without --dry-run to apply changes.")


if __name__ == "__main__":
    main()
