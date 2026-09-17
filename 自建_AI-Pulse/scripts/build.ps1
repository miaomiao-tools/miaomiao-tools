#requires -Version 7.0
[CmdletBinding()]
param([string]$OutputDirectory, [string]$WorkDirectory, [string]$OfflinePackagesDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $projectRoot '_build/publish' }
if (-not $WorkDirectory) { $WorkDirectory = Join-Path $projectRoot '_build/work' }
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
$workPath = [IO.Path]::GetFullPath($WorkDirectory)
foreach ($candidate in @($outputPath,$workPath)) {
    if ($candidate -eq $projectRoot -or $candidate -eq (Join-Path $projectRoot 'src') -or $candidate.StartsWith((Join-Path $projectRoot 'src') + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Choose an output directory outside the source inputs.' }
    if ((Test-Path -LiteralPath $candidate) -and @(Get-ChildItem -LiteralPath $candidate -Force).Count -gt 0) { throw 'Build output/work directories must be empty; use a new directory.' }
}
New-Item -ItemType Directory -Force -Path $outputPath,$workPath | Out-Null
. (Join-Path $PSScriptRoot 'PackageFiles.ps1')
foreach ($name in ($CodeFiles + @('VERSION','global.json','NuGet.Config'))) { Assert-RegularReleaseFile $projectRoot $name }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_HOME = Join-Path $workPath 'dotnet-home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$objectPath = (Join-Path $workPath 'obj') + [IO.Path]::DirectorySeparatorChar
$binaryPath = (Join-Path $workPath 'bin') + [IO.Path]::DirectorySeparatorChar
$packagesPath = Join-Path $workPath 'packages'
$props = @(('-p:BaseIntermediateOutputPath=' + $objectPath), ('-p:MSBuildProjectExtensionsPath=' + $objectPath), ('-p:BaseOutputPath=' + $binaryPath))
Push-Location $projectRoot
try {
    $sdk = (& dotnet --version).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdk -notmatch '^8\.0\.4\d\d$') { throw 'Install .NET SDK 8.0.4xx (8.0.405 or a later patch in that feature band).' }
    $project = Join-Path $projectRoot 'src/AIPulse.csproj'
    $restoreSource = @()
    if ($OfflinePackagesDirectory) {
        $offlinePath = [IO.Path]::GetFullPath($OfflinePackagesDirectory)
        if (-not (Test-Path -LiteralPath $offlinePath -PathType Container)) { throw 'Offline packages directory not found.' }
        $restoreSource = @('--source', $offlinePath)
    }
    & dotnet restore $project --configfile (Join-Path $projectRoot 'NuGet.Config') --packages $packagesPath @restoreSource @props --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    & dotnet publish $project -c Release -r win-x64 --self-contained true --no-restore -o $outputPath @props --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    $inputs = foreach ($name in ($CodeFiles + @('VERSION','global.json','NuGet.Config'))) { [ordered]@{Path=$name;SHA256=(Get-FileHash -LiteralPath (Join-Path $projectRoot $name)).Hash.ToLowerInvariant()} }
    $exe = Join-Path $outputPath 'AI Pulse.exe'
    $runtimePacks = foreach ($name in @('microsoft.netcore.app.runtime.win-x64','microsoft.windowsdesktop.app.runtime.win-x64','microsoft.netcore.app.host.win-x64')) {
        $archive = Join-Path $packagesPath ($name + '/8.0.31/' + $name + '.8.0.31.nupkg')
        if (Test-Path -LiteralPath $archive) { [ordered]@{Package=$name;Version='8.0.31';SHA512=(Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash.ToLowerInvariant()} }
    }
    $record = [ordered]@{SchemaVersion=1;Version=([IO.File]::ReadAllText((Join-Path $projectRoot 'VERSION')).Trim());Sdk=$sdk;Runtime='8.0.31';Target='win-x64';DebugSymbols=$false;RestoreSource=$(if ($OfflinePackagesDirectory) { 'local official package cache' } else { 'https://api.nuget.org/v3/index.json' });ExecutableSHA256=(Get-FileHash -LiteralPath $exe).Hash.ToLowerInvariant();Inputs=@($inputs);RuntimePacks=@($runtimePacks)}
    [IO.File]::WriteAllText((Join-Path $workPath 'build-record.json'), ($record | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    Write-Output ('Built AI Pulse ' + $record.Version + ' for win-x64; SHA-256 ' + $record.ExecutableSHA256)
}
finally { Pop-Location }
