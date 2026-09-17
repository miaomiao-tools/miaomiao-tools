# Changelog

[中文](CHANGELOG.md) · [Home](../README.en.md)

## 1.2.0-experimental.1 — 2026-09-17

First public experimental release, prepared from the existing 1.2.0 feature set: full connectivity dashboard, resizable and pinnable mini panel, history, trends, exports, light transport checks, and manual single-service HTTP checks.

- Preserves durable per-host cooldowns, failure backoff, certificate validation, and global single-instance behavior.
- Uses a separate fallback data directory and public-window activation identity to avoid reading older fallback data or activating an older app window.
- Stops immediately if an explicit diagnostic data directory is unwritable, without falling back to personal storage.
- Disables the old automatically networked `--smoke` entry point while retaining local mock checks.
- Adds Chinese and English documentation, standalone build scripts, explicit package manifests, MIT licensing, and upstream .NET notices.

The UI remains Chinese. This release checks transport and manual HTTP responses, not account or model functionality.
