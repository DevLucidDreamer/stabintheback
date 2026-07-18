# Multiplayer Phase 3 Setup

Phase 3 goal: co-op (team-shared) camping checklist. Anyone can collect an item; it disappears for everyone and the shared progress updates on every clipboard. Late joiners sync to the current state.

## Design

- Items are NOT given their own `NetworkIdentity` (some are children of the networked drawers/fridge from Phase 2, and Mirror disallows nested identities).
- Each `CollectibleItem` gets a stable integer `itemId` (assigned + saved in the scene, identical on all clients).
- One `CampChecklistManager` (single `NetworkIdentity`) is server-authoritative:
  - Scans items at `OnStartServer`, builds required counts.
  - `CmdCollect(id)` (requiresAuthority = false) → server marks collected, increments the shared count, and `RpcCollected` hides that item id on all clients + pushes the new progress.
  - Late joiners call `CmdRequestSync` → `TargetRpc` sends the collected-id list + progress.
- `ItemChecklist` renders from the manager when it exists (shared), otherwise from its local scan (single-player). It subscribes to `CampChecklistManager.OnChanged`.

## Files changed

- `Assets/Scripts/CollectibleItem.cs` — `itemId` + network collect path (local peek, server request).
- `Assets/Scripts/CampChecklistManager.cs` (new) — server-authoritative shared checklist.
- `Assets/Scripts/ItemChecklist.cs` — renders shared progress + `Peek()`.
- `Assets/Scripts/Editor/NetworkPhase3Setup.cs` (new) — `Tools > Multiplayer > Phase 3 > Setup Checklist Sync`.

## Manual Unity Editor steps

1. Open `Assets/Scenes/NetworkDemo.unity`.
2. Run `Tools > Multiplayer > Phase 3 > Setup Checklist Sync`.
   - Creates a `ChecklistManager` object (NetworkIdentity + CampChecklistManager).
   - Assigns a unique `itemId` to every `CollectibleItem`.
3. Save the scene (Ctrl+S).

> Re-run after regenerating the house or adding/removing collectibles.

## Test

1. Original editor: Play, `Host`. Clone: Play, `Client`.
2. One player collects an item (including items inside a drawer/fridge after opening) → it disappears for both, and both clipboards (E key) show it checked.
3. Collect several, then connect another client → it should already show the collected items gone and the shared progress filled.

Phase 4 will network weapon pickup/hold/drop/swing and introduce swing hit detection.
