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
$output = Join-Path $OutputDirectory "MediaForge.msix"
if (Test-Path $output) { Remove-Item $output -Force }

& $makeAppx pack /d $stage /p $output /o
if ($LASTEXITCODE -ne 0) {
    throw "MakeAppx failed with exit code $LASTEXITCODE."
}

# Partner Center recommends an .msixupload file for Store submissions.
# Public symbols are optional and can be added later when crash analytics symbols are available.
$upload = Join-Path $OutputDirectory "MediaForge.msixupload"
if (Test-Path $upload) { Remove-Item $upload -Force }

$tempZip = Join-Path $env:RUNNER_TEMP "MediaForge.msixupload.zip"
if (Test-Path $tempZip) { Remove-Item $tempZip -Force }

Compress-Archive -Path $output -DestinationPath $tempZip -CompressionLevel Optimal
Move-Item -Path $tempZip -Destination $upload -Force

Write-Host "Created MSIX: $output"
Write-Host "Created Store upload package: $upload"
Get-Item $output,$upload | Format-Table FullName,Length -AutoSize
