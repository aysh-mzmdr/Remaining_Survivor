# Remaining_Survivor

Unity **2022.3.45f1**, Built-in Render Pipeline (no URP/HDRP), legacy Input Manager (`UnityEngine.Input`, not the new Input System package — none installed). 2D side-scrolling platformer. Unity MCP is set up for this project; prefer it (or a checked-in `Assets/Scripts/Editor/*.cs` `[MenuItem]` tool) over hand-editing scene/prefab/animator YAML.

## Scene: `Assets/Scenes/SampleScene.unity`

- **Grid/Ground** — a `Tilemap` (+`TilemapRenderer`+`TilemapCollider2D`+`CompositeCollider2D`+static `Rigidbody2D`, `usedByComposite`) built by `Assets/Scripts/Editor/LevelBuilder.cs` (`Tools/Remaining Survivor/Build Level` menu item, re-run after editing the layout constants in that file). Layer `Ground`. 24 cols × 9 rows, cell = 1 world unit, grid origin `(-12, -5.39)`. Layout: floor (row0) + ceiling (row8) + left/right walls (col0/col23), and 3 platform tiers each **2 rows above the previous** (Tier E row2, Tier C row4, Tier A row6) — the empty row in between is required so a lower tier's surface isn't buried inside the tile directly above it (which would leave it unwalkable/uncollidable). `KnightMove.jumpHeight` (2.3) is tuned to clear that 2-unit step. Tiles are the Cainos `TX Tileset Ground_0` asset (same sprite the level's floor always used) with per-cell `Tile.ColliderType.Grid` forced (source tiles default to `Sprite` collider, which traces bumpy silhouettes instead of a flat top).
- **Knight** — the player. `SkeletonAnimation`+`KnightControl` (Spine; animation name → `SetAnimation` mapper) + `KnightMove` (movement/state machine, no physics — X is direct `transform.position` clamped to `minX`/`maxX`, Y is a hand-rolled gravity/raycast ground check masked to the `Ground` layer via `groundLayer`) + `CapsuleCollider2D` (trigger, detection-only) + `PlayerHealth` + `PlayerCombat`. Tag `Player`, layer `Player`.
- **Main Camera** — orthographic, `CameraFollow` (X-follow only, fixed Y, clamped to level bounds) targeting Knight.
- **Enemies** — container with 12 instances of `Assets/Prefabs/SkeletonEnemy.prefab` (a variant of BluBlu's `skeleton rotten.prefab`, scaled to `0.12` — the raw rig is ~13.5 units tall at scale 1) placed across the 3 tiers per `game1.png`'s layout, layer `Enemy`.
- **Canvas** — `HealthBarSlider` (a uGUI `Slider`, top-left anchored, red fill, non-interactable) + `PlayerHealthBarUI`, wired to Knight's `PlayerHealth`.

## Scripts (`Assets/Scripts/`)

- `KnightMove.cs` — player state machine (`Free/Action/Air/Dead`). Public hooks added for combat: `TakeHit(knockbackDir, knockbackDistance)`, `Die()`, `IsDead`, `event OnAttackStarted` (fires at the start of the J/K attack animations). Don't duplicate its private key handling — hook these instead. `HandleActions()` (and therefore attacking) only runs in `State.Free`; `TakeHit()` unconditionally overwrites whatever animation is currently playing, so nothing may call it faster than `PlayerHealth.invulnerabilityDuration` allows (see below) or the player gets stun-locked, unable to ever attack again.
- `CameraFollow.cs` — attached to Main Camera, `target`/`minX`/`maxX` set in the Inspector.
- `PlayerHealth.cs` / `PlayerCombat.cs` — player HP + attack hit detection (`OverlapCircle` on `OnAttackStarted`, no trigger colliders/Rigidbody2D involved). `invulnerabilityDuration` (1.0s) must stay comfortably longer than the "Get Hit" animation and than how often a swarm of enemies can realistically land hits — this is what prevents the stun-lock above (a swarm re-triggering `TakeHit()` before the current hit reaction finishes, permanently stuck in `State.Action`). Both `TakeDamage`/enemy `TakeDamage` take the attacker's position and apply a small instant knockback + a brief red tint flash (`Skeleton.R/G/B` for the player, cached `SpriteRenderer` colors for enemies) so hits read as impactful.
- `EnemyHealth.cs` / `EnemySkeleton.cs` — enemy HP (30, dies in 2 player hits) + AI (`OverlapCircle` detection, raycast-to-ground movement mirroring `KnightMove`'s approach, masked to `Ground`). Tuned down from initial values (`attackDamage` 6, `attackCooldown` 1.8s, `detectionRadius` 3.5) after early playtesting found the enemies too aggressive/swarmy.
- `PlayerHealthBarUI.cs` — thin wrapper around a `Slider`.
- `Editor/LevelBuilder.cs` — see Scene section above.

## Layers & tags

`Player` (8), `Enemy` (9), `Ground` (10) — all `LayerMask` fields on the above scripts must reference these. **Unity-MCP gotcha**: set a `LayerMask` property via `manage_components` with `{"value": <bitmask>}`, not `{"m_Bits": ...}` — the latter silently no-ops (leaves the field at 0/nothing) without erroring, which previously caused ground collision and enemy/player detection to silently fail. Always read the property back after setting a `LayerMask` to confirm.

**More Unity-MCP/testing gotchas learned the hard way:**
- Editing a public field's *default value* in a `.cs` file does **not** retroactively update that field on already-serialized component instances/prefabs (Unity keeps the old serialized value). After changing a default that an existing instance should pick up, explicitly push it with `manage_components set_property` and read it back to confirm — don't assume the source edit was enough.
- `PlayerSettings.runInBackground` is `true` for this project. Without it, Unity freezes the entire Play-mode frame loop (including coroutines/animations) whenever the Editor window lacks OS focus, which made automated/headless testing through Unity-MCP look like a stuck game (state never advancing) when the actual bug was just a lack of window focus.

## Animator: `skeleton rooten.controller`

Shipped as a zero-parameter "cycle through every animation once" demo. Reworked for gameplay: added `IsWalking` (bool), `Attack`/`Hit`/`Dead` (triggers); `Idle↔Walk` gated on `IsWalking`; `Walk→Attack` gated on trigger; removed the stock auto-transitions `Attack→Get Hit`, `Get Hit 2→Death Skeleton rotten`, and `Death Skeleton rotten→Dance` (all three demo-only trip hazards — the first two would misfire gameplay attack/damage timing, the last would make corpses stand up); added `Any State→Get Hit`/`Any State→Death Skeleton rotten` on the `Hit`/`Dead` triggers instead.

## Assets in use

- **Spine2D Knight Character Animation Pack** — player rig/animation (Spine runtime).
- **BluBlu Games/2D Animated Skeletons** — enemy source art/animation only (no gameplay code shipped with it).
- **Cainos/Pixel Art Platformer - Village Props** — level tileset (`Tileset Palette/TP Ground/`) and decorative props (unused so far beyond the ground tile).
