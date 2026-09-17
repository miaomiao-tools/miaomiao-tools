# 🍄 Mushroom Mouse Helper

[中文](README.md) · **English**

A **vibe-coded** pair of **forward / backward** mouse buttons for **Shroom and Gloom**. It only adds W / S; use the game’s own controls for everything else.

![A tiny mushroom hugging a mouse](assets/shroom-mouse-banner.png)

**Windows 10 / 11 x64 · .NET Framework 4.8 / 4.8.1 · Free · MIT**

**0.1.0-experimental.3 · Experimental.** The panel is back above the game, and it can find your game again after a move.

[Windows ZIP (.3)](https://github.com/miaomiao-tools/miaomiao-tools/releases/download/mm-001-v0.1.0-experimental.3/ShroomMouse-0.1.0-experimental.3-win-x64.zip) · [Release page](https://github.com/miaomiao-tools/miaomiao-tools/releases/tag/mm-001-v0.1.0-experimental.3) · [Report an issue](https://github.com/miaomiao-tools/miaomiao-tools/issues)

## Quick start

1. **Extract the whole ZIP** to your own folder, then open `蘑菇鼠标助手.exe`. Do not run it directly inside an archive viewer.
2. On first launch, select your installed `Shroom and Gloom.exe`; Steam’s **Browse local files** can help. Cancelling exits.
3. Start the game and return to its window. Click **前进** (Forward) / **后退** (Backward) for a short W / S press, or hold to keep moving. Releasing, moving off the button, or switching apps releases the key.
4. Click the panel’s **×**, or choose **退出助手** (Exit helper) from the green double-arrow tray menu.

**Upgrading:** exit the old helper normally, then extract the complete new ZIP to a new folder. To keep your selected game and layout, copy `game-path.txt` and `controls.ini` from the old folder to the new one. Keep these personal files on your own computer; do not share them with the folder.

Drag the top to move the panel. **设置** (Settings) changes size and opacity or opens **重新选择游戏…** (Reselect game). Movement stops before selection; cancellation, an invalid file or failed save preserves your previous game. If the panel is missing, use **显示 / 调整按钮** (Show / adjust) or **重置位置和大小** (Reset position and size) in the tray menu.

![The real helper training window](assets/usage-screenshot.png)

Captured from the running helper’s training window: **not the game or proof of gameplay testing**. The app interface is currently Chinese.

A short click lasts about 0.1 seconds. Continuous holds stop after 30 seconds; release and press again to continue. Experimental: exclusive fullscreen and different environments may affect display or input. You need your own game. This unofficial helper includes no game or patch and does not modify game files or saves.

In the current real game, panel display was confirmed; the user confirmed forward/backward movement and stopping on release or moving off the button. Other environments and long holds remain unverified.

[Usage and recovery](docs/USAGE.en.md) · [MIT license](LICENSE) · [Origins and notices](docs/NOTICE.md)

Made by **miaomiao tools**. Little tools, growing slowly 🌱

[Previous release .1](https://github.com/miaomiao-tools/miaomiao-tools/releases/tag/mm-001-v0.1.0-experimental.1)
