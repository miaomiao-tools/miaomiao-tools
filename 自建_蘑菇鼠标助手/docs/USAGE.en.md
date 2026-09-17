# Usage details and limits

[中文](USAGE.md) · **English**

## Settings and recovery

- Layout settings live in `controls.ini` beside the executable. The selected game's full path is saved in `game-path.txt`. If the folder is not writable, the current session can work, but settings may not be saved.
- Choose **重新选择游戏…** (Reselect game) in Settings or the tray menu. The helper releases its held W / S and pauses input before opening the picker. Cancellation, an invalid file, or a failed save preserves the previous target and configuration. It switches only after saving successfully; click a movement button again to move. A missing game file or invalid saved path also prompts on startup.
- If a key release fails, the picker does not open and release ownership is retained for retry. Demo mode stays connected to its training window; reselection is disabled there.
- To reset the layout, exit and delete `controls.ini`, or choose **重置位置和大小** (Reset position and size) from the tray menu.
- If another version is running, exit it normally before trying this one. The helper allows only one instance and does not close an existing instance for you.
- To uninstall, exit normally and delete the extracted folder. There are no services, drivers, registry changes, or startup entries to remove, and no game files to restore.
- Before sharing your helper folder, remove `controls.ini`, `game-path.txt`, and any logs produced during manual developer testing. These are personal settings and records.

## Input behavior

The helper installs a Windows mouse hook while running. It intercepts left clicks on its own panel and simulates W / S while the selected game window is in the foreground. It matches the **full executable path**, rather than just the process name. File selection checks the filename and existence, not authenticity; choose your own trusted game installation.

A short click lasts about 100 milliseconds. Losing focus or moving off the button cancels it immediately. A continuous hold is released after 30 seconds; release and press again to continue. Key release events are global Windows input events and may conflict with physical W / S keys or other remapping tools. Rapid focus changes are subject to Windows timing, so perfect input isolation cannot be guaranteed.

A separate release-guard process normally exits with the helper. If the helper ends unexpectedly, it attempts to release the W / S keys owned by the helper; it cannot cover every system failure. If a key feels stuck after exit, try pressing and releasing that key manually.

The helper itself has no networking, telemetry, or automatic update logic. **启动游戏** (Launch game) calls your installed Steam client through a Steam URI. There is no configurable remapping, automatic walking, universal game support, translation patch, or bundled game content.

## Compatibility

Windows 10 / 11 x64 with .NET Framework 4.8 / 4.8.1. Run both programs normally, at the same permission level. Exclusive fullscreen, different permissions, game updates, and other system configurations may affect visibility or input. ARM64 is untested.

Extract the entire ZIP to a writable folder before opening the EXE. Running it directly inside an archive viewer can put it in a temporary folder and lose saved settings. This does not establish the cause of any display failure.

This is **0.1.0-experimental.3, an experimental release**. Live display was observed on the current computer; the user confirmed forward/backward movement and stopping after release or moving off the button. Long holds, focus changes while holding, and crash recovery were not tested live in this round. The screenshot shows the real training window, not the game. Detailed build and validation records are available in the source package. The app is unsigned; check its source and supplied SHA-256 checksums. Do not disable system security.

[Back to the quick start](../README.en.md) · [License and notices](NOTICE.md)
