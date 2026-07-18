# Multiplayer Phase 5 Setup

Phase 5 goal: 마검(magic sword) combat. A weapon holder's swing is a one-hit kill; victims respawn instantly and drop their sword so the power structure keeps shifting (co-op escape with betrayal).

## Concept (from 마검탈출맵)

마검탈출맵 = a co-op escape map where a small number of "마검" are hidden. Whoever holds a 마검 can one-shot others, so players steal/contest the swords and betray each other while trying to escape. Our swords are the **ladle** and **frozen tuna** (2 swords → limited, contested).

## Rules implemented

- **One-hit kill**: a swing by a weapon holder that connects with another player kills them (server-authoritative).
- **Instant respawn**: the victim teleports to a `NetworkStartPosition` immediately.
- **Power shift**: on death the victim drops the sword they were holding at the death spot; anyone can grab it.
- **Spawn protection**: brief invulnerability after respawn to prevent spawn-camping.
- Only a **weapon holder** can kill (unarmed swings do nothing lethal).

## Files changed

- `Assets/Scripts/PlayerHealth.cs` (new) — `ServerKill` → drop sword + respawn via `TargetRpc`, spawn protection, local "당했다!" feedback.
- `Assets/Scripts/WeaponNetworkManager.cs` — server-side lethal check on swing (`ServerSwingKill` after `strikeDelay`), `ServerDropWeaponOf`.
- `Assets/Scripts/Editor/NetworkPhase5Setup.cs` (new) — `Tools > Multiplayer > Phase 5 > Setup Combat`.

## Manual Unity Editor steps

1. Make sure Phase 1 has been run (NetworkPlayer.prefab exists).
2. Run `Tools > Multiplayer > Phase 5 > Setup Combat` (adds `PlayerHealth` to the prefab).
3. No scene save needed for the prefab change, but save if the scene is dirty.

> Kill range/delay are on the `WeaponManager` object (lethalReach / lethalRadius / strikeDelay).

## Test

1. Original editor: Play, `Host`. Clone: Play, `Client`.
2. One player picks up the ladle or frozen tuna and swings at the other → the other dies and respawns instantly, and the killer keeps their sword.
3. Kill a player who was holding the other sword → that sword drops where they died; pick it up to take the power.
4. Right after respawn, a player can't be re-killed for ~1.5s (spawn protection).

Phase 6 will add the game loop (lobby / round / escape win) on top of this combat + the co-op checklist.
