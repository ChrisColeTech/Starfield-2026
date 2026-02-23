$ErrorActionPreference = "Stop"

$project = "D:\Projects\Starfield-2026\src\Starfield2026.MiniToolbox\src\MiniToolbox.App\MiniToolbox.App.csproj"
$arcDir = "D:\Projects\Starfield-2026\src\Starfield2026.Tests\plza-dump-patched\arc"
$outputDir = "D:\Projects\Starfield-2026\src\Starfield2026.Tests\plza-dump-patched\extracted"

Write-Host "=== Extracting Pokemon models ==="
Write-Host "Arc: $arcDir"
Write-Host "Output: $outputDir"
Write-Host ""

& dotnet run --project $project -c Release --no-build -- trpak --arc $arcDir --all -o $outputDir --filter "ik_pokemon" -p 4

Write-Host ""
Write-Host "=== Extracting character models ==="

& dotnet run --project $project -c Release --no-build -- trpak --arc $arcDir --all -o $outputDir --filter "ik_chara" -p 1 -a split

Write-Host ""
Write-Host "=== Done ==="
