# AI Pulse

[中文](README.md) · **1.2.0-experimental.1** · Windows x64 · **Chinese UI**

![Decorative AI Pulse illustration: a small desktop robot and network pulses](assets/ai-pulse-banner.png)

A little AI connectivity dashboard made with **vibe coding**. When your connection is unreliable, open it to see which service endpoints respond, or keep its mini panel on a second screen.

Built-in entries cover Codex / ChatGPT, Claude, Gemini, Copilot, Cursor, DeepSeek, OpenRouter, and related services. The full dashboard shows connection timings, history, trends, and exports. The mini panel supports dragging, resizing, always-on-top, and remembered window placement.

## Download and start

1. Download `AIPulse-1.2.0-experimental.1-win-x64.zip` from [this release](https://github.com/miaomiao-tools/miaomiao-tools/releases/tag/mm-002-v1.2.0-experimental.1).
2. **Extract the entire ZIP** into a new writable folder and open `AI Pulse.exe`. Intended for Windows 10 / 11 x64; .NET 8.0.31 is bundled.
3. Click 「轻量检测」 (light check). Press **Ctrl+M** for the mini panel. Enable 「自动巡检」 (automatic polling) only when needed; it starts off on every launch.

**Upgrading:** exit the old app normally, retain and copy its **entire `data` folder** beside the new EXE, then launch the new version. This preserves settings, history, cooldowns, and window preferences. A fresh directory does not inherit old cooldowns automatically. Do not erase data or change folders to bypass a protection period. The app uses a global single-instance guard.

## What a result means

Routine checks inspect DNS, TCP, and TLS without sending an HTTP request to the target website. The full dashboard also offers a manual HTTP check for one selected service. It does not log in, send API keys, or call paid models.

**Reachability does not prove model availability.** The app does not validate accounts, subscriptions, regional permissions, Codex sessions, streaming generation, or complete agent workflows. Responses such as 401, 403, and 404 are explained separately; one response cannot establish that an IP is blocked. Spacing, backoff, and durable cooldowns reduce repeated traffic but cannot guarantee freedom from provider restrictions.

This is an experimental release. Feedback is welcome in [Issues](https://github.com/miaomiao-tools/miaomiao-tools/issues); redact your addresses and network details before sharing. The banner is decorative artwork, not a connectivity test screenshot.

![Actual mini panel after first launch, with services awaiting checks and automatic polling off](assets/mini-screenshot.jpg)

Actual first-launch mini interface. No checks have run; this image does not establish service availability.

[User guide](docs/USAGE.en.md) · [Privacy and local data](docs/PRIVACY.en.md) · [Licenses and third-party components](docs/NOTICE.en.md)

Original project code and assets are [MIT licensed](LICENSE). `AIPulse-1.2.0-experimental.1-source.zip` includes standalone build instructions and scripts.
