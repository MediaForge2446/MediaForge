param([string]$RepositoryRoot)
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}

$targets = @(
    @{ Source = 'installer/assets/MediaForge.png.b64'; Dest = 'src/MediaForge.App/Assets/MediaForge.png' }
    @{ Source = 'installer/assets/MediaForge.ico.b64'; Dest = 'installer/assets/MediaForge.ico' }
    @{ Source = 'installer/assets/MediaForge.png.b64'; Dest = 'installer/assets/MediaForge.png' }
    @{ Source = 'installer/assets/WizardImage.png.b64'; Dest = 'installer/assets/WizardImage.png' }
)

foreach ($item in $targets) {
    $sourcePath = Join-Path $RepositoryRoot $item.Source
    $destPath = Join-Path $RepositoryRoot $item.Dest

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
        throw "Failed to decode branding source '$sourcePath': $($_.Exception.Message)"
    }
}
