# tdcko

A **turn-based tower defense** prototype built in Unity, layered on top of the Tower Defense Toolkit (TDTK). Instead of a real-time flow of enemies down a fixed track, the map is made of **concentric rings the player rotates** to reshape the path each turn — creeps then advance a fixed budget while towers fire.

- **Engine:** Unity `6000.6.0f1` (Unity 6)
- **Render pipeline:** URP
- **Base framework:** TDTK (Tower Defense Toolkit), under `Assets/TDTK/`
- **Custom turn/ring layer:** `Assets/TurnManager.cs`, `Assets/GameHandler.cs`, `Assets/SetParents.cs`
- **Main scene:** `Assets/TDTK/Scenes_Demo/Demo_Platform.unity`

## Gameplay

Each round is split into two phases, driven by `TurnManager`:

1. **Planning** — the player receives income, this turn's wave spawns instantly at the spawn points, and the player can build/upgrade towers and **rotate one ring** to redirect the incoming creeps. A movement **preview (ghost)** shows where each creep will end up.
2. **Resolution** — creeps advance in real time toward their precomputed stop point while towers fire. The turn ends once every living creep has spent its movement budget (is "parked") or none remain.

When resolution settles, the next turn begins — or the game ends (base destroyed / waves cleared).

### Rings

- The board has `ringCount` (default **3**) rotatable rings.
- The player may rotate **one ring per turn**. A used rotation can be **reverted** in the same planning phase before ending the turn.
- After a rotation is committed (turn ended), rotations are locked out for `ringRechargeTurns` (default **3**) turns via a shared cooldown.

### Economy

- Each planning turn grants income: `defaultIncomePerTurn` (default **20** gold), with optional per-turn overrides via `incomePerTurnOverrides` (index 0 = turn 1).

### Movement

- Each turn a creep is granted `secondsPerTurn` (default **6**) seconds of real-time travel; distance moved = `speed × secondsPerTurn`.

## Waves & spawning

Waves are configured **per scene** on the `SpawnManager` component (`waveList`) — one `Wave` = one turn. The list is extensible, so the number of turns is effectively unlimited: add more `Wave` entries in the inspector.

- **Spawn points:** 4 corner paths (`Path00`–`Path03`). Each spawn *group* draws a **distinct random corner** each turn (`SpawnManager.AssignGroupPaths`).
- **Spawn groups:** sub-waves sharing a `SubWave.spawnGroup` spawn from the same randomly chosen corner that turn.
- Creep prefabs are resolved by name via `Creep_DB.GetList()`.

## Units (creeps)

Available creeps in the Creep database (`Resources/DB/CreepDB`):

| Unit | Notes |
| --- | --- |
| Ratkin | Fast, weak swarm |
| Wolfkin | Pack skirmisher |
| Goblo | Basic goblin fodder |
| Goblin Shaman | Support caster |
| Goblin King | Goblin boss |
| Young Orc | Early orc line |
| Adult Orc | Sturdier orc |
| Orc Warchief | Orc boss |
| Packleader | Elite pack unit |
| Bearkin | Heavy bruiser |
| Ogre | Heavy elite |

> Note: some source spreadsheets reference an "Orc King" — that creep is **not** in the current Creep_DB and must be added before it can be spawned.

## Project layout

```
Assets/
  TurnManager.cs      turn/phase controller (planning ↔ resolution)
  GameHandler.cs      ring rotation input + revert UI
  SetParents.cs       reparents build platforms at startup
  TDTK/               Tower Defense Toolkit (framework, scenes, art, DB)
    Scenes_Demo/Demo_Platform.unity   main playable scene
Packages/             Unity package manifest
ProjectSettings/      Unity project settings
```

## Running

1. Open the project in Unity `6000.6.0f1`.
2. Open `Assets/TDTK/Scenes_Demo/Demo_Platform.unity`.
3. Press **Play**. Rotate a ring during Planning, then press **End Turn** to resolve the wave.
