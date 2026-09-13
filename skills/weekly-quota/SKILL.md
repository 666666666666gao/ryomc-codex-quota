---
name: weekly-quota
description: Query the user's CPA upstream Codex weekly remaining percentage and reset time, or open the Ryomc quota dashboard inside Codex. Not New API wallet balance or the account signed into Codex itself.
---

# Ryomc upstream weekly quota

Announce that this skill reads upstream weekly quota without changing CPA settings.
Resolve `../../scripts/quota.mjs` relative to this SKILL.md directory (the script is at the plugin root's scripts/quota.mjs).
Use shell execution with Node.js 22 or later. Do not read the private configuration file into model context.

## Query

For ordinary users, use `node <absolute-plugin-root>/scripts/reader.mjs`. Install the lockfile dependencies with `npm ci` in that plugin directory if needed. It reads the saved user-level Codex config.toml (CODEX_HOME or ~/.codex), the selected provider's base_url, model and default profile, and its env_key variable or auth.json OPENAI_API_KEY. It sends the API key only to that same HTTPS base URL plus /quota/weekly, with redirects disabled. Never load private file contents into model context. Never ask ordinary users for CPA management credentials.

The site must deploy the matching quota adapter; a URL does not expose arbitrary upstream accounts. Do not substitute another site's quota, built-in signed-in-account quota, or wallet balance. Runtime CLI/profile/project overrides are not read. Official OAuth-only login is not a relay configuration. Do not change Codex config/auth to make the reader work.

Only when explicitly using the separately configured shared read-only mode, run `node <absolute-plugin-root>/scripts/reader.mjs --shared`. This reads reader.json; never automatically fall back to it or to the administrator query.

For the administrator-only direct CPA connection, explicitly use the following command instead:

Run `node <absolute-plugin-root>/scripts/quota.mjs query`.
It prints only sanitized weekly quota JSON. Report remaining percentage, reset time in Asia/Shanghai, and queried time. Null means unknown, never zero. A successful quota read does not establish model request health.
Do not substitute built-in signed-in-account quota, wallet balance or screenshot numbers for upstream results. The automatic client may read only the API key from local auth.json, never its OAuth credentials.

## Dashboard and first setup

For the Windows floating bar, run `windows/build.ps1` in this plugin's directory to build `artifacts/RyomcQuota.exe` if needed, then launch it with Tommy.dll alongside. This independent window is not a native Codex status bar. Use `Start-Process -WindowStyle Hidden`; the application controls when its own overlay is shown. Default mode reads the saved Codex configuration every refresh. Explicit --shared mode requires the separate reader.json. Only launch when requested. Do not add autostart or change security settings. See server/DEPLOYMENT.md.

The dashboard below is the separate administrator-only mode, not the normal user setup:

Windows v0.4.0: when auth.json is missing, do not create/overwrite it or inspect the system's Codex credentials. Direct the user to the overlay's connection settings button, or launch `RyomcQuota.exe --settings` when requested. Base URL/model prefill works without auth.json. The user enters the site's API key themselves in the masked field; Save and Test stores only this app's Windows Credential Manager entry and tests the endpoint. Never ask for the secret in chat or automate real credential entry. Restore Automatic removes only this app's entry after confirmation. Normal launch prefers an explicitly saved independent connection; failed queries do not fall back. This setting is Windows-overlay-only; the Node reader does not consume the Windows credential entry.

Run `node <absolute-plugin-root>/scripts/quota.mjs serve` as a retained execution session. Do not set up a recurring automation unless requested.
The command prints a loopback URL containing an ephemeral access token. Use the available Codex open-panel tool to open that exact URL as a browser target in the right panel. Do not post it to GitHub or other users. Keep the serving session alive while the dashboard is needed.
If the panel cannot be opened, return its local URL and accurately describe the limitation. Do not claim a native status-bar integration.
If configuration is missing, ask the user to enter their CPA Management Center base URL and management key in the local dashboard. This is NOT their New API model API key. Never ask them to paste secrets into chat.
The account index and ChatGPT account ID can be omitted only for a single account or when the returned metadata supplies the ID. With multiple Codex auth records, require an explicit index. Never select an arbitrary account.
Do not automate the credential-entry form. The user saves it themselves. Then query and verify live output.

## Boundaries

The client only lists CPA auth metadata and proxies a fixed GET to the upstream usage endpoint. No resets, model probes, account changes, proxy edits, or scheduler modifications. Keep upstream error bodies, auth headers, OAuth tokens, identifiers and management keys out of answers.
The dashboard has manual refresh, no background polling. Every success is a timestamped snapshot. After a failed refresh it removes previous values.
