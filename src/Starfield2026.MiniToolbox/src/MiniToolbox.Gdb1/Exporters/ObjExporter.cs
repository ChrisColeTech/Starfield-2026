using System.Globalization;
using System.Text;
using MiniToolbox.Gdb1.Models;

namespace MiniToolbox.Gdb1.Exporters;

/// <summary>
/// Exports meshes to Wavefront OBJ format.
/// </summary>
public static class ObjExporter
{
    /// <summary>
    /// Exports a mesh to OBJ format with MTL file.
    /// </summary>
    /// <param name="mesh">The mesh to export.</param>
    /// <param name="outputPath">Output path for the OBJ file.</param>
    /// <param name="textures">Texture info for MTL generation.</param>
    /// <param name="scale">Scale factor for vertex positions.</param>
    public static void Export(Mesh mesh, string outputPath, IReadOnlyList<TextureInfo>? textures = null, float scale = 100.0f)
    {
        string objDir = Path.GetDirectoryName(outputPath) ?? ".";
        string baseName = Path.GetFileNameWithoutExtension(outputPath);
        string mtlFileName = baseName + ".mtl";
        string mtlPath = Path.Combine(objDir, mtlFileName);

        var sb = new StringBuilder();

        // Header
        sb.AppendLine("# Star Fox GDB1 Model Export");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Vertices: {mesh.Vertices.Count}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Triangles: {mesh.TriangleCount}");

        // Reference MTL file
        if (textures != null && textures.Count > 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"mtllib {mtlFileName}");
        }
        sb.AppendLine();

        sb.AppendLine(CultureInfo.InvariantCulture, $"o {SanitizeName(mesh.Name)}");
        sb.AppendLine();

        // Write vertices
        foreach (var v in mesh.Vertices)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"v {v.X * scale:F6} {v.Y * scale:F6} {v.Z * scale:F6}");
        }
        sb.AppendLine();

        // Write texture coordinates
        foreach (var v in mesh.Vertices)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"vt {v.U:F6} {1.0f - v.V:F6}");
        }
        sb.AppendLine();

        // Write normals
        foreach (var v in mesh.Vertices)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"vn {v.NX:F6} {v.NY:F6} {v.NZ:F6}");
        }
        sb.AppendLine();

        // Use first material if available
        if (textures != null && textures.Count > 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"usemtl {SanitizeName(textures[0].Name)}");
        }

        // Write faces with v/vt/vn format
        for (int i = 0; i < mesh.Indices.Count - 2; i += 3)
        {
            int i1 = mesh.Indices[i] + 1;
            int i2 = mesh.Indices[i + 1] + 1;
            int i3 = mesh.Indices[i + 2] + 1;

            // Validate indices
            if (i1 >= 1 && i1 <= mesh.Vertices.Count &&
                i2 >= 1 && i2 <= mesh.Vertices.Count &&
                i3 >= 1 && i3 <= mesh.Vertices.Count)
            {
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"f {i1}/{i1}/{i1} {i2}/{i2}/{i2} {i3}/{i3}/{i3}");
            }
        }

        File.WriteAllText(outputPath, sb.ToString());

        // Write MTL file
        if (textures != null && textures.Count > 0)
        {
            WriteMtlFile(mtlPath, textures);
        }
    }

    /// <summary>
    /// Writes a MTL material library file.
    /// </summary>
    private static void WriteMtlFile(string mtlPath, IReadOnlyList<TextureInfo> textures)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Star Fox GDB1 Material Library");
        sb.AppendLine();

        foreach (var tex in textures)
        {
            string matName = SanitizeName(tex.Name);
            sb.AppendLine(CultureInfo.InvariantCulture, $"newmtl {matName}");
            sb.AppendLine("Ka 1.000 1.000 1.000");
            sb.AppendLine("Kd 1.000 1.000 1.000");
            sb.AppendLine("Ks 0.000 0.000 0.000");
            sb.AppendLine("d 1.0");
            sb.AppendLine("illum 1");
            sb.AppendLine(CultureInfo.InvariantCulture, $"map_Kd textures/{tex.Name}.png");
            sb.AppendLine();
        }

        File.WriteAllText(mtlPath, sb.ToString());
    }

    /// <summary>
    /// Exports a mesh to OBJ format as a string (without MTL).
    /// </summary>
    public static string ExportToString(Mesh mesh, float scale = 100.0f)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Star Fox GDB1 Model Export");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Vertices: {mesh.Vertices.Count}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Triangles: {mesh.TriangleCount}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"o {SanitizeName(mesh.Name)}");
        sb.AppendLine();

        foreach (var v in mesh.Vertices)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"v {v.X * scale:F6} {v.Y * scale:F6} {v.Z * scale:F6}");
        }
        sb.AppendLine();

        foreach (var v in mesh.Vertices)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"vt {v.U:F6} {1.0f - v.V:F6}");
        }
        sb.AppendLine();

        foreach (var v in mesh.Vertices)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"vn {v.NX:F6} {v.NY:F6} {v.NZ:F6}");
        }
        sb.AppendLine();

        for (int i = 0; i < mesh.Indices.Count - 2; i += 3)
        {
            int i1 = mesh.Indices[i] + 1;
            int i2 = mesh.Indices[i + 1] + 1;
            int i3 = mesh.Indices[i + 2] + 1;

            if (i1 >= 1 && i1 <= mesh.Vertices.Count &&
                i2 >= 1 && i2 <= mesh.Vertices.Count &&
                i3 >= 1 && i3 <= mesh.Vertices.Count)
            {
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"f {i1}/{i1}/{i1} {i2}/{i2}/{i2} {i3}/{i3}/{i3}");
            }
        }

        return sb.ToString();
    }

    private static string SanitizeName(string name)
    {
        return new string(name.Select(c =>
            char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray());
    }
}
