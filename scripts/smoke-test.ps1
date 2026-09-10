[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$PublishDirectory,

    [int]$StartupTimeoutSeconds = 12
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolvedDirectory = (Resolve-Path -LiteralPath $PublishDirectory).Path
$exePath = Join-Path $resolvedDirectory 'MediaForge.exe'

if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) {
    throw "Published executable was not found: $exePath"
}

$process = $null
try {
    $process = Start-Process -FilePath $exePath -WorkingDirectory $resolvedDirectory -PassThru
    $deadline = (Get-Date).AddSeconds($StartupTimeoutSeconds)

    do {
        Start-Sleep -Milliseconds 500
        $process.Refresh()

        if ($process.HasExited) {
            throw "MediaForge exited during startup with code $($process.ExitCode)."
        }
    } while ((Get-Date) -lt $deadline)

    Write-Host "Smoke test passed: MediaForge remained running for $StartupTimeoutSeconds seconds."
}
finally {
    if ($null -ne $process) {
        try {
            if (-not $process.HasExited) {
                $process.CloseMainWindow() | Out-Null
                if (-not $process.WaitForExit(5000)) {
                    $process.Kill($true)
                    $process.WaitForExit()
                }
            }
        }
        catch {
            Write-Warning "Failed to close smoke-test process cleanly: $($_.Exception.Message)"
        }
        finally {
            $process.Dispose()
        }
    }
}
