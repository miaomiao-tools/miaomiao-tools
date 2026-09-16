# Origins and third-party notices

[中文](NOTICE.md) · **English**

This tool's package contains its source, compiled program, and documentation. Publisher: miaomiao tools.

| Component | Source and use | Distribution |
| --- | --- | --- |
| Double-arrow tray icon, panel arrows, and close symbol | Generated at runtime by geometric drawing code in `Program.cs` | No external icon files, game art, or brand avatar |
| Microsoft YaHei UI | Uses the font installed with the user's Windows system; see the official page for Microsoft and Beijing Founder Electronics rights | No font files are included, embedded, converted, or redistributed |
| .NET Framework: mscorlib, System, System.Core, System.Drawing, System.Windows.Forms | Dynamically referenced Microsoft system libraries | No runtime binaries or installers included |
| Windows user32.dll, kernel32.dll, and CLR startup library | Windows API calls and the system loader | No system DLLs included |
| C# compiler csc.exe | Uses the builder's own Windows .NET Framework installation | Compiler not distributed |
| Shroom and Gloom; Steam | Names identify the supported game; a Steam URI calls the installed client | No game code, patches, audio, fonts, images, or executables included |

The release review found no NuGet dependencies, external source packages, embedded third-party resources, or existing third-party license files. Windows and .NET Framework remain subject to their own terms; this project's license does not grant redistribution rights to those components. Geometric graphics follow the license for the project's own code. No license has been assigned to unknown third-party material.

References checked on 2026-09-16:

- [Microsoft YaHei and Microsoft YaHei UI information](https://learn.microsoft.com/en-us/typography/font-list/microsoft-yahei).
- [Windows font use and redistribution](https://learn.microsoft.com/en-us/typography/fonts/font-faq). The app uses an installed system font to draw its interface and does not distribute fonts.
- [Official .NET Framework installation guide](https://learn.microsoft.com/en-us/dotnet/framework/install/).
- [Official Shroom and Gloom Steam store page](https://store.steampowered.com/app/3271280/Shroom_and_Gloom/).
