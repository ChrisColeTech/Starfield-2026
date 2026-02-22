# Build SwitchToolboxCli with full error output
$proj = "D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\src\PokemonGreen.SwitchToolboxCli.App\PokemonGreen.SwitchToolboxCli.App.csproj"

Write-Host "Building SwitchToolboxCli..."
$output = dotnet build $proj -c Release 2>&1
$output | ForEach-Object { Write-Host $_ }
Write-Host ""
Write-Host "Exit code: $LASTEXITCODE"
