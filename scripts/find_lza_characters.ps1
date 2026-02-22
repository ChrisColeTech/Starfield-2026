# List all character models in the LZA archive
$arc  = "D:\Projects\Starfield-2026\src\Starfield2026.Tests\pokemon-lza-dump\arc"
$proj = "D:\Projects\Starfield-2026\src\Starfield2026.MiniToolbox\src\MiniToolbox.App\MiniToolbox.App.csproj"
$out  = "D:\Projects\Starfield-2026\scripts\lza_model_list.txt"

Write-Host "Listing models from LZA archive..."
dotnet run --project $proj -c Release --no-build -- trpak --arc $arc --list > $out 2>&1
Write-Host "Model list saved to $out"

Write-Host ""
Write-Host "=== Chara categories ==="
Select-String -Pattern "^\s+chara/" -Path $out |
    ForEach-Object {
        $parts = $_.Line.Trim().Split('/')
        if ($parts.Count -ge 3) { "$($parts[0])/$($parts[1])" }
    } |
    Sort-Object -Unique |
    ForEach-Object {
        $cat = $_
        $count = (Select-String -Pattern "^\s+$([regex]::Escape($cat))/" -Path $out).Count
        Write-Host "  $cat  ($count models)"
    }

Write-Host ""
Write-Host "=== All chara model entries ==="
Select-String -Pattern "^\s+chara/model" -Path $out | ForEach-Object { Write-Host $_.Line.Trim() }
