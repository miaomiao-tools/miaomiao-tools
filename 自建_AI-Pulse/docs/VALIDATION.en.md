# Validation scope

[中文](VALIDATION.md) · [Build guide](BUILD.en.md)

Version: 1.2.0-experimental.1. Target: Windows x64. UI language: Chinese.

- 104 logic, safety, and isolated-storage checks passed.
- 32 mini-mode checks passed, covering shared state, mode switching, layout constraints, and simulated rendering.
- Automated checks use local loopback services and isolated temporary directories, with no live AI requests, account validation, or paid model calls.
- Human GUI acceptance passed for normal launch in a new empty directory, no personal data, polling off, and full → mini → full switching.
- With all 12 public endpoints disabled and only a `127.0.0.1` test service enabled, F5 produced 1 TCP connection and 0 HTTP requests. One manual HTTP check brought totals to 2 TCP connections and 1 HTTP request.
- Repeating the HTTP check, including after normal exit and relaunch, was blocked by durable cooldowns; counts stayed at 2 / 1. Relaunch restored mini mode, recent history, and cooldowns. Polling remained off.
- The included real first-launch mini screenshot was captured before any check. Full-dashboard capture was limited by the test environment's multi-monitor capture area; comprehensive cross-monitor and DPI layout coverage is not claimed.
- Rebuilding the source ZIP in an independent directory produced a byte-identical EXE. Package files and image links were checked against explicit manifests.

This does not establish coverage of every monitor arrangement, DPI combination, proxy, or network exit. A successful connectivity check does not demonstrate that Codex or another agent can complete a task. Raw diagnostic files can include local environment details and are not redistributed.
