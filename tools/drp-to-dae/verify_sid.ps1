$modelDae = "D:\Projects\Starfield2026\tools\drp-to-dae\test-output\a038\model.dae"
$animDae = Get-ChildItem "D:\Projects\Starfield2026\tools\drp-to-dae\test-output\a038\animations" -Filter "*_anim.dae" | Select-Object -First 1

Write-Host "=== Model DAE bone nodes ==="
$modelMatches = Select-String -Path $modelDae -Pattern '<matrix' | Select-Object -First 3
foreach ($m in $modelMatches) { $m.Line.Trim() }

Write-Host ""
Write-Host "=== Animation DAE channel targets ==="
$animMatches = Select-String -Path $animDae.FullName -Pattern 'target=' | Select-Object -First 3
foreach ($m in $animMatches) { $m.Line.Trim() }

Write-Host ""
Write-Host "=== Log summary ==="
Get-Content "D:\Projects\Starfield2026\tools\drp-to-dae\export_log.txt" | Select-String "Complete|ERROR"
