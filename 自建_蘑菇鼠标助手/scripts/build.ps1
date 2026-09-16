#requires -Version 5.1
[CmdletBinding()]
param([string]$OutputDirectory = (Join-Path $PSScriptRoot '..\bin'))
$ErrorActionPreference = 'Stop'
$packageRoot = Split-Path $PSScriptRoot -Parent
$sourceRoot = Join-Path $packageRoot 'src'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) { throw 'Windows x64 .NET Framework compiler not found. Install .NET Framework 4.8 from Microsoft.' }
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$exe = Join-Path $outputRoot '蘑菇鼠标助手.exe'
$sources = @('Native.cs','Movement.cs','GameLocation.cs','AssemblyInfo.cs','Program.cs') | ForEach-Object { Join-Path $sourceRoot $_ }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /debug- /codepage:65001 /warnaserror+ "/win32manifest:$sourceRoot\app.manifest" "/out:$exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item -LiteralPath (Join-Path $sourceRoot 'App.config') -Destination ($exe + '.config')
foreach ($name in @('README.md','DETAILS.md','BUILD.md','NOTICE.md','LICENSE-STATUS.md','CHANGELOG.md','VERSION')) {
    Copy-Item -LiteralPath (Join-Path $packageRoot $name) -Destination (Join-Path $outputRoot $name)
}
if (Test-Path -LiteralPath (Join-Path $packageRoot 'LICENSE')) {
    Copy-Item -LiteralPath (Join-Path $packageRoot 'LICENSE') -Destination (Join-Path $outputRoot 'LICENSE')
}
Write-Output 'Build completed (Windows x64).'
