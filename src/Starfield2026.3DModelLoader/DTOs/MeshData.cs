#nullable enable
using System.Collections.Generic;

namespace Starfield2026.ModelLoader.DTOs;

public sealed record InputBinding(string Semantic, string SourceId, int Offset);

public sealed class MeshData
{
    public required string GeometryId { get; init; }
    public required float[] Positions { get; init; }
    public required float[] Normals { get; init; }
    public required float[] UVs { get; init; }
    public required int[] Indices { get; init; }
    public required int Stride { get; init; }
    public required List<InputBinding> Inputs { get; init; }
    public string MaterialSymbol { get; init; } = "";
}
