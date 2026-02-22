# COLLADA Exporter Discrepancy Report

## Overview

This report documents all structural differences between:
- **BakedExporter.cs** (new implementation with issues)
- **ColladaExporter.cs** (old, reference implementation)
- **Spica DAE.cs/DAEUtils.cs** (authoritative reference for correctness)

---

## 1. MATRIX FORMAT DISCREPANCIES

### 1.1 Animation Matrix Output Format

| Aspect | BakedExporter | ColladaExporter | Spica |
|--------|---------------|-----------------|-------|
| Format | Row-major (lines 794-800) | Column-major (lines 882-888) | Column-major via MatrixStr (DAEUtils line 55-60) |

**BakedExporter (lines 794-800):**
```csharp
// Row-major with translation at positions 3, 7, 11
return new float[]
{
    r00, r01, r02, tx,   // row 0
    r10, r11, r12, ty,   // row 1
    r20, r21, r22, tz,   // row 2
    0,   0,   0,   1     // row 3
};
```

**ColladaExporter (lines 882-888):**
```csharp
// COLLADA column-major: col0, col1, col2, col3
// Translation goes in column 3 (positions 12-14)
return new float[]
{
    r00, r10, r20, 0,   // column 0
    r01, r11, r21, 0,   // column 1
    r02, r12, r22, 0,   // column 2
    tx,  ty,  tz,  1    // column 3 (translation)
};
```

**Spica DAEUtils.MatrixStr (lines 55-60):**
```csharp
// Column-major: M11 M21 M31 M41 | M12 M22 M32 M42 | M13 M23 M33 M43
return string.Format(CultureInfo.InvariantCulture, Matrix3x4Format,
    m.M11, m.M21, m.M31, m.M41,
    m.M12, m.M22, m.M32, m.M42,
    m.M13, m.M23, m.M33, m.M43);
```

**Impact: CRITICAL**
- Animations will be completely broken with wrong matrix layout
- Bones will transform incorrectly during playback

**Recommended Fix:**
Change BakedExporter `ComputeLocalMatrix` (line 745) to output column-major format matching ColladaExporter.

---

### 1.2 Bone Node Matrix Format (Visual Scene)

| Aspect | BakedExporter | ColladaExporter | Spica |
|--------|---------------|-----------------|-------|
| Format | Row-major (lines 897-903) | System.Numerics column-major via FormatMatrix (lines 1430-1436) | Column-major via SetBoneMatrix |

**BakedExporter ComputeBindPoseMatrix (lines 897-903):**
```csharp
// Row-major with translation in last column (positions 3, 7, 11)
return new float[]
{
    r00, r01, r02, tx,   // row 0
    r10, r11, r12, ty,   // row 1
    r20, r21, r22, tz,   // row 2
    0,   0,   0,   1     // row 3
};
```

**ColladaExporter FormatMatrix (lines 1430-1436):**
```csharp
// Uses Matrix4x4 members in column-major order
return $"{FormatFloat(m.M11)} {FormatFloat(m.M21)} {FormatFloat(m.M31)} {FormatFloat(m.M41)} " +
       $"{FormatFloat(m.M12)} {FormatFloat(m.M22)} {FormatFloat(m.M32)} {FormatFloat(m.M42)} " +
       $"{FormatFloat(m.M13)} {FormatFloat(m.M23)} {FormatFloat(m.M33)} {FormatFloat(m.M43)} " +
       $"{FormatFloat(m.M14)} {FormatFloat(m.M24)} {FormatFloat(m.M34)} {FormatFloat(m.M44)}";
```

**Impact: CRITICAL**
- Skeleton will be malformed in visual scene
- Skinned mesh will deform incorrectly

**Recommended Fix:**
Change BakedExporter `ComputeBindPoseMatrix` to match the column-major output format.

---

### 1.3 Inverse Bind Matrix Format

| Aspect | BakedExporter | ColladaExporter | Spica |
|--------|---------------|-----------------|-------|
| Format | Column-major (lines 458-462) | Column-major (lines 485-488) | Column-major via MatrixStr |

**BakedExporter (lines 458-462):**
```csharp
var ibm = bone.InverseTransform;
// Column-major output
ibmData.Append($"{F(ibm.M11)} {F(ibm.M21)} {F(ibm.M31)} {F(ibm.M41)} ");
ibmData.Append($"{F(ibm.M12)} {F(ibm.M22)} {F(ibm.M32)} {F(ibm.M42)} ");
ibmData.Append($"{F(ibm.M13)} {F(ibm.M23)} {F(ibm.M33)} {F(ibm.M43)} ");
ibmData.Append($"{F(ibm.M14)} {F(ibm.M24)} {F(ibm.M34)} {F(ibm.M44)} ");
```

**ColladaExporter (lines 485-488):**
```csharp
var inv = bone.InverseTransform;
data.Add(inv.M11); data.Add(inv.M21); data.Add(inv.M31); data.Add(inv.M41);
data.Add(inv.M12); data.Add(inv.M22); data.Add(inv.M32); data.Add(inv.M42);
data.Add(inv.M13); data.Add(inv.M23); data.Add(inv.M33); data.Add(inv.M43);
data.Add(inv.M14); data.Add(inv.M24); data.Add(inv.M34); data.Add(inv.M44);
```

**Impact: OK (MATCHES)**
- Both use column-major format for inverse bind matrices
- This is correct

---

## 2. SKINNING CONTROLLER STRUCTURE

### 2.1 Controller ID Naming

| Aspect | BakedExporter | ColladaExporter |
|--------|---------------|-----------------|
| ID Format | `controller_{meshIndex}` (line 444) | `Controller_{index}` (line 406) |

**BakedExporter (line 444):**
```csharp
string ctrlId = $"controller_{meshIndex}";
```

**ColladaExporter (line 406):**
```csharp
Id = $"Controller_{index}",
```

**Impact: LOW**
- Cosmetic difference, both work
- Recommend: Keep consistent with one style

---

### 2.2 Source ID Naming

| Aspect | BakedExporter | ColladaExporter |
|--------|---------------|-----------------|
| Joints | `{ctrlId}-joints` (line 517) | `{controllerId}_joints` (line 469) |
| Bind Poses | `{ctrlId}-bind-poses` (line 537) | `{controllerId}_trans` (line 502) |
| Weights | `{ctrlId}-weights` (line 557) | `{controllerId}_weights` (line 529) |

**Impact: LOW**
- Cosmetic difference, both valid

---

### 2.3 Weights Source Pre-initialization

| Aspect | BakedExporter | ColladaExporter | Spica |
|--------|---------------|-----------------|-------|
| Initial Weight | None (line 467) | 1.0f pre-added (line 511) | Weights built on-demand |

**BakedExporter (line 467):**
```csharp
var uniqueWeights = new Dictionary<string, int>();
```

**ColladaExporter (lines 511-512):**
```csharp
var uniqueWeights = new List<float> { 1.0f };
var weightSet = new HashSet<float> { 1.0f };
```

**Impact: MEDIUM**
- ColladaExporter guarantees weight index 0 is always 1.0
- BakedExporter may produce different weight indices
- Could cause issues with fallback weight bindings

**Recommended Fix:**
Pre-initialize BakedExporter's uniqueWeights with "1" -> 0 mapping.

---

### 2.4 Weight Threshold

| Aspect | BakedExporter | ColladaExporter |
|--------|---------------|-----------------|
| Threshold | `> 0.0001f` (line 477) | `> 0` (line 554) |

**BakedExporter (line 477):**
```csharp
if (weight > 0.0001f)
```

**ColladaExporter (line 554):**
```csharp
if (weight > 0)
```

**Impact: LOW**
- BakedExporter is more aggressive at filtering tiny weights
- Unlikely to cause visible issues

---

### 2.5 Joint Param Type

| Aspect | BakedExporter | ColladaExporter |
|--------|---------------|-----------------|
| Type | `name` (lowercase, line 530) | `Name` (capitalized, line 473) |

**BakedExporter (line 530):**
```csharp
new XElement(NS + "param",
    new XAttribute("name", "JOINT"),
    new XAttribute("type", "name")
)
```

**ColladaExporter (line 473):**
```csharp
IsNameArray = true  // Results in type="Name"
```

**Impact: MEDIUM**
- Some COLLADA parsers may be case-sensitive
- Should use `Name` to match spec

**Recommended Fix:**
Change `"name"` to `"Name"` on line 530.

---

## 3. VISUAL SCENE STRUCTURE

### 3.1 Visual Scene ID/Name

| Aspect | BakedExporter | ColladaExporter |
|--------|---------------|-----------------|
| ID | `Scene` (line 835) | `VisualSceneNode` (line 897) |
| Name | `Scene` (line 836) | `rdmscene` (line 898) |

**BakedExporter (lines 835-836):**
```csharp
new XAttribute("id", "Scene"),
new XAttribute("name", "Scene"),
```

**ColladaExporter (lines 897-898):**
```csharp
scene.Attributes.Append(CreateAttribute(doc, "id", "VisualSceneNode"));
scene.Attributes.Append(CreateAttribute(doc, "name", "rdmscene"));
```

**Impact: LOW**
- Cosmetic difference

---

### 3.2 Bone Node Structure

| Aspect | BakedExporter | ColladaExporter |
|--------|---------------|-----------------|
| ID | `{bone.Name}_id` (line 844) | `{bone.Name}_id` (line 83) |
| SID | `bone.Name` (line 859) | `bone.Name` (line 1383) |
| Has SID | Always (line 859) | Only for JOINT type (line 1382-1383) |

**BakedExporter (lines 856-860):**
```csharp
return new XElement(NS + "node",
    new XAttribute("id", boneId),
    new XAttribute("name", bone.Name),
    new XAttribute("sid", bone.Name),
    new XAttribute("type", "JOINT"),
```

**ColladaExporter (lines 1377-1383):**
```csharp
node.Attributes.Append(CreateAttr(doc, "id", Id));
node.Attributes.Append(CreateAttr(doc, "name", Name));
node.Attributes.Append(CreateAttr(doc, "type", NodeType));

if (NodeType == "JOINT")
    node.Attributes.Append(CreateAttr(doc, "sid", Name));
```

**Impact: OK (MATCHES)**
- Both correctly add SID for JOINT nodes

---

### 3.3 Mesh Node Structure

| Aspect | BakedExporter | ColladaExporter |
|--------|---------------|-----------------|
| ID | `mesh_{meshIndex}_node` (line 921) | `Node_{index}` (line 258) |
| Has Matrix | Yes, identity (lines 924-926) | Yes, via Transform (line 1387-1389) |

**Impact: LOW**
- Different naming convention but functionally equivalent

---

### 3.4 Root Bone Detection

| Aspect | BakedExporter | ColladaExporter |
|--------|---------------|-----------------|
| Method | ParentIndex < 0 OR >= Bones.Count (lines 815-816) | ParentIndex < 0 (line 139) or fallback to first bone (line 144) |

**BakedExporter (lines 815-816):**
```csharp
if (bone.ParentIndex < 0 || bone.ParentIndex >= _skeleton.Bones.Count)
{
    sceneNodes.Add(CreateBoneNode(bone));
}
```

**ColladaExporter (lines 137-144):**
```csharp
foreach (var bone in _skeleton.Bones)
{
    if (bone.ParentIndex < 0)
        return bone.Name + "_id";
}
// Fallback to first bone if no root found
return _skeleton.Bones[0].Name + "_id";
```

**Impact: LOW**
- BakedExporter handles invalid parent indices
- Both should work correctly for valid skeletons

---

### 3.5 Mesh Instance skeleton Reference

| Aspect | BakedExporter | ColladaExporter |
|--------|---------------|-----------------|
| Format | `#{rootBoneId}` directly (line 930) | `#` + SkeletonRootId (line 1401) |

**BakedExporter (line 930):**
```csharp
new XElement(NS + "skeleton", $"#{rootBoneId}"),
```

**ColladaExporter (lines 1400-1401):**
```csharp
var skel = doc.CreateElement("skeleton");
skel.InnerText = "#" + SkeletonRootId;
```

**Impact: OK (MATCHES)**
- Both produce correct skeleton reference

---

## 4. ANIMATION STRUCTURE

### 4.1 Animation ID Naming

| Aspect | BakedExporter | ColladaExporter | Spica |
|--------|---------------|-----------------|-------|
| Animation ID | `{boneId}_anim` (line 622) | `{boneId}_transform` (line 703) | `{AnimName}_{BoneName}_transform_id` (line 811) |
| Sampler ID | `{animId}-sampler` (line 718) | `{animId}_sampler` (line 744) | `{animName}_samp_id` (line 821) |

**BakedExporter (lines 622-623):**
```csharp
string animId = $"{boneId}_anim";
```

**ColladaExporter (lines 703-704):**
```csharp
string animId = $"{boneId}_transform";
```

**Impact: LOW**
- Different naming, both valid

---

### 4.2 Animation Element name Attribute

| Aspect | BakedExporter | ColladaExporter |
|--------|---------------|-----------------|
| Has name | No | Yes (line 707) |

**BakedExporter (lines 654-655):**
```csharp
return new XElement(NS + "animation",
    new XAttribute("id", animId),
    // No name attribute
```

**ColladaExporter (lines 706-707):**
```csharp
animElement.Attributes.Append(CreateAttribute(doc, "id", animId));
animElement.Attributes.Append(CreateAttribute(doc, "name", $"{skelBone.Name}_transform"));
```

**Impact: LOW**
- name attribute is optional

---

### 4.3 Source ID Suffixes

| Aspect | BakedExporter | ColladaExporter |
|--------|---------------|-----------------|
| Input | `{animId}-input` (line 657) | `{animId}_input` (line 711) |
| Output | `{animId}-output` (line 677) | `{animId}_output` (line 720) |
| Interp | `{animId}-interp` (line 697) | `{animId}_interp` (line 734) |

**Impact: LOW**
- Hyphen vs underscore - both valid

---

### 4.4 Channel Target Format

| Aspect | BakedExporter | ColladaExporter | Spica |
|--------|---------------|-----------------|-------|
| Target | `{boneId}/transform` (line 735) | `{boneId}/transform` (line 765) | `{BoneName}_bone_id/transform` (line 823) |

**BakedExporter (line 735):**
```csharp
new XAttribute("target", $"{boneId}/transform")
```

**ColladaExporter (line 765):**
```csharp
channel.Attributes.Append(CreateAttribute(doc, "target", $"{boneId}/transform"));
```

**Impact: OK (MATCHES)**
- Both use same target format

---

### 4.5 Bone ID Suffix Mismatch with Spica

| Aspect | BakedExporter/ColladaExporter | Spica |
|--------|-------------------------------|-------|
| Bone ID | `{boneName}_id` | `{boneName}_bone_id` |

**BakedExporter (line 621):**
```csharp
string boneId = $"{skelBone.Name}_id";
```

**Spica (line 823):**
```csharp
Anim.channel.target = $"{SklBone.Name}_bone_id/transform";
```

**Impact: CRITICAL (for Spica compatibility)**
- If importing Spica-exported animations into DrpToDae models, bone IDs won't match
- Animations will fail to bind to bones

**Recommended Fix:**
Change bone ID format to `{boneName}_bone_id` to match Spica convention, OR ensure consistency within DrpToDae project.

---

## 5. GEOMETRY STRUCTURE

### 5.1 Geometry ID Naming

| Aspect | BakedExporter | ColladaExporter |
|--------|---------------|-----------------|
| ID | `geometry_{index}` (line 297) | `{baseName}_{index}` (line 151) |

**BakedExporter (line 297):**
```csharp
string geoId = $"geometry_{index}";
```

**ColladaExporter (line 151):**
```csharp
string geometryId = $"{baseName}_{index}";
```

**Impact: LOW**
- Different naming convention

---

### 5.2 Vertex Color Source

| Aspect | BakedExporter | ColladaExporter |
|--------|---------------|-----------------|
| Has Color | No | Yes (lines 369-387, 212-218) |

**ColladaExporter (lines 212-218):**
```csharp
colladaPoly.Inputs.Add(new ColladaInput
{
    Semantic = SemanticType.COLOR,
    Source = "#" + colorSource.Id,
    Offset = inputOffset++,
    Set = 0
});
```

**Impact: MEDIUM**
- BakedExporter loses vertex color information
- May affect models that rely on vertex colors

**Recommended Fix:**
Add vertex color source to BakedExporter geometry output.

---

### 5.3 Triangle Input Count

| Aspect | BakedExporter | ColladaExporter |
|--------|---------------|-----------------|
| Inputs | 3 (VERTEX, NORMAL, TEXCOORD) | 4 (VERTEX, NORMAL, TEXCOORD, COLOR) |

**BakedExporter (lines 399-414):**
```csharp
new XElement(NS + "input", semantic="VERTEX", offset="0"),
new XElement(NS + "input", semantic="NORMAL", offset="1"),
new XElement(NS + "input", semantic="TEXCOORD", offset="2", set="0"),
```

**ColladaExporter has COLOR at offset 3**

**Impact: MEDIUM**
- Index stride differs (3 vs 4)
- Vertex colors not exported

---

### 5.4 up_axis Element

| Aspect | BakedExporter | ColladaExporter |
|--------|---------------|-----------------|
| Has up_axis | Yes, Y_UP (line 144) | No |

**BakedExporter (line 144):**
```csharp
new XElement(NS + "up_axis", "Y_UP")
```

**Impact: LOW**
- Good practice to include
- BakedExporter is more complete here

---

## 6. MATERIAL/EFFECT STRUCTURE

### 6.1 Effect ID Naming

| Aspect | BakedExporter | ColladaExporter |
|--------|---------------|-----------------|
| ID | `effect_{meshIdx}_{polyIdx}` (line 188) | `Effect_{index}` (line 227) |

**Impact: LOW**
- Different naming schemes

---

### 6.2 Material Symbol in Triangles

| Aspect | BakedExporter | ColladaExporter |
|--------|---------------|-----------------|
| Symbol | `material_{meshIdx}_{polyIdx}` (line 398) | `Mat{index}` (line 179) |

**BakedExporter (line 398):**
```csharp
new XAttribute("material", $"material_{_model.Meshes.IndexOf(mesh)}_{mesh.Polygons.IndexOf(poly)}"),
```

**ColladaExporter (line 179):**
```csharp
string materialSymbol = $"Mat{index}";
```

**Impact: MEDIUM**
- Must match between triangles material attr and instance_material symbol
- Both exporters are internally consistent

---

### 6.3 bind_vertex_input semantic

| Aspect | BakedExporter | ColladaExporter |
|--------|---------------|-----------------|
| Semantic | `TEXCOORD0` (line 937) | `CHANNEL0` (line 1419) |

**BakedExporter (line 937):**
```csharp
new XAttribute("semantic", "TEXCOORD0"),
```

**ColladaExporter (line 1419-1420):**
```csharp
bindVertexInput.Attributes.Append(CreateAttr(doc, "semantic", "CHANNEL0"));
```

**Impact: LOW**
- Both are valid semantic names for texture binding

---

## 7. NUMERIC FORMATTING

### 7.1 Float Formatting

| Aspect | BakedExporter | ColladaExporter |
|--------|---------------|-----------------|
| Format | `0.######` (line 993) | `G` general (line 1299) or `0.######` (line 828) |

**BakedExporter (line 993):**
```csharp
return value.ToString("0.######", CultureInfo.InvariantCulture);
```

**ColladaExporter FormatMatrix (line 1299):**
```csharp
return f.ToString("G", CultureInfo.InvariantCulture);
```

**ColladaExporter FormatFloat (line 828):**
```csharp
return f.ToString("0.######", CultureInfo.InvariantCulture);
```

**Impact: LOW**
- Slight precision differences
- Both use InvariantCulture (correct)

---

## SUMMARY OF CRITICAL ISSUES

| # | Issue | Location | Impact | Fix Priority |
|---|-------|----------|--------|--------------|
| 1 | Animation matrix in row-major instead of column-major | BakedExporter lines 794-800 | Animations broken | CRITICAL |
| 2 | Bone node matrix in row-major instead of column-major | BakedExporter lines 897-903 | Skeleton malformed | CRITICAL |
| 3 | Bone ID suffix mismatch with Spica (`_id` vs `_bone_id`) | BakedExporter line 844 | Cross-tool incompatibility | HIGH |
| 4 | Missing vertex color source | BakedExporter geometry section | Lost color data | MEDIUM |
| 5 | Joint param type lowercase `name` vs `Name` | BakedExporter line 530 | Parser compatibility | MEDIUM |
| 6 | Weight pre-initialization missing | BakedExporter line 467 | Potential weight issues | MEDIUM |

---

## RECOMMENDED FIXES

### Fix 1: Animation Matrix Format (CRITICAL)
Change `ComputeLocalMatrix` (line 745) to output column-major:
```csharp
return new float[]
{
    r00, r10, r20, 0,   // column 0
    r01, r11, r21, 0,   // column 1
    r02, r12, r22, 0,   // column 2
    tx,  ty,  tz,  1    // column 3
};
```

### Fix 2: Bone Node Matrix Format (CRITICAL)
Change `ComputeBindPoseMatrix` (line 869) to output column-major:
```csharp
return new float[]
{
    r00, r10, r20, 0,
    r01, r11, r21, 0,
    r02, r12, r22, 0,
    tx,  ty,  tz,  1
};
```

### Fix 3: Bone ID Convention (HIGH)
Decide on consistent bone ID format. If Spica compatibility needed:
```csharp
string boneId = $"{bone.Name}_bone_id";  // Match Spica
```

### Fix 4: Add Vertex Colors (MEDIUM)
Add color source similar to ColladaExporter lines 369-387.

### Fix 5: Joint Param Type (MEDIUM)
Line 530: Change `"name"` to `"Name"`.

### Fix 6: Weight Pre-initialization (MEDIUM)
Line 467: Initialize with default weight:
```csharp
var uniqueWeights = new Dictionary<string, int> { { "1", 0 } };
```

---

## VERIFICATION CHECKLIST

After fixes, verify:
- [ ] Skeleton appears correct in bind pose
- [ ] Animation plays correctly (bones move properly)
- [ ] Skinned mesh deforms correctly
- [ ] Vertex colors preserved (if source has them)
- [ ] Cross-tool compatibility with Spica exports
- [ ] DAE validates against COLLADA 1.4.1 schema
