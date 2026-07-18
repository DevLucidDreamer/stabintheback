# Multiplayer Phase 1 Setup

Phase 1 goal: spawn one networked player per connection, let only the local player read input/camera/audio, and sync remote player movement/look over Mirror with Telepathy.

## Files changed by Codex

- `Assets/Scripts/PlayerController.cs`
  - Added an input enable gate.
  - Exposed pitch so the network layer can sync camera look.
- `Assets/Scripts/PlayerInteraction.cs`
  - Added an input/UI enable gate for local-player-only interaction.
- `Assets/Scripts/ItemChecklist.cs`
  - Added an input/UI enable gate for local-player-only checklist controls.
- `Assets/Scripts/NetworkPlayerSetup.cs`
  - Local/remote player split.
  - Camera and AudioListener are enabled only for the local player.
  - Remote players get a simple visual avatar.
  - Camera pitch is synced with a small SyncVar/Command.
- `Assets/Scripts/Editor/NetworkPhase1Setup.cs`
  - Adds `Tools > Multiplayer > Phase 1 > Setup Player Spawning`.

## Manual Unity Editor steps

1. Open `Assets/Scenes/NetworkDemo.unity`.
2. Run `Tools > Multiplayer > Phase 1 > Setup Player Spawning`.
3. Save the scene.
4. Confirm these objects/settings:
   - `Assets/Prefabs/NetworkPlayer.prefab` exists.
   - `NetworkBootstrap > Network Manager > Player Prefab` points to `NetworkPlayer`.
   - `Auto Create Player` is enabled.
   - Scene object `SinglePlayerScenePlayer_DisabledForNetwork` is disabled.
   - `NetworkSpawnPoints` contains several `NetworkStartPosition` children.

## Test

1. Original editor: Play, click `Host`.
2. ParrelSync clone: Play, click `Client`.
3. Each window should control only its own spawned player.
4. The other player should appear as a simple capsule/sphere avatar and move/turn over the network.

Phase 2 will network-authorize `Openable` state changes. Until then, interactions remain local/single-player behavior and should not be used as the multiplayer validation target.
