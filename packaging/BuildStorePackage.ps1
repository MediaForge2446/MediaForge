# Build a Store-ready MSIX package and .msixupload container.
#
# The MSIX is intentionally unsigned in CI. Microsoft signs the package as part
# of the Microsoft Store submission/certification flow.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$PublishDirectory,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$MakeAppxPath,
    [string]$IdentityName = "MediaForge",
    [string]$Publisher = "CN=MediaForge",
    [string]$PublisherDisplayName = "MediaForge"
)

$ErrorActionPreference = "Stop"

function Find-MakeAppx {
    $candidates = @(
        $env:MAKEAPPX_PATH
        (Get-Command MakeAppx.exe -ErrorAction SilentlyContinue).Source
        (Get-ChildItem "$env:ProgramFiles(x86)\Windows Kits\10\bin" -Recurse -Filter "MakeAppx.exe" -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending |
            Select-Object -First 1 -ExpandProperty FullName)
        (Get-ChildItem "$env:ProgramFiles(x86)\Windows Kits\10\App Certification Kit" -Filter "MakeAppx.exe" -ErrorAction SilentlyContinue |
            Select-Object -First 1 -ExpandProperty FullName)
    ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}

if (-not (Test-Path $PublishDirectory)) {
    throw "Publish directory not found: $PublishDirectory"
}

$makeAppx = if ($MakeAppxPath) { $MakeAppxPath } else { Find-MakeAppx }
if (-not $makeAppx -or -not (Test-Path $makeAppx)) {
    throw "Windows SDK MakeAppx.exe was not found or the supplied path is invalid: $MakeAppxPath"
}

$stage = Join-Path $env:RUNNER_TEMP "MediaForgeMsixStage"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null

Copy-Item (Join-Path $PublishDirectory "*") $stage -Recurse -Force

# Trim FFmpeg's development-only folders while preserving the runtime bin folder
# and the root license/notice files required for a distributable build.
$ffmpeg = Join-Path $stage "tools\ffmpeg"
$ffmpegBin = Join-Path $ffmpeg "bin"
if (-not (Test-Path (Join-Path $ffmpegBin "ffmpeg.exe"))) {
    throw "Bundled FFmpeg runtime is missing ffmpeg.exe: $ffmpegBin"
}

Get-ChildItem -Path $ffmpeg -Directory -Force -ErrorAction SilentlyContinue |
    Where-Object { -not $_.Name.Equals("bin", [StringComparison]::OrdinalIgnoreCase) } |
    Remove-Item -Recurse -Force

Get-ChildItem -Path $ffmpegBin -File -Force -ErrorAction SilentlyContinue |
    Where-Object {
        $_.Name -in @("ffplay.exe", "ffprobe.exe") -or
        $_.Extension -notin @(".exe", ".dll")
    } |
    Remove-Item -Force

if (-not (Test-Path (Join-Path $ffmpegBin "ffmpeg.exe"))) {
    throw "FFmpeg runtime was removed unexpectedly while trimming the package."
}

$branding = Join-Path $PublishDirectory "Assets\MediaForge.png"
if (-not (Test-Path $branding)) {
    throw "MediaForge branding asset was not published: $branding"
}

$assets = Join-Path $stage "Assets"
New-Item -ItemType Directory -Force -Path $assets | Out-Null

Copy-Item $branding (Join-Path $assets "Square44x44Logo.png") -Force
Copy-Item $branding (Join-Path $assets "Square150x150Logo.png") -Force
Copy-Item $branding (Join-Path $assets "StoreLogo.png") -Force

$version4 = switch (($Version -split '\.').Count) {
    1 { "$Version.0.0.0" }
    2 { "$Version.0.0" }
    3 { "$Version.0" }
    default { $Version }
}

$manifestTemplate = Join-Path $PSScriptRoot "Package.appxmanifest.template.xml"
$manifestPath = Join-Path $stage "AppxManifest.xml"
$manifest = Get-Content -Raw -Encoding UTF8 $manifestTemplate
$manifest = $manifest.Replace("__IDENTITY_NAME__", $IdentityName)
$manifest = $manifest.Replace("__PUBLISHER__", $Publisher)
$manifest = $manifest.Replace("__PUBLISHER_DISPLAY_NAME__", $PublisherDisplayName)
$manifest = $manifest.Replace("__VERSION__", $version4)
Set-Content -Path $manifestPath -Value $manifest -Encoding UTF8

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

# Build the optional public-symbol archive used by Partner Center for crash
# analytics. PDBs are deliberately removed from the MSIX payload.
$appSym = $null
$symbolFiles = @(Get-ChildItem -Path $PublishDirectory -Recurse -Filter "*.pdb" -File -ErrorAction SilentlyContinue)
if ($symbolFiles.Count -gt 0) {
    $symbolStage = Join-Path $env:RUNNER_TEMP "MediaForgeSymbols"
    if (Test-Path $symbolStage) { Remove-Item $symbolStage -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $symbolStage | Out-Null

    foreach ($symbol in $symbolFiles) {
        $targetSymbol = Join-Path $symbolStage $symbol.Name
        Copy-Item $symbol.FullName $targetSymbol -Force
    }

    $appSym = Join-Path $OutputDirectory "MediaForge.appxsym"
    if (Test-Path $appSym) { Remove-Item $appSym -Force }

    # Create a standard ZIP first, then rename it to Partner Center's .appxsym
    # extension. Use the .NET ZIP API so this works consistently under Windows
    # PowerShell and does not depend on the destination-file extension rules of
    # Compress-Archive.
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $symbolZip = Join-Path $env:RUNNER_TEMP "MediaForge.appxsym.zip"
    if (Test-Path $symbolZip) { Remove-Item $symbolZip -Force }
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $symbolStage,
        $symbolZip,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false
    )
    if (-not (Test-Path $symbolZip)) {
        throw "Failed to create public symbol ZIP: $symbolZip"
    }
    Move-Item -Path $symbolZip -Destination $appSym -Force

    # Never ship raw PDBs inside the customer package.
    Get-ChildItem -Path $stage -Recurse -Filter "*.pdb" -File -ErrorAction SilentlyContinue |
        Remove-Item -Force

    Remove-Item $symbolStage -Recurse -Force
    Write-Host "Created public symbols: $appSym"
} else {
    Write-Warning "No PDB files were produced. The Store upload will not include crash-analysis symbols."
}

$output = Join-Path $OutputDirectory "MediaForge.msix"
if (Test-Path $output) { Remove-Item $output -Force }

& $makeAppx pack /d $stage /p $output /o
if ($LASTEXITCODE -ne 0) {
    throw "MakeAppx failed with exit code $LASTEXITCODE."
}

# Partner Center accepts an .msixupload container. It is a ZIP containing the
# MSIX plus the optional .appxsym symbol archive.
$upload = Join-Path $OutputDirectory "MediaForge.msixupload"
if (Test-Path $upload) { Remove-Item $upload -Force }

$uploadStage = Join-Path $env:RUNNER_TEMP "MediaForgeStoreUpload"
if (Test-Path $uploadStage) { Remove-Item $uploadStage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $uploadStage | Out-Null

Copy-Item $output (Join-Path $uploadStage "MediaForge.msix") -Force
if ($appSym) {
    Copy-Item $appSym (Join-Path $uploadStage "MediaForge.appxsym") -Force
}

$tempZip = Join-Path $env:RUNNER_TEMP "MediaForge.msixupload.zip"
if (Test-Path $tempZip) { Remove-Item $tempZip -Force }

Compress-Archive -Path (Join-Path $uploadStage "*") -DestinationPath $tempZip -CompressionLevel Optimal
Move-Item -Path $tempZip -Destination $upload -Force

Remove-Item $uploadStage -Recurse -Force

Write-Host "Created MSIX: $output"
Write-Host "Created Store upload package: $upload"
Get-Item $output,$upload | Format-Table FullName,Length -AutoSize
if ($appSym) {
    Get-Item $appSym | Format-Table FullName,Length -AutoSize
}
