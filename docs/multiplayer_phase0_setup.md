# Multiplayer Phase 0 Setup

Phase 0 goal: install the base packages, keep the transport swappable, and verify an empty Host/Client connection with Mirror + Telepathy before gameplay networking begins.

## Files changed by Codex

- `Packages/manifest.json`
  - Added Steamworks.NET `2025.163.0`
  - Added ParrelSync `1.5.3`
- `steam_appid.txt`
  - Added Steam test app id `480`
- `Assets/Scripts/Editor/NetworkPhase0Setup.cs`
  - Adds a menu item that creates `NetworkBootstrap` after Mirror is imported.

## Manual Unity Editor steps

1. Open Unity and let Package Manager resolve the new UPM dependencies.
2. Import Mirror manually:
   - Download `Mirror-96.10.3.unitypackage`
   - URL: `https://github.com/MirrorNetworking/Mirror/releases/download/v96.10.3/Mirror-96.10.3.unitypackage`
   - Import it into the project.
3. Open or duplicate the current test scene for networking.
   - Recommended name: `Assets/Scenes/NetworkDemo.unity`
   - Keep the existing single-player `Demo.unity` intact.
4. Run `Tools > Multiplayer > Phase 0 > Create Empty Network Bootstrap`.
   - This creates/fills a `NetworkBootstrap` GameObject.
   - It adds `NetworkManager`, `TelepathyTransport`, and `NetworkManagerHUD` when available.
   - It sets `autoCreatePlayer` to `false` for the empty-room connection test.
5. Save the scene.

## ParrelSync setup

1. After ParrelSync appears in Unity, open `ParrelSync > Clones Manager`.
2. Create one clone.
3. Open the clone editor.
4. In the original editor, press Play and click `Host` in the Mirror HUD.
5. In the clone editor, press Play and click `Client`.
6. Confirm both editors stay connected with no console errors.

## Notes

- Use Telepathy for Phase 0-4 local development.
- Do not add FizzySteamworks to gameplay logic. It should remain a transport swap for the later Steam validation phase.
- FizzySteamworks latest checked release: `FizzySteamworks-6.0.1`.
  - UPM URL for later: `https://github.com/Chykary/FizzySteamworks.git?path=/com.mirror.steamworks.net#FizzySteamworks-6.0.1`
  - Unitypackage URL for manual fallback: `https://github.com/Chykary/FizzySteamworks/releases/download/FizzySteamworks-6.0.1/FizzySteamworks-6.0.1.unitypackage`
