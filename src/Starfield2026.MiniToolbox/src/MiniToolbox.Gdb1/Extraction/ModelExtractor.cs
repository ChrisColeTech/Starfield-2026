using System.Buffers.Binary;
using MiniToolbox.Gdb1.Models;

namespace MiniToolbox.Gdb1.Extraction;

/// <summary>
/// Extracts 3D models from .modelgdb + .modelbin files.
/// </summary>
public class ModelExtractor
{
    private const float VertexScale = 32767.0f;
    private const int VertexRecordSize = 20;

    private readonly string _gdbPath;
    private readonly string _binPath;
    private readonly string _metaPath;
    private readonly byte[] _gdbData;
    private readonly byte[] _binData;
    private readonly Gdb1Parser _parser;
    private readonly ResourceDatabase _resourceDb;

    public ModelExtractor(string gdbPath, ResourceDatabase resourceDb)
    {
        _gdbPath = gdbPath;
        _binPath = Path.ChangeExtension(gdbPath, ".modelbin");
        _metaPath = Path.ChangeExtension(gdbPath, ".resourcemetadata");
        _resourceDb = resourceDb;

        _gdbData = File.ReadAllBytes(_gdbPath);

        if (!File.Exists(_binPath))
            throw new FileNotFoundException($"Missing modelbin: {_binPath}");

        _binData = File.ReadAllBytes(_binPath);
        _parser = new Gdb1Parser(_gdbData);
    }

    /// <summary>
    /// Gets the model name from metadata or GDB strings.
    /// </summary>
    public string GetModelName()
    {
        // Try metadata first
        if (File.Exists(_metaPath))
        {
            var metaData = File.ReadAllBytes(_metaPath);
            var metaParser = new Gdb1Parser(metaData);
            var strings = metaParser.ExtractStrings();

            foreach (var s in strings)
            {
                if (s.Contains(".cmdl"))
                    return Path.GetFileNameWithoutExtension(s);
            }
        }

        // Fallback to GDB strings
        var gdbStrings = _parser.ExtractStrings();
        foreach (var s in gdbStrings)
        {
            if (s.Contains(".cmdl"))
                return Path.GetFileNameWithoutExtension(s);
        }

        // Last resort: use file stem
        return Path.GetFileNameWithoutExtension(_gdbPath);
    }

    /// <summary>
    /// Finds texture ResourceIDs referenced by this model.
    /// </summary>
    public List<string> GetTextureRefs()
    {
        var validIds = _resourceDb.Textures.Keys.ToHashSet();
        return _parser.FindResourceIds(validIds);
    }

    /// <summary>
    /// Finds animation ResourceIDs referenced by this model.
    /// </summary>
    public List<string> GetAnimationRefs()
    {
        var validIds = _resourceDb.Animations.Keys.ToHashSet();
        return _parser.FindResourceIds(validIds);
    }

    /// <summary>
    /// Reads index buffer and converts triangle strip to triangle list.
    /// </summary>
    private List<int> ReadIndices(int endOffset)
    {
        var rawIndices = new List<ushort>();

        for (int i = 0; i < endOffset; i += 2)
        {
            ushort idx = BinaryPrimitives.ReadUInt16LittleEndian(_binData.AsSpan(i, 2));
            rawIndices.Add(idx);
        }

        // Convert triangle strip to triangle list
        var triangles = new List<int>();

        for (int i = 0; i < rawIndices.Count - 2; i++)
        {
            int i0 = rawIndices[i];
            int i1 = rawIndices[i + 1];
            int i2 = rawIndices[i + 2];

            // Skip degenerate triangles
            if (i0 == i1 || i1 == i2 || i0 == i2)
                continue;

            // Alternate winding order for triangle strips
            if (i % 2 == 0)
            {
                triangles.Add(i0);
                triangles.Add(i1);
                triangles.Add(i2);
            }
            else
            {
                triangles.Add(i0);
                triangles.Add(i2);
                triangles.Add(i1);
            }
        }

        return triangles;
    }

    /// <summary>
    /// Reads vertex buffer with 20-byte records.
    /// Layout: position (6 bytes), normal (6 bytes), UV (4 bytes), color/flags (4 bytes)
    /// </summary>
    private List<Vertex> ReadVertices(int startOffset, int count)
    {
        var vertices = new List<Vertex>(count);

        for (int i = 0; i < count; i++)
        {
            int off = startOffset + i * VertexRecordSize;
            if (off + VertexRecordSize > _binData.Length)
                break;

            // Position: 3 shorts (6 bytes)
            short px = BinaryPrimitives.ReadInt16LittleEndian(_binData.AsSpan(off, 2));
            short py = BinaryPrimitives.ReadInt16LittleEndian(_binData.AsSpan(off + 2, 2));
            short pz = BinaryPrimitives.ReadInt16LittleEndian(_binData.AsSpan(off + 4, 2));

            // Normal: 3 shorts (6 bytes)
            short nx = BinaryPrimitives.ReadInt16LittleEndian(_binData.AsSpan(off + 6, 2));
            short ny = BinaryPrimitives.ReadInt16LittleEndian(_binData.AsSpan(off + 8, 2));
            short nz = BinaryPrimitives.ReadInt16LittleEndian(_binData.AsSpan(off + 10, 2));

            // Bytes 12-15 are 0xFFFFFFFF marker, skip them
            // UV: 2 shorts at offset 16-19 - range 0-32767 maps to 0.0-1.0
            short tu = BinaryPrimitives.ReadInt16LittleEndian(_binData.AsSpan(off + 16, 2));
            short tv = BinaryPrimitives.ReadInt16LittleEndian(_binData.AsSpan(off + 18, 2));

            vertices.Add(new Vertex
            {
                X = px / VertexScale,
                Y = py / VertexScale,
                Z = pz / VertexScale,
                NX = nx / VertexScale,
                NY = ny / VertexScale,
                NZ = nz / VertexScale,
                U = tu / VertexScale,
                V = tv / VertexScale
            });
        }

        return vertices;
    }

    /// <summary>
    /// Extracts the mesh from modelbin.
    /// </summary>
    public Mesh Extract()
    {
        string modelName = GetModelName();
        var textureIds = GetTextureRefs();

        // Find vertex data start (first 0xFFFFFFFF marker at offset +12)
        int vertexStart = 0;
        for (int i = 0; i < _binData.Length - 16; i++)
        {
            if (_binData[i + 12] == 0xFF &&
                _binData[i + 13] == 0xFF &&
                _binData[i + 14] == 0xFF &&
                _binData[i + 15] == 0xFF)
            {
                vertexStart = i;
                break;
            }
        }

        // Count vertices by finding max index
        int maxIndex = 0;
        for (int i = 0; i < vertexStart; i += 2)
        {
            ushort idx = BinaryPrimitives.ReadUInt16LittleEndian(_binData.AsSpan(i, 2));
            if (idx < 10000)
                maxIndex = Math.Max(maxIndex, idx);
        }

        int vertexCount = maxIndex + 1;

        var indices = ReadIndices(vertexStart);
        var vertices = ReadVertices(vertexStart, vertexCount);

        return new Mesh
        {
            Name = modelName,
            Vertices = vertices,
            Indices = indices,
            TextureIds = textureIds
        };
    }

    public string GdbPath => _gdbPath;
    public string BinPath => _binPath;
    public string MetaPath => _metaPath;
}
