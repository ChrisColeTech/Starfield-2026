#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Starfield2026.ModelLoader.DTOs;
using static Starfield2026.ModelLoader.Loaders.ColladaHelpers;

namespace Starfield2026.ModelLoader.Loaders;

public static class ClipLoader
{
    public static AnimationClip Load(string daePath, Skeleton skeleton, string clipName)
    {
        var doc = XDocument.Load(daePath);
        var root = doc.Root!;

        var floatSources = new Dictionary<string, float[]>(StringComparer.Ordinal);
        foreach (var src in root.Descendants(Col + "source"))
        {
            string? id = src.Attribute("id")?.Value;
            var fa = src.Element(Col + "float_array");
            if (id != null && fa != null)
            {
                float[] data = ParseFloats((string)fa);
                if (data.Length > 0)
                    floatSources[id] = data;
            }
        }

        var animations = root.Descendants(Col + "animation")
            .Where(a => a.Elements(Col + "sampler").Any())
            .ToList();

        var tracks = new List<BoneTrack>();
        float maxTime = 0f;

        foreach (var anim in animations)
        {
            var channel = anim.Element(Col + "channel");
            if (channel == null) continue;

            string target = (string?)channel.Attribute("target") ?? "";
            if (!target.EndsWith("/transform", StringComparison.Ordinal)) continue;

            string boneName = target[..target.IndexOf('/')];
            if (!skeleton.TryGetBoneIndex(boneName, out int boneIndex)) continue;

            var sampler = anim.Element(Col + "sampler");
            if (sampler == null) continue;

            string? inputId = GetSamplerSourceId(sampler, "INPUT")?.TrimStart('#');
            string? outputId = GetSamplerSourceId(sampler, "OUTPUT")?.TrimStart('#');
            if (inputId == null || outputId == null) continue;
            if (!floatSources.TryGetValue(inputId, out float[]? times)) continue;
            if (!floatSources.TryGetValue(outputId, out float[]? values)) continue;

            int keyCount = Math.Min(times.Length, values.Length / 16);
            if (keyCount == 0) continue;

            var keyframes = new List<Keyframe>(keyCount);
            for (int i = 0; i < keyCount; i++)
            {
                Matrix m = ReadMatrixFromFloats(values, i * 16);
                keyframes.Add(new Keyframe(times[i], m));
                if (times[i] > maxTime) maxTime = times[i];
            }

            tracks.Add(new BoneTrack(boneIndex, keyframes));
        }

        return new AnimationClip(clipName, maxTime, tracks);
    }
}
