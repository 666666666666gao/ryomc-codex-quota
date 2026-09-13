# Server / Windows reader (v0.3.0)

## Automatic Codex configuration mode

The default Windows/Node reader uses the existing Codex API key, never an administrator credential. It appends `/quota/weekly` to the configured HTTPS Base URL. For a `/v1` Base URL expose the exact path `GET /v1/quota/weekly`, forwarding both Authorization and X-Quota-Model to this service. Disable public caching and redirects. Do not expose the collector's private port publicly.

The New API adapter requires Docker CLI access to its PostgreSQL container (tested against the deployed New API v1.0.0-rc.31 schema). Add these fields to the private service config below:

```json
{
  "postgresContainer": "new-api-postgres",
  "postgresUser": "newapi",
  "postgresDatabase": "newapi",
  "channelId": 1,
  "cpaBaseUrl": "http://cli-proxy-api:8317"
}
```

Use your actual container/database/channel values. The service queries the token and user state, token model restriction, effective group, and enabled channel candidates for X-Quota-Model. Disabled/deleted keys and users are denied. Expired/exhausted tokens may perform this read-only query; no wallet charge or model request is made. Requests with IP-restricted tokens are denied rather than bypassing restrictions. Auto group, cross-group retry, an absent route or another candidate channel are not guessed. The selected CPA must have exactly one active Codex OAuth account and no alternate Codex API-key/OpenAI-compatible provider list.

This is a deliberately single-upstream adapter, not generic recursive tracing of arbitrary proxy sites or account pools. Clients of other sites require an equivalent same-origin endpoint implemented by their administrator. Never send another site's API key to Ryomc.

The executable reads CODEX_HOME/config.toml or ~/.codex/config.toml, including a saved default profile. It reads the selected provider's env_key or auth.json OPENAI_API_KEY. It does not read OAuth tokens, OS keychain credentials, project or per-thread overrides. Every refresh re-reads the saved configuration. See README for the exact limitations.

Windows users of automatic mode **skip configure.ps1 and reader.json**. Download the release, extract RyomcQuota.exe and Tommy.dll together, and launch the EXE. A TOML parser dependency is used because actual configurations contain nested tables, comments, literal/escaped strings and profiles. The build downloads the upstream Tommy 3.1.2 package with a pinned SHA-256 and includes its MIT license; no global .NET SDK installation is needed. The executable is unsigned.

## Optional original shared-token mode

The service is an optional administrator-deployed adapter. It reads one explicitly selected CPA auth file and CPA YAML configuration on the server; it does not need or expose the CPA management key. It uses the current per-account proxy when present, otherwise the global proxy. It never refreshes OAuth credentials or modifies CPA files.

Install Python 3, PyYAML and curl. Place `quota_service.py` in `/opt/ryomc-quota/`.
Create `/etc/ryomc-quota/config.json` with owner-only access, outside the repository:

```json
{
  "readToken": "GENERATE_A_RANDOM_TOKEN_AT_LEAST_32_CHARACTERS",
  "authFile": "/your/private/cpa/auths/selected-account.json",
  "cpaConfig": "/your/private/cpa/config.yaml",
  "bind": "127.0.0.1",
  "port": 8320
}
```

Run `python3 /opt/ryomc-quota/quota_service.py`. Configure your HTTPS reverse proxy to expose only `GET /quota/weekly`. For a containerized reverse proxy, bind to its private bridge gateway rather than opening the service on a public interface. Use an explicit hostname guard if a shared Nginx include serves multiple domains.

Requests need `Authorization: Bearer <readToken>`. This token grants access only to the shared weekly summary. It is not a CPA or New API key. Share it only with intended viewers; changing the server token and restarting revokes all copies. Individual per-user revocation is not implemented.

The service caches successful or failed lookups for 120 seconds, coalesces concurrent lookups, and never serves a previous success after a failed refresh. It returns only remainingPercent, resetAt, queriedAt and windowSeconds. A cache timestamp is the actual query time, not the HTTP response time. Raw errors and account metadata are not exposed.

## Windows

Run `windows/build.ps1` to produce `artifacts/RyomcQuota.exe` using Windows .NET Framework's C# compiler. No .NET SDK or third-party GUI library is required. The executable is unsigned.

Only for explicitly selected shared-token mode: run `windows/configure.ps1` and enter the HTTPS endpoint and read-only token (masked input). Or manually create `%USERPROFILE%\.config\ryomc-codex-quota\reader.json`:

```json
{
  "endpoint": "https://your-service.example/quota/weekly",
  "readToken": "YOUR_READ_ONLY_TOKEN"
}
```

Run `RyomcQuota.exe --shared` for this optional mode; double-click uses the new automatic mode. It is a separate window, not native Codex UI. It appears only while the foreground process is ChatGPT or Codex, follows that window near its lower-right edge, and hides for other foreground apps. This matches this Windows Codex build's process name; it can also match a separate ChatGPT desktop app with the same process name.

Drag the text to reposition for the current run; click ↻ to refresh or × to exit. It queries every two minutes while visible; there is no Windows startup entry or scheduled task. Reader configuration contains a limited read token, never a management key. Do not commit it. Keep your user's directory access restricted.

Run `node scripts/reader.mjs --shared` to query this optional shared-token endpoint from a Codex skill. It does not read the administrator config.json. Omitting --shared selects automatic Codex configuration instead. The original administrator-only dashboard remains available separately.

## Checks / rollback

- No Authorization header: 401, no quota data.
- Wrong token: 401, no quota data.
- Valid token: four-field weekly summary or a generic 503.
- Unrelated URL: 404.
- Rollback: stop/disable the dedicated service and remove only its Nginx location, validate Nginx configuration, then reload. Do not modify existing New API or CPA proxy paths.

For upgrading from v0.2.0, back up the collector, private config and custom Nginx include before replacing them. Keep `/quota/weekly` if old shared-token readers still use it. Add `/v1/quota/weekly` separately; test `nginx -t` before reload. Roll back to those three backups and restart only the quota service if needed.

New API mode checks: missing/invalid/disabled key -> 401; invalid model/restricted token -> 403; ambiguous route -> 409; supported valid key -> sanitized summary. Confirm both internal and public HTTPS access without printing keys. Cloudflare may block a particular test source even when the origin is healthy; do not disable site security just to run a test.
