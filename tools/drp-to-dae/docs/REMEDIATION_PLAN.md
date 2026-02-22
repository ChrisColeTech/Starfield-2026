# BakedExporter Remediation Plan

## Overview
This document outlines the systematic fixes for BakedExporter.cs based on the discrepancy report.

## Fixes to Implement

### 1. CRITICAL: Fix ComputeBindPoseMatrix (line 869)
**Current:** Row-major (translation at positions 3, 7, 11)
**Required:** Column-major (translation at positions 12, 13, 14)

```csharp
// BEFORE (wrong):
return new float[]
{
    r00, r01, r02, tx,   // row 0
    r10, r11, r12, ty,   // row 1
    r20, r21, r22, tz,   // row 2
    0,   0,   0,   1     // row 3
};

// AFTER (correct):
return new float[]
{
    r00, r10, r20, 0,   // column 0
    r01, r11, r21, 0,   // column 1
    r02, r12, r22, 0,   // column 2
    tx,  ty,  tz,  1    // column 3
};
```

### 2. CRITICAL: Fix ComputeLocalMatrix (line 745)
**Same fix as above - change to column-major format**

### 3. MEDIUM: Fix Joint Param Type (line 530)
**Current:** `type="name"` (lowercase)
**Required:** `type="Name"` (capitalized)

### 4. MEDIUM: Pre-initialize weights dictionary (line 467)
**Current:** Empty dictionary
**Required:** Pre-initialize with weight 1.0 at index 0

```csharp
// BEFORE:
var uniqueWeights = new Dictionary<string, int>();

// AFTER:
var uniqueWeights = new Dictionary<string, int> { { "1", 0 } };
```

### 5. OPTIONAL: Vertex colors (for future)
Not critical for basic functionality but should be added later.

## Implementation Order
1. Fix both matrix functions (critical)
2. Fix joint param type
3. Fix weight pre-initialization
4. Test Phase 2 (bind pose)
5. Test Phase 3 (animation)

## Verification
After all fixes, the model should:
- Display correctly in bind pose (skeleton correct)
- Animate without warping
- Skin correctly to bones
