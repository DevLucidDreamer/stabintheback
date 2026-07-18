# Multiplayer Phase 2 Setup

Phase 2 goal: network-authorize `Openable` state changes so drawers, doors, and the fridge open/close identically on every client with server authority.

## Design

- `Openable` stays a plain single-player `MonoBehaviour`. If an `IOpenableNetworkController` is on the same object, `Interact` delegates the toggle to it; otherwise it toggles locally. Single-player `Demo.unity` is unaffected.
- `NetworkOpenable : NetworkBehaviour` holds the open state in a `[SyncVar]` and implements `IOpenableNetworkController`.
  - Any client can request a toggle via `[Command(requiresAuthority = false)]` (scene objects have no client ownership).
  - Server flips the SyncVar; the hook drives `Openable.SetOpen` on every client so each one animates.
  - Late joiners get the current state in `OnStartClient`.
  - Falls back to a local toggle if the object is not spawned (offline).

## Files changed

- `Assets/Scripts/Openable.cs`
  - Added `IOpenableNetworkController` interface, `SetOpen(bool)`, and toggle delegation.
- `Assets/Scripts/NetworkOpenable.cs` (new)
  - Server-authoritative open/close sync.
- `Assets/Scripts/Editor/NetworkPhase2Setup.cs` (new)
  - Adds `Tools > Multiplayer > Phase 2 > Setup Interactable Sync`.

## Manual Unity Editor steps

1. Open `Assets/Scenes/NetworkDemo.unity`.
2. Run `Tools > Multiplayer > Phase 2 > Setup Interactable Sync`.
   - Adds `NetworkIdentity` + `NetworkOpenable` to every `Openable` in the scene (drawers, fridge, front-door panels).
3. Save the scene (Ctrl+S).

> If you regenerate the house (`Tools > House > Build House`), the new Openables lose these components — re-run the Phase 2 menu afterward.

## Test

1. Original editor: Play, click `Host`.
2. ParrelSync clone: Play, click `Client`.
3. Open a drawer / the fridge / a front door on one window → it opens on the other window too.
4. Have the client open something, then connect a second client (or reconnect) → the newly joined client should see it already open.

Phase 3 will network the `CollectibleItem` pickups and the shared `ItemChecklist` progress.
