using System.CommandLine;
using System.CommandLine.Invocation;
using System.Runtime.InteropServices;
using Assimp;

var fileArgument = new Argument<FileInfo>("file", "Input file (FBX or HKX).");
var outputOption = new Option<DirectoryInfo?>(new[] { "-o", "--output" }, "Output directory.");
var skeletonOption = new Option<FileInfo?>("--skeleton", "Skeleton HKX file for animation conversion.");
var fpsOption = new Option<float>("--fps", () => 30f, "Animation FPS.");
var verboseOption = new Option<bool>(new[] { "-v", "--verbose" }, () => false, "Verbose output.");

var rootCommand = new RootCommand("FBX/HKX to DAE converter.");
rootCommand.AddArgument(fileArgument);
rootCommand.AddOption(outputOption);
rootCommand.AddOption(skeletonOption);
rootCommand.AddOption(fpsOption);
rootCommand.AddOption(verboseOption);

rootCommand.SetHandler((InvocationContext ctx) =>
{
    FileInfo file = ctx.ParseResult.GetValueForArgument(fileArgument);
    DirectoryInfo? output = ctx.ParseResult.GetValueForOption(outputOption);
    FileInfo? skeleton = ctx.ParseResult.GetValueForOption(skeletonOption);
    float fps = ctx.ParseResult.GetValueForOption(fpsOption);
    bool verbose = ctx.ParseResult.GetValueForOption(verboseOption);
    
    ctx.ExitCode = Run(file, output, skeleton, fps, verbose);
});

return await rootCommand.InvokeAsync(args);

static int Run(FileInfo file, DirectoryInfo? outputDir, FileInfo? skeletonFile, float fps, bool verbose)
{
    if (!file.Exists)
    {
        Console.Error.WriteLine($"error: file not found: {file.FullName}");
        return 1;
    }

    string outputRoot = outputDir?.FullName ?? file.Directory!.FullName;
    Directory.CreateDirectory(outputRoot);

    string ext = file.Extension.ToLower();
    
    if (ext == ".hkx")
    {
        return ConvertHkx(file, outputRoot, skeletonFile, fps, verbose);
    }
    else if (ext == ".fbx")
    {
        return ConvertFbx(file, outputRoot, fps, verbose);
    }
    else
    {
        Console.Error.WriteLine($"error: unsupported format: {ext}");
        return 1;
    }
}

static int ConvertHkx(FileInfo file, string outputRoot, FileInfo? skeletonFile, float fps, bool verbose)
{
    if (HavokDll.Initialize() != 0)
    {
        Console.Error.WriteLine($"error: failed to initialize Havok: {HavokDll.GetLastError()}");
        return 1;
    }

    try
    {
        string baseName = Path.GetFileNameWithoutExtension(file.Name);
        if (baseName.EndsWith(".anm"))
            baseName = baseName[..^4];

        bool isSkeleton = file.Name.Contains(".skl.");
        
        if (isSkeleton)
        {
            string outputPath = Path.Combine(outputRoot, $"{baseName}.dae");
            int result = HavokDll.SkeletonToDae(file.FullName, outputPath);
            
            if (result != 0)
            {
                Console.Error.WriteLine($"error: {HavokDll.GetLastError()}");
                return 1;
            }
            
            Console.WriteLine($"exported: {outputPath}");
            return 0;
        }
        else
        {
            if (skeletonFile == null || !skeletonFile.Exists)
            {
                Console.Error.WriteLine("error: animation HKX requires --skeleton option");
                return 1;
            }

            string clipsDir = Path.Combine(outputRoot, "clips", baseName);
            Directory.CreateDirectory(clipsDir);
            
            string outputPath = Path.Combine(clipsDir, $"{baseName}.dae");
            int result = HavokDll.AnimationToDae(file.FullName, skeletonFile.FullName, outputPath, fps);
            
            if (result != 0)
            {
                Console.Error.WriteLine($"error: {HavokDll.GetLastError()}");
                return 1;
            }
            
            Console.WriteLine($"exported: {outputPath}");
            return 0;
        }
    }
    finally
    {
        HavokDll.Shutdown();
    }
}

static int ConvertFbx(FileInfo file, string outputRoot, float fps, bool verbose)
{
    using AssimpContext context = new AssimpContext();
    
    if (verbose)
        Console.WriteLine($"Loading FBX: {file.FullName}");

    Scene? scene;
    try
    {
        scene = context.ImportFile(file.FullName, 
            PostProcessSteps.Triangulate | 
            PostProcessSteps.JoinIdenticalVertices |
            PostProcessSteps.FlipUVs);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"error: failed to load FBX: {ex.Message}");
        return 1;
    }

    if (scene == null || !scene.HasMeshes)
    {
        Console.Error.WriteLine("error: scene is empty or has no meshes");
        return 1;
    }

    if (verbose)
        Console.WriteLine($"Scene: {scene.MeshCount} meshes, {scene.AnimationCount} animations");

    string baseName = Path.GetFileNameWithoutExtension(file.Name);
    string outputPath = Path.Combine(outputRoot, $"{baseName}.dae");
    
    if (!context.ExportFile(scene, outputPath, "collada"))
    {
        Console.Error.WriteLine("error: failed to export DAE");
        return 1;
    }
    
    Console.WriteLine($"exported: {outputPath}");
    return 0;
}

internal static class HavokDll
{
    private const string DllName = "HavokAnimationExporterDll.dll";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int hae_initialize();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void hae_shutdown();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr hae_get_last_error();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int hae_skeleton_to_dae([MarshalAs(UnmanagedType.LPStr)] string hkxPath, [MarshalAs(UnmanagedType.LPStr)] string outputPath);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int hae_animation_to_dae([MarshalAs(UnmanagedType.LPStr)] string hkxPath, [MarshalAs(UnmanagedType.LPStr)] string skeletonPath, [MarshalAs(UnmanagedType.LPStr)] string outputPath, float fps);

    public static int Initialize() => hae_initialize();
    public static void Shutdown() => hae_shutdown();
    public static string GetLastError() => Marshal.PtrToStringAnsi(hae_get_last_error()) ?? "unknown error";
    public static int SkeletonToDae(string hkxPath, string outputPath) => hae_skeleton_to_dae(hkxPath, outputPath);
    public static int AnimationToDae(string hkxPath, string skeletonPath, string outputPath, float fps) => hae_animation_to_dae(hkxPath, skeletonPath, outputPath, fps);
}
