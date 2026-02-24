"""Analyze DAE bone structure - check actual XML node format."""
import os

BASE = r"D:\Projects\Starfield-2026\src\Starfield2026.Assets\Models\Characters\sun-moon\field\tr0002_00"

def main():
    model_path = os.path.join(BASE, "model.dae")
    clip_path = os.path.join(BASE, "clips", "clip_000.dae")
    
    # Show raw lines containing 'node' and 'type' to see actual format
    print("=== MODEL: lines with JOINT ===")
    with open(model_path, encoding='utf-8') as f:
        count = 0
        for i, line in enumerate(f, 1):
            if 'JOINT' in line:
                print(f"  L{i}: {line.rstrip()[:150]}")
                count += 1
                if count >= 10:
                    break
    if count == 0:
        print("  (none found)")
    
    # Also check for <node> elements with type attribute
    print("\n=== MODEL: first 10 <node> elements ===")
    with open(model_path, encoding='utf-8') as f:
        count = 0
        for i, line in enumerate(f, 1):
            stripped = line.strip()
            if stripped.startswith('<node '):
                print(f"  L{i}: {stripped[:150]}")
                count += 1
                if count >= 10:
                    break
    
    print("\n=== CLIP: first 10 <node> elements ===")
    with open(clip_path, encoding='utf-8') as f:
        count = 0
        for i, line in enumerate(f, 1):
            stripped = line.strip()
            if stripped.startswith('<node '):
                print(f"  L{i}: {stripped[:150]}")
                count += 1
                if count >= 10:
                    break

    print("\n=== CLIP: first 10 <animation> channel targets ===")
    with open(clip_path, encoding='utf-8') as f:
        count = 0
        for i, line in enumerate(f, 1):
            stripped = line.strip()
            if '<channel' in stripped:
                print(f"  L{i}: {stripped[:150]}")
                count += 1
                if count >= 10:
                    break

if __name__ == "__main__":
    main()
