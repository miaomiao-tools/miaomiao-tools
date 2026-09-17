#requires -Version 7.0
[CmdletBinding()]
param([string]$OutputDirectory = (Join-Path $PSScriptRoot '..\bin'), [switch]$CodeOnly)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'PackageFiles.ps1')
$sourceRoot = Join-Path $projectRoot 'src'
foreach ($name in $CodeFiles) { Assert-RegularReleaseFile $projectRoot $name }
if (-not $CodeOnly) {
    Assert-PackageDocuments $projectRoot
}
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) { throw 'Windows x64 .NET Framework compiler not found.' }
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$exe = Join-Path $outputRoot '蘑菇鼠标助手.exe'
$sources = @('Native.cs','Movement.cs','GameLocation.cs','AssemblyInfo.cs','Program.cs') | ForEach-Object { Join-Path $sourceRoot $_ }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /debug- /codepage:65001 /warnaserror+ "/win32manifest:$sourceRoot\app.manifest" "/out:$exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item -LiteralPath (Join-Path $sourceRoot 'App.config') -Destination ($exe + '.config')
if (-not $CodeOnly) {
    foreach ($name in $PortableFiles) {
        $target = Join-Path $outputRoot $name
        New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $target
    }
}
$record = [ordered]@{
    Version = ([IO.File]::ReadAllText((Join-Path $projectRoot 'VERSION'))).Trim()
    SourceFiles = @($CodeFiles | ForEach-Object { [ordered]@{Path=$_;SHA256=(Get-FileHash -LiteralPath (Join-Path $projectRoot $_)).Hash.ToLowerInvariant()} })
    BinaryFiles = @($BinaryFiles | ForEach-Object { [ordered]@{Path=$_;SHA256=(Get-FileHash -LiteralPath (Join-Path $outputRoot $_)).Hash.ToLowerInvariant()} })
}
[IO.File]::WriteAllText((Join-Path $outputRoot 'build-record.json'), ($record | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
Write-Output 'Build completed (Windows x64).'
