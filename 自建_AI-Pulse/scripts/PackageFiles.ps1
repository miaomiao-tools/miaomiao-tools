#requires -Version 7.0
$PortableDocuments = @('README.md','README.en.md','LICENSE','docs/USAGE.md','docs/USAGE.en.md','docs/PRIVACY.md','docs/PRIVACY.en.md','docs/NOTICE.md','docs/NOTICE.en.md')
$DeveloperDocuments = @('VERSION','global.json','NuGet.Config','docs/BUILD.md','docs/BUILD.en.md','docs/CHANGELOG.md','docs/CHANGELOG.en.md','docs/VALIDATION.md','docs/VALIDATION.en.md')
$LicenseFiles = @('docs/licenses/dotnet-runtime-LICENSE.txt','docs/licenses/dotnet-runtime-THIRD-PARTY-NOTICES.txt','docs/licenses/dotnet-windowsdesktop-LICENSE.txt','docs/licenses/dotnet-wpf-THIRD-PARTY-NOTICES.txt','docs/licenses/dotnet-winforms-THIRD-PARTY-NOTICES.txt','docs/licenses/SOURCES.json')
$AssetFiles = @('assets/ai-pulse-banner.png','assets/mini-screenshot.jpg','assets/SOURCES.md')
$CodeFiles = @('src/AIPulse.csproj','src/app.manifest','src/app.ico','src/App.xaml','src/App.xaml.cs','src/AppShell.cs','src/Charts.cs','src/ContractTests.cs','src/Dashboard.cs','src/LightProbe.cs','src/MainWindow.xaml','src/MainWindow.xaml.cs','src/MiniPresenter.cs','src/MiniTests.cs','src/MiniWindow.xaml','src/MiniWindow.xaml.cs','src/Models.cs','src/ProbeEngine.cs','src/ProbeGuard.cs','src/ReleaseTests.cs','src/SafetyTests.cs','src/SettingsDialog.cs','src/Storage.cs','src/WindowLayout.cs')
$ScriptFiles = @('scripts/build.ps1','scripts/pack.ps1','scripts/test.ps1','scripts/PackageFiles.ps1')
$PortableFiles = $PortableDocuments + $LicenseFiles + $AssetFiles
$SourceFiles = $PortableFiles + $DeveloperDocuments + $CodeFiles + $ScriptFiles + @('.gitignore')
$BinaryFiles = @('AI Pulse.exe')

function Assert-RegularReleaseFile([string]$BaseDirectory, [string]$Relative) {
    if ([IO.Path]::IsPathRooted($Relative) -or ($Relative -split '[\\/]') -contains '..') { throw 'Release paths must be relative and contained.' }
    if (($Relative -split '[\\/]') | Where-Object { $_ -in @('data','reports','backup','bin','obj','.git') }) { throw "Private/generated path rejected: $Relative" }
    if ([IO.Path]::GetFileName($Relative) -match '\.(log|pdb|tmp|lnk|user|suo)$' -or [IO.Path]::GetFileName($Relative) -eq 'startup-error.txt') { throw "Private/debug file rejected: $Relative" }
    $base = [IO.Path]::GetFullPath($BaseDirectory).TrimEnd('\','/')
    $item = Get-Item -LiteralPath (Join-Path $base $Relative) -ErrorAction Stop
    if ($item.PSIsContainer) { throw "Not a regular release file: $Relative" }
    while ($item) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse point rejected: $Relative" }
        if ($item.FullName.TrimEnd('\','/') -eq $base) { break }
        $item = Get-Item -LiteralPath (Split-Path $item.FullName -Parent) -ErrorAction Stop
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
            if (-not $allowed.Contains($target)) { throw "Local link missing from package: $name -> $link" }
        }
    }
}

function Assert-PackageDocuments([string]$ProjectDirectory) {
    foreach ($name in $SourceFiles) { Assert-RegularReleaseFile $ProjectDirectory $name }
    Assert-DocumentLinks $ProjectDirectory $SourceFiles
    Assert-DocumentLinks $ProjectDirectory $PortableFiles
}
