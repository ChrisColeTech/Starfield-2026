# Quick progress check
$logFile = "D:\Projects\Starfield-2026\src\Starfield2026.Tests\pokemon-lza-dump\scan_extract\_scan_log.txt"
$outDir = "D:\Projects\Starfield-2026\src\Starfield2026.Tests\pokemon-lza-dump\scan_extract"

if (Test-Path $logFile) {
    Write-Host "=== Log tail ==="
    Get-Content $logFile -Tail 5

    $errors = (Get-Content $logFile | Where-Object { $_ -match "ERR" }).Count
    Write-Host "`nErrors: $errors"
} else {
    Write-Host "Log not found yet"
}

if (Test-Path $outDir) {
    $dirs = (Get-ChildItem $outDir -Directory).Count
    $empty = (Get-ChildItem $outDir -Directory | Where-Object { (Get-ChildItem $_.FullName -File).Count -eq 0 }).Count
    Write-Host "Dirs: $dirs (empty: $empty)"
}

# Show a trainer example
$sample = Get-ChildItem $outDir -Directory | Where-Object { $_.Name -match "rival_f" } | Select-Object -First 1
if ($sample) {
    Write-Host "`nRival F files:"
    Get-ChildItem $sample.FullName | ForEach-Object { Write-Host "  $($_.Name) ($([math]::Round($_.Length/1KB,1)) KB)" }
}
