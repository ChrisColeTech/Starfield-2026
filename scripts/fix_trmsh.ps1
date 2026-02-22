# Undo the bad renames and use a smarter approach
$scanDir = "D:\Projects\Starfield-2026\src\Starfield2026.Tests\pokemon-lza-dump\scan_extract"

# First: undo all renames - rename mesh_*.trmsh back to .bin if they were created by fix_trmsh.ps1
# Only undo files matching mesh_XX.trmsh pattern (our renamed files)
$undone = 0
Get-ChildItem $scanDir -Directory | ForEach-Object {
    Get-ChildItem $_.FullName -Filter "mesh_*.trmsh" | ForEach-Object {
        $newName = $_.Name -replace '\.trmsh$', '.bin'
        $newPath = Join-Path $_.Directory.FullName $newName
        if (-not (Test-Path $newPath)) {
            Rename-Item $_.FullName $newPath
            $undone++
        }
    }
}
Write-Host "Undone $undone renames"

# Check convertible count after undo
$convertible = Get-ChildItem $scanDir -Directory | Where-Object {
    $d = $_.FullName
    (Get-ChildItem $d -Filter "*.trmdl").Count -gt 0 -and
    (Get-ChildItem $d -Filter "*.trmsh").Count -gt 0 -and
    (Get-ChildItem $d -Filter "*.trmbf").Count -gt 0
}
Write-Host "Convertible after undo: $($convertible.Count)"
