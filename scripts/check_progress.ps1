# Diagnose why Pokemon packs fail - check file contents
$scanDir = "D:\Projects\Starfield-2026\src\Starfield2026.Tests\pokemon-lza-dump\scan_extract"

# Get all pokemon packs
$pmPacks = Get-ChildItem $scanDir -Directory | Where-Object { $_.Name -match "ik_pokemondata" }
Write-Host "Total ik_pokemondata packs: $($pmPacks.Count)"

# Separate into those with .trmsh and those without
$withMsh = $pmPacks | Where-Object { (Get-ChildItem $_.FullName -Filter "*.trmsh").Count -gt 0 }
$noMsh = $pmPacks | Where-Object { (Get-ChildItem $_.FullName -Filter "*.trmsh").Count -eq 0 }

Write-Host "  With .trmsh: $($withMsh.Count)"
Write-Host "  Without .trmsh: $($noMsh.Count)"

# Check what files the no-trmsh packs have
Write-Host "`n=== Sample packs WITHOUT .trmsh ==="
$noMsh | Select-Object -First 10 | ForEach-Object {
    $files = Get-ChildItem $_.FullName -File
    $exts = ($files | ForEach-Object { $_.Extension } | Sort-Object -Unique) -join ","
    $totalKB = [math]::Round(($files | Measure-Object Length -Sum).Sum / 1KB, 0)
    Write-Host "  $($_.Name.Substring(0, [Math]::Min(60, $_.Name.Length))) | $($files.Count) files | $totalKB KB | $exts"
}

# Check sample packs WITH .trmsh that still fail
Write-Host "`n=== Sample packs WITH .trmsh ==="
$withMsh | Select-Object -First 10 | ForEach-Object {
    $files = Get-ChildItem $_.FullName -File
    $exts = ($files | ForEach-Object { $_.Extension } | Sort-Object -Unique) -join ","
    $trmshCount = (Get-ChildItem $_.FullName -Filter "*.trmsh").Count
    $trmbfCount = (Get-ChildItem $_.FullName -Filter "*.trmbf").Count
    $trmdlCount = (Get-ChildItem $_.FullName -Filter "*.trmdl").Count
    $totalKB = [math]::Round(($files | Measure-Object Length -Sum).Sum / 1KB, 0)
    Write-Host "  $($_.Name.Substring(0, [Math]::Min(55, $_.Name.Length))) | trmdl:$trmdlCount trmsh:$trmshCount trmbf:$trmbfCount | $totalKB KB"
}

# How many have trmdl+trmsh+trmbf (convertible)?
$convertible = $pmPacks | Where-Object {
    $d = $_.FullName
    (Get-ChildItem $d -Filter "*.trmdl").Count -gt 0 -and
    (Get-ChildItem $d -Filter "*.trmsh").Count -gt 0 -and
    (Get-ChildItem $d -Filter "*.trmbf").Count -gt 0
}
Write-Host "`nConvertible (trmdl+trmsh+trmbf): $($convertible.Count)"

# Packs with trmdl but no trmsh — these are the missed ones
$mdlNoMsh = $pmPacks | Where-Object {
    $d = $_.FullName
    (Get-ChildItem $d -Filter "*.trmdl").Count -gt 0 -and
    (Get-ChildItem $d -Filter "*.trmsh").Count -eq 0
}
Write-Host "Has .trmdl but no .trmsh: $($mdlNoMsh.Count)"

# Check if these might have unnamed mesh files (no extension or .bin)
Write-Host "`n=== Packs with .trmdl but no .trmsh (first 5) ==="
$mdlNoMsh | Select-Object -First 5 | ForEach-Object {
    $files = Get-ChildItem $_.FullName -File
    $exts = ($files | ForEach-Object { $_.Extension } | Group-Object | ForEach-Object { "$($_.Name):$($_.Count)" }) -join ", "
    Write-Host "  $($_.Name.Substring(0, [Math]::Min(55, $_.Name.Length)))"
    Write-Host "    $exts"
    # Show first few bytes of .bin files
    Get-ChildItem $_.FullName -Filter "*.bin" | Select-Object -First 2 | ForEach-Object {
        $bytes = [System.IO.File]::ReadAllBytes($_.FullName)
        $hex = ($bytes | Select-Object -First 8 | ForEach-Object { '{0:X2}' -f $_ }) -join ' '
        Write-Host "    $($_.Name) ($($_.Length) bytes): $hex"
    }
}
