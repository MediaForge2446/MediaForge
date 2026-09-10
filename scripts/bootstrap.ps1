$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

function Assert-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "$Name was not found in PATH. Install the required Windows/.NET development tooling and try again."
    }
}

Assert-Command 'dotnet'

$info = dotnet --info
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to execute dotnet --info.'
}

Write-Host 'Restoring MediaForge...' -ForegroundColor Cyan
dotnet restore .\MediaForge.sln
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet restore failed.'
}

Write-Host 'Building MediaForge (Debug / x64)...' -ForegroundColor Cyan
dotnet build .\MediaForge.sln --configuration Debug --framework net10.0-windows10.0.26100.0 --runtime win-x64 --no-restore
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet build failed.'
}

Write-Host 'Bootstrap completed successfully.' -ForegroundColor Green
Write-Host 'Run the application from Visual Studio or launch the generated WinExe from:' -ForegroundColor Gray
Write-Host '.\src\MediaForge\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\MediaForge.exe' -ForegroundColor Gray
