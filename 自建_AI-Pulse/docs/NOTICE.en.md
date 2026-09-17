# Licenses and third-party components

[中文](NOTICE.md) · [Home](../README.en.md)

Original AI Pulse code, its app icon, and the bundled illustration are [MIT licensed](../LICENSE), copyright 2026 miaomiao tools. See [asset provenance](../assets/SOURCES.md). Service names identify test targets and do not imply affiliation or endorsement.

The portable EXE bundles the Microsoft .NET 8.0.31 Windows x64 runtime and desktop components. The app declares no additional NuGet `PackageReference`. Self-contained distribution still requires the upstream licenses and notices, in addition to this project's MIT license:

| Component | Included upstream text |
| --- | --- |
| .NET Runtime and app host | [MIT LICENSE](licenses/dotnet-runtime-LICENSE.txt), [complete third-party notices](licenses/dotnet-runtime-THIRD-PARTY-NOTICES.txt) |
| Windows Desktop Runtime | [LICENSE](licenses/dotnet-windowsdesktop-LICENSE.txt) |
| Matching WPF revision | [Complete third-party notices](licenses/dotnet-wpf-THIRD-PARTY-NOTICES.txt) |
| Matching Windows Forms revision | [Complete third-party notices](licenses/dotnet-winforms-THIRD-PARTY-NOTICES.txt) |

License texts come directly from official packages. WPF and Windows Forms notices are pinned to the commits in the Windows Desktop 8.0.31 dependency map. [licenses/SOURCES.json](licenses/SOURCES.json) records source URLs, commits, and SHA-256 hashes. Complete upstream notices may cover a broader upstream repository; retaining them does not assert that this app uses every referenced feature or test asset.

The UI uses installed Windows fonts: Segoe UI, Microsoft YaHei UI, and Segoe MDL2 Assets. No font files are redistributed. Third-party components keep their respective licenses. No code-signing certificate is supplied with this release.
