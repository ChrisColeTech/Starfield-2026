using MiniToolbox.Spica.Formats.CtrH3D;
using MiniToolbox.Spica.Formats.CtrH3D.Animation;
using MiniToolbox.Spica.Formats.CtrH3D.Model;
using MiniToolbox.Spica.Formats.GFL2;
using MiniToolbox.Spica.Formats.GFL2.Model;
using MiniToolbox.Spica.Formats.GFL2.Motion;
using MiniToolbox.Spica.Formats.GFL2.Shader;
using MiniToolbox.Spica.Formats.GFL2.Texture;

using System.IO;

namespace SpicaCli.Formats
{
    public class GFL2OverWorld
    {
        const uint GFModelConstant = 0x15122117;
        const uint GFTextureConstant = 0x15041213;
        const uint GFMotionConstant = 0x00060000;
        const uint GFModelPackConstant = 0x00010000;

        public static H3D OpenAsH3D(Stream Input, GFPackage.Header Header, H3DDict<H3DBone> Skeleton = null)
        {
            BinaryReader Reader = new BinaryReader(Input);

            GFModelPack MdlPack = new GFModelPack();
            GFMotionPack MotPack = new GFMotionPack();

            // Process all entries — each can be a nested GFPackage (character BG),
            // a direct GFModelPack (map BG), a direct GFMotion, or other data.
            for (int i = 0; i < Header.Entries.Length; i++)
            {
                var entry = Header.Entries[i];
                if (entry.Length < 4) continue;

                Reader.BaseStream.Seek(entry.Address, SeekOrigin.Begin);
                uint magic = Reader.ReadUInt32();
                Reader.BaseStream.Seek(entry.Address, SeekOrigin.Begin);

                if (magic == GFModelPackConstant)
                {
                    // Direct GFModelPack — used by map BG entries
                    var pack = new GFModelPack(Reader);
                    foreach (var mdl in pack.Models)
                        MdlPack.Models.Add(mdl);
                    foreach (var tex in pack.Textures)
                        MdlPack.Textures.Add(tex);
                    foreach (var shd in pack.Shaders)
                        MdlPack.Shaders.Add(shd);
                }
                else if (magic == GFMotionConstant)
                {
                    // Direct GFMotion — used by map BG animation entries
                    MotPack.Add(new GFMotion(Reader, MotPack.Count));
                }
                else if (entry.Length >= 0x80)
                {
                    // Try as nested GFPackage (original character BG path)
                    ReadModelsBG(entry, Reader, MdlPack);
                    ReadAnimsBG(entry, Reader, MotPack);
                }
            }

            H3D Output = MdlPack.ToH3D();

            foreach (GFMotion Mot in MotPack)
            {
                H3DMaterialAnim MatAnim = Mot.ToH3DMaterialAnimation();
                H3DAnimation    VisAnim = Mot.ToH3DVisibilityAnimation();

                if (MatAnim != null)
                {
                    MatAnim.Name = $"Motion_{Mot.Index}";
                    Output.MaterialAnimations.Add(MatAnim);
                }

                if (VisAnim != null)
                {
                    VisAnim.Name = $"Motion_{Mot.Index}";
                    Output.VisibilityAnimations.Add(VisAnim);
                }
            }

            return Output;
        }

        private static void ReadModelsBG(GFPackage.Entry File, BinaryReader Reader, GFModelPack MdlPack)
        {
            if (File.Length < 0x80) return;

            Reader.BaseStream.Seek(File.Address, SeekOrigin.Begin);

            // Validate this is actually a nested GFPackage before parsing
            byte m0 = Reader.ReadByte();
            byte m1 = Reader.ReadByte();
            Reader.BaseStream.Seek(File.Address, SeekOrigin.Begin);

            if (m0 < (byte)'A' || m0 > (byte)'Z' || m1 < (byte)'A' || m1 > (byte)'Z')
                return;

            GFPackage.Header Header = GFPackage.GetPackageHeader(Reader.BaseStream);

            foreach (GFPackage.Entry Entry in Header.Entries)
            {
                if (Entry.Length < 4) continue;

                Reader.BaseStream.Seek(Entry.Address, SeekOrigin.Begin);

                uint MagicNum = Reader.ReadUInt32();

                switch (MagicNum)
                {
                    case GFModelConstant:
                        Reader.BaseStream.Seek(-4, SeekOrigin.Current);
                        MdlPack.Models.Add(new GFModel(Reader, $"Model_{MdlPack.Models.Count}"));
                        break;

                    case GFTextureConstant:
                        uint Count = Reader.ReadUInt32();

                        string Signature = string.Empty;

                        for (int i = 0; i < 8; i++)
                        {
                            byte Value = Reader.ReadByte();
                            if (Value < 0x20 || Value > 0x7e) break;
                            Signature += (char)Value;
                        }

                        Reader.BaseStream.Seek(Entry.Address, SeekOrigin.Begin);

                        if (Signature == "texture")
                            MdlPack.Textures.Add(new GFTexture(Reader));
                        else
                            MdlPack.Shaders.Add(new GFShader(Reader));

                        break;
                }
            }
        }

        private static void ReadAnimsBG(GFPackage.Entry File, BinaryReader Reader, GFMotionPack MotPack)
        {
            if (File.Length < 0x80) return;

            Reader.BaseStream.Seek(File.Address, SeekOrigin.Begin);

            // Validate this is actually a nested GFPackage before parsing
            byte m0 = Reader.ReadByte();
            byte m1 = Reader.ReadByte();
            Reader.BaseStream.Seek(File.Address, SeekOrigin.Begin);

            if (m0 < (byte)'A' || m0 > (byte)'Z' || m1 < (byte)'A' || m1 > (byte)'Z')
                return;

            GFPackage.Header Header = GFPackage.GetPackageHeader(Reader.BaseStream);

            foreach (GFPackage.Entry Entry in Header.Entries)
            {
                if (Entry.Length < 4) continue;

                Reader.BaseStream.Seek(Entry.Address, SeekOrigin.Begin);

                uint MagicNum = Reader.ReadUInt32();

                if (MagicNum == GFMotionConstant)
                {
                    Reader.BaseStream.Seek(-4, SeekOrigin.Current);
                    MotPack.Add(new GFMotion(Reader, MotPack.Count));
                }
            }
        }
    }
}
