"""
Merge battle animation clips into field character folders.

For each Sun/Moon character that exists in both battle/ and field/:
1. Find battle clips whose slot name doesn't already exist in field
2. Copy the .dae clip files into field/clips/ as battle_clip_NNN.dae
3. Add entries to the field manifest.json with updated file paths

Field versions win on duplicate slot names (e.g. both have anim_0).
Battle clips play on the field skeleton — the loader skips bone tracks
for bones that don't exist in the target rig.

Usage:
    python merge_battle_clips.py [--dry-run]
"""

import json
import shutil
import sys
from pathlib import Path

ASSETS_ROOT = Path(__file__).resolve().parent.parent / "src" / "Starfield2026.Assets"
SUNMOON = ASSETS_ROOT / "Models" / "Characters" / "sun-moon"
BATTLE_DIR = SUNMOON / "battle"
FIELD_DIR = SUNMOON / "field"


def merge_character(char_id: str, dry_run: bool) -> dict:
    """Merge battle clips into field for one character. Returns stats."""
    battle_path = BATTLE_DIR / char_id
    field_path = FIELD_DIR / char_id
    battle_manifest = battle_path / "manifest.json"
    field_manifest = field_path / "manifest.json"

    if not battle_manifest.exists() or not field_manifest.exists():
        return {"skipped": True, "reason": "missing manifest"}

    with open(battle_manifest, "r", encoding="utf-8") as f:
        battle_data = json.load(f)
    with open(field_manifest, "r", encoding="utf-8") as f:
        field_data = json.load(f)

    battle_clips = battle_data.get("clips", [])
    field_clips = field_data.get("clips", [])

    if not battle_clips:
        return {"skipped": True, "reason": "no battle clips"}

    # Build set of slot names already in field (e.g. "anim_0", "anim_1")
    field_slot_names = {c["name"] for c in field_clips}

    # Find battle clips not present in field
    new_clips = [c for c in battle_clips if c["name"] not in field_slot_names]
    if not new_clips:
        return {"skipped": False, "copied": 0, "reason": "all slots already present"}

    # Continue index numbering from field
    next_index = max((c["index"] for c in field_clips), default=-1) + 1
    # Continue clip file numbering from field
    next_file_num = len(field_clips)

    added_entries = []
    copied_files = 0

    for clip in new_clips:
        src_file = clip.get("file", "")
        if not src_file:
            continue

        src_path = battle_path / src_file
        if not src_path.exists():
            continue

        # New filename in field/clips/
        dest_filename = f"battle_clip_{next_file_num:03d}.dae"
        dest_rel = f"clips/{dest_filename}"
        dest_path = field_path / dest_rel

        if not dry_run:
            dest_path.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src_path, dest_path)

        added_entries.append({
            "index": next_index,
            "name": clip["name"],
            "file": dest_rel,
            "frameCount": clip.get("frameCount", 0),
            "fps": clip.get("fps", 30),
            "boneCount": clip.get("boneCount", 0),
        })

        next_index += 1
        next_file_num += 1
        copied_files += 1

    if added_entries and not dry_run:
        field_data["clips"].extend(added_entries)
        with open(field_manifest, "w", encoding="utf-8") as f:
            json.dump(field_data, f, indent=2, ensure_ascii=False)
            f.write("\n")

    return {"skipped": False, "copied": copied_files, "new_slots": [c["name"] for c in added_entries]}


def main():
    dry_run = "--dry-run" in sys.argv

    if not BATTLE_DIR.exists() or not FIELD_DIR.exists():
        print(f"ERROR: Expected directories not found:")
        print(f"  battle: {BATTLE_DIR}")
        print(f"  field:  {FIELD_DIR}")
        sys.exit(1)

    battle_chars = {p.name for p in BATTLE_DIR.iterdir() if p.is_dir()}
    field_chars = {p.name for p in FIELD_DIR.iterdir() if p.is_dir()}
    overlap = sorted(battle_chars & field_chars)

    print(f"Battle: {len(battle_chars)}  Field: {len(field_chars)}  Overlap: {len(overlap)}")
    if dry_run:
        print("DRY RUN — no files will be modified\n")

    total_copied = 0
    total_merged = 0

    for char_id in overlap:
        result = merge_character(char_id, dry_run)
        if result.get("skipped"):
            continue

        copied = result.get("copied", 0)
        if copied > 0:
            slots = ", ".join(result.get("new_slots", []))
            print(f"  {char_id}: +{copied} clips ({slots})")
            total_copied += copied
            total_merged += 1

    # Delete merged battle character folders (clips already copied to field)
    deleted = 0
    for char_id in overlap:
        battle_path = BATTLE_DIR / char_id
        if battle_path.exists():
            if not dry_run:
                shutil.rmtree(battle_path)
            deleted += 1

    # Delete battle-only characters that have no field counterpart
    # (move them into field/ instead of deleting)
    battle_only = sorted(battle_chars - field_chars)
    moved = 0
    for char_id in battle_only:
        src = BATTLE_DIR / char_id
        dst = FIELD_DIR / char_id
        if src.exists():
            if not dry_run:
                shutil.move(str(src), str(dst))
            print(f"  {char_id}: moved battle-only -> field/")
            moved += 1

    # Remove battle directory if empty
    if not dry_run and BATTLE_DIR.exists() and not any(BATTLE_DIR.iterdir()):
        BATTLE_DIR.rmdir()
        print("Removed empty battle/ directory")

    print(f"\nDone: {total_merged} characters merged, {total_copied} clip files copied, "
          f"{deleted} battle folders deleted, {moved} battle-only moved to field/")
    if dry_run:
        print("(dry run — re-run without --dry-run to apply)")


if __name__ == "__main__":
    main()
