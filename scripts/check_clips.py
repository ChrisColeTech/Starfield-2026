import os

base = r"D:\Projects\Starfield-2026\src\Starfield2026.Tests\plza-dump-patched\extracted"

if not os.path.exists(base):
    print(f"Output directory does not exist: {base}")
    exit(1)

dirs = [d for d in os.listdir(base) if os.path.isdir(os.path.join(base, d))]
print(f"Total extracted folders: {len(dirs)}")

# Count folders with model.dae
has_dae = [d for d in dirs if os.path.isfile(os.path.join(base, d, "model.dae"))]
print(f"Folders with model.dae: {len(has_dae)}")

# Count folders with clips/
has_clips = [d for d in dirs if os.path.isdir(os.path.join(base, d, "clips"))]
print(f"Folders with clips/: {len(has_clips)}")

# Count pokemon vs character
pokemon = [d for d in dirs if "pm" in d.lower()]
chara = [d for d in dirs if "pm" not in d.lower()]
print(f"\nPokemon folders: {len(pokemon)}")
print(f"Character/other folders: {len(chara)}")

# Show first 10
print(f"\nSample folders:")
for d in sorted(dirs)[:10]:
    contents = os.listdir(os.path.join(base, d))
    print(f"  {d}: {len(contents)} files")
