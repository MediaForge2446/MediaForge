param([string]$RepositoryRoot)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}

$sourcePath = Join-Path $RepositoryRoot 'installer/assets/MediaForge.png.b64'
$destPath = Join-Path $RepositoryRoot 'src/MediaForge.App/Assets/MediaForge.png'

if (-not (Test-Path $sourcePath)) {
    throw "Branding source not found: $sourcePath"
}

$directory = Split-Path -Parent $destPath
New-Item -ItemType Directory -Force -Path $directory | Out-Null

$base64 = (Get-Content -Raw -Encoding UTF8 $sourcePath).Trim()
if ([string]::IsNullOrWhiteSpace($base64)) {
    throw "Branding source is empty: $sourcePath"
}

try {
    [IO.File]::WriteAllBytes($destPath, [Convert]::FromBase64String($base64))
} catch {
    throw "Branding source is not valid Base64: $sourcePath. $($_.Exception.Message)"
}

if (-not (Test-Path $destPath)) {
    throw "Generated branding asset is missing: $destPath"
}

Write-Host "Branding asset prepared successfully: $destPath"
