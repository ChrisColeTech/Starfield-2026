"""Measure trainer model heights across sun-moon and scarlet gens."""
import os
import xml.etree.ElementTree as ET

NS = "http://www.collada.org/2005/11/COLLADASchema"
BASE = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", "src", "Starfield2026.Assets", "Models", "Characters"))

SM_ROOT = os.path.join(BASE, "sun-moon", "trainers")
SC_ROOT = os.path.join(BASE, "scarlet", "characters")


def get_position_bounds(dae_path):
    """Parse position float arrays from DAE and compute bounding box height."""
    tree = ET.parse(dae_path)
    root = tree.getroot()

    # Find vertices position sources
    pos_source_ids = set()
    for vertices in root.iter(f"{{{NS}}}vertices"):
        for inp in vertices.findall(f"{{{NS}}}input"):
            if inp.get("semantic") == "POSITION":
                pos_source_ids.add(inp.get("source", "").lstrip("#"))

    min_y, max_y = float('inf'), float('-inf')
    min_z, max_z = float('inf'), float('-inf')

    for source in root.iter(f"{{{NS}}}source"):
        sid = source.get("id", "")
        if sid not in pos_source_ids:
            continue
        fa = source.find(f"{{{NS}}}float_array")
        if fa is None or not fa.text:
            continue
        vals = [float(v) for v in fa.text.split()]
        # Positions are [x,y,z, x,y,z, ...]
        for i in range(1, len(vals), 3):  # Y values
            min_y = min(min_y, vals[i])
            max_y = max(max_y, vals[i])
        for i in range(2, len(vals), 3):  # Z values
            min_z = min(min_z, vals[i])
            max_z = max(max_z, vals[i])

    height_y = max_y - min_y if max_y > min_y else 0
    height_z = max_z - min_z if max_z > min_z else 0
    # DAE could be Y-up or Z-up
    return max(height_y, height_z)


def scan_trainers(root_dir, max_per_type=3):
    """Scan trainer folders and measure models."""
    results = []
    if not os.path.isdir(root_dir):
        return results
    for body_type in sorted(os.listdir(root_dir)):
        body_dir = os.path.join(root_dir, body_type)
        if not os.path.isdir(body_dir):
            continue
        # Could be individual trainer folders or model.dae directly
        model_dae = os.path.join(body_dir, "model.dae")
        if os.path.exists(model_dae):
            h = get_position_bounds(model_dae)
            results.append((body_type, body_type, h))
        else:
            count = 0
            for trainer in sorted(os.listdir(body_dir)):
                trainer_dir = os.path.join(body_dir, trainer)
                mdae = os.path.join(trainer_dir, "model.dae")
                if os.path.isdir(trainer_dir) and os.path.exists(mdae):
                    h = get_position_bounds(mdae)
                    results.append((body_type, trainer, h))
                    count += 1
                    if count >= max_per_type:
                        break
    return results


print(f"Sun-Moon trainers: {SM_ROOT}")
print(f"Scarlet trainers:  {SC_ROOT}\n")

sm_results = scan_trainers(SM_ROOT, max_per_type=3)
sc_results = scan_trainers(SC_ROOT, max_per_type=3)

print(f"{'Type':<8} {'Trainer':<20} {'Height':>10}   Source")
print("-" * 55)
for body, trainer, h in sm_results:
    print(f"{body:<8} {trainer:<20} {h:>10.2f}   sun-moon")
print()
for body, trainer, h in sc_results:
    print(f"{body:<8} {trainer:<20} {h:>10.4f}   scarlet")

if sm_results and sc_results:
    avg_sm = sum(h for _, _, h in sm_results) / len(sm_results)
    avg_sc = sum(h for _, _, h in sc_results) / len(sc_results)
    print(f"\nAvg sun-moon: {avg_sm:.2f}")
    print(f"Avg scarlet:  {avg_sc:.4f}")
    print(f"Ratio: {avg_sm / avg_sc:.1f}x")
