# Search the hash cache for trainer-related .tranm animation files
$hashFile = "D:\Projects\Starfield-2026\src\Starfield2026.MiniToolbox\lib\hashes_inside_fd.txt"
$outFile  = "D:\Projects\Starfield-2026\scripts\trainer_anims_found.txt"

if (-not (Test-Path $hashFile)) {
    Write-Host "Hash file not found: $hashFile"
    exit 1
}

Write-Host "Searching $hashFile for trainer animation files..."

# Search for .tranm files related to trainers (tr prefix) or chara
$results = Select-String -Pattern "\.tranm" -Path $hashFile | 
    Where-Object { $_.Line -match "chara|trainer|model_tr|model_uq|anime" } |
    ForEach-Object { $_.Line }

$results | Out-File $outFile -Encoding utf8
Write-Host "Found $($results.Count) results -> $outFile"

# Also search for any .tranm with tr0 prefix specifically
Write-Host ""
Write-Host "--- Sample .tranm paths (first 20 with 'tr' in path): ---"
Select-String -Pattern "\.tranm" -Path $hashFile |
    Where-Object { $_.Line -match "/tr\d" } |
    Select-Object -First 20 |
    ForEach-Object { Write-Host $_.Line }

# Also show unique directory prefixes for ALL .tranm files
Write-Host ""
Write-Host "--- Unique directory patterns for .tranm files (first 30): ---"
Select-String -Pattern "\.tranm" -Path $hashFile |
    ForEach-Object { 
        $path = $_.Line.Trim()
        $lastSlash = $path.LastIndexOf('/')
        if ($lastSlash -ge 0) { $path.Substring(0, $lastSlash) } else { "(root)" }
    } |
    Sort-Object -Unique |
    Select-Object -First 30 |
    ForEach-Object { Write-Host $_ }
