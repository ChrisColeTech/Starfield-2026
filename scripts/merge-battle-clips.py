"""
Merge battle animation clips into field character folders.

For each trainer in the assets field dir, finds matching battle clips
from the spica-exported root and copies them in, renumbered to avoid
collisions with existing field clips.

Usage:
  python merge-battle-clips.py
  python merge-battle-clips.py --dry-run
"""

import os
import shutil
import sys

FIELD_DIR = "D:/Projects/Starfield-2026/src/Starfield2026.Assets/Models/Characters/sun-moon/field"
BATTLE_DIR = "D:/Projects/Starfield-2026/src/Starfield2026.Tests/sun-moon-dump/spica-exported"


def get_clip_count(clips_dir: str) -> int:
    if not os.path.isdir(clips_dir):
        return 0
    return len([f for f in os.listdir(clips_dir) if f.startswith("clip_") and f.endswith(".dae")])


def get_max_clip_index(clips_dir: str) -> int:
    if not os.path.isdir(clips_dir):
        return -1
    indices = []
    for f in os.listdir(clips_dir):
        if f.startswith("clip_") and f.endswith(".dae"):
            try:
                idx = int(f[5:-4])  # clip_000.dae -> 0
                indices.append(idx)
            except ValueError:
                pass
    return max(indices) if indices else -1


def main():
    dry_run = "--dry-run" in sys.argv

    print(f"Field dir:  {FIELD_DIR}")
    print(f"Battle dir: {BATTLE_DIR}")
    print(f"Dry run:    {dry_run}")
    print()

    total = 0
    merged = 0
    clips_added = 0
    skipped = 0

    for entry in sorted(os.listdir(FIELD_DIR)):
        field_path = os.path.join(FIELD_DIR, entry)
        if not os.path.isdir(field_path):
            continue

        # Look for matching battle trainer in spica-exported root
        battle_path = os.path.join(BATTLE_DIR, entry)
        battle_clips = os.path.join(battle_path, "clips")

        if not os.path.isdir(battle_clips):
            continue

        total += 1
        battle_clip_files = sorted(
            f for f in os.listdir(battle_clips)
            if f.startswith("clip_") and f.endswith(".dae")
        )

        if not battle_clip_files:
            skipped += 1
            continue

        field_clips = os.path.join(field_path, "clips")
        if not os.path.isdir(field_clips):
            os.makedirs(field_clips, exist_ok=True)

        # Find next available index
        next_idx = get_max_clip_index(field_clips) + 1

        added = 0
        for bf in battle_clip_files:
            src = os.path.join(battle_clips, bf)
            dst_name = f"clip_{next_idx:03d}.dae"
            dst = os.path.join(field_clips, dst_name)

            if not dry_run:
                shutil.copy2(src, dst)

            next_idx += 1
            added += 1

        clips_added += added
        merged += 1
        action = "WOULD MERGE" if dry_run else "MERGED"
        existing = get_clip_count(field_clips) - added if not dry_run else get_clip_count(field_clips)
        print(f"  {action}: {entry} — {added} battle clips added (was {existing}, now {existing + added})")

    print(f"\nDone: {total} matching trainers, {merged} merged, {clips_added} clips added, {skipped} skipped (no clips)")

    if dry_run and merged > 0:
        print("\nRe-run without --dry-run to apply, then run fix-field-manifests.py to update manifests.")


if __name__ == "__main__":
    main()
