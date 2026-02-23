#nullable enable
using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Xna.Framework;

namespace Starfield2026.ModelLoader.Loaders;

internal static class ColladaHelpers
{
    internal static readonly XNamespace Col = "http://www.collada.org/2005/11/COLLADASchema";

    internal static Matrix ReadNodeTransform(XElement node)
    {
        var matrixElem = node.Element(Col + "matrix");
        if (matrixElem != null)
        {
            float[] vals = ParseFloats((string)matrixElem);
            if (vals.Length >= 16)
                return ReadMatrixFromFloats(vals, 0);
        }

        Vector3 translation = Vector3.Zero;
        Vector3 scaleVec = Vector3.One;
        Matrix rotation = Matrix.Identity;

        var translate = node.Element(Col + "translate");
        if (translate != null)
        {
            float[] t = ParseFloats((string)translate);
            if (t.Length >= 3)
                translation = new Vector3(t[0], t[1], t[2]);
        }

        foreach (var rotate in node.Elements(Col + "rotate"))
        {
            float[] r = ParseFloats((string)rotate);
            if (r.Length >= 4)
                rotation *= Matrix.CreateFromAxisAngle(
                    new Vector3(r[0], r[1], r[2]),
                    MathHelper.ToRadians(r[3]));
        }

        var scale = node.Element(Col + "scale");
        if (scale != null)
        {
            float[] s = ParseFloats((string)scale);
            if (s.Length >= 3)
                scaleVec = new Vector3(s[0], s[1], s[2]);
        }

        return Matrix.CreateScale(scaleVec) * rotation * Matrix.CreateTranslation(translation);
    }

    internal static Matrix ReadMatrixFromFloats(float[] vals, int offset)
    {
        return new Matrix(
            vals[offset + 0], vals[offset + 4], vals[offset + 8],  vals[offset + 12],
            vals[offset + 1], vals[offset + 5], vals[offset + 9],  vals[offset + 13],
            vals[offset + 2], vals[offset + 6], vals[offset + 10], vals[offset + 14],
            vals[offset + 3], vals[offset + 7], vals[offset + 11], vals[offset + 15]);
    }

    internal static string? GetSamplerSourceId(XElement sampler, string semantic)
    {
        return (string?)sampler.Elements(Col + "input")
            .FirstOrDefault(i => (string?)i.Attribute("semantic") == semantic)
            ?.Attribute("source");
    }

    internal static float[] ParseFloats(string text)
    {
        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var result = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            result[i] = float.Parse(parts[i], CultureInfo.InvariantCulture);
        return result;
    }

    internal static int[] ParseInts(string text)
    {
        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var result = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            result[i] = int.Parse(parts[i], CultureInfo.InvariantCulture);
        return result;
    }

    internal static string[] ParseNames(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Array.Empty<string>();
        return value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    }

    internal static float[] ParseFloatsNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Array.Empty<float>();
        return ParseFloats(value);
    }

    internal static int[] ParseIntsNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Array.Empty<int>();
        return ParseInts(value);
    }
}
