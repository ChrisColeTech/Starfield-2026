# Check DLL timestamp and run with dotnet run instead
$dll = "D:\Projects\Starfield-2026\src\Starfield2026.MiniToolbox\src\MiniToolbox.App\bin\Release\net8.0\MiniToolbox.App.dll"
$dllWin = "D:\Projects\Starfield-2026\src\Starfield2026.MiniToolbox\src\MiniToolbox.App\bin\Release\net8.0-windows\MiniToolbox.App.dll"
Write-Host "net8.0 DLL: $(Get-Item $dll -ErrorAction SilentlyContinue | ForEach-Object { $_.LastWriteTime })"
Write-Host "net8.0-windows DLL: $(Get-Item $dllWin -ErrorAction SilentlyContinue | ForEach-Object { $_.LastWriteTime })"
Write-Host "Current time: $(Get-Date)"

# Try with dotnet run which uses the project file
$proj = "D:\Projects\Starfield-2026\src\Starfield2026.MiniToolbox\src\MiniToolbox.App\MiniToolbox.App.csproj"
$arc = "D:\Projects\Starfield-2026\src\Starfield2026.Tests\pokemon-lza-dump\arc"
$out = "D:\Projects\Starfield-2026\src\Starfield2026.Tests\pokemon-lza-dump\scan_extract"

Write-Host "`nRunning with dotnet run..."
& dotnet run --project $proj -c Release -- trpak --scan-extract --arc $arc -o $out 2>&1 | Select-Object -First 20
Write-Host "`nExit: $LASTEXITCODE"
