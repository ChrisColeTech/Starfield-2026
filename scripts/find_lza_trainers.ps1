# Find trainer/character TRPAKs in the LZA extracted bins
$extractedDir = "D:\Projects\Starfield-2026\src\Starfield2026.Tests\pokemon-lza-dump\bins\extracted"

$dirs = Get-ChildItem $extractedDir -Directory
Write-Host "Total extracted TRPAK dirs: $($dirs.Count)"
Write-Host ""

# Show first 20 to understand naming pattern
Write-Host "=== First 20 directory names ==="
$dirs | Select-Object -First 20 | ForEach-Object { Write-Host "  $($_.Name)" }
Write-Host ""

# Find anything with character/trainer patterns
Write-Host "=== Directories matching chara/model/trainer/rival/npc ==="
$charaDirs = $dirs | Where-Object { $_.Name -match "chara|model_tr|model_uq|trainer|rival|npc_|hero" }
Write-Host "Found: $($charaDirs.Count)"
$charaDirs | ForEach-Object { Write-Host "  $($_.Name)" }
Write-Host ""

# Find anything with tr prefix patterns  
Write-Host "=== Directories with tr0-9 pattern ==="
$trDirs = $dirs | Where-Object { $_.Name -match "tr\d{3,4}" }
Write-Host "Found: $($trDirs.Count)"
$trDirs | Select-Object -First 30 | ForEach-Object { Write-Host "  $($_.Name)" }
Write-Host ""

# Find anything with trmdl
Write-Host "=== Directories with trmdl in name ==="
$trmdlDirs = $dirs | Where-Object { $_.Name -match "trmdl" }
Write-Host "Found: $($trmdlDirs.Count)"
$trmdlDirs | Select-Object -First 30 | ForEach-Object { Write-Host "  $($_.Name)" }
Write-Host ""

# Find all unique category prefixes (first word before _)  
Write-Host "=== Top 30 name prefixes ==="
$prefixes = $dirs | ForEach-Object { 
    $parts = $_.Name -split "_"
    if ($parts.Count -ge 2) { "$($parts[0])_$($parts[1])" } else { $parts[0] }
} | Group-Object | Sort-Object Count -Descending | Select-Object -First 30
$prefixes | ForEach-Object { Write-Host "  $($_.Name)  ($($_.Count) dirs)" }
