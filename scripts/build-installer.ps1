$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$publish=Join-Path $root 'artifacts\publish'
$installer=Join-Path $root 'artifacts\installer'
if(Test-Path $publish){Remove-Item $publish -Recurse -Force}
if(Test-Path $installer){Remove-Item $installer -Recurse -Force}
New-Item -ItemType Directory -Force -Path $publish,$installer | Out-Null
dotnet restore (Join-Path $root 'src\MediaForge\MediaForge.csproj')
dotnet publish (Join-Path $root 'src\MediaForge\MediaForge.csproj') -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -p:PublishReadyToRun=false -o $publish
if(-not (Get-Command iscc -ErrorAction SilentlyContinue)){throw 'Inno Setup is required (iscc.exe).'}
iscc /DPublishDir="$publish" /DVCRedistPath="$(Join-Path $root 'artifacts\vc_redist.x64.exe')" (Join-Path $root 'installer\MediaForge.iss')
Write-Host "Installer: $(Join-Path $installer 'Setup.exe')"
