# Build, check, and package

[中文](BUILD.md) · **English**

Environment: Windows x64, .NET Framework 4.8 / 4.8.1, and PowerShell 7. Building, self-testing, and packaging require no dependency downloads, game installation, or administrator privileges.

Download and fully extract the source archive. Open PowerShell in its root folder:

```powershell
pwsh -NoProfile -File .\scripts\build.ps1
$report = Join-Path $env:TEMP ('shroommouse-test-' + [Guid]::NewGuid().ToString('N') + '.txt')
$exe = (Resolve-Path '.\bin\蘑菇鼠标助手.exe').Path
$test = Start-Process -FilePath $exe -ArgumentList ('--test "' + $report + '"') -WindowStyle Hidden -Wait -PassThru
if ($test.ExitCode -ne 0) { throw 'Self-test failed; inspect the report.' }
Get-Content -LiteralPath $report
pwsh -NoProfile -File .\scripts\pack.ps1
```

Follow your existing script execution policy; no permanent policy change is required. PowerShell 7 was used for this release's build and packaging. Windows PowerShell 5.1 was not validated. A passing report ends with `ALL CHECKS PASSED`. Players running the compiled app do not need PowerShell.

`build.ps1` uses the local compiler at `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319`. It compiles a fixed list of C# files into an optimized x64 Windows executable without PDB files. Output defaults to `bin`; `-OutputDirectory` selects another new folder. The script does not run the normal GUI or launch the game.

`pack.ps1` uses an explicit file list to create portable and source ZIPs in `dist`, excluding user settings and test reports. Build first, and package again after rebuilding or changing included documents. The project's own code is MIT-licensed. The script does not connect to GitHub or upload anything.

`release-files.json` lists relative paths, file sizes, and SHA-256 hashes. `SHA256SUMS.txt` also includes the manifest's hash, but does not hash itself. ZIP members have a fixed order and timestamps, with no absolute paths, machine names, or personal author fields.

This process rebuilds a functionally equivalent program. The older C# compiler supplied with Windows writes PE timestamps and module identifiers, so rebuilding identical source does not guarantee byte-for-byte identical executables or ZIPs. Compiler and system assembly versions can also affect output. Verify new builds against their newly generated manifests; the published hashes are not cross-machine reproducibility guarantees.

The portable app needs neither source files nor PowerShell. `--demo` is for manual developer testing: it creates a test window, installs a real mouse hook, sends real key events, and writes `demo-events.txt` beside the executable. It is not an isolated test and was not run during this candidate's validation. Normal launch also connects to desktop input; choose a suitable environment before trying it.

The 2026-09-17 bilingual update changes online documentation only. The current packer still includes the original Chinese document set, and the published ZIPs and tag have not been replaced. The English pages are available in the current repository.
