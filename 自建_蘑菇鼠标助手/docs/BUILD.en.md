# Build, check, and package

[中文](BUILD.md) · **English**

For **0.1.0-experimental.3, an experimental release**. Requires Windows x64, .NET Framework 4.8 / 4.8.1, and PowerShell 7. No dependency downloads, game installation, or administrator rights are needed. Players running the EXE do not need PowerShell.

Run from the tool's source directory or the fully extracted source ZIP root:

```powershell
$build = Join-Path $env:TEMP ('ShroomMouse build ' + [Guid]::NewGuid().ToString('N'))
$package = Join-Path $env:TEMP ('ShroomMouse package ' + [Guid]::NewGuid().ToString('N'))
pwsh -NoProfile -File .\scripts\build.ps1 -OutputDirectory $build
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$report = Join-Path $build 'test-results.txt'
$test = Start-Process -FilePath (Join-Path $build '蘑菇鼠标助手.exe') -ArgumentList ('--test "' + $report + '"') -WindowStyle Hidden -Wait -PassThru
if ($test.ExitCode -ne 0) { throw 'Self-test failed.' }
Get-Content -LiteralPath $report
pwsh -NoProfile -File .\scripts\pack.ps1 -BuildDirectory $build -OutputDirectory $package
if ($LASTEXITCODE -ne 0) { throw 'Packaging failed.' }
Get-ChildItem -LiteralPath $package
```

Follow the existing execution policy; no permanent system-policy change is needed. Defaults are the tool's `bin` and `dist` folders. Both parameters support separate folders, Unicode, and spaces.

## File sources

- `build.ps1` uses `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`, an explicit source list, optimized x64 output, warnings as errors, and no PDB files.
- A full build copies the portable files: **two quick-start guides, LICENSE, docs/ usage and notices, and assets/ images**. Development and history documents are source-package only. Use `-CodeOnly` for a stage build before a genuine screenshot is available; it writes only the EXE, configuration, and local build record, never a fake screenshot.
- `pack.ps1` reads only EXE/config from `-BuildDirectory`. Documents, source, and images always come from the script's **current tool directory**, ignoring stale bin documents. Version, source hashes, and executable hashes must match `build-record.json`. Rebuild after changing source or binaries; document edits only require repackaging.
- The portable ZIP has only the EXE, runtime config, two READMEs and LICENSE at its root; docs/ contains two usage guides and one bilingual NOTICE, and assets/ holds both images. The source ZIP adds version, build, changelog, and validation records. Relative links are checked separately for each package. The explicit allowlist excludes settings, logs, PDBs, temporary files, and old nested extracted folders. Missing images, broken relative links, or reparse points fail packaging.
- `build-record.json` is local build evidence, not a ZIP member or a public machine-information record.

## Outputs and verification

The output folder receives two ZIPs, `release-files.json`, and `SHA256SUMS.txt`. The JSON records relative paths, sizes, and SHA-256 for each archive and member. The checksum file covers both ZIPs and the JSON, without self-reference. ZIP members have fixed ordering and timestamps, with no personal author or absolute-path metadata.

This rebuilds a functionally equivalent program. The older compiler writes timestamps and module identifiers, so **byte-identical rebuilt EXEs are not guaranteed**. Generate fresh hashes for every build. Scripts do not commit, tag, contact GitHub, or upload.

## Test boundaries

`--test` is offline: no mouse hook or real keyboard input. It creates and cleans isolated temporary files beside the requested report. Success ends with `ALL CHECKS PASSED`.

`--demo` is a **real desktop-input test**. It opens a helper and training window, installs a mouse hook, sends W/S, and writes `demo-events.txt` beside the EXE. It remains attached to its training window, with reselection disabled. It is neither an offline test nor a gameplay test. Exit through the helper's × button, tray Exit, or by closing the training window. Native selection, reselection, and persistence use normal launch and need real desktop interaction. See [validation history](VALIDATION.en.md) for the actual validation scope.

The release uses the accepted .3 executable without recompilation; only documentation and download links changed. Historical .1 tags and attachments remain unchanged. The app interface remains Chinese.
