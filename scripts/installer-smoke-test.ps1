[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$InstallerPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$installer = (Resolve-Path -LiteralPath $InstallerPath).Path
$root = Join-Path ([System.IO.Path]::GetTempPath()) "MediaForge-InstallerSmoke-$([guid]::NewGuid().ToString('N'))"
$installDirectory = Join-Path $root 'MediaForge'
$userDesktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
$userPrograms = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\MediaForge'
$desktopShortcut = Join-Path $userDesktop 'MediaForge.lnk'
$startShortcut = Join-Path $userPrograms 'MediaForge.lnk'
$installedExe = Join-Path $installDirectory 'MediaForge.exe'
$uninstaller = Join-Path $installDirectory 'unins000.exe'

New-Item -ItemType Directory -Path $root -Force | Out-Null

try {
    $installProcess = Start-Process -FilePath $installer -ArgumentList @(
        '/VERYSILENT',
        '/SUPPRESSMSGBOXES',
        '/NORESTART',
        "/DIR=$installDirectory",
        '/TASKS=desktopicon'
    ) -Wait -PassThru

    if ($installProcess.ExitCode -ne 0) {
        throw "Installer exited with code $($installProcess.ExitCode)."
    }

    foreach ($path in @($installedExe, $desktopShortcut, $startShortcut, $uninstaller)) {
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Installer smoke test artifact is missing: $path"
        }
    }

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $installedExe
    $startInfo.WorkingDirectory = $installDirectory
    $startInfo.UseShellExecute = $false
    $process = [System.Diagnostics.Process]::Start($startInfo)

    try {
        $deadline = (Get-Date).AddSeconds(12)
        do {
            Start-Sleep -Milliseconds 500
            if ($process.HasExited) {
                throw "Installed MediaForge exited during startup with code $($process.ExitCode)."
            }
        } while ((Get-Date) -lt $deadline)
    }
    finally {
        try {
            if ($null -ne $process -and -not $process.HasExited) {
                $process.CloseMainWindow() | Out-Null
                if (-not $process.WaitForExit(5000)) {
                    $process.Kill($true)
                    $process.WaitForExit()
                }
            }
        }
        finally {
            if ($null -ne $process) {
                $process.Dispose()
            }
        }
    }

    Write-Host 'Installer smoke test passed.'
}
finally {
    if (Test-Path -LiteralPath $uninstaller) {
        try {
            $uninstallProcess = Start-Process -FilePath $uninstaller -ArgumentList @(
                '/VERYSILENT',
                '/SUPPRESSMSGBOXES',
                '/NORESTART'
            ) -Wait -PassThru

            if ($uninstallProcess.ExitCode -ne 0) {
                Write-Warning "Uninstaller exited with code $($uninstallProcess.ExitCode)."
            }
        }
        catch {
            Write-Warning "Failed to run uninstall smoke step: $($_.Exception.Message)"
        }
    }

    foreach ($path in @($desktopShortcut, $startShortcut)) {
        try {
            if (Test-Path -LiteralPath $path) {
                Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
            }
        }
        catch {
        }
    }

    try {
        if (Test-Path -LiteralPath $root) {
            Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    catch {
    }
}
