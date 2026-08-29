# Main Title Screen Setup

A clean title screen that launches the game with one click.

## Design (no extra NetworkManager)

The title scene has **no** NetworkManager. Buttons store the intent in a static (`GameLaunch`) and load the game scene; the game scene's `NetworkBootstrap` runs `NetworkAutoLaunch` which starts Host/Client from that intent. This avoids duplicate NetworkManagers and doesn't disturb the existing Mirror bootstrap.

- `GameLaunch` (static): `Mode` (None/Host/Client) + `Address`.
- `NetworkAutoLaunch` (on NetworkBootstrap): on Start, StartHost / StartClient per `GameLaunch.Mode`. If None (scene opened directly), does nothing → HUD still usable.
- `MainMenu` (on the title Canvas): wires buttons by name.
  - 게임 시작 (호스트) → Host, load NetworkDemo.
  - 참가하기 → Client to the IP field (default 127.0.0.1), load NetworkDemo.
  - 나가기 → quit.

## Files

- `Assets/Scripts/GameLaunch.cs`, `NetworkAutoLaunch.cs`, `MainMenu.cs` (runtime)
- `Assets/Scripts/Editor/TitleSceneSetup.cs` (editor) — builds the scene UI, adds NetworkAutoLaunch to NetworkDemo, sets build order.

## Setup (one menu)

`Tools > Title > Setup Main Title`:
1. Adds `NetworkAutoLaunch` to `NetworkBootstrap` in NetworkDemo and saves it.
2. Creates `Assets/Scenes/MainTitle.unity` with a clean uGUI menu (title, IP field, Host/Join/Quit) + EventSystem (Input System UI module) + a background camera.
3. Sets Build Settings order to **[MainTitle, Lobby, Stage3_CursedFortress, Stage2_Campground]** so the game boots at the title and both modes are included.

## Test

1. Press Play from `MainTitle` (or just Play — it's the first build scene).
2. Click **게임 시작 (호스트)** → loads NetworkDemo and hosts; the player spawns.
3. From a ParrelSync clone: Play → **참가하기** (127.0.0.1) → joins the host.

## Notes

- UI uses legacy uGUI Text/InputField (no TMP dependency) with the built-in font.
- To rename the game, edit the "Title"/"Subtitle" texts in `MainTitle.unity` (or in `TitleSceneSetup.BuildTitleUI`).
- Returning to the title after a game isn't wired yet (would need a NetworkManager.StopHost + LoadScene("MainTitle")); can be added with a pause menu later.
