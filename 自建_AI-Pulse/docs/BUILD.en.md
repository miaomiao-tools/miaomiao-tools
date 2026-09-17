# Standalone build and packaging

[中文](BUILD.md) · [Home](../README.en.md)

Requirements: Windows x64, PowerShell 7, and .NET SDK **8.0.405 or a later patch in the same 8.0.4xx feature band**. `global.json` pins the feature band; the project pins the self-contained runtime to 8.0.31. The source ZIP builds independently of other repository projects and needs no account or API key.

From the extracted source root:

```powershell
pwsh -File ./scripts/build.ps1
pwsh -File ./scripts/test.ps1 -Executable './_build/publish/AI Pulse.exe' -OutputDirectory './_test/run-1' -IncludeMini
pwsh -File ./scripts/pack.ps1 -PublishDirectory './_build/publish' -BuildRecord './_build/work/build-record.json' -OutputDirectory './_package/release-1'
```

By default, restore uses only the official NuGet source in `NuGet.Config`; the first build needs internet access. Output directories must be empty. Use new `-OutputDirectory` and `-WorkDirectory` values for another build. `build.ps1` also accepts `-OfflinePackagesDirectory`, pointing to a directory of official `.nupkg` archives you have independently prepared and verified. That cache is not a release attachment; it must contain all required runtime packs and SDK tooling packages.

The published output is a self-contained single-file `AI Pulse.exe`. A separate build record stores input hashes, SDK/runtime versions, package hashes, and the EXE hash. PDB files are disabled and local build paths are mapped away from source mappings. At runtime, .NET may extract native components into the Windows user temporary directory.

The 104 logic/safety checks and 32 mini checks in `test.ps1` use temporary directories and loopback services, without probing real AI providers. Mini checks include simulated-state rendering, not proof of internet availability. Keep raw diagnostic reports locally; do not place them in public packages. The old automatically networked `--smoke` entry point is disabled and simply exits.

`pack.ps1` uses explicit file lists in `PackageFiles.ps1`, checks that source inputs match the build record, validates relative document links, and rejects reparse points, private-data paths, and debug logs. It creates portable and source ZIPs, `SHA256SUMS.txt`, and a per-file `release-files.json`. Build directories, personal data, diagnostic reports, and other projects are excluded.

Use `Get-FileHash -Algorithm SHA256` to compare downloaded files with the published `SHA256SUMS.txt`. It covers both ZIPs and `release-files.json`; it does not hash itself recursively. Normal-launch and human GUI checks are also needed before release; automated checks do not cover every display layout.

See [LICENSE](../LICENSE), the runtime [notices](NOTICE.en.md), and the actual [validation scope](VALIDATION.en.md).
