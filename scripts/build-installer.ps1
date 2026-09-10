[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$RuntimeIdentifier = 'win-x64',

    [ValidatePattern('^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$')]
    [string]$Version = '1.0.0'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$profile = if ($RuntimeIdentifier -eq 'win-arm64') { 'win-arm64' } else { 'win-x64' }
$architecture = if ($RuntimeIdentifier -eq 'win-arm64') { 'arm64' } else { 'x64compatible' }
$publishDirectory = Join-Path $repoRoot "artifacts\publish\$RuntimeIdentifier"
$installerDirectory = Join-Path $repoRoot 'artifacts\installer'
$projectPath = Join-Path $repoRoot 'src\MediaForge\MediaForge.csproj'
$installerScript = Join-Path $repoRoot 'installer\MediaForge.iss'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET SDK is required. Install .NET 10 SDK and retry.'
}

$isccCandidates = @(
    (Join-Path ${env:ProgramFiles} 'Inno Setup 7\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe')
)
$iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($iscc)) {
    throw 'Inno Setup 7 is required. Install JRSoftware.InnoSetup.7 and retry.'
}

New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $installerDirectory -Force | Out-Null

Push-Location $repoRoot
try {
    dotnet restore .\MediaForge.sln
    dotnet build .\MediaForge.sln --configuration Release --no-restore
    dotnet test .\MediaForge.sln --configuration Release --no-build
    dotnet publish $projectPath --configuration Release --runtime $RuntimeIdentifier --self-contained true --publish-profile $profile --no-restore

    & $iscc "/DPublishDir=$publishDirectory" "/DArchitecture=$architecture" "/DAppVersion=$Version" $installerScript
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
    }

    & (Join-Path $repoRoot 'scripts\smoke-test.ps1') -PublishDirectory $publishDirectory

    $installerPath = Join-Path $installerDirectory "MediaForge-Setup-$architecture.exe"
    if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
        throw "Installer output was not created: $installerPath"
    }

    & (Join-Path $repoRoot 'scripts\installer-smoke-test.ps1') -InstallerPath $installerPath
    if ($LASTEXITCODE -ne 0) {
        throw 'Installer smoke test failed.'
    }

    Write-Host "Installer created and validated: $installerPath"
}
finally {
    Pop-Location
}
