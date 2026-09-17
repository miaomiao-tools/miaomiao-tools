# User guide

[中文](USAGE.md) · [Home](../README.en.md)

## Routine checks

A fresh launch shows an empty waiting state and does not probe services automatically. Click 「轻量检测」 (light check), or press **F5**, to check once. The default router/gateway mode uses normal Windows routing, suitable for a router-managed proxy. It does not change system proxies, routes, or gateway settings.

The full dashboard also supports system/environment proxies and an explicit HTTP or SOCKS5 proxy without credentials. Match the route to the agent you use: different applications may take different paths. Custom endpoints accept HTTP / HTTPS URLs without credentials, query strings, or fragments.

A light check observes DNS, TCP, and, for HTTPS, a TLS handshake with certificate validation enabled. HTTP endpoints have no TLS stage. With a proxy, some timings describe the connection to that proxy; details identify the dialed host and scope. HTTP CONNECT negotiation still communicates with the proxy.

For a service response, select one entry in the full dashboard and request a manual HTTP check. This checks only that entry, does not follow redirects, does not attach cookies or credentials, and does not request model generation.

## Mini panel on a second screen

- Press **Ctrl+M** to switch between the dashboard and mini panel, or use the mini / expand buttons.
- Drag the title area to move the panel and its edges to resize. The service list scrolls.
- **Ctrl+T**, or the pin button, toggles always-on-top. Position, size, mode, and pin state are remembered.
- Select an entry for a short explanation, hover for the full tooltip, or double-click to expand the dashboard.
- Automatic polling runs no more often than every **5 minutes** and starts off after each launch. Both panels share one task and timer; switching does not trigger another probe.
- Closing the window exits the app and stops polling. `--mini` and `--full` select a mode; a normal launch restores the previous mode.

## Results and protection

| Result | Meaning |
| --- | --- |
| Connected | The transport stages tested were reachable, not proof of successful login or generation |
| Authentication required / 401 | An HTTP response arrived; this tool does not test accounts |
| 403 / challenge / 451 | May indicate verification, permissions, or access restrictions; insufficient to establish an IP ban |
| 429 | The service is rate-limiting; wait for the protection period |
| 404 / redirect | A response arrived; application functionality remains unverified |
| Timeout / connection or certificate error | Review the failed stage, route, and details; certificate validation is never bypassed |
| Protection period | This attempt skipped the host without connecting to it |

Per host, light checks are spaced by at least **60 seconds** and manual HTTP checks by at least **10 minutes**. Connection failures back off progressively. A 403, 429, 451, or recognized challenge pauses the host; repeated restrictions can extend the pause to 24 hours. A longer server `Retry-After` is respected. Reservations are saved before networking. Restarting, changing routes or panels, and clearing history do not reset cooldowns; probes stop if protection state cannot be saved reliably.

These controls reduce traffic; they cannot guarantee that a provider will never restrict access. Private browsing isolates browser sessions but does not change your public IP or guarantee avoidance of restrictions; [Chrome's official explanation](https://support.google.com/chrome/answer/95464?hl=en) also notes that websites and network providers may still observe activity. This tool does not read browser sessions. Prefer infrequent light checks for routine monitoring.

## Data, upgrades, and recovery

Extract everything into a writable directory. Data normally lives beside the EXE in `data`: `settings.json`, `history.json`, `cooldowns.json`, and `window.json`. Exports may contain custom URLs and connection IPs; review them before sharing.

To upgrade, exit the old version normally, back up and copy its **entire `data` folder** beside the new EXE. A new folder does not automatically inherit data or protection periods. If an earlier run reported an unwritable program directory, migrate from the actual data directory shown in that message. To roll back, exit the new app and use the retained old program with the complete pre-upgrade data backup.

If another instance exists, a new instance does not start probing. This public build asks you to use or normally exit an older running version; it never forcibly closes it. For saving or protection-state errors, preserve the original files and check permissions and disk space. Do not delete cooldown files to clear a pause.
