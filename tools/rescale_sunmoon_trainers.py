"""
Rescale sun-moon trainer DAE files to match scarlet scale (divide by 100).
Reads from Characters/sun-moon/, writes to Characters/sun-moon-v2/.
Also fixes texture refs (swaps to _1 variants).
"""
import os
import sys
import shutil
import xml.etree.ElementTree as ET
from pathlib import Path

CHARS_ROOT = Path(__file__).resolve().parent.parent / "src" / "Starfield2026.Assets" / "Models" / "Characters"
SRC_ROOT = CHARS_ROOT / "sun-moon"
DST_ROOT = CHARS_ROOT / "sun-moon-v2"
SCALE_FACTOR = 0.01

NS = "http://www.collada.org/2005/11/COLLADASchema"
ET.register_namespace("", NS)


def rescale_float_array(text, factor):
    values = text.split()
    return " ".join(f"{float(v) * factor:.6g}" if v.replace('.','',1).replace('-','',1).replace('e','',1).replace('E','',1).isdigit() else v for v in values)


def is_position_source(root, source_id):
    for vertices in root.iter(f"{{{NS}}}vertices"):
        for inp in vertices.findall(f"{{{NS}}}input"):
            if inp.get("semantic") == "POSITION" and inp.get("source", "").lstrip("#") == source_id:
                return True
    for channel in root.iter(f"{{{NS}}}channel"):
        target = channel.get("target", "")
        if "translate" in target or "location" in target:
            source_attr = channel.get("source", "").lstrip("#")
            for sampler in root.iter(f"{{{NS}}}sampler"):
                if sampler.get("id") == source_attr:
                    for inp in sampler.findall(f"{{{NS}}}input"):
                        if inp.get("semantic") == "OUTPUT" and inp.get("source", "").lstrip("#") == source_id:
                            return True
    return "position" in source_id.lower()


def rescale_dae(src_path, dst_path, factor):
    tree = ET.parse(str(src_path))
    root = tree.getroot()

    for source in root.iter(f"{{{NS}}}source"):
        source_id = source.get("id", "")
        accessor = source.find(f".//{{{NS}}}accessor")
        if accessor is not None:
            params = accessor.findall(f"{{{NS}}}param")
            if [p.get("name", "") for p in params] == ["X", "Y", "Z"]:
                if is_position_source(root, source_id):
                    fa = source.find(f"{{{NS}}}float_array")
                    if fa is not None and fa.text:
                        fa.text = rescale_float_array(fa.text, factor)

    for translate in root.iter(f"{{{NS}}}translate"):
        if translate.text:
            translate.text = rescale_float_array(translate.text, factor)

    for matrix in root.iter(f"{{{NS}}}matrix"):
        if matrix.text:
            vals = matrix.text.split()
            if len(vals) == 16:
                try:
                    for idx in [3, 7, 11]:
                        vals[idx] = f"{float(vals[idx]) * factor:.6g}"
                    matrix.text = " ".join(vals)
                except ValueError:
                    pass

    # Fix texture refs: swap to _1 variants
    image_ids = {img.get("id") for img in root.iter(f"{{{NS}}}image") if img.get("id")}
    for effect in root.iter(f"{{{NS}}}effect"):
        for surface in effect.iter(f"{{{NS}}}surface"):
            init_from = surface.find(f"{{{NS}}}init_from")
            if init_from is not None and init_from.text:
                new_ref = init_from.text + "_1"
                if new_ref in image_ids:
                    init_from.text = new_ref

    os.makedirs(str(dst_path.parent), exist_ok=True)
    tree.write(str(dst_path), xml_declaration=True, encoding="utf-8")


def main():
    dry_run = "--dry-run" in sys.argv

    if not SRC_ROOT.exists():
        print(f"ERROR: {SRC_ROOT} not found")
        sys.exit(1)

    print(f"Source: {SRC_ROOT}")
    print(f"Output: {DST_ROOT}")

    dae_count = 0
    copy_count = 0

    for src_file in sorted(SRC_ROOT.rglob("*")):
        if not src_file.is_file():
            continue
        rel = src_file.relative_to(SRC_ROOT)
        dst_file = DST_ROOT / rel

        # Skip cache files
        if src_file.name.endswith(".cached.glb") or src_file.name.endswith(".baked.glb"):
            continue

        if src_file.suffix == ".dae":
            if not dry_run:
                rescale_dae(src_file, dst_file, SCALE_FACTOR)
            dae_count += 1
        else:
            if not dry_run:
                os.makedirs(str(dst_file.parent), exist_ok=True)
                shutil.copy2(str(src_file), str(dst_file))
            copy_count += 1

    print(f"\nDone! Rescaled {dae_count} DAEs, copied {copy_count} other files.")
    if dry_run:
        print("(Dry run — no files written)")


if __name__ == "__main__":
    main()
