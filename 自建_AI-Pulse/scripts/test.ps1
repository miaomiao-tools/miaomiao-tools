#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable, [Parameter(Mandatory)][string]$OutputDirectory, [switch]$IncludeMini)
$ErrorActionPreference = 'Stop'
$exePath = [IO.Path]::GetFullPath($Executable)
$resultPath = [IO.Path]::GetFullPath($OutputDirectory)
if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) { throw 'Executable not found.' }
if ((Test-Path -LiteralPath $resultPath) -and @(Get-ChildItem -LiteralPath $resultPath -Force).Count -gt 0) { throw 'Choose a new empty diagnostic directory.' }
New-Item -ItemType Directory -Force -Path $resultPath | Out-Null
function Invoke-Diagnostic([string]$Flag, [string]$Argument) {
    $info = [Diagnostics.ProcessStartInfo]::new($exePath)
    $info.UseShellExecute = $false; $info.CreateNoWindow = $true; $info.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $info.WorkingDirectory = $resultPath
    $info.ArgumentList.Add($Flag); $info.ArgumentList.Add($Argument)
    $process = [Diagnostics.Process]::Start($info)
    if (-not $process.WaitForExit(45000)) { throw 'Diagnostic exceeded 45 seconds; inspect only this diagnostic process before retrying.' }
    if ($process.ExitCode -ne 0) { throw ('Diagnostic failed with exit code ' + $process.ExitCode) }
    $process.Dispose()
}
Invoke-Diagnostic '--self-test' (Join-Path $resultPath 'contract-tests.json')
$contract = Get-Content -LiteralPath (Join-Path $resultPath 'contract-tests.json') -Raw | ConvertFrom-Json
if ($contract.Failed -ne 0) { throw 'Contract tests failed.' }
Write-Output ('Contract tests passed: ' + $contract.Total)
if ($IncludeMini) {
    Invoke-Diagnostic '--mini-qa' (Join-Path $resultPath 'mini')
    $mini = Get-Content -LiteralPath (Join-Path $resultPath 'mini/mini-tests.json') -Raw | ConvertFrom-Json
    if ($mini.Failed -ne 0) { throw 'Mini tests failed.' }
    Write-Output ('Mini checks passed: ' + $mini.Total)
}
