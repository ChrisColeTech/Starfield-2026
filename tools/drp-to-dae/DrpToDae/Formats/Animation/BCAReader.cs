using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using VbnSkeleton = DrpToDae.Formats.VBN.VBN;

namespace DrpToDae.Formats.Animation
{
    /// <summary>
    /// Reads BCA animation data from BCA + BCL + VBN files and produces AnimationData.
    /// 
    /// Architecture:
    ///   BCA = global sorted track index (hash, default value, key count, flags)
    ///   BCL = per-bone curve data, entries map to VBN bones by sequential index
    ///   VBN = skeleton with bone names
    /// 
    /// BCA track hashes are embedded within BCL entry data. We find them by scanning
    /// the BCL for uint32 values matching BCA hashes, then group by proximity to
    /// identify which tracks belong to which bone.
    /// </summary>
    public static class BCAReader
    {
        // Flag → channel mapping (derived from analysis)
        // Flags increment by 4 from 0x02: (flag-2)/4 gives channel index 0-8
        // Channel indices map to: rotX(0), rotY(1), rotZ(2), posX(3), posY(4), posZ(5), sclX(6), sclY(7), sclZ(8)
        private enum ChannelType
        {
            Static = -1,
            RotX = 0,
            RotY = 1,
            RotZ = 2,
            PosX = 3,
            PosY = 4,
            PosZ = 5,
            SclX = 6,
            SclY = 7,
            SclZ = 8,
        }

        private struct BcaTrack
        {
            public uint Hash;
            public float DefaultValue;
            public uint KeyCount;
            public uint Flags;
        }

        public static AnimationData Read(byte[] bcaData, byte[] bclData, VbnSkeleton vbn)
        {
            // Parse BCA header
            int trackCount = BitConverter.ToInt32(bcaData, 0x08);
            int maxTracks = Math.Min(trackCount, (bcaData.Length - 0x28) / 24);

            // BCA header field at 0x18 might be frame count
            int frameCount = BitConverter.ToInt32(bcaData, 0x18);
            if (frameCount <= 0 || frameCount > 10000) frameCount = 100; // fallback

            var anim = new AnimationData("BCA_Animation") { FrameCount = frameCount };

            // Parse all BCA tracks
            var tracks = new List<BcaTrack>();
            var hashSet = new HashSet<uint>();
            for (int t = 0; t < maxTracks; t++)
            {
                int off = 0x28 + t * 24;
                var track = new BcaTrack
                {
                    Hash = BitConverter.ToUInt32(bcaData, off),
                    DefaultValue = BitConverter.ToSingle(bcaData, off + 4),
                    KeyCount = BitConverter.ToUInt32(bcaData, off + 8),
                    Flags = BitConverter.ToUInt32(bcaData, off + 12)
                };
                tracks.Add(track);
                hashSet.Add(track.Hash);
            }

            // BCL entry count
            uint bclEntryCount = BitConverter.ToUInt32(bclData, 0x08);

            // Find all BCA hash positions in BCL
            var hashPositions = new List<(uint hash, int offset)>();
            for (int i = 0; i <= bclData.Length - 4; i += 4)
            {
                uint val = BitConverter.ToUInt32(bclData, i);
                if (hashSet.Contains(val))
                    hashPositions.Add((val, i));
            }
            hashPositions.Sort((a, b) => a.offset.CompareTo(b.offset));

            // Group into bone entries by proximity (gap > 500 bytes = new bone)
            var boneGroups = new List<List<(uint hash, int offset)>>();
            var currentGroup = new List<(uint hash, int offset)>();
            for (int i = 0; i < hashPositions.Count; i++)
            {
                if (currentGroup.Count == 0)
                {
                    currentGroup.Add(hashPositions[i]);
                }
                else
                {
                    int gap = hashPositions[i].offset - currentGroup.Last().offset;
                    if (gap > 500)
                    {
                        boneGroups.Add(currentGroup);
                        currentGroup = new List<(uint hash, int offset)> { hashPositions[i] };
                    }
                    else
                    {
                        currentGroup.Add(hashPositions[i]);
                    }
                }
            }
            if (currentGroup.Count > 0) boneGroups.Add(currentGroup);

            // Build track lookup
            var trackByHash = new Dictionary<uint, BcaTrack>();
            foreach (var t in tracks)
                trackByHash[t.Hash] = t;

            // Map each bone group to a VBN bone by index
            for (int boneIdx = 0; boneIdx < boneGroups.Count; boneIdx++)
            {
                string boneName;
                if (boneIdx < vbn.Bones.Count)
                    boneName = vbn.Bones[boneIdx].Name;
                else
                    boneName = $"Bone_{boneIdx}";

                var keyNode = new KeyNode(boneName)
                {
                    BoneIndex = boneIdx,
                    Hash = boneIdx < vbn.Bones.Count ? (int)vbn.Bones[boneIdx].BoneId : boneIdx,
                    RotationType = RotationType.Euler
                };

                var group = boneGroups[boneIdx];

                foreach (var (hash, bclOffset) in group)
                {
                    if (!trackByHash.TryGetValue(hash, out var track))
                        continue;

                    var channel = GetChannel(track.Flags);
                    float defVal = track.DefaultValue;

                    // For now, create a single-keyframe animation using the default value
                    // This produces a valid DAE structure with correct bone mapping
                    // TODO: Decode BCL curve data for full keyframe animation
                    var keyframe = new KeyFrame(defVal, 0);

                    switch (channel)
                    {
                        case ChannelType.RotX: keyNode.XRot.Keys.Add(keyframe); break;
                        case ChannelType.RotY: keyNode.YRot.Keys.Add(keyframe); break;
                        case ChannelType.RotZ: keyNode.ZRot.Keys.Add(keyframe); break;
                        case ChannelType.PosX: keyNode.XPos.Keys.Add(keyframe); break;
                        case ChannelType.PosY: keyNode.YPos.Keys.Add(keyframe); break;
                        case ChannelType.PosZ: keyNode.ZPos.Keys.Add(keyframe); break;
                        case ChannelType.SclX: keyNode.XScale.Keys.Add(keyframe); break;
                        case ChannelType.SclY: keyNode.YScale.Keys.Add(keyframe); break;
                        case ChannelType.SclZ: keyNode.ZScale.Keys.Add(keyframe); break;
                        case ChannelType.Static:
                        default:
                            // Flag 0x00 or unknown — skip
                            break;
                    }
                }

                // Only add bones that have some animation data
                if (keyNode.HasPositionAnimation || keyNode.HasRotationAnimation || keyNode.HasScaleAnimation)
                {
                    anim.Bones.Add(keyNode);
                }
            }

            // Also add bones with no BCL group but that have BCA tracks with keys > 0
            // These are tracks we couldn't map to a bone — add them as unnamed tracks
            var mappedHashes = new HashSet<uint>(boneGroups.SelectMany(g => g.Select(x => x.hash)));
            int unmappedIdx = 0;
            foreach (var track in tracks)
            {
                if (mappedHashes.Contains(track.Hash)) continue;
                if (track.KeyCount == 0) continue; // skip static tracks

                var channel = GetChannel(track.Flags);
                if (channel == ChannelType.Static) continue;

                var keyNode = new KeyNode($"Unmapped_{unmappedIdx++}")
                {
                    BoneIndex = -1,
                    Hash = (int)track.Hash,
                    RotationType = RotationType.Euler
                };

                var keyframe = new KeyFrame(track.DefaultValue, 0);
                switch (channel)
                {
                    case ChannelType.RotX: keyNode.XRot.Keys.Add(keyframe); break;
                    case ChannelType.RotY: keyNode.YRot.Keys.Add(keyframe); break;
                    case ChannelType.RotZ: keyNode.ZRot.Keys.Add(keyframe); break;
                    case ChannelType.PosX: keyNode.XPos.Keys.Add(keyframe); break;
                    case ChannelType.PosY: keyNode.YPos.Keys.Add(keyframe); break;
                    case ChannelType.PosZ: keyNode.ZPos.Keys.Add(keyframe); break;
                    case ChannelType.SclX: keyNode.XScale.Keys.Add(keyframe); break;
                    case ChannelType.SclY: keyNode.YScale.Keys.Add(keyframe); break;
                    case ChannelType.SclZ: keyNode.ZScale.Keys.Add(keyframe); break;
                }

                if (keyNode.HasPositionAnimation || keyNode.HasRotationAnimation || keyNode.HasScaleAnimation)
                    anim.Bones.Add(keyNode);
            }

            return anim;
        }

        private static ChannelType GetChannel(uint flags)
        {
            // Flags observed: 0x00, 0x06, 0x0A, 0x0E, 0x12, 0x16, 0x1A, 0x1E, 0x22, 0x26
            // Pattern: (flag - 2) / 4 = channel index 0..8
            // But flag 0x00 is special (static/no-op)
            if (flags == 0x00) return ChannelType.Static;

            // Some flags don't follow the (f-2)/4 pattern cleanly
            return flags switch
            {
                0x02 => ChannelType.RotX,
                0x06 => ChannelType.RotY,
                0x0A => ChannelType.RotZ,
                0x0E => ChannelType.PosX,
                0x12 => ChannelType.PosY,
                0x16 => ChannelType.PosZ,
                0x1A => ChannelType.SclX,
                0x1E => ChannelType.SclY,
                0x22 => ChannelType.SclZ,
                0x26 => ChannelType.SclZ, // overflow — treat as last scale channel
                _ => ChannelType.Static
            };
        }
    }
}
