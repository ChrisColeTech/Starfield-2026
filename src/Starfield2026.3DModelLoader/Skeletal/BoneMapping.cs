#nullable enable
using System;
using System.Collections.Generic;

namespace Starfield2026.ModelLoader.Skeletal;

/// <summary>
/// Maps bone names from a reference skeleton to a target skeleton.
/// The reference skeleton is the Sun-Moon field rig (tr0001_00).
/// Each target skeleton family has its own mapping.
/// Bones not in the map are looked up by their original name (passthrough).
/// </summary>
public static class BoneMapping
{
    /// <summary>
    /// Detect which skeleton family a rig belongs to and return the
    /// appropriate name map (reference bone name → target bone name).
    /// Returns null if the rig IS the reference skeleton (no remapping needed).
    /// </summary>
    public static Dictionary<string, string>? GetMapForRig(SkeletonRig rig)
    {
        // If the rig has Sun-Moon style names, no remapping needed
        if (rig.TryGetBoneIndex("Waist", out _) && rig.TryGetBoneIndex("LThigh", out _))
            return null;

        // Scarlet style: lowercase_with_underscores
        if (rig.TryGetBoneIndex("waist", out _) && rig.TryGetBoneIndex("left_leg_01", out _))
            return SunMoonToScarlet;

        // Unknown skeleton — return null, clips will match what they can
        return null;
    }

    /// <summary>
    /// Sun-Moon reference bone name → Scarlet bone name.
    /// Only includes bones that differ — identical names are passed through.
    /// </summary>
    public static readonly Dictionary<string, string> SunMoonToScarlet = new(StringComparer.OrdinalIgnoreCase)
    {
        // Root / spine
        ["Origin"]   = "origin",
        ["Waist"]    = "waist",
        ["Spine2"]   = "spine_01",
        ["Spine3"]   = "spine_02",
        ["Neck"]     = "neck",
        ["Head"]     = "head",

        // Left arm
        ["LShoulder"]  = "left_shoulder",
        ["LArm"]       = "left_arm_01",
        ["LForeArm"]   = "left_arm_02",
        ["LHand"]      = "left_hand",
        ["LFingerA"]   = "left_thumb_01",
        ["LFingerB1"]  = "left_index_01",
        ["LFingerB2"]  = "left_index_02",

        // Right arm
        ["RShoulder"]  = "right_shoulder",
        ["RArm"]       = "right_arm_01",
        ["RForeArm"]   = "right_arm_02",
        ["RHand"]      = "right_hand",
        ["RFingerA"]   = "right_thumb_01",
        ["RFingerB1"]  = "right_index_01",
        ["RFingerB2"]  = "right_index_02",

        // Hips / legs
        ["Hips"]    = "foot_base",
        ["LThigh"]  = "left_leg_01",
        ["LLeg"]    = "left_leg_02",
        ["LFoot"]   = "left_foot",
        ["LToe"]    = "left_toe",
        ["RThigh"]  = "right_leg_01",
        ["RLeg"]    = "right_leg_02",
        ["RFoot"]   = "right_foot",
        ["RToe"]    = "right_toe",
    };
}
