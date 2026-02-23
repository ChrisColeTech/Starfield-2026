$ErrorActionPreference = "Stop"

$project = "D:\Projects\Starfield-2026\src\Starfield2026.MiniToolbox\src\MiniToolbox.App\MiniToolbox.App.csproj"
$arcDir = "D:\Projects\Starfield-2026\src\Starfield2026.Tests\plza-dump-patched\arc"

Write-Host "=== Listing models from archive ==="
Write-Host "Arc: $arcDir"
Write-Host ""

& dotnet run --project $project -c Release --no-build -- trpak --arc $arcDir --list
