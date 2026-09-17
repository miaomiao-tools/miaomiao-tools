# Privacy and local data

[中文](PRIVACY.md) · [Home](../README.en.md)

AI Pulse requires no login, reads no browser cookies, API keys, or agent accounts, and sends no model-generation requests. The application has no telemetry, advertising, or cloud-sync feature.

After you request a check or enable polling, the app resolves and connects to built-in or custom endpoints using your selected route. Light checks stop at the transport layer; a manual HTTP check sends one unauthenticated request. DNS providers, target servers, and proxies may still see connection origins and destination domains. This is not an anonymity service.

The network overview reads local adapter and gateway information. Results record times, URLs, route mode, phase timings, status, connection IPs, response details, and cooldown deadlines. History retains up to 240 rounds. Settings, custom endpoints, history, cooldowns, and window preferences stay in the `data` directory beside the EXE. If the program directory is unwritable, this public build uses `%LOCALAPPDATA%\MiaomiaoTools\AIPulse` and displays a notice. An explicitly supplied diagnostic directory fails closed instead of falling back to personal data.

A startup failure may create `startup-error.txt` beside the program. Manually exported CSV / JSON files and diagnostic reports may include URLs, IPs, paths, or other environment details. These files are not uploaded automatically. Review and redact them before sharing.

The public portable and source packages contain no personal settings, history, cooldowns, logs, or accounts. Data created during use belongs to you. Clearing history does not clear cooldowns. Preserve the entire `data` folder when migrating or upgrading; see the [user guide](USAGE.en.md).
