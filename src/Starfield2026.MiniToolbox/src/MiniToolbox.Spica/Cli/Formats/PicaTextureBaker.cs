using MiniToolbox.Spica.Formats.CtrH3D;
using MiniToolbox.Spica.Formats.CtrH3D.Model;
using MiniToolbox.Spica.Formats.CtrH3D.Model.Material;
using MiniToolbox.Spica.Formats.CtrH3D.Model.Mesh;
using MiniToolbox.Spica.Math3D;
using MiniToolbox.Spica.PICA.Commands;
using MiniToolbox.Spica.PICA.Converters;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;

namespace SpicaCli.Formats
{
    /// <summary>
    /// Evaluates the PICA200 6-stage texture combiner pipeline in software and
    /// bakes multi-texture materials into a single composited diffuse texture.
    /// Rasterizes vertex colors into UV space so materials that reference
    /// PrimaryColor (like fire effects) get correct per-pixel colors.
    /// </summary>
    public static class PicaTextureBaker
    {
        public static void BakeScene(H3D scene, string texturesDir)
        {
            var texCache = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
            var texSizes = new Dictionary<string, (int W, int H)>(StringComparer.OrdinalIgnoreCase);

            foreach (var model in scene.Models)
            {
                for (int matIdx = 0; matIdx < model.Materials.Count; matIdx++)
                {
                    var mat = model.Materials[matIdx];
                    if (!NeedsBaking(mat)) continue;

                    BakeMaterial(mat, matIdx, model, scene, texturesDir, texCache, texSizes);
                }
            }
        }

        public static bool NeedsBaking(H3DMaterial material)
        {
            if (string.IsNullOrEmpty(material.Texture1Name) &&
                string.IsNullOrEmpty(material.Texture2Name))
                return false;

            var stages = material.MaterialParams.TexEnvStages;

            for (int s = 0; s < 6; s++)
            {
                var stage = stages[s];
                if (stage.IsColorPassThrough && stage.IsAlphaPassThrough)
                    continue;

                for (int i = 0; i < 3; i++)
                {
                    var cs = stage.Source.Color[i];
                    var als = stage.Source.Alpha[i];

                    if (cs == PICATextureCombinerSource.Texture1 ||
                        cs == PICATextureCombinerSource.Texture2 ||
                        als == PICATextureCombinerSource.Texture1 ||
                        als == PICATextureCombinerSource.Texture2)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check if any non-pass-through stage references PrimaryColor or FragmentPrimaryColor.
        /// </summary>
        static bool UsesPrimaryColor(H3DMaterialParams mp)
        {
            for (int s = 0; s < 6; s++)
            {
                var stage = mp.TexEnvStages[s];
                if (stage.IsColorPassThrough && stage.IsAlphaPassThrough)
                    continue;

                for (int i = 0; i < 3; i++)
                {
                    if (stage.Source.Color[i] == PICATextureCombinerSource.PrimaryColor ||
                        stage.Source.Color[i] == PICATextureCombinerSource.FragmentPrimaryColor ||
                        stage.Source.Alpha[i] == PICATextureCombinerSource.PrimaryColor ||
                        stage.Source.Alpha[i] == PICATextureCombinerSource.FragmentPrimaryColor)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Rasterize vertex colors from all meshes using the given material index
        /// into a texture in UV space. Returns RGBA float array, or null if no
        /// vertex colors are present.
        /// </summary>
        static float[] RasterizeVertexColors(H3DModel model, int materialIndex, int width, int height)
        {
            float[] colorMap = new float[width * height * 4];
            float[] weightMap = new float[width * height]; // track coverage for averaging
            bool hasAny = false;

            foreach (var mesh in model.Meshes)
            {
                if (mesh.MaterialIndex != materialIndex) continue;

                // Check if this mesh has vertex color attribute
                bool hasColor = false;
                foreach (var attr in mesh.Attributes)
                {
                    if (attr.Name == PICAAttributeName.Color) { hasColor = true; break; }
                }
                if (!hasColor) continue;

                PICAVertex[] verts = mesh.GetVertices();

                foreach (var subMesh in mesh.SubMeshes)
                {
                    ushort[] indices = subMesh.Indices;

                    // Process triangles
                    for (int t = 0; t + 2 < indices.Length; t += 3)
                    {
                        var v0 = verts[indices[t]];
                        var v1 = verts[indices[t + 1]];
                        var v2 = verts[indices[t + 2]];

                        RasterizeTriangle(colorMap, weightMap, width, height,
                            v0.TexCoord0, v0.Color,
                            v1.TexCoord0, v1.Color,
                            v2.TexCoord0, v2.Color);
                        hasAny = true;
                    }
                }
            }

            if (!hasAny) return null;

            // Normalize by weight (where triangles overlapped)
            for (int i = 0; i < width * height; i++)
            {
                if (weightMap[i] > 0)
                {
                    int pi = i * 4;
                    float w = weightMap[i];
                    colorMap[pi + 0] /= w;
                    colorMap[pi + 1] /= w;
                    colorMap[pi + 2] /= w;
                    colorMap[pi + 3] /= w;
                }
                else
                {
                    // No triangle coverage — default to white (neutral for multiply)
                    int pi = i * 4;
                    colorMap[pi + 0] = 1;
                    colorMap[pi + 1] = 1;
                    colorMap[pi + 2] = 1;
                    colorMap[pi + 3] = 1;
                }
            }

            return colorMap;
        }

        static void RasterizeTriangle(
            float[] colorMap, float[] weightMap, int w, int h,
            Vector4 uv0, Vector4 c0,
            Vector4 uv1, Vector4 c1,
            Vector4 uv2, Vector4 c2)
        {
            // Convert UV (0-1) to pixel coordinates
            // UV Y is typically flipped in 3DS textures
            float px0 = uv0.X * w, py0 = (1 - uv0.Y) * h;
            float px1 = uv1.X * w, py1 = (1 - uv1.Y) * h;
            float px2 = uv2.X * w, py2 = (1 - uv2.Y) * h;

            // Bounding box
            int minX = Math.Max(0, (int)MathF.Floor(Math.Min(px0, Math.Min(px1, px2))));
            int maxX = Math.Min(w - 1, (int)MathF.Ceiling(Math.Max(px0, Math.Max(px1, px2))));
            int minY = Math.Max(0, (int)MathF.Floor(Math.Min(py0, Math.Min(py1, py2))));
            int maxY = Math.Min(h - 1, (int)MathF.Ceiling(Math.Max(py0, Math.Max(py1, py2))));

            float denom = (py1 - py2) * (px0 - px2) + (px2 - px1) * (py0 - py2);
            if (MathF.Abs(denom) < 1e-8f) return; // degenerate triangle

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;

                    float w0 = ((py1 - py2) * (px - px2) + (px2 - px1) * (py - py2)) / denom;
                    float w1 = ((py2 - py0) * (px - px2) + (px0 - px2) * (py - py2)) / denom;
                    float w2 = 1 - w0 - w1;

                    if (w0 < -0.001f || w1 < -0.001f || w2 < -0.001f) continue;

                    int pi = (y * w + x) * 4;
                    colorMap[pi + 0] += w0 * c0.X + w1 * c1.X + w2 * c2.X;
                    colorMap[pi + 1] += w0 * c0.Y + w1 * c1.Y + w2 * c2.Y;
                    colorMap[pi + 2] += w0 * c0.Z + w1 * c1.Z + w2 * c2.Z;
                    colorMap[pi + 3] += w0 * c0.W + w1 * c1.W + w2 * c2.W;
                    weightMap[y * w + x] += 1;
                }
            }
        }

        static void BakeMaterial(
            H3DMaterial mat,
            int matIdx,
            H3DModel model,
            H3D scene,
            string texturesDir,
            Dictionary<string, float[]> texCache,
            Dictionary<string, (int W, int H)> texSizes)
        {
            var mp = mat.MaterialParams;

            // Resolve textures
            string[] texNames = { mat.Texture0Name, mat.Texture1Name, mat.Texture2Name };
            float[][] texData = new float[3][];
            int[] texW = new int[3];
            int[] texH = new int[3];

            int maxW = 1, maxH = 1;

            for (int t = 0; t < 3; t++)
            {
                if (string.IsNullOrEmpty(texNames[t])) continue;

                if (!texCache.TryGetValue(texNames[t], out float[] pixels))
                {
                    pixels = LoadTexturePixels(texNames[t], texturesDir, scene, out int w, out int h);
                    if (pixels != null)
                    {
                        texCache[texNames[t]] = pixels;
                        texSizes[texNames[t]] = (w, h);
                    }
                }

                texData[t] = pixels;
                if (pixels != null && texSizes.TryGetValue(texNames[t], out var sz))
                {
                    texW[t] = sz.W;
                    texH[t] = sz.H;
                    if (sz.W > maxW) maxW = sz.W;
                    if (sz.H > maxH) maxH = sz.H;
                }
            }

            if (texData[0] == null && texData[1] == null && texData[2] == null)
                return;

            // Build constant color lookup
            RGBA[] constants = {
                mp.Constant0Color, mp.Constant1Color, mp.Constant2Color,
                mp.Constant3Color, mp.Constant4Color, mp.Constant5Color
            };

            float[] bufferColor = {
                mp.TexEnvBufferColor.R / 255f,
                mp.TexEnvBufferColor.G / 255f,
                mp.TexEnvBufferColor.B / 255f,
                mp.TexEnvBufferColor.A / 255f
            };

            // Rasterize vertex colors if this material uses PrimaryColor
            float[] vertexColorMap = null;
            if (UsesPrimaryColor(mp))
            {
                vertexColorMap = RasterizeVertexColors(model, matIdx, maxW, maxH);
            }

            // Evaluate pipeline for each pixel
            float[] output = new float[maxW * maxH * 4];

            for (int y = 0; y < maxH; y++)
            {
                for (int x = 0; x < maxW; x++)
                {
                    // Sample all 3 textures at this coordinate
                    float[][] texSamples = new float[3][];
                    for (int t = 0; t < 3; t++)
                    {
                        if (texData[t] != null)
                            texSamples[t] = SampleTexture(texData[t], texW[t], texH[t], x, y, maxW, maxH);
                        else
                            texSamples[t] = new float[] { 0, 0, 0, 1 };
                    }

                    // Sample vertex color at this pixel
                    float[] primaryColor;
                    if (vertexColorMap != null)
                    {
                        int vci = (y * maxW + x) * 4;
                        primaryColor = new float[] {
                            vertexColorMap[vci], vertexColorMap[vci + 1],
                            vertexColorMap[vci + 2], vertexColorMap[vci + 3]
                        };
                    }
                    else
                    {
                        primaryColor = new float[] { 1, 1, 1, 1 };
                    }

                    // Run 6-stage pipeline
                    float[] previous = { 0, 0, 0, 0 };
                    float[] buffer = (float[])bufferColor.Clone();

                    for (int s = 0; s < 6; s++)
                    {
                        var stage = mp.TexEnvStages[s];

                        // Resolve constant color for this stage
                        int constIdx = mp.GetConstantIndex(s);
                        float[] stageConstant;
                        if (constIdx >= 0 && constIdx < 6)
                        {
                            var c = constants[constIdx];
                            stageConstant = new float[] { c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f };
                        }
                        else
                        {
                            var c = stage.Color;
                            stageConstant = new float[] { c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f };
                        }

                        // Resolve color sources (3 sources)
                        float[][] colorSrc = new float[3][];
                        for (int i = 0; i < 3; i++)
                        {
                            float[] raw = ResolveSource(stage.Source.Color[i], texSamples, previous, buffer, stageConstant, primaryColor);
                            colorSrc[i] = ApplyColorOperand(raw, stage.Operand.Color[i]);
                        }

                        // Resolve alpha sources (3 sources)
                        float[] alphaSrc = new float[3];
                        for (int i = 0; i < 3; i++)
                        {
                            float[] raw = ResolveSource(stage.Source.Alpha[i], texSamples, previous, buffer, stageConstant, primaryColor);
                            alphaSrc[i] = ApplyAlphaOperand(raw, stage.Operand.Alpha[i]);
                        }

                        // Apply combiner
                        float[] colorResult = ApplyCombinerColor(stage.Combiner.Color, colorSrc);
                        float alphaResult = ApplyCombinerAlpha(stage.Combiner.Alpha, alphaSrc);

                        // Apply scale
                        float colorScale = GetScale(stage.Scale.Color);
                        float alphaScale = GetScale(stage.Scale.Alpha);

                        float[] stageResult = {
                            Clamp01(colorResult[0] * colorScale),
                            Clamp01(colorResult[1] * colorScale),
                            Clamp01(colorResult[2] * colorScale),
                            Clamp01(alphaResult * alphaScale)
                        };

                        // Update buffer if flagged (stages 1-4 only)
                        // Buffer gets the PREVIOUS stage output, not current
                        if (stage.UpdateColorBuffer)
                        {
                            buffer[0] = previous[0];
                            buffer[1] = previous[1];
                            buffer[2] = previous[2];
                        }
                        if (stage.UpdateAlphaBuffer)
                        {
                            buffer[3] = previous[3];
                        }

                        previous = stageResult;
                    }

                    int pxIdx = (y * maxW + x) * 4;
                    output[pxIdx + 0] = previous[0];
                    output[pxIdx + 1] = previous[1];
                    output[pxIdx + 2] = previous[2];
                    output[pxIdx + 3] = previous[3];
                }
            }

            // Save baked texture — overwrite the original Texture0 file so the
            // DAE exporter's library_images and surface refs stay correct.
            string tex0Name = texNames[0];
            string bakedPath = null;
            string[] saveCandidates = {
                Path.Combine(texturesDir, $"{tex0Name}.png"),
                Path.Combine(texturesDir, $"{tex0Name}.tga.png"),
            };
            foreach (string cand in saveCandidates)
            {
                if (File.Exists(cand)) { bakedPath = cand; break; }
            }
            bakedPath ??= Path.Combine(texturesDir, $"{tex0Name}.png");

            SaveFloatPixels(output, maxW, maxH, bakedPath);

            // Invalidate cache so subsequent materials don't load stale data
            texCache.Remove(tex0Name);

            // Clear Texture1/2 so the DAE exporter doesn't try to reference them.
            // Keep Texture0Name as-is — the file we just wrote replaces the original.
            mat.Texture1Name = null;
            mat.Texture2Name = null;

            Console.WriteLine($"  Baked: {Path.GetFileName(bakedPath)} ({maxW}x{maxH}) from [{string.Join(", ", texNames)}]");
        }

        static float[] LoadTexturePixels(string texName, string texturesDir, H3D scene, out int width, out int height)
        {
            width = 0;
            height = 0;

            string[] candidates = {
                Path.Combine(texturesDir, $"{texName}.png"),
                Path.Combine(texturesDir, $"{texName}.tga.png"),
            };

            foreach (string path in candidates)
            {
                if (File.Exists(path))
                    return LoadBitmapPixels(path, out width, out height);
            }

            // Fall back to H3D scene texture data
            foreach (var tex in scene.Textures)
            {
                if (string.Equals(tex.Name, texName, StringComparison.OrdinalIgnoreCase))
                {
                    width = tex.Width;
                    height = tex.Height;
                    byte[] rgba = tex.ToRGBA();
                    float[] pixels = new float[width * height * 4];
                    for (int i = 0; i < rgba.Length; i++)
                        pixels[i] = rgba[i] / 255f;
                    return pixels;
                }
            }

            return null;
        }

        static float[] LoadBitmapPixels(string path, out int width, out int height)
        {
            using var bmp = new Bitmap(path);
            width = bmp.Width;
            height = bmp.Height;

            var rect = new Rectangle(0, 0, width, height);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            byte[] raw = new byte[width * height * 4];
            Marshal.Copy(data.Scan0, raw, 0, raw.Length);
            bmp.UnlockBits(data);

            float[] pixels = new float[width * height * 4];
            for (int i = 0; i < width * height; i++)
            {
                int si = i * 4;
                pixels[si + 0] = raw[si + 2] / 255f; // R
                pixels[si + 1] = raw[si + 1] / 255f; // G
                pixels[si + 2] = raw[si + 0] / 255f; // B
                pixels[si + 3] = raw[si + 3] / 255f; // A
            }
            return pixels;
        }

        static float[] SampleTexture(float[] texData, int texW, int texH, int x, int y, int outW, int outH)
        {
            int sx = texW == outW ? x : (x * texW / outW);
            int sy = texH == outH ? y : (y * texH / outH);
            sx = Math.Clamp(sx, 0, texW - 1);
            sy = Math.Clamp(sy, 0, texH - 1);

            int idx = (sy * texW + sx) * 4;
            return new float[] { texData[idx], texData[idx + 1], texData[idx + 2], texData[idx + 3] };
        }

        static float[] ResolveSource(
            PICATextureCombinerSource source,
            float[][] texSamples,
            float[] previous,
            float[] buffer,
            float[] stageConstant,
            float[] primaryColor)
        {
            return source switch
            {
                PICATextureCombinerSource.Texture0 => texSamples[0],
                PICATextureCombinerSource.Texture1 => texSamples[1],
                PICATextureCombinerSource.Texture2 => texSamples[2],
                PICATextureCombinerSource.Texture3 => new float[] { 0, 0, 0, 0 },
                PICATextureCombinerSource.Previous => previous,
                PICATextureCombinerSource.PreviousBuffer => buffer,
                PICATextureCombinerSource.Constant => stageConstant,
                PICATextureCombinerSource.PrimaryColor => primaryColor,
                PICATextureCombinerSource.FragmentPrimaryColor => primaryColor,
                PICATextureCombinerSource.FragmentSecondaryColor => new float[] { 0, 0, 0, 0 },
                _ => new float[] { 0, 0, 0, 0 }
            };
        }

        static float[] ApplyColorOperand(float[] src, PICATextureCombinerColorOp op)
        {
            return op switch
            {
                PICATextureCombinerColorOp.Color => new float[] { src[0], src[1], src[2] },
                PICATextureCombinerColorOp.OneMinusColor => new float[] { 1 - src[0], 1 - src[1], 1 - src[2] },
                PICATextureCombinerColorOp.Alpha => new float[] { src[3], src[3], src[3] },
                PICATextureCombinerColorOp.OneMinusAlpha => new float[] { 1 - src[3], 1 - src[3], 1 - src[3] },
                PICATextureCombinerColorOp.Red => new float[] { src[0], src[0], src[0] },
                PICATextureCombinerColorOp.OneMinusRed => new float[] { 1 - src[0], 1 - src[0], 1 - src[0] },
                PICATextureCombinerColorOp.Green => new float[] { src[1], src[1], src[1] },
                PICATextureCombinerColorOp.OneMinusGreen => new float[] { 1 - src[1], 1 - src[1], 1 - src[1] },
                PICATextureCombinerColorOp.Blue => new float[] { src[2], src[2], src[2] },
                PICATextureCombinerColorOp.OneMinusBlue => new float[] { 1 - src[2], 1 - src[2], 1 - src[2] },
                _ => new float[] { src[0], src[1], src[2] }
            };
        }

        static float ApplyAlphaOperand(float[] src, PICATextureCombinerAlphaOp op)
        {
            return op switch
            {
                PICATextureCombinerAlphaOp.Alpha => src[3],
                PICATextureCombinerAlphaOp.OneMinusAlpha => 1 - src[3],
                PICATextureCombinerAlphaOp.Red => src[0],
                PICATextureCombinerAlphaOp.OneMinusRed => 1 - src[0],
                PICATextureCombinerAlphaOp.Green => src[1],
                PICATextureCombinerAlphaOp.OneMinusGreen => 1 - src[1],
                PICATextureCombinerAlphaOp.Blue => src[2],
                PICATextureCombinerAlphaOp.OneMinusBlue => 1 - src[2],
                _ => src[3]
            };
        }

        static float[] ApplyCombinerColor(PICATextureCombinerMode mode, float[][] src)
        {
            float[] s0 = src[0], s1 = src[1], s2 = src[2];

            return mode switch
            {
                PICATextureCombinerMode.Replace => new float[] { s0[0], s0[1], s0[2] },
                PICATextureCombinerMode.Modulate => new float[] {
                    s0[0] * s1[0], s0[1] * s1[1], s0[2] * s1[2] },
                PICATextureCombinerMode.Add => new float[] {
                    s0[0] + s1[0], s0[1] + s1[1], s0[2] + s1[2] },
                PICATextureCombinerMode.AddSigned => new float[] {
                    s0[0] + s1[0] - 0.5f, s0[1] + s1[1] - 0.5f, s0[2] + s1[2] - 0.5f },
                PICATextureCombinerMode.Interpolate => new float[] {
                    s0[0] * s2[0] + s1[0] * (1 - s2[0]),
                    s0[1] * s2[1] + s1[1] * (1 - s2[1]),
                    s0[2] * s2[2] + s1[2] * (1 - s2[2]) },
                PICATextureCombinerMode.Subtract => new float[] {
                    s0[0] - s1[0], s0[1] - s1[1], s0[2] - s1[2] },
                PICATextureCombinerMode.DotProduct3Rgb => new float[] {
                    DotProduct3(s0, s1), DotProduct3(s0, s1), DotProduct3(s0, s1) },
                PICATextureCombinerMode.DotProduct3Rgba => new float[] {
                    DotProduct3(s0, s1), DotProduct3(s0, s1), DotProduct3(s0, s1) },
                PICATextureCombinerMode.MultAdd => new float[] {
                    s0[0] * s1[0] + s2[0], s0[1] * s1[1] + s2[1], s0[2] * s1[2] + s2[2] },
                PICATextureCombinerMode.AddMult => new float[] {
                    (s0[0] + s1[0]) * s2[0], (s0[1] + s1[1]) * s2[1], (s0[2] + s1[2]) * s2[2] },
                _ => new float[] { s0[0], s0[1], s0[2] }
            };
        }

        static float ApplyCombinerAlpha(PICATextureCombinerMode mode, float[] src)
        {
            float s0 = src[0], s1 = src[1], s2 = src[2];

            return mode switch
            {
                PICATextureCombinerMode.Replace => s0,
                PICATextureCombinerMode.Modulate => s0 * s1,
                PICATextureCombinerMode.Add => s0 + s1,
                PICATextureCombinerMode.AddSigned => s0 + s1 - 0.5f,
                PICATextureCombinerMode.Interpolate => s0 * s2 + s1 * (1 - s2),
                PICATextureCombinerMode.Subtract => s0 - s1,
                PICATextureCombinerMode.DotProduct3Rgb => s0,
                PICATextureCombinerMode.DotProduct3Rgba => s0 * s1,
                PICATextureCombinerMode.MultAdd => s0 * s1 + s2,
                PICATextureCombinerMode.AddMult => (s0 + s1) * s2,
                _ => s0
            };
        }

        static float DotProduct3(float[] a, float[] b)
        {
            float dot = 4 * (
                (a[0] - 0.5f) * (b[0] - 0.5f) +
                (a[1] - 0.5f) * (b[1] - 0.5f) +
                (a[2] - 0.5f) * (b[2] - 0.5f));
            return Math.Max(0, dot);
        }

        static float GetScale(PICATextureCombinerScale scale)
        {
            return scale switch
            {
                PICATextureCombinerScale.One => 1f,
                PICATextureCombinerScale.Two => 2f,
                PICATextureCombinerScale.Four => 4f,
                _ => 1f
            };
        }

        static float Clamp01(float v) => Math.Clamp(v, 0f, 1f);

        static void SaveFloatPixels(float[] pixels, int width, int height, string path)
        {
            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, width, height);
            var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            byte[] raw = new byte[width * height * 4];
            for (int i = 0; i < width * height; i++)
            {
                int si = i * 4;
                raw[si + 0] = (byte)(Clamp01(pixels[si + 2]) * 255); // B
                raw[si + 1] = (byte)(Clamp01(pixels[si + 1]) * 255); // G
                raw[si + 2] = (byte)(Clamp01(pixels[si + 0]) * 255); // R
                raw[si + 3] = (byte)(Clamp01(pixels[si + 3]) * 255); // A
            }

            Marshal.Copy(raw, 0, data.Scan0, raw.Length);
            bmp.UnlockBits(data);
            bmp.Save(path, ImageFormat.Png);
        }
    }
}
