// Quick test: BCAReader → AnimationExporter pipeline
// Usage: dotnet run -- <bcl_path> <bca_path> <vbn_path> <output_dae_path>
using DrpToDae.Formats.Animation;
using DrpToDae.Formats.VBN;
using VbnSkeleton = DrpToDae.Formats.VBN.VBN;

if (args.Length < 4)
{
    Console.WriteLine("Usage: BcaExportTest <bcl_path> <bca_path> <vbn_path> <output_dae>");
    return 1;
}

string bclPath = args[0];
string bcaPath = args[1];
string vbnPath = args[2];
string outPath = args[3];

byte[] bclData = File.ReadAllBytes(bclPath);
byte[] bcaData = File.ReadAllBytes(bcaPath);
var vbn = new VbnSkeleton(vbnPath);

Console.WriteLine($"BCL: {bclData.Length} bytes");
Console.WriteLine($"BCA: {bcaData.Length} bytes");
Console.WriteLine($"VBN: {vbn.Bones.Count} bones");

var anim = BCAReader.Read(bcaData, bclData, vbn);

Console.WriteLine($"\nAnimationData:");
Console.WriteLine($"  Name: {anim.Name}");
Console.WriteLine($"  FrameCount: {anim.FrameCount}");
Console.WriteLine($"  Bones: {anim.Bones.Count}");

foreach (var bone in anim.Bones)
{
    Console.Write($"  {bone.Name} (idx={bone.BoneIndex}):");
    if (bone.HasPositionAnimation) Console.Write($" pos({bone.XPos.Keys.Count},{bone.YPos.Keys.Count},{bone.ZPos.Keys.Count})");
    if (bone.HasRotationAnimation) Console.Write($" rot({bone.XRot.Keys.Count},{bone.YRot.Keys.Count},{bone.ZRot.Keys.Count})");
    if (bone.HasScaleAnimation) Console.Write($" scl({bone.XScale.Keys.Count},{bone.YScale.Keys.Count},{bone.ZScale.Keys.Count})");
    Console.WriteLine();
}

Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? ".");
AnimationExporter.ExportToCollada(anim, outPath);
Console.WriteLine($"\nDAE exported: {outPath}");
Console.WriteLine($"  File size: {new FileInfo(outPath).Length} bytes");

return 0;
