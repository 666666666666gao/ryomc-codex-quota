# Server / Windows reader

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

Run `windows/configure.ps1` and enter the HTTPS endpoint and read-only token (masked input). Or manually create `%USERPROFILE%\.config\ryomc-codex-quota\reader.json`:

```json
{
  "endpoint": "https://your-service.example/quota/weekly",
  "readToken": "YOUR_READ_ONLY_TOKEN"
}
```

Double-click `RyomcQuota.exe`. It is a separate window, not native Codex UI. It appears only while the foreground process is ChatGPT or Codex, follows that window near its lower-right edge, and hides for other foreground apps. This matches this Windows Codex build's process name; it can also match a separate ChatGPT desktop app with the same process name.

Drag the text to reposition for the current run; click ↻ to refresh or × to exit. It queries every two minutes while visible; there is no Windows startup entry or scheduled task. Reader configuration contains a limited read token, never a management key. Do not commit it. Keep your user's directory access restricted.

Run `node scripts/reader.mjs` to query the same read-only endpoint from a Codex skill. It does not read the administrator config.json. The original administrator-only dashboard remains available separately.

## Checks / rollback

- No Authorization header: 401, no quota data.
- Wrong token: 401, no quota data.
- Valid token: four-field weekly summary or a generic 503.
- Unrelated URL: 404.
- Rollback: stop/disable the dedicated service and remove only its Nginx location, validate Nginx configuration, then reload. Do not modify existing New API or CPA proxy paths.
