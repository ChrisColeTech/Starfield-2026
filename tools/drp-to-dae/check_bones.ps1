$daes = Get-ChildItem "D:\Projects\Starfield2026\tools\drp-to-dae\test-output" -Recurse -Filter "*_anim.dae"
$hits = @()
foreach ($dae in $daes) {
    $matches = Select-String -Path $dae.FullName -Pattern "Bone_[0-9A-F]{8}_id"
    if ($matches) { $hits += $matches }
}
if ($hits.Count -gt 0) {
    "Found $($hits.Count) unresolved bone(s):"
    $hits | Select-Object -First 10 | ForEach-Object { "  $($_.Filename): $($_.Line.Trim().Substring(0, [Math]::Min(80, $_.Line.Trim().Length)))" }
} else {
    "No Bone_XXXXXXXX fallbacks found -- all bones resolved!"
}
"Total DAEs checked: $($daes.Count)"
