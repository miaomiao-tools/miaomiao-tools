#requires -Version 5.1
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
$packageRoot = Split-Path $PSScriptRoot -Parent
$version = (Get-Content -LiteralPath (Join-Path $packageRoot 'VERSION') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+-experimental\.\d+$') { throw 'Unexpected experimental version.' }
$docs = @('README.md','DETAILS.md','BUILD.md','NOTICE.md','LICENSE','LICENSE-STATUS.md','CHANGELOG.md','VERSION')
$sourceFiles = $docs + @('RELEASE-NOTES.md','.gitignore','src/Native.cs','src/Movement.cs','src/Program.cs','src/GameLocation.cs','src/AssemblyInfo.cs','src/app.manifest','src/App.config','scripts/build.ps1','scripts/pack.ps1')
$runtimeFiles = @('蘑菇鼠标助手.exe','蘑菇鼠标助手.exe.config') + $docs
$dist = Join-Path $packageRoot 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null

function Write-PackageZip([string]$baseDirectory, [string[]]$files, [string]$destination, [string]$prefix) {
    foreach ($relative in $files) {
        $path = Join-Path $baseDirectory $relative
        $item = Get-Item -LiteralPath $path
        if ($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Not a regular release file: $relative" }
    }
    $stream = [IO.File]::Open($destination, [IO.FileMode]::Create, [IO.FileAccess]::Write)
    try {
        $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
        try {
            foreach ($relative in ($files | Sort-Object -CaseSensitive)) {
                $entry = $zip.CreateEntry(($prefix + '/' + $relative.Replace('\','/')), [IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = [DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
                $entry.ExternalAttributes = 0
                $inputStream = [IO.File]::OpenRead((Join-Path $baseDirectory $relative))
                $outputStream = $entry.Open()
                try { $inputStream.CopyTo($outputStream) }
                finally { $outputStream.Dispose(); $inputStream.Dispose() }
            }
        } finally { $zip.Dispose() }
    } finally { $stream.Dispose() }
}

$binaryName = "ShroomMouse-$version-win-x64.zip"
$sourceName = "ShroomMouse-$version-source.zip"
Write-PackageZip (Join-Path $packageRoot 'bin') $runtimeFiles (Join-Path $dist $binaryName) 'ShroomMouse'
Write-PackageZip $packageRoot $sourceFiles (Join-Path $dist $sourceName) "ShroomMouse-$version-source"
$allFiles = $sourceFiles + ($runtimeFiles | ForEach-Object { 'bin/' + $_ }) + @("dist/$binaryName", "dist/$sourceName")
$manifest = @($allFiles | Sort-Object -CaseSensitive -Unique | ForEach-Object {
    $path = Join-Path $packageRoot $_
    [pscustomobject]@{Path=$_;Bytes=(Get-Item -LiteralPath $path).Length;SHA256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
})
$utf8 = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $packageRoot 'release-files.json'), (ConvertTo-Json -InputObject $manifest -Depth 3) + "`n", $utf8)
$sums = @($manifest | ForEach-Object { $_.SHA256 + '  ' + $_.Path })
$sums += (Get-FileHash -LiteralPath (Join-Path $packageRoot 'release-files.json') -Algorithm SHA256).Hash.ToLowerInvariant() + '  release-files.json'
[IO.File]::WriteAllText((Join-Path $packageRoot 'SHA256SUMS.txt'), ($sums -join "`n") + "`n", $utf8)
Write-Output ("Packed {0} files; portable and source archives ready." -f $manifest.Count)
