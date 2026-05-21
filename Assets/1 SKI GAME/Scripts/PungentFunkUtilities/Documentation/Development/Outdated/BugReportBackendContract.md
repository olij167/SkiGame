# PungentFunk Utilities Bug Report Backend Contract

Status: scaffolded contract. The Unity editor package does not submit live reports by default.

## Endpoint

`POST https://pungentfunk.net/api/bug-report`

Future backend responsibilities:

- validate the payload schema and payload size;
- rate-limit incoming reports;
- store the report server-side;
- send an email notification;
- send a Discord notification;
- optionally create a private tracker or GitHub issue later.

## JSON Payload Example

```json
{
  "schemaVersion": "1.0",
  "title": "Issue: Generated scripting entry wording",
  "details": "The generated entry is unclear and should be rewritten before release.",
  "category": "HelpContentIssue",
  "severity": "Normal",
  "utilityId": "help-browser",
  "sectionId": "generation-dashboard",
  "topicId": "generation-dashboard",
  "contextLabel": "Help Coverage review row",
  "contextPath": "help-browser/generation-dashboard/generation-dashboard",
  "sourceWindow": "Help Browser",
  "helpTab": "Generation Dashboard / GeneratedTooltipEntries",
  "packageVersion": "PungentFunk Utilities local editor package",
  "unityVersion": "2022.3.x",
  "editorPlatform": "Windows",
  "timestampUtc": "2026-05-10T00:00:00.0000000Z",
  "anonymousInstallId": "local-anonymous-id",
  "includeDiagnostics": true,
  "includeConsoleSummary": false,
  "contactEmail": "",
  "contactDiscord": "",
  "diagnosticsSummary": "Unity, platform, utility, topic, and source context.",
  "recentExceptionSummary": "Console summary not included.",
  "backendEndpoint": "https://pungentfunk.net/api/bug-report",
  "backendStatus": "Scaffolded / backend missing"
}
```

## Expected Response Example

```json
{
  "ok": true,
  "reportId": "pfr_20260510_000001",
  "stored": true,
  "emailNotificationQueued": true,
  "discordNotificationQueued": true
}
```

## Validation Checklist

- Accept only known `schemaVersion` values.
- Require non-empty `title`, `details`, `category`, `severity`, and `timestampUtc`.
- Validate category and severity enum values.
- Treat diagnostics and console summaries as opt-in.
- Limit payload size and strip unsafe markup before forwarding.
- Rate-limit by IP, anonymous install ID, and contact identity when present.
- Store the original payload plus server receipt metadata.

## Notification Notes

Email forwarding should happen on the server after validation and storage. Discord forwarding should also happen server-side and should avoid dumping oversized diagnostics directly into a channel.

## Security Notes

Do not put secrets in the Unity package. SMTP credentials, Discord webhook URLs, GitHub tokens, private tracker credentials, and signing keys belong only on `pungentfunk.net` or its server-side secret store.

The editor-side scaffold should send only to a configurable endpoint in a future pass, and only after the developer explicitly clicks submit.
