# Extract ONLY model_uq (gym leaders, elite four, rival, champion)
# These have full animation sets in chara/motion_uq/

$base = "D:\Projects\Starfield-2026\src\Starfield2026.Tests\scarlet-dump\exported-baked"
$arc  = "D:\Projects\Starfield-2026\src\Starfield2026.Tests\scarlet-dump\arc"
$proj = "D:\Projects\Starfield-2026\src\Starfield2026.MiniToolbox\src\MiniToolbox.App\MiniToolbox.App.csproj"

# Clear output folder
if (Test-Path $base) { Remove-Item $base -Recurse -Force }
New-Item -ItemType Directory -Path $base -Force | Out-Null

$models = @(
  "chara/model_uq/tr0000_primitive/tr0000_00.trmdl",
  "chara/model_uq/tr0020_future/tr0020_00.trmdl",
  "chara/model_uq/tr0040_friend/tr0040_00.trmdl",
  "chara/model_uq/tr0050_junior/tr0050_00.trmdl",
  "chara/model_uq/tr0070_senior/tr0070_00.trmdl",
  "chara/model_uq/tr0080_principal/tr0080_00.trmdl",
  "chara/model_uq/tr0081_principal/tr0081_00.trmdl",
  "chara/model_uq/tr0090_tbattle/tr0090_00.trmdl",
  "chara/model_uq/tr0100_tmath/tr0100_00.trmdl",
  "chara/model_uq/tr0110_thistory/tr0110_00.trmdl",
  "chara/model_uq/tr0120_tnursing/tr0120_00.trmdl",
  "chara/model_uq/tr0130_thome/tr0130_00.trmdl",
  "chara/model_uq/tr0140_tecology/tr0140_00.trmdl",
  "chara/model_uq/tr0150_tlanguage/tr0150_00.trmdl",
  "chara/model_uq/tr0160_dragon/tr0160_00.trmdl",
  "chara/model_uq/tr0170_ground/tr0170_00.trmdl",
  "chara/model_uq/tr0180_steel/tr0180_00.trmdl",
  "chara/model_uq/tr0190_league/tr0190_00.trmdl",
  "chara/model_uq/tr0200_normal/tr0200_00.trmdl",
  "chara/model_uq/tr0210_insect/tr0210_00.trmdl",
  "chara/model_uq/tr0220_water/tr0220_00.trmdl",
  "chara/model_uq/tr0230_grass/tr0230_00.trmdl",
  "chara/model_uq/tr0240_electricity/tr0240_00.trmdl",
  "chara/model_uq/tr0241_electricity/tr0241_00.trmdl",
  "chara/model_uq/tr0250_ice/tr0250_00.trmdl",
  "chara/model_uq/tr0260_esper/tr0260_00.trmdl",
  "chara/model_uq/tr0270_ghost/tr0270_00.trmdl",
  "chara/model_uq/tr0280_fire/tr0280_00.trmdl",
  "chara/model_uq/tr0290_battle/tr0290_00.trmdl",
  "chara/model_uq/tr0300_poison/tr0300_00.trmdl",
  "chara/model_uq/tr0310_mr/tr0310_00.trmdl",
  "chara/model_uq/tr0320_eviil/tr0320_00.trmdl",
  "chara/model_uq/tr9999_heroine/tr9999_00_00.trmdl"
)

$i = 0
$ok = 0
$fail = 0

foreach ($m in $models) {
  $i++
  $name = [System.IO.Path]::GetFileNameWithoutExtension($m)
  $outDir = Join-Path $base $name
  Write-Host "[$i/$($models.Count)] $name..." -NoNewline
  $result = dotnet run --project $proj -c Release --no-build -- trpak --arc $arc --model $m --baked -o $outDir 2>&1
  
  $manifest = Join-Path $outDir "manifest.json"
  $clips = 0
  if (Test-Path $manifest) {
    $json = Get-Content $manifest -Raw | ConvertFrom-Json
    $clips = ($json.clips | Measure-Object).Count
  }
  
  if ($LASTEXITCODE -eq 0) {
    Write-Host " OK ($clips clips)"
    $ok++
  } else {
    Write-Host " FAILED"
    $fail++
  }
}

Write-Host ""
Write-Host "=== Done ==="
Write-Host "Succeeded: $ok"
Write-Host "Failed:    $fail"
Write-Host "Total:     $i"
