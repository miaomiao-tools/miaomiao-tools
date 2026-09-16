# Usage details and limits

[中文](DETAILS.md) · **English**

## Settings and recovery

- Layout settings live in `controls.ini` beside the executable. The selected game's full path is saved in `game-path.txt`. If the folder is not writable, the current session can work, but settings may not be saved.
- To select another game location, exit the helper, delete its `game-path.txt`, and reopen it. A missing game file or invalid saved path also prompts for a new selection.
- To reset the layout, exit and delete `controls.ini`, or choose **重置位置和大小** (Reset position and size) from the tray menu.
- If another version is running, exit it normally before trying this one. The helper allows only one instance and does not close an existing instance for you.
- To uninstall, exit normally and delete the extracted folder. There are no services, drivers, registry changes, or startup entries to remove, and no game files to restore.
- Before sharing your helper folder, remove `controls.ini`, `game-path.txt`, and any logs produced during manual developer testing. These are personal settings and records.

## Input behavior

The helper installs a Windows mouse hook while running. It intercepts left clicks on its own panel and simulates W / S while the selected game window is in the foreground. It matches the **full executable path**, rather than just the process name. File selection checks the filename and existence, not authenticity; choose your own trusted game installation.

A short click lasts about 100 milliseconds. Losing focus or moving off the button cancels it immediately. A continuous hold is released after 30 seconds; release and press again to continue. Key release events are global Windows input events and may conflict with physical W / S keys or other remapping tools. Rapid focus changes are subject to Windows timing, so perfect input isolation cannot be guaranteed.

A separate release-guard process normally exits with the helper. If the helper ends unexpectedly, it attempts to release the W / S keys owned by the helper; it cannot cover every system failure. If a key feels stuck after exit, try pressing and releasing that key manually.

The helper itself has no networking, telemetry, or automatic update logic. **启动游戏** (Launch game) calls your installed Steam client through a Steam URI. There is no configurable remapping, automatic walking, universal game support, translation patch, or bundled game content.

## Compatibility and validation

Target environment: Windows 10 / 11 x64 with .NET Framework 4.8 / 4.8.1. Run both the game and helper at the same normal permission level. Windowed or borderless mode is recommended. Exclusive fullscreen, mismatched permissions, or game updates may affect visibility and input. ARM64 is outside the current validation scope.

Offline self-tests ran on one Windows 11 x64 computer. They cover the key state machine, Windows input structure sizes, game-path selection, cancellation, persistence, and full-path matching. These tests do not install mouse hooks, create a game window, or send real input; they write only the requested report and temporary test files beside it.

**Not tested in this release round:** native file-picker interaction, real mouse hooks, actual key injection, Steam launch, crash-guard recovery, exclusive fullscreen, multiple monitors / DPI settings, Windows 10, other system languages, or actual gameplay. Feedback and tests from the earlier personal version do not substitute for this candidate's validation.

The experimental executable is unsigned. If Windows reports an unknown publisher, check the source and SHA-256 checksum; disabling system security is not required. See [BUILD.en.md](BUILD.en.md) for source-build instructions.
