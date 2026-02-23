#nullable enable
using Microsoft.Xna.Framework;

namespace Starfield2026.ModelLoader.DTOs;

public sealed record Bone(
    int Index,
    string Name,
    string NodeId,
    int ParentIndex,
    Matrix LocalTransform);
