"""
Fix sun-moon Pokemon DAE texture references.

The sun-moon extraction produces two texture variants:
  - BodyA1.tga.png   = layer 0 (often black mask/lookup)
  - BodyA1.tga_1.png  = layer 1 (actual diffuse color)

The DAE effect surfaces reference the layer-0 image IDs.
This script swaps them to point at the _1 (color) variants.

Usage:
    python fix_sunmoon_textures.py --test pm0385_00   # test one model
    python fix_sunmoon_textures.py                     # fix all
    python fix_sunmoon_textures.py --dry-run            # preview changes
"""

import os
import sys
import xml.etree.ElementTree as ET

SUNMOON_ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", "src", "Starfield2026.Assets", "Models", "Pokemon", "sun-moon-v2"))

NS = "http://www.collada.org/2005/11/COLLADASchema"
ET.register_namespace("", NS)


def fix_texture_refs(dae_path, dry_run=False):
    """Swap effect surface init_from refs to _1 variants. Returns number of changes."""
    tree = ET.parse(dae_path)
    root = tree.getroot()

    # Collect all image IDs in the document
    image_ids = set()
    for img in root.iter(f"{{{NS}}}image"):
        iid = img.get("id", "")
        if iid:
            image_ids.add(iid)

    changes = 0
    for effect in root.iter(f"{{{NS}}}effect"):
        for surface in effect.iter(f"{{{NS}}}surface"):
            init_from = surface.find(f"{{{NS}}}init_from")
            if init_from is not None and init_from.text:
                old_ref = init_from.text
                new_ref = old_ref + "_1"
                if new_ref in image_ids:
                    init_from.text = new_ref
                    changes += 1

    if changes > 0 and not dry_run:
        tree.write(dae_path, xml_declaration=True, encoding="utf-8")

    return changes


def process_folder(folder_path, dry_run=False):
    """Fix texture refs in model.dae (not clips — clips are animations only)."""
    model_dae = os.path.join(folder_path, "model.dae")
    if not os.path.exists(model_dae):
        return 0
    return fix_texture_refs(model_dae, dry_run)


def main():
    dry_run = "--dry-run" in sys.argv
    test_model = None

    for i, arg in enumerate(sys.argv):
        if arg == "--test" and i + 1 < len(sys.argv):
            test_model = sys.argv[i + 1]

    if test_model:
        folder = os.path.join(SUNMOON_ROOT, test_model)
        if not os.path.isdir(folder):
            print(f"ERROR: Folder not found: {folder}")
            sys.exit(1)
        changes = process_folder(folder, dry_run)
        action = "Would fix" if dry_run else "Fixed"
        print(f"{action} {changes} texture refs in {test_model}")
        return

    if dry_run:
        print("=== DRY RUN ===\n")

    folders = sorted([
        f for f in os.listdir(SUNMOON_ROOT)
        if os.path.isdir(os.path.join(SUNMOON_ROOT, f)) and f.startswith("pm")
    ])

    print(f"Processing {len(folders)} folders in {SUNMOON_ROOT}")

    total_changes = 0
    fixed_models = 0
    for i, folder in enumerate(folders):
        path = os.path.join(SUNMOON_ROOT, folder)
        changes = process_folder(path, dry_run)
        if changes > 0:
            fixed_models += 1
            total_changes += changes
        if (i + 1) % 100 == 0:
            print(f"  Progress: {i + 1}/{len(folders)}...")

    action = "Would fix" if dry_run else "Fixed"
    print(f"\nDone! {action} {total_changes} texture refs across {fixed_models} models.")


if __name__ == "__main__":
    main()
