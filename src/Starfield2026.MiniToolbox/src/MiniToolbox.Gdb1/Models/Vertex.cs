namespace MiniToolbox.Gdb1.Models;

/// <summary>
/// Represents a vertex in a 3D model.
/// </summary>
public struct Vertex
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float NX { get; set; }
    public float NY { get; set; }
    public float NZ { get; set; }
    public float U { get; set; }
    public float V { get; set; }

    public Vertex(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
        NX = 0;
        NY = 0;
        NZ = 0;
        U = 0;
        V = 0;
    }

    public Vertex(float x, float y, float z, float nx, float ny, float nz)
    {
        X = x;
        Y = y;
        Z = z;
        NX = nx;
        NY = ny;
        NZ = nz;
        U = 0;
        V = 0;
    }
}
