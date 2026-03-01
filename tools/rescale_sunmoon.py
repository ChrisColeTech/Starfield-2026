"""
Rescale sun-moon Pokemon DAE files to match scarlet scale (divide by 100).

Reads from sun-moon/, writes rescaled files to sun-moon-v2/.
Non-DAE files (textures, manifests, etc.) are copied as-is.

Usage:
    python rescale_sunmoon.py [--dry-run]
"""

import os
import sys
import shutil
import xml.etree.ElementTree as ET

POKEMON_ROOT = r"D:\Projects\Starfield-2026\src\Starfield2026.Assets\Models\Pokemon"
SUNMOON_ROOT = os.path.join(POKEMON_ROOT, "sun-moon")
OUTPUT_ROOT = os.path.join(POKEMON_ROOT, "sun-moon-v2")
SCALE_FACTOR = 0.01  # divide by 100

# COLLADA namespace
NS = "http://www.collada.org/2005/11/COLLADASchema"
ET.register_namespace("", NS)


def rescale_float_array(text, factor):
    """Multiply all floats in a space-separated string by factor."""
    values = text.split()
    scaled = []
    for v in values:
        try:
            scaled.append(f"{float(v) * factor:.6g}")
        except ValueError:
            scaled.append(v)
    return " ".join(scaled)


def is_position_source(root, source_id):
    """Check if a source ID is referenced as POSITION input anywhere."""
    for vertices in root.iter(f"{{{NS}}}vertices"):
        for inp in vertices.findall(f"{{{NS}}}input"):
            if inp.get("semantic") == "POSITION":
                ref = inp.get("source", "").lstrip("#")
                if ref == source_id:
                    return True

    for channel in root.iter(f"{{{NS}}}channel"):
        target = channel.get("target", "")
        if "translate" in target or "location" in target:
            source_attr = channel.get("source", "").lstrip("#")
            for sampler in root.iter(f"{{{NS}}}sampler"):
                if sampler.get("id") == source_attr:
                    for inp in sampler.findall(f"{{{NS}}}input"):
                        if inp.get("semantic") == "OUTPUT":
                            ref = inp.get("source", "").lstrip("#")
                            if ref == source_id:
                                return True

    return "position" in source_id.lower()


def rescale_dae(src_path, dst_path, factor, dry_run=False):
    """Rescale position data in a COLLADA DAE file, write to dst_path."""
    tree = ET.parse(src_path)
    root = tree.getroot()

    modified = False

    for source in root.iter(f"{{{NS}}}source"):
        source_id = source.get("id", "")
        is_position = False

        accessor = source.find(f".//{{{NS}}}accessor")
        if accessor is not None:
            params = accessor.findall(f"{{{NS}}}param")
            param_names = [p.get("name", "") for p in params]
            if param_names == ["X", "Y", "Z"]:
                is_position = is_position_source(root, source_id)

        if is_position:
            float_array = source.find(f"{{{NS}}}float_array")
            if float_array is not None and float_array.text:
                float_array.text = rescale_float_array(float_array.text, factor)
                modified = True

    for translate in root.iter(f"{{{NS}}}translate"):
        if translate.text:
            translate.text = rescale_float_array(translate.text, factor)
            modified = True

    for matrix in root.iter(f"{{{NS}}}matrix"):
        if matrix.text:
            vals = matrix.text.split()
            if len(vals) == 16:
                try:
                    for idx in [3, 7, 11]:
                        vals[idx] = f"{float(vals[idx]) * factor:.6g}"
                    matrix.text = " ".join(vals)
                    modified = True
                except ValueError:
                    pass

    if not dry_run:
        os.makedirs(os.path.dirname(dst_path), exist_ok=True)
        tree.write(dst_path, xml_declaration=True, encoding="utf-8")

    return modified


def process_folder(src_folder, dst_folder, factor, dry_run=False):
    """Process all files in a Pokemon folder. Rescale DAEs, copy everything else."""
    dae_count = 0

    for root_dir, dirs, files in os.walk(src_folder):
        rel = os.path.relpath(root_dir, src_folder)
        out_dir = os.path.join(dst_folder, rel) if rel != "." else dst_folder

        for f in files:
            src_path = os.path.join(root_dir, f)
            dst_path = os.path.join(out_dir, f)

            if f.endswith(".dae"):
                rescale_dae(src_path, dst_path, factor, dry_run)
                dae_count += 1
            elif not f.endswith(".cached.glb") and not f.endswith(".baked.glb"):
                # Copy non-DAE, non-cache files as-is
                if not dry_run:
                    os.makedirs(out_dir, exist_ok=True)
                    shutil.copy2(src_path, dst_path)

    return dae_count


def main():
    dry_run = "--dry-run" in sys.argv

    if dry_run:
        print("=== DRY RUN — no files will be written ===\n")

    if not os.path.isdir(SUNMOON_ROOT):
        print(f"ERROR: Directory not found: {SUNMOON_ROOT}")
        sys.exit(1)

    folders = sorted([
        f for f in os.listdir(SUNMOON_ROOT)
        if os.path.isdir(os.path.join(SUNMOON_ROOT, f)) and f.startswith("pm")
    ])

    print(f"Source: {SUNMOON_ROOT}")
    print(f"Output: {OUTPUT_ROOT}")
    print(f"Found {len(folders)} Pokemon folders")
    print(f"Scale factor: {SCALE_FACTOR} (divide by {1/SCALE_FACTOR:.0f})\n")

    total_files = 0
    for i, folder in enumerate(folders):
        src = os.path.join(SUNMOON_ROOT, folder)
        dst = os.path.join(OUTPUT_ROOT, folder)
        count = process_folder(src, dst, SCALE_FACTOR, dry_run)
        total_files += count
        if (i + 1) % 50 == 0:
            print(f"  Progress: {i + 1}/{len(folders)} folders...")

    print(f"\nDone! Processed {total_files} DAE files across {len(folders)} folders.")
    if dry_run:
        print("(Dry run — no files were written)")
    else:
        print(f"Output written to: {OUTPUT_ROOT}")


if __name__ == "__main__":
    main()
