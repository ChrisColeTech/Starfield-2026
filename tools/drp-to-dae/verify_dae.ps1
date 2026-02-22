param(
    [string]$OutputDir = "D:\Projects\Starfield2026\tools\drp-to-dae\test-output"
)

$daes = Get-ChildItem $OutputDir -Recurse -Filter "*_anim.dae"
Write-Host "Checking $($daes.Count) animation DAE files..."

# Check 1: Unresolved bone names
$boneHits = @()
foreach ($dae in $daes) {
    $matches = Select-String -Path $dae.FullName -Pattern "Bone_[0-9A-F]{8}_id_transform"
    if ($matches) { $boneHits += $matches }
}

if ($boneHits.Count -gt 0) {
    Write-Host "FAIL: $($boneHits.Count) unresolved Bone_XXXXXXXX entries found:"
    $boneHits | Select-Object -First 5 | ForEach-Object { "  $($_.Filename)" }
} else {
    Write-Host "PASS: No unresolved Bone_XXXXXXXX bone names"
}

# Check 2: Channel targets use bone name (not _id suffix)
$badTargets = @()
foreach ($dae in $daes) {
    $matches = Select-String -Path $dae.FullName -Pattern 'target="[A-Za-z0-9_]+_id/transform"'
    if ($matches) { $badTargets += $matches }
}

if ($badTargets.Count -gt 0) {
    Write-Host "FAIL: $($badTargets.Count) channel targets still use _id suffix:"
    $badTargets | Select-Object -First 3 | ForEach-Object { "  $($_.Filename): $($_.Line.Trim())" }
} else {
    Write-Host "PASS: All channel targets use bone name (no _id suffix)"
}

# Check 3: Sample a target from first anim DAE
$sample = $daes | Select-Object -First 1
if ($sample) {
    $targets = Select-String -Path $sample.FullName -Pattern 'target=' | Select-Object -First 3
    Write-Host "`nSample targets from $($sample.Name):"
    $targets | ForEach-Object { "  $($_.Line.Trim())" }
}

Write-Host "`nDone."
