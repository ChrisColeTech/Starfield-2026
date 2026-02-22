# Convert all Pokemon, trainers, and characters to target dir
$proj = "D:\Projects\Starfield-2026\src\Starfield2026.MiniToolbox\src\MiniToolbox.App\MiniToolbox.App.csproj"
$scanDir = "D:\Projects\Starfield-2026\src\Starfield2026.Tests\pokemon-lza-dump\scan_extract"
$outDir = "D:\Projects\Starfield-2026\src\Starfield2026.Tests\pokemon-lza-dump\extracted"
$logBase = "D:\Projects\Starfield-2026\src\Starfield2026.Tests\pokemon-lza-dump"

# Clean target
if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }

# Run 1: Pokemon (ik_pokemondata)
$logFile = "$logBase\extract_pokemon_log.txt"
$errFile = "$logBase\extract_pokemon_err.txt"
Write-Host "=== Batch 1: Pokemon (ik_pokemondata) ==="
$p = Start-Process -FilePath "dotnet" -ArgumentList "run --project `"$proj`" -c Release -- trpak --convert-dir `"$scanDir`" -o `"$outDir`" --filter `"ik_pokemondata`"" -NoNewWindow -RedirectStandardOutput $logFile -RedirectStandardError $errFile -PassThru
$p.WaitForExit(1200000)  # 20 min
Write-Host "Exit: $($p.ExitCode)"
$tail = Get-Content $logFile -Tail 5
$tail | ForEach-Object { Write-Host "  $_" }

# Run 2: Characters/Trainers (ik_chara)
$logFile2 = "$logBase\extract_chara_log.txt"
$errFile2 = "$logBase\extract_chara_err.txt"
Write-Host "`n=== Batch 2: Characters/Trainers (ik_chara) ==="
$p2 = Start-Process -FilePath "dotnet" -ArgumentList "run --project `"$proj`" -c Release -- trpak --convert-dir `"$scanDir`" -o `"$outDir`" --filter `"ik_chara`"" -NoNewWindow -RedirectStandardOutput $logFile2 -RedirectStandardError $errFile2 -PassThru
$p2.WaitForExit(1200000)
Write-Host "Exit: $($p2.ExitCode)"
$tail2 = Get-Content $logFile2 -Tail 5
$tail2 | ForEach-Object { Write-Host "  $_" }

# Final counts
Write-Host "`n=== FINAL RESULTS ==="
$allDirs = Get-ChildItem $outDir -Directory -ErrorAction SilentlyContinue
$withDae = $allDirs | Where-Object { Test-Path (Join-Path $_.FullName "model.dae") }
$withTex = $allDirs | Where-Object { (Get-ChildItem (Join-Path $_.FullName "textures") -Filter "*.png" -ErrorAction SilentlyContinue | Measure-Object).Count -gt 0 }

$pokemon = $withDae | Where-Object { $_.Name -match "ik_pokemondata" }
$chars = $withDae | Where-Object { $_.Name -match "ik_chara" }

Write-Host "Total DAE models:     $($withDae.Count)"
Write-Host "  Pokemon:            $($pokemon.Count)"
Write-Host "  Characters/Trainers: $($chars.Count)"
Write-Host "With textures:        $($withTex.Count)"

# Disk usage
$diskMB = [math]::Round((Get-ChildItem $outDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB, 0)
Write-Host "Disk usage:           $diskMB MB"
