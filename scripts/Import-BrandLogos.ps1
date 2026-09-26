param([switch]$Force)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $projectRoot 'Data/brand-logo-sources.json'
$outputDir = Join-Path $projectRoot 'wwwroot/images/brands'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem

foreach ($brand in (Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json)) {
    $destination = Join-Path $outputDir $brand.file
    if ((Test-Path -LiteralPath $destination) -and -not $Force) { continue }

    $temporary = Join-Path $env:TEMP ("itshop-brand-" + [guid]::NewGuid().ToString('N'))
    try {
        Invoke-WebRequest -Uri $brand.url -OutFile $temporary
        if ($brand.archiveEntry) {
            $archive = [System.IO.Compression.ZipFile]::OpenRead($temporary)
            try {
                $entry = $archive.GetEntry($brand.archiveEntry)
                if ($null -eq $entry) { throw "Archive entry missing: $($brand.archiveEntry)" }
                $source = $entry.Open()
                $target = [System.IO.File]::Create($destination)
                try { $source.CopyTo($target) }
                finally { $target.Dispose(); $source.Dispose() }
            }
            finally { $archive.Dispose() }
        }
        else {
            Copy-Item -LiteralPath $temporary -Destination $destination -Force
        }

        # The Lian Li header asset is white; use its one-colour dark variant on white cards.
        if ($brand.name -eq 'Lian Li') {
            $darkSvg = (Get-Content -LiteralPath $destination -Raw) -replace '(?i)#FFFFFF', '#111111'
            [System.IO.File]::WriteAllText($destination, $darkSvg, [System.Text.Encoding]::UTF8)
        }
        if ($brand.name -eq 'Seasonic') {
            $croppedSvg = (Get-Content -LiteralPath $destination -Raw) -replace 'viewBox="0 0 276 110"', 'viewBox="45 28 185 48"'
            [System.IO.File]::WriteAllText($destination, $croppedSvg, [System.Text.Encoding]::UTF8)
        }

        $bytes = [System.IO.File]::ReadAllBytes($destination)
        $extension = [System.IO.Path]::GetExtension($destination).ToLowerInvariant()
        if ($bytes.Length -lt 100) { throw "Logo file is too small: $($brand.name)" }
        if ($extension -eq '.svg') {
            $svg = [System.Text.Encoding]::UTF8.GetString($bytes)
            if ($svg -notmatch '<svg\b' -or $svg -match '(?i)<script\b|<foreignObject\b|\bon\w+\s*=|\b(?:href|src)\s*=\s*["'']\s*(?:https?:|data:|//)') {
                throw "Unsafe or invalid SVG: $($brand.name)"
            }
        }
        elseif ($extension -eq '.png') {
            if ($bytes[0] -ne 137 -or $bytes[1] -ne 80 -or $bytes[2] -ne 78 -or $bytes[3] -ne 71) { throw "Invalid PNG: $($brand.name)" }
        }
        elseif ($extension -eq '.jpg') {
            if ($bytes[0] -ne 255 -or $bytes[1] -ne 216) { throw "Invalid JPEG: $($brand.name)" }
        }
        else { throw "Unsupported logo format: $extension" }

        Write-Host "Imported $($brand.name): $($brand.file)"
    }
    catch {
        Remove-Item -LiteralPath $destination -ErrorAction SilentlyContinue
        throw
    }
    finally {
        Remove-Item -LiteralPath $temporary -ErrorAction SilentlyContinue
    }
}
