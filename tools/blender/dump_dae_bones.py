"""Dump bone hierarchy and transforms from a COLLADA DAE file.

Usage: python dump_dae_bones.py <dae_file> [output.txt]
"""

import sys
import xml.etree.ElementTree as ET
from pathlib import Path

NS = {'c': 'http://www.collada.org/2005/11/COLLADASchema'}


def parse_matrix(text):
    """Parse a 4x4 matrix from space-separated floats."""
    vals = [float(v) for v in text.strip().split()]
    if len(vals) != 16:
        return None
    # COLLADA stores row-major
    rows = []
    for i in range(4):
        rows.append(vals[i*4:(i+1)*4])
    return rows


def fmt_matrix(m):
    """Format matrix as readable string."""
    lines = []
    for row in m:
        lines.append("    [" + ", ".join(f"{v:10.6f}" for v in row) + "]")
    return "\n".join(lines)


def extract_translation(m):
    """Extract translation from 4x4 matrix."""
    return (m[0][3], m[1][3], m[2][3])


def walk_joints(node, parent_name, depth, results):
    """Recursively walk joint nodes and collect data."""
    node_type = node.get('type', '')
    name = node.get('name', node.get('id', '?'))
    sid = node.get('sid', '')

    # Get transform matrix
    matrix_el = node.find('c:matrix', NS)
    matrix = None
    if matrix_el is not None:
        matrix = parse_matrix(matrix_el.text)

    results.append({
        'name': name,
        'sid': sid,
        'type': node_type,
        'parent': parent_name,
        'depth': depth,
        'matrix': matrix,
    })

    # Recurse into child nodes
    for child in node.findall('c:node', NS):
        walk_joints(child, name, depth + 1, results)


def dump_dae(dae_path, out_path):
    tree = ET.parse(dae_path)
    root = tree.getroot()

    results = []

    # Find all visual scenes
    vis_scenes = root.findall('.//c:visual_scene', NS)
    for scene in vis_scenes:
        for node in scene.findall('c:node', NS):
            walk_joints(node, None, 0, results)

    # Also check for skeleton in controllers
    controllers = root.findall('.//c:controller', NS)
    skin_joints = []
    for ctrl in controllers:
        skin = ctrl.find('c:skin', NS)
        if skin is None:
            continue
        joints_input = skin.find('.//c:joints/c:input[@semantic="JOINT"]', NS)
        if joints_input is not None:
            source_id = joints_input.get('source', '').lstrip('#')
            source = skin.find(f'.//c:source[@id="{source_id}"]', NS)
            if source is not None:
                name_array = source.find('c:Name_array', NS)
                if name_array is not None:
                    skin_joints = name_array.text.strip().split()

    # Check for animations
    animations = root.findall('.//c:animation', NS)
    animated_targets = set()
    for anim in animations:
        for channel in anim.findall('.//c:channel', NS):
            target = channel.get('target', '')
            bone_name = target.split('/')[0]
            animated_targets.add(bone_name)

    # Write output
    with open(out_path, 'w') as f:
        f.write(f"DAE: {dae_path}\n")
        f.write(f"{'='*80}\n\n")

        # Joint hierarchy
        f.write("JOINT HIERARCHY\n")
        f.write(f"{'-'*80}\n")
        joints = [r for r in results if r['type'] == 'JOINT']
        non_joints = [r for r in results if r['type'] != 'JOINT']

        for j in joints:
            indent = "  " * j['depth']
            tx = ""
            if j['matrix']:
                t = extract_translation(j['matrix'])
                tx = f"  pos=({t[0]:.4f}, {t[1]:.4f}, {t[2]:.4f})"
            animated = " [ANIMATED]" if j['name'] in animated_targets or j['sid'] in animated_targets else ""
            f.write(f"{indent}{j['name']} (parent: {j['parent']}){tx}{animated}\n")

        f.write(f"\nTotal joints: {len(joints)}\n")

        # Non-joint nodes
        if non_joints:
            f.write(f"\nNON-JOINT NODES\n")
            f.write(f"{'-'*80}\n")
            for n in non_joints:
                indent = "  " * n['depth']
                f.write(f"{indent}{n['name']} type={n['type']} (parent: {n['parent']})\n")
            f.write(f"\nTotal non-joint nodes: {len(non_joints)}\n")

        # Skin joints
        if skin_joints:
            f.write(f"\nSKIN JOINT LIST (from controller)\n")
            f.write(f"{'-'*80}\n")
            for name in skin_joints:
                f.write(f"  {name}\n")
            f.write(f"\nTotal skin joints: {len(skin_joints)}\n")

        # Animated targets
        if animated_targets:
            f.write(f"\nANIMATED TARGETS\n")
            f.write(f"{'-'*80}\n")
            for name in sorted(animated_targets):
                f.write(f"  {name}\n")
            f.write(f"\nTotal animated: {len(animated_targets)}\n")

        # Full matrix dump
        f.write(f"\nFULL TRANSFORM MATRICES\n")
        f.write(f"{'-'*80}\n")
        for j in joints:
            f.write(f"\n{j['name']} (parent: {j['parent']}):\n")
            if j['matrix']:
                f.write(fmt_matrix(j['matrix']) + "\n")
            else:
                f.write("    (no matrix)\n")

    print(f"Wrote {out_path} ({len(joints)} joints, {len(non_joints)} non-joints, {len(animated_targets)} animated)")


if __name__ == '__main__':
    if len(sys.argv) < 2:
        print("Usage: python dump_dae_bones.py <dae_file> [output.txt]")
        sys.exit(1)

    dae_path = sys.argv[1]
    if len(sys.argv) > 2:
        out_path = sys.argv[2]
    else:
        out_path = Path(dae_path).stem + "_bones.txt"

    dump_dae(dae_path, out_path)
