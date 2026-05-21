Teams Presence Light – Screen Capture Edition

A lightweight, dependency-free solution to display your Microsoft Teams presence using a Tuya-controlled smart light.

📌 Overview

This project is the successor to my original Teams Presence Light, which relied on the PresenceLight application and Azure AD / Microsoft Graph API to retrieve Teams presence information.

Due to Microsoft authentication changes and PresenceLight’s inability to authenticate multi-tenant accounts created after October 2018, the original approach stopped working.

Instead of rebuilding the entire pipeline or using AI-based computer vision, I implemented a simpler, faster and more robust solution:

👉 Capturing the screen
👉 Cropping the Microsoft Teams tray icon
👉 Reading the icon’s color
👉 Sending the detected state to an API running in Docker
👉 Updating a Tuya smart lamp accordingly

No Azure AD.
No Graph API.
No PresenceLight dependency.
No AI required.

Just pure practicality.

✨ Features

Detects Teams presence by reading the systray icon color

Works with any Tuya-compatible RGB light

Uses a lightweight .NET console app

Sends presence updates to a local Docker API

Extremely fast and reliable

No external cloud authentication

No need for AI or vision models

 ┌────────────────────────┐
 │  .NET Console App      │
 │  - Screenshot          │
 │  - Crop systray area   │
 │  - Detect Teams color  │
 └───────────┬────────────┘
             │ POST /status
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

