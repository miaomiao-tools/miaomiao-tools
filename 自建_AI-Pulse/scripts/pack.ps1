#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PublishDirectory,
    [Parameter(Mandatory)][string]$BuildRecord,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$publishPath = [IO.Path]::GetFullPath($PublishDirectory)
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
. (Join-Path $PSScriptRoot 'PackageFiles.ps1')
Assert-PackageDocuments $projectRoot
$version = [IO.File]::ReadAllText((Join-Path $projectRoot 'VERSION')).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+(?:-[A-Za-z0-9.]+)?$') { throw 'Invalid version.' }
$record = Get-Content -LiteralPath $BuildRecord -Raw | ConvertFrom-Json
if ($record.Version -ne $version -or $record.Target -ne 'win-x64' -or $record.Runtime -ne '8.0.31') { throw 'Build record/version mismatch.' }
$expectedInputs = @($CodeFiles + @('VERSION','global.json','NuGet.Config'))
if (@($record.Inputs).Count -ne $expectedInputs.Count) { throw 'Build input manifest mismatch.' }
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($inputFile in $record.Inputs) {
    if ($inputFile.Path -notin $expectedInputs -or -not $seen.Add($inputFile.Path)) { throw 'Unexpected or duplicate build input.' }
    if ((Get-FileHash -LiteralPath (Join-Path $projectRoot $inputFile.Path)).Hash -ne $inputFile.SHA256) { throw ('Source changed since build: ' + $inputFile.Path) }
}
foreach ($name in $BinaryFiles) { Assert-RegularReleaseFile $publishPath $name }
if (@(Get-ChildItem -LiteralPath $publishPath -Force).Count -ne $BinaryFiles.Count) { throw 'Publish directory must contain only the release EXE.' }
$exe = Join-Path $publishPath 'AI Pulse.exe'
if ((Get-FileHash -LiteralPath $exe).Hash -ne $record.ExecutableSHA256) { throw 'Executable hash differs from the build record.' }
if ((Get-Item -LiteralPath $exe).VersionInfo.ProductVersion -ne $version) { throw 'Executable version mismatch.' }
if ($outputPath -eq $projectRoot -or $outputPath -eq $publishPath) { throw 'Choose a separate package output directory.' }
if ((Test-Path -LiteralPath $outputPath) -and @(Get-ChildItem -LiteralPath $outputPath -Force).Count -gt 0) { throw 'Package output must be empty; use a new directory.' }
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null

function New-ReleaseArchive([string]$FileName, [string]$ArchiveRoot, [object[]]$Members) {
    $path = Join-Path $outputPath $FileName
    $stream = [IO.File]::Open($path, [IO.FileMode]::CreateNew)
    $zip = [IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create,$false)
    $entries = [Collections.Generic.List[object]]::new()
    try {
        foreach ($member in ($Members | Sort-Object Relative)) {
            Assert-RegularReleaseFile $member.Base $member.Relative
            $fullPath = Join-Path $member.Base $member.Relative
            $entryName = $ArchiveRoot + '/' + $member.Relative.Replace('\','/')
            $entry = $zip.CreateEntry($entryName,[IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = [DateTimeOffset]::Parse('2026-09-17T00:00:00Z')
            $inputStream = [IO.File]::OpenRead($fullPath)
            $destination = $entry.Open()
            try { $inputStream.CopyTo($destination) } finally { $destination.Dispose(); $inputStream.Dispose() }
            $entries.Add([ordered]@{Path=$entryName;Bytes=(Get-Item -LiteralPath $fullPath).Length;SHA256=(Get-FileHash -LiteralPath $fullPath).Hash.ToLowerInvariant()})
        }
    }
    finally { $zip.Dispose(); $stream.Dispose() }
    return [ordered]@{Path=$FileName;Bytes=(Get-Item -LiteralPath $path).Length;SHA256=(Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant();Members=@($entries.ToArray())}
}

$portableMembers = @($PortableFiles | ForEach-Object { [pscustomobject]@{Base=$projectRoot;Relative=$_} }) + @($BinaryFiles | ForEach-Object { [pscustomobject]@{Base=$publishPath;Relative=$_} })
$sourceMembers = @($SourceFiles | ForEach-Object { [pscustomobject]@{Base=$projectRoot;Relative=$_} })
$portable = New-ReleaseArchive ('AIPulse-' + $version + '-win-x64.zip') 'AIPulse' $portableMembers
$source = New-ReleaseArchive ('AIPulse-' + $version + '-source.zip') ('AIPulse-' + $version + '-source') $sourceMembers
$manifest = [ordered]@{SchemaVersion=2;ToolId='MM-002';Version=$version;Tag=('mm-002-v' + $version);Archives=@($portable,$source)}
$manifestPath = Join-Path $outputPath 'release-files.json'
[IO.File]::WriteAllText($manifestPath,($manifest | ConvertTo-Json -Depth 9),[Text.UTF8Encoding]::new($false))
$lines = foreach ($name in @($portable.Path,$source.Path,'release-files.json')) { (Get-FileHash -LiteralPath (Join-Path $outputPath $name)).Hash.ToLowerInvariant() + '  ' + $name }
[IO.File]::WriteAllText((Join-Path $outputPath 'SHA256SUMS.txt'),(($lines -join "`n") + "`n"),[Text.UTF8Encoding]::new($false))
if (@(Get-ChildItem -LiteralPath $outputPath -Force).Count -ne 4) { throw 'Expected exactly four release attachments.' }
Write-Output ('Packaged ' + $version + ': ' + $portable.Members.Count + ' portable files, ' + $source.Members.Count + ' source files, four release attachments.')
