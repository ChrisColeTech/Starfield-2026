# Dump first 20 actual FD hashes from our archive and check against community file
$exe = "dotnet"
# We'll write a small diagnostic that prints FD hashes
# Actually, let's just check the FD file hash count and compare
# The community _fd file has 238K lines, our archive has 178K file hashes

# Check if ANY of the first 10 community hashes exist in our FD
$communityFile = "D:\Projects\Starfield-2026\src\Starfield2026.Tests\pokemon-lza-dump\hashes_inside_fd_community.txt"

Write-Host "=== Community FD file stats ==="
$lines = Get-Content $communityFile
$hashLines = $lines | Where-Object { $_ -match '^0x' }
Write-Host "Total lines: $($lines.Count)"
Write-Host "Hash lines: $($hashLines.Count)"

# Check unique paths
$paths = $hashLines | ForEach-Object { ($_ -split '\s+',2)[1] }
$extensions = $paths | ForEach-Object { [System.IO.Path]::GetExtension($_) } | Group-Object | Sort-Object -Property Count -Descending | Select-Object -First 15
Write-Host "`n=== Extension breakdown ==="
foreach ($e in $extensions) {
    Write-Host "  $($e.Name): $($e.Count)"
}

# Count .trmdl entries
$trmdlEntries = $hashLines | Where-Object { $_ -match '\.trmdl$' }
Write-Host "`n=== TRMDL count: $($trmdlEntries.Count) ==="
$trmdlEntries | Select-Object -First 10 | ForEach-Object { Write-Host "  $_" }

Write-Host "`n=== TRPAK file format ==="
$trpakFile = "D:\Projects\Starfield-2026\src\Starfield2026.Tests\pokemon-lza-dump\hashes_inside_trpak_community.txt"
Get-Content $trpakFile -Head 20
