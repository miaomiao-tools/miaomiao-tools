# 🍄 Mushroom Mouse Helper

[中文](README.md) · **English**

**A tiny mushroom, a mouse, and two ways to wander.**

This is a **vibe coding project** for **Shroom and Gloom**. It adds two floating mouse buttons — **forward** and **backward** — to help with mouse-only play. Park the little panel wherever it feels comfy.

**Experimental · Windows 10 / 11, 64-bit · Free · MIT license**

The current app interface is in Chinese. This guide includes the labels you will see; the executable is named `蘑菇鼠标助手.exe`.

## What does it do?

| Mouse action | What happens |
| --- | --- |
| Click forward — 前进 | A short W press moves you forward |
| Click backward — 后退 | A short S press moves you backward |
| Hold either button | Keeps the corresponding key held |
| Release or move off the button | Stops movement; very short clicks last about 0.1 seconds |
| Switch to another app | Releases the key and hides the panel |

It only handles **W / S**. Keep using the game's own mouse controls for everything else. If a screen needs another keyboard key, the helper cannot replace it. A continuous hold is released after 30 seconds; release the mouse button and press again to continue.

## Four little steps

1. **Download and extract:** open the [release page](https://github.com/miaomiao-tools/miaomiao-tools/releases/tag/mm-001-v0.1.0-experimental.1), download `ShroomMouse-0.1.0-experimental.1-win-x64.zip`, and extract the whole archive into a folder of your choice.
2. **Pick your game:** open `蘑菇鼠标助手.exe`. On first launch, select `Shroom and Gloom.exe` from your installed game folder. Steam's **Browse local files** option can help you find it. Cancelling closes the helper.
3. **Go exploring:** click **启动游戏** (Launch game), or start the game from Steam yourself. Return to the game window to use the buttons. Windowed or borderless mode is recommended.
4. **All done:** click **×** at the top right of the panel, or right-click the green double-arrow icon in the notification area and select **退出助手** (Exit helper).

Drag the panel's top area to move it. **设置** means Settings; it adjusts size and opacity. If the panel goes missing, click the green double-arrow tray icon or use **重置位置和大小** (Reset position and size).

## A few small notes

- You need your own installation of [Shroom and Gloom](https://store.steampowered.com/app/3271280/Shroom_and_Gloom/). This independent helper is not affiliated with the game developers and includes no game files or translation patch.
- This is an experiment. Offline checks passed, but **this release candidate has not been retested in the actual game**. Fullscreen mode, game updates, and different computers may affect it.
- Run the game and helper normally, without elevation. It needs .NET Framework 4.8 / 4.8.1; see [Microsoft's installation guide](https://learn.microsoft.com/en-us/dotnet/framework/install/) if it is missing.
- Run only one copy of the helper. Avoid holding physical W / S keys at the same time.
- It does not change game files or saves, or start automatically with Windows. To uninstall, exit the helper and delete its extracted folder.

See [usage details](DETAILS.en.md) for settings, recovery, and known limits. The executable is unsigned; use the SHA-256 checksums on the release page to verify your download.

---

Made by **miaomiao tools**. Little tools, growing slowly 🌱

[MIT license](LICENSE) · [Third-party notices](NOTICE.en.md) · [Changelog](CHANGELOG.en.md) · [Building from source](BUILD.en.md)
