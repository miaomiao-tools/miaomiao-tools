# Changelog

[中文](CHANGELOG.md) · **English**

## 0.1.0-experimental.3 · Experimental release · 2026-09-17

- Uses the same EXE accepted in the .3 candidate checks below. Updated bilingual guides, download links, and upgrade instructions without recompilation.
- Since public .1: fixed the panel being covered, added game reselection, and simplified portable documents. The user confirmed current real-game display, forward/backward movement, and stopping on release/move-off.
- Long holds, switching/exiting while holding, crash recovery, and other environments remain untested live. Historical .1 is retained; .2 was a local candidate only.

## 0.1.0-experimental.3 · Pre-release local candidate record

- Fixed opacity style updates losing native topmost state and allowing the game to cover the panel. Layer restoration preserves game focus. Live display was observed; the user confirmed forward/backward movement and stopping on release/move-off.
- Portable root reduced to five essential files; advanced usage and bilingual notices are in docs/. Developer and history documents are source-only.
- Consolidated duplicated release and license-status documents into the changelog and notices; each package has its own link checks.
- Published .1 downloads and tags remain unchanged.

## 0.1.0-experimental.2 · Local candidate, not published

- Added **重新选择游戏…** (Reselect game) to Settings and the tray menu. Release and pause input first; switch only after saving, preserving the old target and setting on cancellation or failure.
- Save paths atomically, clear the previous target cache on success, and retain exact full-path matching.
- Fixed missing English documents and stale build-folder copies. Both ZIPs include bilingual guides, the mushroom illustration, and a real helper training-window screenshot.
- Support separate output folders, Unicode and spaces; validate build provenance, file allowlists, relative links, and images.
- Added clear published .1 Windows ZIP and issue links to all four home pages; distinguish local .2 from public .1. App UI remains Chinese.
- No publication or replacement of existing tags or attachments in this round. Offline regressions and training-window interactions are not gameplay tests.

## 2026-09-17 · Online documentation and presentation

- Added Chinese / English introductions, usage guides, technical details, build instructions, third-party notices, and release notes.
- Improved the collection's short description and topics with accurate game and purpose keywords.
- This is an online documentation update. The original version tag, program, and download attachments are unchanged.

## 0.1.0-experimental.1

The first standalone experimental release candidate, based on the earlier personal-use version:

- Replaced the fixed local game path with first-run file selection while preserving full-path foreground-game matching.
- Locates build tools from the current Windows installation and adds public version metadata, requirements, privacy notes, and third-party notices.
- Packages an explicit file list, excluding personal settings, historical logs, debug symbols, and game patches.
- Preserves W / S input behavior, short clicks, release handling, focus protection, the 30-second safeguard, and the single-instance limit.

This version belongs to a separate public experimental series. It does not imply that the earlier personal v1.0 underwent the same public-release validation.
