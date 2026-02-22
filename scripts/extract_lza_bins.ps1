# Build SwitchToolboxCli and run bulk-extract-bins on LZA archive
$proj = "D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\src\PokemonGreen.SwitchToolboxCli.App\PokemonGreen.SwitchToolboxCli.App.csproj"
$lzaArc = "D:\Projects\Starfield-2026\src\Starfield2026.Tests\pokemon-lza-dump\arc"
$outDir = "D:\Projects\Starfield-2026\src\Starfield2026.Tests\pokemon-lza-dump\bins\extracted"

Write-Host "Building SwitchToolboxCli..."
dotnet build $proj -c Release 2>&1 | Select-Object -Last 3

if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD FAILED"
    exit 1
}

Write-Host ""
Write-Host "Running bulk-extract-bins on LZA archive..."
Write-Host "  Archive: $lzaArc"
Write-Host "  Output:  $outDir"
Write-Host ""

dotnet run --project $proj -c Release --no-build -- bulk-extract-bins $lzaArc -o $outDir --resume true 2>&1

Write-Host ""
Write-Host "=== Done ==="
Write-Host "Exit code: $LASTEXITCODE"
