Teams Presence Light

A lightweight, dependency-free solution to display your Microsoft Teams presence using a Tuya-controlled smart light.

📌 Overview

This project is the successor to my original Teams Presence Light, which relied on the PresenceLight application and Azure AD / Microsoft Graph API to retrieve Teams presence information.

Due to Microsoft authentication changes and PresenceLight’s inability to authenticate multi-tenant accounts created after October 2018, the original approach stopped working.

Instead of rebuilding the entire pipeline, I implemented a simpler local solution:

👉 Reading your presence from the local log of the new Microsoft Teams client
👉 Sending the detected state to an API running in Docker
👉 Updating a Tuya smart lamp accordingly

No Azure AD.
No Graph API.
No PresenceLight dependency.
No AI required.

Just pure practicality.

🔄 From pixels to logs

The first version captured the screen and read the color of the Teams tray icon. It worked, but it broke silently whenever the icon moved: a Teams restart or update, a new tray icon, a resolution change...

The new Teams client (MSTeams) writes every presence change to its own log:

```
UserPresenceAction: {cloud_context: https://teams.microsoft.com, availability: Busy}
```

The app now follows that log instead. It does not depend on the icon position, the screen resolution or the Teams UI language, and it picks up the change as soon as Teams applies it.

Screen capture is still available as a fallback (see Configuration).

✨ Features

Detects Teams presence from the local Teams log (or, optionally, from the systray icon color)

Follows log rotation and starts with your last known status

Logs a warning instead of failing silently when no presence data is found

Works with any Tuya-compatible RGB light

Uses a lightweight .NET console app

Sends presence updates to a local Docker API

No external cloud authentication

🎨 Status mapping

| Teams availability            | Light call                                    |
|-------------------------------|-----------------------------------------------|
| Available, AvailableIdle      | `SetPresenceColor?color=Green`                |
| Away, BeRightBack             | `SetPresenceColor?color=Yellow`               |
| Busy, BusyIdle                | `SetPresenceColor?color=Red`                  |
| DoNotDisturb                  | `Presenting`                                  |
| Offline, PresenceUnknown      | No change                                     |

⚙️ Configuration

`appsettings.json` next to the executable:

```json
{
  "ApiBaseUrl": "https://localhost:8082/",
  "Source": "TeamsLog",
  "Capture": {
    "PollIntervalMs": 1000
  }
}
```

- `Source`: `TeamsLog` (default) or `Screen` to use the old tray icon capture.
- `TeamsLogDirectory` (optional): defaults to `%LOCALAPPDATA%\Packages\MSTeams_8wekyb3d8bbwe\LocalCache\Microsoft\MSTeams\Logs`.
- `Capture`: `PollIntervalMs` applies to both sources; the rest of the values (tray icon position and size) are only used by `Screen`.

⚠️ The Teams log format is internal and undocumented, so a Teams update could change it. If that happens the app logs a warning (`No presence lines found` or `Unmapped Teams availability`) and you can switch to `"Source": "Screen"` in the meantime.

```
 ┌────────────────────────┐
 │  .NET Console App      │
 │  - Tail Teams log      │
 │  - Parse availability  │
 │  - Map to light color  │
 └───────────┬────────────┘
             │ GET /TeamsPresence/...
             ▼
 ┌────────────────────────┐
 │ Local Docker API (.NET)│
 │ - Logic for color map  │
 │ - Calls Tuya IoT API   │
 └───────────┬────────────┘
             │
             ▼
 ┌────────────────────────┐
 │ Tuya Smart Lamp        │
 │ RGB light as presence  │
 └────────────────────────┘
```
