using MiniToolbox.Spica.Formats.CtrGfx;
using MiniToolbox.Spica.Formats.CtrH3D;
using MiniToolbox.Spica.Formats.CtrH3D.Animation;
using MiniToolbox.Spica.Formats.CtrH3D.Model;
using MiniToolbox.Spica.Formats.Generic.StudioMdl;
using MiniToolbox.Spica.Formats.Generic.WavefrontOBJ;
using MiniToolbox.Spica.Formats.GFL2;
using MiniToolbox.Spica.Formats.GFL2.Model;
using MiniToolbox.Spica.Formats.GFL2.Motion;
using MiniToolbox.Spica.Formats.GFL2.Texture;
using MiniToolbox.Spica.Formats.GFLX;
using MiniToolbox.Spica.Formats.ModelBinary;
using MiniToolbox.Spica.Formats.MTFramework.Model;
using MiniToolbox.Spica.Formats.MTFramework.Shader;
using MiniToolbox.Spica.Formats.MTFramework.Texture;

using System.IO;
using System.Text;

namespace SpicaCli.Formats
{
    public static class FormatIdentifier
    {
        public static H3D IdentifyAndOpen(string FileName, H3DDict<H3DBone> Skeleton = null)
        {
            string FilePath = Path.GetDirectoryName(FileName);

            switch (Path.GetExtension(FileName).ToLower())
            {
                case ".smd": return new SMD(FileName).ToH3D(FilePath);
                case ".obj": return new OBJ(FileName).ToH3D(FilePath);
                case ".mbn":
                    using (FileStream Input = new FileStream(FileName, FileMode.Open))
                    {
                        H3D BaseScene = H3D.Open(File.ReadAllBytes(FileName.Replace(".mbn", ".bch")));

                        MBn ModelBinary = new MBn(new BinaryReader(Input), BaseScene);

                        return ModelBinary.ToH3D();
                    }
            }

            H3D Output = null;

            using (FileStream FS = new FileStream(FileName, FileMode.Open))
            {
                Output = IdentifyAndOpen(FS, Skeleton);
            }

            return Output;
        }

        public static H3D IdentifyAndOpen(Stream FS, H3DDict<H3DBone> Skeleton = null)
        {
            H3D Output = null;

            if (FS.Length > 4)
            {
                long startPos = FS.Position;
                BinaryReader Reader = new BinaryReader(FS);

                uint MagicNum = Reader.ReadUInt32();

                FS.Seek(startPos, SeekOrigin.Begin);

                string Magic = Encoding.ASCII.GetString(Reader.ReadBytes(4));

                FS.Seek(startPos, SeekOrigin.Begin);

                if (Magic.StartsWith("BCH"))
                {
                    return H3D.Open(Reader.ReadBytes((int)(FS.Length - startPos)));
                }
                else if (Magic.StartsWith("TEX"))
                {
                    return new MTTexture(Reader, "Texture").ToH3D();
                }
                else if (Magic.StartsWith("CGFX"))
                {
                    return Gfx.Open(FS);
                }
                else if (Magic.StartsWith("GFLXPAK"))
                {
                    return new GFLXPack(Reader).ToH3D();
                }
                else
                {
                    if (GFPackage.IsValidPackage(FS))
                    {
                        GFPackage.Header PackHeader = GFPackage.GetPackageHeader(FS);

                        switch (PackHeader.Magic)
                        {
                            case "AD": Output = GFPackedTexture.OpenAsH3D(FS, PackHeader, 1); break;
                            case "BG":
                            case "SB": Output = GFL2OverWorld.OpenAsH3D(FS, PackHeader, Skeleton); break;
                            case "BS": Output = GFBtlSklAnim.OpenAsH3D(FS, PackHeader, Skeleton); break;
                            case "CM": Output = GFCharaModel.OpenAsH3D(FS, PackHeader); break;
                            case "GR": Output = GFOWMapModel.OpenAsH3D(FS, PackHeader); break;
                            case "MM": Output = GFOWCharaModel.OpenAsH3D(FS, PackHeader); break;
                            case "PC": Output = GFPkmnModel.OpenAsH3D(FS, PackHeader, Skeleton); break;
                            case "PT": Output = GFPackedTexture.OpenAsH3D(FS, PackHeader, 0); break;
                            case "PK":
                            case "PB":
                                Output = GFPkmnSklAnim.OpenAsH3D(FS, PackHeader, Skeleton); break;
                        }
                    }
                    else
                    {
                        switch (MagicNum)
                        {
                            case 0x15122117:
                                Output = new H3D();
                                Output.Models.Add(new GFModel(Reader, "Model").ToH3DModel());
                                break;

                            case 0x15041213:
                                Output = new H3D();
                                Output.Textures.Add(new GFTexture(Reader).ToH3DTexture());
                                break;

                            case 0x00010000: Output = new GFModelPack(Reader).ToH3D(); break;
                            case 0x00060000:
                                if (Skeleton != null)
                                {
                                    Output = new H3D();

                                    GFMotion Motion = new GFMotion(Reader, 0);

                                    H3DAnimation    SklAnim = Motion.ToH3DSkeletalAnimation(Skeleton);
                                    H3DMaterialAnim MatAnim = Motion.ToH3DMaterialAnimation();
                                    H3DAnimation    VisAnim = Motion.ToH3DVisibilityAnimation();

                                    if (SklAnim != null) Output.SkeletalAnimations.Add(SklAnim);
                                    if (MatAnim != null) Output.MaterialAnimations.Add(MatAnim);
                                    if (VisAnim != null) Output.VisibilityAnimations.Add(VisAnim);
                                }

                                break;
                        }
                    }
                }
            }

            return Output;
        }
    }
}
