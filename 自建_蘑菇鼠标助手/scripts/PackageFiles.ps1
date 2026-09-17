#requires -Version 7.0
$PortableDocuments = @('README.md','README.en.md','LICENSE','docs/USAGE.md','docs/USAGE.en.md','docs/NOTICE.md')
$DeveloperDocuments = @('VERSION','docs/BUILD.md','docs/BUILD.en.md','docs/CHANGELOG.md','docs/CHANGELOG.en.md','docs/VALIDATION.md','docs/VALIDATION.en.md')
$DocumentFiles = $PortableDocuments + $DeveloperDocuments
$AssetFiles = @('assets/shroom-mouse-banner.png','assets/usage-screenshot.png')
$PortableFiles = $PortableDocuments + $AssetFiles
$CodeFiles = @('src/Native.cs','src/Movement.cs','src/Program.cs','src/GameLocation.cs','src/AssemblyInfo.cs','src/app.manifest','src/App.config')
$ScriptFiles = @('scripts/build.ps1','scripts/pack.ps1','scripts/PackageFiles.ps1')
$SourceFiles = $DocumentFiles + $AssetFiles + $CodeFiles + $ScriptFiles + @('.gitignore')
$BinaryFiles = @('蘑菇鼠标助手.exe','蘑菇鼠标助手.exe.config')

function Assert-RegularReleaseFile([string]$BaseDirectory, [string]$Relative) {
    if ([IO.Path]::IsPathRooted($Relative) -or ($Relative -split '[\\/]') -contains '..') { throw 'Release paths must be relative and contained.' }
    $leaf = [IO.Path]::GetFileName($Relative)
    if ($leaf -in @('controls.ini','game-path.txt','demo-events.txt','test-results.txt','build-record.json') -or $leaf -match '\.(log|pdb|tmp|lnk|user|suo)$') { throw "Private or debug file rejected: $Relative" }
    $base = [IO.Path]::GetFullPath($BaseDirectory).TrimEnd('\','/')
    $path = Join-Path $base $Relative
    $item = Get-Item -LiteralPath $path -ErrorAction Stop
    if ($item.PSIsContainer) { throw "Not a regular release file: $Relative" }
    while ($item) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse point rejected: $Relative" }
        if ($item.FullName.TrimEnd('\','/') -eq $base) { break }
        $item = $item.PSParentPath | ForEach-Object { Get-Item -LiteralPath $_ -ErrorAction Stop }
    }
}

function Assert-DocumentLinks([string]$ProjectDirectory, [string[]]$Files) {
    $base = [IO.Path]::GetFullPath($ProjectDirectory).TrimEnd('\','/')
    $allowed = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($name in $Files) { [void]$allowed.Add([IO.Path]::GetFullPath((Join-Path $base $name))) }
    foreach ($name in ($Files | Where-Object { $_ -like '*.md' })) {
        $document = Join-Path $base $name
        foreach ($match in [regex]::Matches([IO.File]::ReadAllText($document), '\]\(([^\s)]+)(?:\s+"[^"]*")?\)')) {
            $link = $match.Groups[1].Value
            if ($link -match '^[a-zA-Z][a-zA-Z0-9+.-]*:' -or $link.StartsWith('#')) { continue }
            $local = [Uri]::UnescapeDataString(($link -split '[#?]',2)[0])
            $target = [IO.Path]::GetFullPath((Join-Path (Split-Path $document -Parent) $local))
            if (-not $allowed.Contains($target)) { throw "Local document link missing from package: $name -> $link" }
        }
    }
}

function Assert-PackageDocuments([string]$ProjectDirectory) {
    foreach ($name in ($DocumentFiles + $AssetFiles)) { Assert-RegularReleaseFile $ProjectDirectory $name }
    Assert-DocumentLinks $ProjectDirectory ($DocumentFiles + $AssetFiles)
    Assert-DocumentLinks $ProjectDirectory $PortableFiles
}
