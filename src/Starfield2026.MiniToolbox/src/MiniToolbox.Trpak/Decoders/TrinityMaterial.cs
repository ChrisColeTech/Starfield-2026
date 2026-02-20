using MiniToolbox.Trpak.Flatbuffers.TR.Model;
using MiniToolbox.Core.Utils;

namespace MiniToolbox.Trpak.Decoders
{
    /// <summary>
    /// Data-only material decoded from TRMTR / Gfx2 material files.
    /// Ported from gftool Material.cs — all GL rendering code removed.
    /// </summary>
    public class TrinityMaterial
    {
        public string Name { get; set; }
        public string ShaderName { get; set; }
        public List<TextureRef> Textures { get; } = new();
        public List<(string Name, string Value)> ShaderParams { get; } = new();
        public TRFloatParameter[] FloatParams { get; set; } = Array.Empty<TRFloatParameter>();
        public TRVec2fParameter[] Vec2Params { get; set; } = Array.Empty<TRVec2fParameter>();
        public TRVec3fParameter[] Vec3Params { get; set; } = Array.Empty<TRVec3fParameter>();
        public TRVec4fParameter[] Vec4Params { get; set; } = Array.Empty<TRVec4fParameter>();
        public TRSampler[] Samplers { get; set; } = Array.Empty<TRSampler>();

        /// <summary>
        /// Lightweight texture reference (no GL texture loading).
        /// </summary>
        public class TextureRef
        {
            public string Name { get; set; } = string.Empty;
            public string FilePath { get; set; } = string.Empty;
            public uint Slot { get; set; }
        }

        public TrinityMaterial(PathString modelPath, TRMaterial trmat)
        {
            Name = trmat.Name;
            ShaderName = ResolveShaderName(trmat.Shader?.Length > 0 ? trmat.Shader[0].Name : string.Empty);

            FloatParams = trmat.FloatParams ?? Array.Empty<TRFloatParameter>();
            Vec2Params = trmat.Vec2fParams ?? Array.Empty<TRVec2fParameter>();
            Vec3Params = trmat.Vec3fParams ?? Array.Empty<TRVec3fParameter>();
            Vec4Params = trmat.Vec4fParams ?? Array.Empty<TRVec4fParameter>();
            Samplers = trmat.Samplers ?? Array.Empty<TRSampler>();

            if (trmat.Shader != null && trmat.Shader.Length > 0 && trmat.Shader[0].Values != null)
            {
                foreach (var param in trmat.Shader[0].Values)
                    ShaderParams.Add((param.Name, param.Value));
            }

            foreach (var tex in trmat.Textures ?? Array.Empty<TRTexture>())
            {
                Textures.Add(new TextureRef
                {
                    Name = tex.Name,
                    FilePath = modelPath.Combine(tex.File),
                    Slot = tex.Slot
                });
            }
        }

        private static string ResolveShaderName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Standard";

            return name switch
            {
                "Opaque" => "Standard",
                "Transparent" => "Transparent",
                "Hair" => "Hair",
                "SSS" => "SSS",
                "EyeClearCoat" => "EyeClearCoat",
                "Unlit" => "Unlit",
                _ => name
            };
        }
    }
}
