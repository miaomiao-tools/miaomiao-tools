# Validation history

[中文](VALIDATION.md) · **English**

## .3 candidate · 2026-09-17

**Display and basic mouse movement were confirmed in the current real game.** The maintainer directly observed the panel above the game. The user then explicitly confirmed forward/backward movement and stopping after releasing or moving off the button. The maintainer did not send game movement input or interrupt continued play.

### Cause and fix

The selected full game path matched the running process, and the foreground handle was correct. The old helper was visible, not minimized or cloaked, entirely within the game’s bounds, and at the same DPI. Its native TOPMOST flag was missing, its window was behind the game, and a hit test at the panel’s center found the game.

Read-only inspection of the installed .NET Framework code confirmed that Opacity updates can rewrite extended styles from CreateParams when the layered state changes. The old helper’s CreateParams did not retain the TopMost flag, allowing the managed property to remain true while the native flag was lost. Running from an archive viewer’s temporary directory was observed, but was not the cause of this display failure.

The fix retains the flag in CreateParams and restores only the helper’s layer when shown or returning to the game. It checks the native flag every 500 milliseconds and restores again only if lost. Native calls preserve bounds, focus, and owner ordering. Hidden panels are not shown by maintenance, and selection/exit suspends it. Full-path matching, foreground checks, input safeguards and the guard process retain their existing behavior.

Read-only samples after the fix found TOPMOST=true, the helper above the game, and a center hit on the helper, while the game remained foreground. This confirms the current computer and game state; fullscreen dimensions alone do not establish exclusive-fullscreen coverage. Native layer reference: [Microsoft SetWindowPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos).

### Coverage

- The candidate, freshly extracted portable executable, and source-package rebuild each passed **68 offline checks**. These cover existing input/selection safeguards plus slash normalization, repeated separators, shortcut rejection, native topmost loss, retry after failure, throttling, suspension while hidden/selecting, and visibility states.
- **24 packaging regressions** passed: Unicode/space output paths, source rebuilds, separate link closure for both archives, five essential portable root files, source-only development records, dirty output exclusion, current documents, and rejection of missing English/images or stale/tampered code and binaries.
- The executable tested by the user is byte-for-byte the final portable executable. Documentation was repackaged without recompilation.
- Existing illustration and genuine training-window screenshot bytes are unchanged. The screenshot is not evidence for this round’s game validation. The portable archive excludes personal paths, layout, logs, and developer test artifacts.

**Not tested live in this round:** switching apps or exiting while holding, the continuous 30-second limit, crash-guard recovery, native reselection-failure interactions, a second real installation, exclusive fullscreen, switching monitors, Windows 10, other system languages, or ARM64. Offline logic coverage of focus changes and input safeguards does not establish every native interaction as passed. The .2 entries below are historical.

## .2 previous candidate


Target environment: Windows 10 / 11 x64 with .NET Framework 4.8 / 4.8.1. Run both the game and helper at the same normal permission level. Windowed or borderless mode is recommended. Exclusive fullscreen, mismatched permissions, or game updates may affect visibility and input. ARM64 is outside the current validation scope.

Offline self-tests ran on one Windows 11 x64 computer. They cover the key state machine, Windows input structure sizes, game-path selection, cancellation, persistence, and full-path matching. These tests do not install mouse hooks, create a game window, or send real input; they write only the requested report and temporary test files beside it.

These documents describe **0.1.0-experimental.2, a local unpublished candidate**. Published .1 attachments remain unchanged. New regressions cover switching while holding a key, blocked input during selection, save failures preserving the old setting, same-name installations in different folders, and restarting with the new saved target. Chinese / English guides and both presentation images are included in the candidate; the app interface remains Chinese.

The maintainer tested the **real --demo helper training window**: forward/backward clicks with paired W/S down/up events, moving off a button during a short drag, switching apps while idle and returning, closing the training window with the helper and guard exiting, and reopening for another pair of W/S clicks. The short drag released W after about 22 milliseconds; it is not a long-hold test. The screenshot also shows this training window, **not the game, and does not establish gameplay validation**.

The final executable was also tested after fresh extraction: --demo clicks/releases, dragging off S (released after about 6 milliseconds), zero scene clicks, and normal exit.

Using an **isolated configuration preloaded with a valid game location**, the maintainer opened Reselect game in normal mode. Escape cancellation preserved the configuration bytes. Selecting the same installed game through the native filename field saved successfully with no leftover temporary file; after exit and restart, the panel opened without asking again. This does not cover first-run selection or switching to a second real installation.

**Not completed in this round:** first-run selection, switching to another real installation, native invalid-choice/save-failure interactions, long holds, switching apps or exiting while holding, Steam launch, crash-guard recovery, exclusive fullscreen, multiple monitors / DPI, Windows 10, other system languages, or actual gameplay. The first-run picker existed, but the desktop test tool could not enumerate its hidden-owner window; that limitation is neither a pass nor a program deadlock. Offline regressions cover invalid choices, save failures, reselection while holding, and same-name targets in different folders.

The experimental executable is unsigned. If Windows reports an unknown publisher, check the source and SHA-256 checksum; disabling system security is not required. See [BUILD.en.md](BUILD.en.md) for source-build instructions.
