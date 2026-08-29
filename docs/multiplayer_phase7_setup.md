# Multiplayer Phase 7 Setup — Weapon Models, Lobby Options, Campground, Ragdoll

Phase 7 goal: import the food-weapon models, let the **host** decide the room size in the lobby, auto-start the campground once the room fills, and make dying funny (ragdoll corpse).

## 1. Weapon models (.blend → Unity)

### The problem

`Assets/Models/weapons/*.blend` are Blender **5.0** files. Unity can import `.blend` only by shelling out to a locally installed Blender — and **Blender is not installed on this machine**, so Unity fell back to a preview-only import (see `Logs/AssetImportWorker*.log`: `PreviewImporter`, not `ModelImporter`). Same story for `Assets/Player/goshi(final!).blend`.

### The workaround

`Tools/blend_import/` reads the `.blend` container directly:

- `blendparse.py` — zstd-decompresses the file, parses the Blender 5.0 header (`BLENDER17-01v0502`, 32-byte BHead) and the SDNA type database.
- `blend2json.py` — pulls meshes out of Blender 5.0's new `AttributeStorage` (`position`, `.corner_vert`, `material_index`, `UVMap`, `sharp_face`) plus each material's viewport colour, converts Blender Z-up right-handed to Unity Y-up left-handed (`(x, y, z) → (x, z, y)` with reversed winding), and writes `Assets/Models/weapons/Converted/*.mesh.json`.

Re-run it whenever the models change:

```bash
python Tools/blend_import/blend2json.py Assets/Models/weapons Assets/Models/weapons/Converted
```

(Needs Python 3.14+ for the built-in `compression.zstd` module.)

### Turning JSON into prefabs

`Tools > Weapons > Build Weapon Prefabs` (`WeaponPrefabBuilder`) reads the JSON and writes:

- `Assets/Models/weapons/Generated/*.asset` — Mesh assets
- `Assets/Materials/W_*.mat` — one flat URP Lit material per Blender material (shared names like `W_wood` are reused across weapons)
- `Assets/Prefabs/Weapons/*.prefab` — `Weapon` + fitted `BoxCollider` + model child

The models are all authored lying along +X with the grip at the −X end, at ~5× game scale. The builder's `Defs` table gives each weapon its real length, grip point (normalised box coords) and in-hand rotation:

| Prefab | 이름 | 길이 | 사거리/반경 |
|---|---|---|---|
| Sausage_Skewer | 소세지 꼬치 | 0.95 m | 1.5 / 0.85 |
| Great_Ladle | 거대 국자 | 1.10 m | 1.6 / 1.00 |
| Frozen_Tuna | 냉동 참치 | 1.05 m | 1.5 / 1.05 |
| Carrot_Greatsword | 당근 대검 | 1.30 m | 1.9 / 0.95 |
| Baguette_Club | 바게트 몽둥이 | 0.85 m | 1.4 / 0.85 |
| Whisk_Axe | 거품기 도끼 | 0.90 m | 1.5 / 0.95 |
| Pineapple_MorningStar | 파인애플 철퇴 | 0.85 m | 1.4 / 1.05 |
| Banana_Bow | 바나나 활 | 1.10 m | 1.3 / 0.80 |
| Bread_Shield | 식빵 방패 | 0.62 m | 1.1 / 0.90 |
| Rubber_Duck | 고무 오리 | 0.38 m | 1.0 / 0.70 |

`Weapon` gained `swingReach` / `swingRadius` (per-weapon kill range, used by `WeaponNetworkManager.ServerSwingKill`) and `groundOffset` (the prefab origin is the grip, so dropped weapons need lifting or they sink into the floor).

Hold poses are baked into the prefab — tweak `holdPosition` / `holdEuler` in the Inspector if a weapon sits badly in hand.

## 2. Lobby options (host only)

`LobbyManager` changes:

- `[SyncVar] targetPlayers` (default 4, clamped to `minPlayers`..`maxPlayers` = 1..8) — everyone sees it in the `Member n/N` readout. 1 is allowed so the host can test alone.
- **Tab** opens the 방 옵션 panel. It only opens when `NetworkServer.active`, i.e. for the host; guests get nothing. The panel calls `PlayerController.SetInputPaused(true)` — **not** `SetInputEnabled(false)`, which would stop `CharacterController.Move()` and make the player sink through the floor. `OnDisable` restores input so leaving the lobby with the panel open can't strand you.
- Lowering the cap below the number of people already connected is blocked.
- Changing the cap writes both `NetworkManager.singleton.maxConnections` *and* `NetworkServer.maxConnections` — the latter is a separate copy taken at `Listen()`, so changing only the manager has no effect on a server that is already running.
- Countdown starts **only** when the room is full (`playerCount >= targetPlayers`), then `ServerChangeScene(firstStageScene)`. The ready pad is display-only; it used to also start the game, which meant a lone host standing on it counted as "everyone ready" (1/1) and left immediately regardless of the cap.
- `firstStageScene` is now `Stage2_Campground` (was `NetworkDemo`), both in `LobbyBuilder` and in the existing `Lobby.unity`.

Lobby spawn points went from 5 to 8 to match the cap.

## 3. Campground

`Stage2Builder` now places the real weapon prefabs (`PlaceWeapons`) instead of the old primitive 도끼/바비큐집게, at 8 spots around the fire, tents, picnic table and lakeside. Spawn points went to 8. The exit zone loops back to **Lobby** instead of `NetworkDemo`, so the cycle is 대기실 → 캠핑장 → 대기실.

The environment was rebuilt from a bare plane + 10 sphere-trees into something worth looking at:

- **Terrain** — tinted grass patches over the base plane, a dirt path from spawn through the fire to the exit, a fire pad, and a ring of berms that walls the map in while reading as low hills.
- **Lake** with a stone rim and a wooden dock (the 낚싯대 sits on it). The water plane has its collider stripped so nobody walks on it.
- **Campfire** — 14 scattered stones, a cone of split logs, two-tone flame, plus a tripod with a pot and four log seats.
- **Tents** — proper A-frames with a ridge pole, an open door flap and four guy ropes each.
- Picnic table with splayed legs, cooler, two camp chairs, a bulb-strung clothesline (the 랜턴 hangs on its pole), a split-wood pile with a chopping stump, and a signpost by the spawn.
- **Forest** — three rings of mixed conifers (stacked cones) and broadleaf trees (three-lobe canopies), each randomly scaled and rotated, plus a few inside the camp as cover. Trees collide on the trunk only.
- ~55 scattered rocks, bushes and stumps.
- Low warm sun with soft shadows, trilight ambient, and linear fog so the far treeline fades out.

`Random.InitState(2026)` keeps the layout identical across rebuilds. Terrain/Lake/Forest/Scatter are flagged batching-static, and all conifers share one `Assets/Models/Generated/Camp_Cone.asset` mesh instead of baking a copy per tree into the scene file.

## 4. Ragdoll death

- `Tools > Player > Build Ragdoll Prefab` (`RagdollBuilder`) reads the bones actually used by the goshi `SkinnedMeshRenderer`, measures each bone's length from its nearest child, and adds `CapsuleCollider` + `Rigidbody` + `CharacterJoint` (parent-first ordering so joints always find their connected body). Saves `Assets/Prefabs/GoshiRagdoll.prefab` on the Ignore Raycast layer and wires it into `NetworkPlayer.prefab`'s `PlayerRagdoll`.
  - The goshi rig is *generic*, not humanoid (`animationType: 2`), and its bones are named `Bone`, `Bone.001`… — Unity's Ragdoll wizard can't be used, hence the name-agnostic approach.
- `WeaponNetworkManager.ServerSwingKill` passes the blow direction to `PlayerHealth.ServerKill(killer, blowDirection)`.
- `PlayerHealth` fires `RpcDie(position, rotation, blowDirection)`; every client spawns a local corpse via `PlayerRagdoll.SpawnCorpse`. Corpse physics is **not** networked — only the death spot is — so it costs no bandwidth and each client sees a slightly different flop.
- `PlayerRagdoll.CopyPose` copies the dying animation pose onto the corpse by matching bone names (the first-person owner's avatar is disabled, so they get the bind pose instead).
- `RagdollCorpse` launches the body, lets it flop for 6 s, then sinks it into the ground and destroys it.

Respawn is still instant, as before.

## Files

New:
- `Tools/blend_import/blendparse.py`, `Tools/blend_import/blend2json.py`
- `Assets/Models/weapons/Converted/*.mesh.json`
- `Assets/Scripts/Editor/WeaponPrefabBuilder.cs`
- `Assets/Scripts/Editor/GoshiModel.cs`
- `Assets/Scripts/Editor/RagdollBuilder.cs`
- `Assets/Scripts/Editor/GameContentSetup.cs`
- `Assets/Scripts/PlayerRagdoll.cs`
- `Assets/Scripts/RagdollCorpse.cs`

Changed:
- `Assets/Scripts/Weapon.cs` — `swingReach`, `swingRadius`, `groundOffset`
- `Assets/Scripts/WeaponNetworkManager.cs` — per-weapon reach, blow direction
- `Assets/Scripts/PlayerHealth.cs` — `RpcDie`, blow direction
- `Assets/Scripts/LobbyManager.cs` — host options, room-full start
- `Assets/Scripts/Editor/LobbyBuilder.cs` — campground target, 8 spawns, cap 8
- `Assets/Scripts/Editor/Stage2Builder.cs` — real weapons, 8 spawns, loop to Lobby
- `Assets/Scripts/Editor/PlayerAnimatorSetup.cs` — uses `GoshiModel`, wires an idle clip
- `Assets/Scenes/Lobby.unity` — `firstStageScene: NetworkDemo`

## Manual Unity Editor steps

1. `Tools > Setup > Build Everything (Weapons, Player, Campground)` — runs weapons → animations → ragdoll → campground in the right order.
   (Or run them one at a time from `Tools > Weapons`, `Tools > Player`, `Tools > Stage`.)
2. `Tools > Lobby > Build Lobby` if you want the lobby rebuilt with 8 spawn points. The existing `Lobby.unity` already points at the campground without this.
3. Ctrl+S.

## Test

1. Host on `Lobby`. Press **Tab** → 방 옵션 opens, set 정원 to 2.
2. Join with a ParrelSync clone → `Member 2/2` → 5-second countdown → both load `Stage2_Campground`.
3. Guest presses Tab → nothing happens (host only).
4. Pick up 소세지 꼬치, hit the other player → they respawn instantly and a goshi corpse flies off in the direction of the blow, flops, and sinks after ~6 s.
5. Long weapons (당근 대검) reach noticeably further than 고무 오리.

## Known limits

- **`goshi(final!).blend` is not the model in use.** Without Blender installed Unity cannot import a rigged, skinned, animated `.blend`, and hand-parsing an armature + actions is well beyond what the mesh converter does. `Assets/Player/goshi(final).fbx` — the same character, exported from Blender 5.1, with a *richer* rig (8 bones vs the `.blend`'s 5) and 5 animation clips — is used instead. `GoshiModel.Load()` already prefers the `.blend`, so **installing Blender and reimporting is all it takes** to switch over; no code change needed.
- The weapon converter handles static meshes only (no armatures, no shape keys) and takes each material's viewport colour, not its node graph. That is exactly what these flat-shaded props need.
- Corpses are client-local, so two players may see the same body land differently. That is deliberate — it keeps death free of bandwidth cost.
