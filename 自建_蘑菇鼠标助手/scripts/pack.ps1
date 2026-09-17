#requires -Version 7.0
[CmdletBinding()]
param([string]$BuildDirectory = (Join-Path $PSScriptRoot '..\bin'), [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\dist'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
$projectRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'PackageFiles.ps1')
$buildRoot = [IO.Path]::GetFullPath($BuildDirectory)
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$version = ([IO.File]::ReadAllText((Join-Path $projectRoot 'VERSION'))).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+-experimental\.\d+$') { throw 'Unexpected experimental version.' }
foreach ($name in $SourceFiles) { Assert-RegularReleaseFile $projectRoot $name }
foreach ($name in $BinaryFiles) { Assert-RegularReleaseFile $buildRoot $name }
Assert-PackageDocuments $projectRoot
$buildRecord = Get-Content -LiteralPath (Join-Path $buildRoot 'build-record.json') -Raw | ConvertFrom-Json
if ($buildRecord.Version -ne $version -or (Get-Item -LiteralPath (Join-Path $buildRoot $BinaryFiles[0])).VersionInfo.ProductVersion -ne $version) { throw 'Built version does not match project VERSION.' }
foreach ($set in @(@{Names=$CodeFiles;Rows=$buildRecord.SourceFiles;Root=$projectRoot}, @{Names=$BinaryFiles;Rows=$buildRecord.BinaryFiles;Root=$buildRoot})) {
    if (@($set.Rows).Count -ne $set.Names.Count) { throw 'Build record does not match the required file list.' }
    foreach ($name in $set.Names) {
        $rows = @($set.Rows | Where-Object { $_.Path -ceq $name })
        if ($rows.Count -ne 1 -or $rows[0].SHA256 -ne (Get-FileHash -LiteralPath (Join-Path $set.Root $name)).Hash.ToLowerInvariant()) { throw "Build is stale or modified: $name. Rebuild before packaging." }
    }
}

function New-EntrySpec([string]$BaseDirectory, [string]$Relative) {
    $path = Join-Path $BaseDirectory $Relative
    [pscustomobject]@{Relative=$Relative.Replace('\','/');File=$path;Bytes=(Get-Item -LiteralPath $path).Length;SHA256=(Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant()}
}
function Write-PackageZip([object[]]$Entries, [string]$Destination, [string]$Prefix) {
    $stream = [IO.File]::Open($Destination, [IO.FileMode]::Create, [IO.FileAccess]::Write)
    try {
        $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
        try {
            foreach ($spec in ($Entries | Sort-Object Relative -CaseSensitive)) {
                $entry = $zip.CreateEntry(($Prefix + '/' + $spec.Relative), [IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = [DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero); $entry.ExternalAttributes = 0
                $inputStream = [IO.File]::OpenRead($spec.File); $outputStream = $entry.Open()
                try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose(); $inputStream.Dispose() }
            }
        } finally { $zip.Dispose() }
    } finally { $stream.Dispose() }
}

$portable = @($BinaryFiles | ForEach-Object { New-EntrySpec $buildRoot $_ }) + @($PortableFiles | ForEach-Object { New-EntrySpec $projectRoot $_ })
$source = @($SourceFiles | ForEach-Object { New-EntrySpec $projectRoot $_ })
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$archives = @()
foreach ($spec in @(@{Name="ShroomMouse-$version-win-x64.zip";Prefix='ShroomMouse';Files=$portable}, @{Name="ShroomMouse-$version-source.zip";Prefix="ShroomMouse-$version-source";Files=$source})) {
    $path = Join-Path $outputRoot $spec.Name
    Write-PackageZip $spec.Files $path $spec.Prefix
    $archives += [ordered]@{Path=$spec.Name;Bytes=(Get-Item -LiteralPath $path).Length;SHA256=(Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant();Members=@($spec.Files | Sort-Object Relative -CaseSensitive | ForEach-Object { [ordered]@{Path=$spec.Prefix+'/'+$_.Relative;Bytes=$_.Bytes;SHA256=$_.SHA256} })}
}
$manifest = [ordered]@{SchemaVersion=2;Version=$version;Archives=$archives}
$utf8 = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $outputRoot 'release-files.json'), ($manifest | ConvertTo-Json -Depth 8) + "`n", $utf8)
$sums = @($archives | ForEach-Object { $_.SHA256 + '  ' + $_.Path })
$sums += (Get-FileHash -LiteralPath (Join-Path $outputRoot 'release-files.json')).Hash.ToLowerInvariant() + '  release-files.json'
[IO.File]::WriteAllText((Join-Path $outputRoot 'SHA256SUMS.txt'), ($sums -join "`n") + "`n", $utf8)
Write-Output 'Portable and source archives ready; current project documents and assets included.'
