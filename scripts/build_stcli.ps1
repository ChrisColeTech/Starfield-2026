# Build SwitchToolboxCli with full error output
$proj = "D:\Projects\Starfield2026\src\Starfield2026.SwitchToolboxCli\src\Starfield2026.SwitchToolboxCli.App\Starfield2026.SwitchToolboxCli.App.csproj"

Write-Host "Building SwitchToolboxCli..."
$output = dotnet build $proj -c Release 2>&1
$output | ForEach-Object { Write-Host $_ }
Write-Host ""
Write-Host "Exit code: $LASTEXITCODE"
