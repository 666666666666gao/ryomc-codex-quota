---
name: weekly-quota
description: Query the user's CPA upstream Codex weekly remaining percentage and reset time, or open the Ryomc quota dashboard inside Codex. Not New API wallet balance or the account signed into Codex itself.
---

# Ryomc upstream weekly quota

Announce that this skill reads upstream weekly quota without changing CPA settings.
Resolve `../../scripts/quota.mjs` relative to this SKILL.md directory (the script is at the plugin root's scripts/quota.mjs).
Use shell execution with Node.js 22 or later. Do not read the private configuration file into model context.

## Query

For ordinary users of the shared service, use `node <absolute-plugin-root>/scripts/reader.mjs`. It reads only the dedicated endpoint and read token from the user's `.config/ryomc-codex-quota/reader.json`. Never ask ordinary users for CPA management credentials. Do not automatically fall back to an administrator query if this fails.

For the administrator-only direct CPA connection, explicitly use the following command instead:

Run `node <absolute-plugin-root>/scripts/quota.mjs query`.
It prints only sanitized weekly quota JSON. Report remaining percentage, reset time in Asia/Shanghai, and queried time. Null means unknown, never zero. A successful quota read does not establish model request health.
Do not substitute the built-in signed-in-account usage tool, local auth.json, New API wallet balance, or screenshot numbers.

## Dashboard and first setup

For the Windows floating bar, run `windows/build.ps1` in this plugin's directory to build `artifacts/RyomcQuota.exe` if it does not exist, then launch that executable. It is an independently running window, not a native Codex status bar. On Windows, use `Start-Process -WindowStyle Hidden` to start the helper; the application controls when its own overlay is shown. Requires reader.json with an HTTPS endpoint and a dedicated read-only token. Only launch when the user requests it. Do not add autostart or change security settings. See server/DEPLOYMENT.md.

The dashboard below is the separate administrator-only mode, not the normal user setup:

Run `node <absolute-plugin-root>/scripts/quota.mjs serve` as a retained execution session. Do not set up a recurring automation unless requested.
The command prints a loopback URL containing an ephemeral access token. Use the available Codex open-panel tool to open that exact URL as a browser target in the right panel. Do not post it to GitHub or other users. Keep the serving session alive while the dashboard is needed.
If the panel cannot be opened, return its local URL and accurately describe the limitation. Do not claim a native status-bar integration.
If configuration is missing, ask the user to enter their CPA Management Center base URL and management key in the local dashboard. This is NOT their New API model API key. Never ask them to paste secrets into chat.
The account index and ChatGPT account ID can be omitted only for a single account or when the returned metadata supplies the ID. With multiple Codex auth records, require an explicit index. Never select an arbitrary account.
Do not automate the credential-entry form. The user saves it themselves. Then query and verify live output.

## Boundaries

The client only lists CPA auth metadata and proxies a fixed GET to the upstream usage endpoint. No resets, model probes, account changes, proxy edits, or scheduler modifications. Keep upstream error bodies, auth headers, OAuth tokens, identifiers and management keys out of answers.
The dashboard has manual refresh, no background polling. Every success is a timestamped snapshot. After a failed refresh it removes previous values.
