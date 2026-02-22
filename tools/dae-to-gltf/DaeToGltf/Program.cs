using System.Diagnostics;
using BCnEncoder.Encoder;
using BCnEncoder.ImageSharp;
using K4os.Compression.LZ4;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

var inputArg = args.FirstOrDefault(a => a.StartsWith("--input="))?.Substring(8);
var outputArg = args.FirstOrDefault(a => a.StartsWith("--output="))?.Substring(9);
var compressTextures = !args.Contains("--no-texture-compress");

if (inputArg is null || outputArg is null)
{
    Console.WriteLine("Usage: DaeToGltf --input=<path> --output=<path> [--no-texture-compress]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --input              Input directory containing DAE files and textures");
    Console.WriteLine("  --output             Output directory for converted assets");
    Console.WriteLine("  --no-texture-compress  Skip DDS compression, just copy PNGs");
    return 1;
}

var inputDir = Path.GetFullPath(inputArg);
var outputDir = Path.GetFullPath(outputArg);

if (!Directory.Exists(inputDir))
{
    Console.WriteLine($"Error: Input directory does not exist: {inputDir}");
    return 1;
}

Console.WriteLine($"Input:  {inputDir}");
Console.WriteLine($"Output: {outputDir}");
Console.WriteLine($"Compress textures: {compressTextures}");
Console.WriteLine();

var sw = Stopwatch.StartNew();
var stats = new ConversionStats();

await ProcessDirectoryAsync(inputDir, outputDir, compressTextures, stats);

sw.Stop();

Console.WriteLine();
Console.WriteLine("=== Conversion Summary ===");
Console.WriteLine($"DAE files compressed: {stats.DaeConverted}");
Console.WriteLine($"DAE files failed:     {stats.DaeFailed}");
Console.WriteLine($"Textures processed:   {stats.TexturesProcessed}");
Console.WriteLine($"Total original size:  {FormatBytes(stats.OriginalBytes)}");
Console.WriteLine($"Total output size:    {FormatBytes(stats.OutputBytes)}");
Console.WriteLine($"Compression ratio:    {(double)stats.OutputBytes / stats.OriginalBytes:P1}");
Console.WriteLine($"Time elapsed:         {sw.ElapsedMilliseconds}ms");

return 0;

static async Task ProcessDirectoryAsync(string inputDir, string outputDir, bool compressTextures, ConversionStats stats)
{
    Directory.CreateDirectory(outputDir);

    var daeFiles = Directory.GetFiles(inputDir, "*.dae", SearchOption.AllDirectories);
    var textureFiles = Directory.GetFiles(inputDir, "*.png", SearchOption.AllDirectories)
        .Concat(Directory.GetFiles(inputDir, "*.jpg", SearchOption.AllDirectories))
        .ToArray();

    Console.WriteLine($"Found {daeFiles.Length} DAE files and {textureFiles.Length} texture files");

    foreach (var daeFile in daeFiles)
    {
        var relativePath = Path.GetRelativePath(inputDir, daeFile);
        var outputFileName = relativePath + ".lz4";
        var outputPath = Path.Combine(outputDir, outputFileName);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        stats.OriginalBytes += new FileInfo(daeFile).Length;

        if (await CompressDaeAsync(daeFile, outputPath))
        {
            stats.DaeConverted++;
            stats.OutputBytes += new FileInfo(outputPath).Length;
            Console.WriteLine($"[DAE] {relativePath} -> {outputFileName}");
        }
        else
        {
            stats.DaeFailed++;
            Console.WriteLine($"[FAIL] {relativePath}");
        }
    }

    foreach (var textureFile in textureFiles)
    {
        var relativePath = Path.GetRelativePath(inputDir, textureFile);
        stats.OriginalBytes += new FileInfo(textureFile).Length;

        if (compressTextures)
        {
            var outputFileName = Path.ChangeExtension(relativePath, ".dds.lz4");
            var outputPath = Path.Combine(outputDir, outputFileName);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            if (await ConvertTextureToCompressedDdsAsync(textureFile, outputPath))
            {
                stats.TexturesProcessed++;
                stats.OutputBytes += new FileInfo(outputPath).Length;
                Console.WriteLine($"[TEX] {relativePath} -> {outputFileName}");
            }
            else
            {
                Console.WriteLine($"[FAIL] {relativePath}");
            }
        }
        else
        {
            var outputPath = Path.Combine(outputDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.Copy(textureFile, outputPath, true);
            stats.TexturesProcessed++;
            stats.OutputBytes += new FileInfo(outputPath).Length;
            Console.WriteLine($"[COPY] {relativePath}");
        }
    }
}

static async Task<bool> CompressDaeAsync(string daePath, string outputPath)
{
    try
    {
        var daeBytes = await File.ReadAllBytesAsync(daePath);
        var compressedBytes = LZ4Pickler.Pickle(daeBytes, LZ4Level.L10_OPT);
        await File.WriteAllBytesAsync(outputPath, compressedBytes);
        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Error: {ex.Message}");
        return false;
    }
}

static async Task<bool> ConvertTextureToCompressedDdsAsync(string texturePath, string outputPath)
{
    try
    {
        using var image = await Image.LoadAsync<Rgba32>(texturePath);
        
        var encoder = new BcEncoder();
        encoder.OutputOptions.GenerateMipMaps = true;
        encoder.OutputOptions.MaxMipMapLevel = 4;
        encoder.OutputOptions.Format = BCnEncoder.Shared.CompressionFormat.Bc7;
        encoder.OutputOptions.Quality = CompressionQuality.Fast;
        encoder.OutputOptions.FileFormat = BCnEncoder.Shared.OutputFileFormat.Dds;
        
        byte[] ddsBytes;
        using (var ddsStream = new MemoryStream())
        {
            encoder.EncodeToStream(image, ddsStream);
            ddsBytes = ddsStream.ToArray();
        }
        
        var compressedBytes = LZ4Pickler.Pickle(ddsBytes, LZ4Level.L10_OPT);
        await File.WriteAllBytesAsync(outputPath, compressedBytes);
        
        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Error: {ex.Message}");
        return false;
    }
}

static string FormatBytes(long bytes)
{
    string[] sizes = ["B", "KB", "MB", "GB"];
    double len = bytes;
    int order = 0;
    while (len >= 1024 && order < sizes.Length - 1)
    {
        order++;
        len /= 1024;
    }
    return $"{len:0.##} {sizes[order]}";
}

internal class ConversionStats
{
    public int DaeConverted;
    public int DaeFailed;
    public int TexturesProcessed;
    public long OriginalBytes;
    public long OutputBytes;
}
