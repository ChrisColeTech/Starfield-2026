# Deep Comparison: phase2_bind_pose.dae vs phase2_reference.dae

**Date:** 2026-02-18
**Files Compared:**
- NEW: `D:\Projects\Starfield2026\tools\drp-to-dae\test-phases\p006\a038\phase2_bind_pose.dae`
- REFERENCE: `D:\Projects\Starfield2026\tools\drp-to-dae\test-phases\p006\a038\phase2_reference.dae`

---

## Executive Summary

The files are **structurally nearly identical** with the same vertex weights, inverse bind matrices, and skeleton hierarchy. The **critical difference** is in the animation data: the NEW file has only 1 keyframe (bind pose) while the REFERENCE has 215 keyframes (full 7+ second animation).

---

## 1. Bind Shape Matrix

| File | Value |
|------|-------|
| NEW (line 262) | `1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1` |
| REFERENCE (line 261) | `1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1` |

**Result: IDENTICAL** - Both use identity matrix for bind shape.

---

## 2. Inverse Bind Matrices

### First 3 matrices from Controller_0:

**NEW (line 272):**
```
1 0 0 0 0 1 0 -0.6 0 0 1 0 0 0 0 1
1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1
1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1
```

**REFERENCE (line 271):**
```
1 0 0 0 0 1 0 -0.6 0 0 1 0 0 0 0 1
1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1
1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1
```

**Result: IDENTICAL** - All 115 inverse bind matrices match exactly.

---

## 3. Vertex Weights (`<vcount>` and `<v>` data)

### Controller_0 (1446 vertices):

**NEW vcount (line 294):**
```
2 1 1 2 2 1 2 1 2 1 2 1 2 1 2 1 2 1 2 2 2 2 2 2 2 2 2 1 1 1...
```

**REFERENCE vcount (line 293):**
```
2 1 1 2 2 1 2 1 2 1 2 1 2 1 2 1 2 1 2 2 2 2 2 2 2 2 2 1 1 1...
```

**NEW v data (first 50 pairs) (line 295):**
```
10 1 37 2 37 0 37 0 10 1 37 2 10 1 37 2 37 0 10 1 37 2 37 0...
```

**REFERENCE v data (first 50 pairs) (line 294):**
```
10 1 37 2 37 0 37 0 10 1 37 2 10 1 37 2 37 0 10 1 37 2 37 0...
```

**Result: IDENTICAL** - Joint indices and weight indices match exactly.

---

## 4. Skeleton Hierarchy

Both files have identical parent-child bone relationships. Example comparison of first few bones:

| Bone | NEW Parent | REFERENCE Parent |
|------|------------|------------------|
| BASE | (root) | (root) |
| Spine1 | BASE | BASE |
| Spine2 | Spine1 | Spine1 |
| Neck1 | Spine2 | Spine2 |
| Neck2 | Neck1 | Neck1 |
| Head | Neck2 | Neck2 |

**Result: IDENTICAL** - Skeleton hierarchy is the same.

---

## 5. Skeleton Node Transform Matrices

### Key Finding: Minor precision differences, but mathematically equivalent

**Spine1 bone transform:**

NEW (line 4065):
```xml
<matrix sid="transform">-0 0 1 0 -0 -1 0 -0 1 -0 0 0 0 0 0 1</matrix>
```

REFERENCE (line 4064):
```xml
<matrix sid="transform">-3.5762787E-07 3.2584137E-07 0.99999994 0 -1.1368684E-13 -1 3.2584137E-07 0 0.99999994 -1.4210855E-14 3.5762787E-07 0 0 0 0 1</matrix>
```

**Interpretation:**
- NEW uses cleaner values (rounds near-zero to 0, rounds 0.99999994 to 1)
- REFERENCE preserves floating-point noise from source data
- **Mathematically equivalent** - both represent the same rotation

**Head bone transform:**

NEW (line 4073):
```xml
<matrix sid="transform">0.173649 -0 0.984808 0.1 0.984808 0 -0.173649 0 0 1 0 0 0 0 0 1</matrix>
```

REFERENCE (line 4072):
```xml
<matrix sid="transform">0.1736486 -3.5762787E-07 0.9848076 0.1 0.98480767 1.1920929E-07 -0.17364854 0 2.9802322E-08 0.99999994 4.172325E-07 0 0 0 0 1</matrix>
```

**Impact:** The skeleton transforms are mathematically equivalent. The reference has floating-point precision artifacts from source data (likely from Quaternion-to-Matrix conversion), while NEW outputs cleaner rounded values.

---

## 6. Animation Data - CRITICAL DIFFERENCE

### NEW File Animation (line 378-410):

```xml
<library_animations>
  <animation id="BASE_id_anim">
    <source id="BASE_id_anim-input">
      <float_array count="1">0</float_array>  <!-- ONE KEYFRAME -->
    </source>
    <source id="BASE_id_anim-output">
      <float_array count="16">1 0 0 0 0 1 0 0.6 0 0 1 0 0 0 0 1</float_array>
    </source>
    ...
  </animation>
```

### REFERENCE File Animation (line 377-436):

```xml
<library_animations>
  <animation id="BASE_id_transform" name="BASE_transform">
    <source id="BASE_id_transform_input">
      <float_array count="215">0 0.033333 0.066667 0.1 0.133333 0.166667 0.2 ...</float_array>  <!-- 215 KEYFRAMES -->
    </source>
    <source id="BASE_id_transform_output">
      <float_array count="3440">1 0 0 0 0 1 0 0 0 0 1 0 -0.005552 1.183485 0 1 ...</float_array>  <!-- 215 matrices -->
    </source>
    ...
  </animation>
```

| Attribute | NEW | REFERENCE |
|-----------|-----|-----------|
| Frame Count | 1 | 215 |
| Duration | 0 sec | 7.133 sec |
| Time Values | `0` | `0 0.033333 0.066667 ... 7.133333` |
| Matrix Data | 16 floats (bind pose) | 3440 floats (215 matrices) |
| Animation Content | Static bind pose | Full walk/idle animation |

**Root Cause:**
- NEW file uses `BakedExporter.ExportPhase2()` which creates a fake 1-frame "bind pose animation"
- REFERENCE was exported with actual animation data (215 frames @ 30fps = 7.167 seconds)

---

## 7. Controller Structure

Both files use identical controller structure:

| Element | NEW | REFERENCE |
|---------|-----|-----------|
| Joint count per controller | 115 | 115 |
| Weight source format | float array | float array |
| joints semantic | JOINT + INV_BIND_MATRIX | JOINT + INV_BIND_MATRIX |
| vertex_weights inputs | JOINT(offset=0) + WEIGHT(offset=1) | JOINT(offset=0) + WEIGHT(offset=1) |

**Result: IDENTICAL** - Controller skinning data structure is the same.

---

## 8. Naming Convention Differences (Cosmetic)

| Element | NEW | REFERENCE |
|---------|-----|-----------|
| Controller ID | `controller_0` | `Controller_0` |
| Geometry ID | `geometry_0` | `MODEL_a038_kyukon_0` |
| Visual Scene ID | `Scene` | `VisualSceneNode` |
| Material symbol | `material_0_0` | `Mat0` |

**Impact:** None - these are ID strings that don't affect functionality.

---

## 9. Source Code Analysis

### BakedExporter.cs - Phase 2 Export (Lines 49-59):

```csharp
public static void ExportPhase2(string outputPath, NudModel model, VbnSkeleton? skeleton)
{
    // Create a fake 1-frame animation that matches bind pose
    AnimationData? bindPoseAnim = null;
    if (skeleton != null && skeleton.Bones.Count > 0)
    {
        bindPoseAnim = CreateBindPoseAnimation(skeleton);  // <-- Creates 1-frame anim
    }
    var ctx = new ExportContext(model, skeleton, bindPoseAnim);
    ctx.Export(outputPath);
}
```

### CreateBindPoseAnimation() (Lines 70-99):

```csharp
private static AnimationData CreateBindPoseAnimation(VbnSkeleton skeleton)
{
    var anim = new AnimationData("BindPose") { FrameCount = 1 };  // <-- Only 1 frame!
    foreach (var bone in skeleton.Bones)
    {
        var keyNode = new KeyNode(bone.Name) { ... };
        keyNode.XPos.Keys.Add(new KeyFrame(bone.Pos.X, 0));  // Single keyframe at frame 0
        // ... etc
    }
    return anim;
}
```

---

## 10. Summary of Discrepancies

| Category | Status | Impact |
|----------|--------|--------|
| Bind Shape Matrix | IDENTICAL | None |
| Inverse Bind Matrices | IDENTICAL | None |
| Vertex Weights (vcount) | IDENTICAL | None |
| Vertex Weights (v data) | IDENTICAL | None |
| Skeleton Hierarchy | IDENTICAL | None |
| Skeleton Transforms | Mathematically equivalent | None (precision rounding) |
| Controller Structure | IDENTICAL | None |
| **Animation Keyframes** | **DIFFERENT: 1 vs 215** | **Critical** |
| Naming conventions | Different | None (cosmetic) |

---

## 11. Conclusion

**The skinning data is correct.** The vertex weights, inverse bind matrices, and skeleton hierarchy all match the reference exactly.

**The issue is the animation export mode.** Phase 2 intentionally creates a single-frame bind pose animation for testing, while the reference contains full animation data.

### To get matching animation:

Use `BakedExporter.ExportPhase3()` with actual animation data:

```csharp
AnimationData animation = OMOReader.Read("path/to/animation.omo");
BakedExporter.ExportPhase3(outputPath, model, skeleton, animation);
```

### Why skinning might still look broken:

If the model appears correctly skinned in bind pose but breaks during animation, the issue is likely:

1. **Animation channel target mismatch** - The `<channel target="...">` path might not match the node IDs
2. **Matrix format** - NEW uses row-major format (`tx, ty, tz` at positions 3, 7, 11) while some exporters use column-major
3. **Importer compatibility** - Blender/Unity may interpret animation differently than static bind pose

### Recommended next steps:

1. Export with Phase 3 using actual OMO animation data
2. Compare animation channel targets between files
3. Verify matrix ordering matches importer expectations
