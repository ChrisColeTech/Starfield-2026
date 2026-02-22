# Check TRPFS header to determine FileSystem section size
$trpfsPath = "D:\Projects\Starfield-2026\src\Starfield2026.Tests\pokemon-lza-dump\arc\data.trpfs"

$f = [System.IO.File]::OpenRead($trpfsPath)
$br = New-Object System.IO.BinaryReader($f)

$magic = $br.ReadUInt64()
$offset = $br.ReadInt64()
$fileSize = $f.Length

$fsSize = $fileSize - $offset

Write-Host "TRPFS file size: $fileSize bytes ($([math]::Round($fileSize / 1GB, 2)) GB)"
Write-Host "FS offset:       $offset bytes ($([math]::Round($offset / 1GB, 2)) GB)"
Write-Host "FS section size: $fsSize bytes ($([math]::Round($fsSize / 1MB, 2)) MB)"
Write-Host "Magic:           0x$($magic.ToString('X16'))"

$br.Close()
$f.Close()
