param(
    [switch]$RetryMissingOnly
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content -LiteralPath (Join-Path $projectRoot 'Data/catalog-image-sources.json') -Raw | ConvertFrom-Json
$imageRoot = Join-Path $projectRoot 'wwwroot/images/products'
New-Item -ItemType Directory -Path $imageRoot -Force | Out-Null

foreach ($item in $manifest) {
    $destination = Join-Path $projectRoot ('wwwroot' + $item.localPath.Replace('/', [IO.Path]::DirectorySeparatorChar))
    if ($RetryMissingOnly -and (Test-Path -LiteralPath $destination)) {
        continue
    }

    $download = Join-Path $env:TEMP ("itshop-image-{0}-{1}" -f $item.productId, [guid]::NewGuid().ToString('N'))
    try {
        Invoke-WebRequest -Uri $item.sourceImage -OutFile $download -TimeoutSec 30 -UserAgent 'Mozilla/5.0'
        $bytes = [IO.File]::ReadAllBytes($download)
        if ($bytes.Length -lt 2048) {
            throw "Image too small ($($bytes.Length) bytes)"
        }

        $extension = [IO.Path]::GetExtension($destination).ToLowerInvariant()
        $valid = switch ($extension) {
            '.jpg' { $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xD8 }
            '.png' { $bytes[0] -eq 0x89 -and $bytes[1] -eq 0x50 -and $bytes[2] -eq 0x4E -and $bytes[3] -eq 0x47 }
            '.webp' { [Text.Encoding]::ASCII.GetString($bytes, 0, 4) -eq 'RIFF' -and [Text.Encoding]::ASCII.GetString($bytes, 8, 4) -eq 'WEBP' }
            default { $false }
        }
        if (-not $valid) {
            throw "Downloaded content is not $extension"
        }

        [IO.File]::WriteAllBytes($destination, $bytes)
        Write-Output "OK $($item.productId) $($item.name) ($($bytes.Length) bytes)"
    }
    catch {
        Write-Output "FAILED $($item.productId) $($item.name): $($_.Exception.Message)"
    }
    finally {
        Remove-Item -LiteralPath $download -ErrorAction SilentlyContinue
    }
}
