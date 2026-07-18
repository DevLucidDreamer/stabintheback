# Multiplayer Phase 4 Setup

Phase 4 goal: network weapon pickup / hold / drop / swing, and introduce swing **hit detection** (first attack judgement).

## Design

- Weapons are NOT given their own `NetworkIdentity` (the frozen tuna is a child of the networked fridge; nested identities aren't allowed).
- Each `Weapon` gets a stable integer `weaponId` (assigned + saved in the scene).
- One `WeaponNetworkManager` (single `NetworkIdentity`) is server-authoritative for "who holds which weapon":
  - `CmdPickup(id)` / `CmdDrop(id,pos,euler)` / `CmdSwing()` (requiresAuthority = false).
  - Server keeps `weaponId → holder netId`; auto-drops a player's previous weapon on a new pickup.
  - `RpcHold` / `RpcFree` / `RpcSwing` make **each client** reparent its local weapon copy to the holder's hand socket (or drop it to the world) and play the swing animation. Late joiners sync via `CmdRequestSync` → `TargetRpc`.
- `PlayerInteraction` routes pickup/drop/swing through the manager when it exists; otherwise it behaves locally (single-player). The swing animation now runs even for remote players (outside the input gate).
- **Hit detection**: at the strike moment of a swing, each client does an `OverlapSphere` in front of the swinging player and applies knockback to non-kinematic `Rigidbody`s. A `PlayerInteraction.OnSwingHit` event is the hook for a future damage system.

## Files changed

- `Assets/Scripts/Weapon.cs` — `weaponId` + `AttachTo` / `DetachTo`.
- `Assets/Scripts/PlayerInteraction.cs` — network routing, visual hooks (`AttachWeaponVisual`/`DetachWeaponVisual`/`PlaySwingVisual`), always-on swing, swing hit detection.
- `Assets/Scripts/WeaponNetworkManager.cs` (new) — server-authoritative hold/drop/swing sync.
- `Assets/Scripts/Editor/NetworkPhase4Setup.cs` (new) — `Tools > Multiplayer > Phase 4 > Setup Weapon Sync`.

## Manual Unity Editor steps

1. Open `Assets/Scenes/NetworkDemo.unity`.
2. Run `Tools > Multiplayer > Phase 4 > Setup Weapon Sync`.
   - Creates a `WeaponManager` object (NetworkIdentity + WeaponNetworkManager).
   - Assigns a unique `weaponId` to every `Weapon` (ladle, frozen tuna).
3. Save the scene (Ctrl+S).

> Re-run after regenerating the house or adding weapons.

## Test

1. Original editor: Play, `Host`. Clone: Play, `Client`.
2. One player picks up the ladle → the other player sees it in that player's hand.
3. Left-click to swing → the swing animation shows on both windows.
4. Swing near a pushable cube → the cube is knocked back (note: the loose cubes are not networked, so knockback is applied per-client and may diverge — this is expected until targets are networked).
5. Press G to drop → the weapon appears in the world for both, and can be picked up again.

## Notes / limitations

- Attack judgement currently applies knockback + fires `OnSwingHit`. There is no health/damage yet (по roadmap this is where judgement is *introduced*).
- The stray physics cubes are not networked; a proper networked hittable/health target is future work.
