using System;
using System.IO;
using DrpToDae.Formats.NUT;

using System.Collections.Generic;

namespace DrpToDae.IO
{
    public static class NutExporter
    {
        public static int ExportTextures(string inputPath, string outputFolder)
        {
            return ExportTextures(inputPath, outputFolder, out _);
        }

        public static int ExportTextures(string inputPath, string outputFolder, out List<string> exportedFiles)
        {
            exportedFiles = new List<string>();

            if (!File.Exists(inputPath))
                throw new FileNotFoundException($"NUT file not found: {inputPath}");

            Directory.CreateDirectory(outputFolder);

            NUTFile nut = new NUTFile();
            nut.Read(inputPath);

            Console.WriteLine($"Found {nut.Textures.Count} textures in {Path.GetFileName(inputPath)}");

            int exported = 0;
            int skipped = 0;

            foreach (NutTexture tex in nut.Textures)
            {
                string fileName = $"Tex_0x{tex.HashId:X8}.png";
                string outputPath = Path.Combine(outputFolder, fileName);

                try
                {
                    if (tex.surfaces.Count == 0 || tex.surfaces[0].mipmaps.Count == 0)
                    {
                        Console.WriteLine($"  Skipping {fileName}: No texture data");
                        skipped++;
                        continue;
                    }

                    byte[] rawData = tex.surfaces[0].mipmaps[0];

                    if (NutTexture.IsBlockCompressed(tex.Format))
                    {
                        byte[] rgbaData = DDSDecoder.Decode(rawData, tex.Width, tex.Height, tex.Format);
                        PngWriter.SaveAsPng(rgbaData, tex.Width, tex.Height, outputPath);
                    }
                    else
                    {
                        byte[] rgbaData = DDSDecoder.Decode(rawData, tex.Width, tex.Height, tex.Format);
                        PngWriter.SaveAsPng(rgbaData, tex.Width, tex.Height, outputPath);
                    }

                    Console.WriteLine($"  Exported: {fileName} ({tex.Width}x{tex.Height}, {tex.Format})");
                    exportedFiles.Add($"textures/{fileName}");
                    exported++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Failed to export {fileName}: {ex.Message}");
                    skipped++;
                }
            }

            Console.WriteLine($"Exported {exported} textures, skipped {skipped}");
            return exported;
        }

        public static void ExportTexture(NutTexture tex, string outputPath)
        {
            if (tex.surfaces.Count == 0 || tex.surfaces[0].mipmaps.Count == 0)
                throw new InvalidOperationException("Texture has no data");

            byte[] rawData = tex.surfaces[0].mipmaps[0];
            byte[] rgbaData = DDSDecoder.Decode(rawData, tex.Width, tex.Height, tex.Format);

            string? directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            PngWriter.SaveAsPng(rgbaData, tex.Width, tex.Height, outputPath);
        }
    }
}
