# List all unique chara/ subdirectory categories in the model list
$modelList = "D:\Projects\Starfield-2026\src\Starfield2026.Tests\scarlet-dump\model_list.txt"
$outFile   = "D:\Projects\Starfield-2026\scripts\chara_categories.txt"

Write-Host "Searching for all chara/ categories..."

# Get all unique category folders under chara/
$categories = Select-String -Pattern "^\s+chara/" -Path $modelList |
    ForEach-Object { 
        $line = $_.Line.Trim()
        # Extract: chara/CATEGORY/FOLDER/
        $parts = $line.Split('/')
        if ($parts.Count -ge 3) { "$($parts[0])/$($parts[1])" }
    } |
    Sort-Object -Unique

Write-Host "=== Chara categories ==="
foreach ($cat in $categories) {
    $count = (Select-String -Pattern "^\s+$([regex]::Escape($cat))/" -Path $modelList).Count
    Write-Host "  $cat  ($count models)"
}
Write-Host ""

# Now show ALL model_uq entries (these should be gym leaders/unique characters)
Write-Host "=== model_uq entries (gym leaders, elite four, etc) ==="
Select-String -Pattern "model_uq/" -Path $modelList | ForEach-Object { Write-Host $_.Line.Trim() }

Write-Host ""
Write-Host "=== Looking for 'gym|elite|champion|leader|battle' in any chara path ==="
Select-String -Pattern "gym|elite|champion|leader|battle" -Path $modelList |
    Where-Object { $_.Line -match "chara/" } |
    ForEach-Object { Write-Host $_.Line.Trim() }
