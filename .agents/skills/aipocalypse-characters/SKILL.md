---
name: aipocalypse-characters
description: How characters get into aipocalypse from any vendor — model vs entity prefab under Content/[Kind]/[Name]/, the animator parameter contract and CharacterKit, EnemyAnimator and the central animation tick, prefab variants, verifying looks and animation. Use for any task that adds, swaps, re-skins, animates or tunes an enemy, NPC, companion, animal or boss model. Behaviour is aipocalypse-enemy-ai; Synty specifics are tools-unity-synty-characters.
---

# Characters in aipocalypse

A character is two things kept apart:
- a **model prefab**, which is the vendor's look, rebuilt or swapped at will;
- an **entity prefab**, which holds our components, nests the model, and is what scenes and spawners use.

Vendor skills make models: **tools-unity-synty-characters** for Synty. This skill turns any model into a working character and keeps the vendors interchangeable. Behaviour (trees, navigation, spawning) is **aipocalypse-enemy-ai**.

Worked example, built with this skill: **`Content/Enemies/Goblin/`**, ten wandering Sidekick goblins in WorldMap (Goblin 1–10 outside the home gate, six looks): looks 01–05 copied from Goblin Fighters `.sk` files, look 06 written from presets. The setup scripts are in `AgentScripts/Characters/Goblin/`.

## 1. What exists

| Piece | Where | Role |
| --- | --- | --- |
| `EnemyAnimator` | `_Core/EnemySystem/` | On the enemy root: `_animator` (the Model's Animator), `_fidgets` (fidget states in the controller). Writes the contract parameters. No `Update`. |
| `LocomotionAnimation` | `_Core/EnemySystem/` | Plain C# rules: where to look, lean, when to fidget, from speed and turn rate. EditMode-tested. |
| `EnemyAnimationManager` | `_Core/EnemySystem/` | `ITickable` registered by `EnemyInstaller`: advances every registered enemy's `EnemyAnimator` each frame on scaled time (pause freezes it). Profiler marker `Aipocalypse.EnemyAnimationManager.Animate`. |
| `EnemyAnimationSettings` | SO, `Data/Enemies/EnemyAnimationSettings.asset` | Glance interval / range, look and lean into turns, fidget delay / chance, damping. Linked from `EnemyDefinition.Animation`. |
| `EnemyMover.Velocity`, `EnemyBrain.Animation` | `_Core/EnemySystem/` | The animator reads movement through the mover; the brain finds the optional `EnemyAnimator` on `Start` and initializes it on activation. |
| `EnemyBody`, `EnemyStats` | `_Core/EnemySystem/` | Health from the `MaxHealth` stat, knockback, and calls `EnemyAnimator.Hit` / `Die`; the visual it sinks after death is the `Model`. Rules in aipocalypse-enemy-ai §1–2. |
| `CharacterKit.cs` | `scripts/` here | `Describe`, `Snapshot`, `ClipInfo`, `BuildLocomotionController`: run with `unity command script run`. |
| `EnemyAnimationTests` | `Tests/EditMode/` | Rules, plus `AnimatedEnemyPrefabs_AreWiredToTheParameterContract` over every enemy prefab with an `EnemyAnimator`. |

**Not built** (don't assume): animated NPCs, companions, animals or bosses; animation LOD (fewer layers or a lower rate far away); ragdolls; attack animations; pooling.
- Unique characters (companions, NPCs) will be `EntityScope` entities (ARCHITECTURE.md → Entities). Reuse `LocomotionAnimation` and the parameter contract, ticked by their own system.
- Propose the design before building any of these.
- Animals from Malbers bring the heavy per-instance Animal Controller and its own NavMeshAgent AI: ASSETS.md allows it only for a few unique animals, never crowds, and asks first.

## 2. Pipeline

1. **Source.** Read ASSETS.md → Characters & animation (plus Env packs with characters: PolyartStudio Farmlands animals and Gobold, SICS Toon Farm people and animals).
   - Undecided or Avoid → ask first.
   - Prefer a Humanoid rig with clips that fit it.
   - For Synty, follow tools-unity-synty-characters.
   - For other vendors, `CharacterKit.Describe` their prefab, check the rig type (Generic animals can't use humanoid clips or the humanoid look layers) and confirm the materials are URP.
   - Vendor prefabs often carry vendor AI / controller scripts and NavMeshAgents (PolyartStudio `NPCStateMachine`, SICS `CharacterEdit`, Malbers AC). Those never go on our prefabs.
   - **Humanoid clips** for any humanoid model come first from the Kevin Iglesias library (ASSETS.md: Human packs Should; Spellcasting, Zombie and Giant Could), which covers locomotion, idles, hits, deaths, melee, guns, throws, work and emotes on one rig. Find a clip by the pack PDF in `Human Animations/`, then under `Animations/Male/<Combat|Idles|Misc|Movement|Social|Work>/`. Use the in-place clip, never its `[RM]` copy (in `RootMotion/` subfolders), but read the authored speed from the `[RM]` copy: `ClipInfo` reports 0 on the in-place one. Measured on HumanM: walk and crouch walk ~1.9 m/s, run ~3.8, sprint ~5.7; Zombie walk ~0.9. For what Kevin Iglesias lacks, the Synty action packs are Could: Bow Combat (archers), Sword Combat (combos with wind-up / hit sub-clips, parries), Idles, Emotes and Taunts. They have Sidekick-format clips that fit Sidekick models best (tools-unity-synty-characters §4).
2. **Model prefab** in `Content/<Kind>/<Name>/Model/` (one per look). Contract:
   - The root holds the Animator, with an avatar built from **this** hierarchy (humanoid if the clips are humanoid).
   - No controller (the entity assigns it). `applyRootMotion` off. Culling `CullCompletely`: safe because the mover moves the body, and off-screen enemies then skip their animation.
   - Mesh LODs where the model has none, kept to levels that don't tear (Sidekick: up to LOD2, selection bias 0, §5).
   - SkinnedMeshRenderer `updateWhenOffscreen` off; mesh not readable unless something reads it.
   - No vendor MonoBehaviours. One exception, on unique characters only: the Kevin Iglesias IK Helper Tool (PLUGINS.md), which must sit next to the Animator.
   - Synty Sidekick: `SidekickKit.Build`. Other vendors: a first-party prefab nesting or unpacking the vendor model, never an edited vendor prefab.
3. **Controller.** Write `AgentScripts/Characters/<Name>/<Name>.animspec.txt` (format in the header of `scripts/CharacterKit.cs`; the Goblin's is the example), then:
   ```bash
   unity command script run --file .agents/skills/aipocalypse-characters/scripts/CharacterKit.cs \
     --entry CharacterKit.BuildLocomotionController --args '["AgentScripts/Characters/Goblin/Goblin.animspec.txt"]'
   ```
   This writes `Content/<Kind>/<Name>/<Name>.controller` (rebuilt in place, GUID kept) and `Animations/<Name>_AdditiveNeutral.anim`.
   - Move thresholds are the clips' **authored** speeds.
   - Set the definition's `MoveSpeed` stat near the walk speed so the walk plays at its own pace.
   - Hits and deaths: `hit.*` / `heavy.*` lines (front, back, left, right) and `death` lines. Hits go in an upper-body override layer so the legs keep walking. Humanoid clips from any vendor retarget; the Goblin's come from Malbers Common (ASSETS.md). Kevin Iglesias has more: `Combat/HumanM@Damage01–05`, `Death01–05`, `CombatDamage01–02`, `CombatDeath01–06`, `Knockdown01`. Check them on the model with `Snapshot` first: `H_Death2.anim` threw the body off its origin.
   - Attacks and held weapons: `attack` lines (full-body base-layer states, tagged `Attack`), `combatIdle` (a second locomotion tree with this idle, used while fighting and after each swing) and `grip` + `grip.mask` (a held hand pose in an override layer masked to one hand, so the hand stays closed while walking). The Goblin's are Kevin Iglesias Human Melee (ASSETS.md): pick clips by its PDF's naming (`1H` one-handed, `_R` right hand, `HumanM` masculine rig), and prefer the ones its own demo controllers use. Guns, grenades and throws come from its Soldier and Throwing packs the same way.
   - **Two-handed holds** (rifle, bazooka, greatsword, spear) can leave the off hand beside the weapon on a model whose arms differ from the vendor rig. Check it with `Snapshot`. To fix one, use the IK Helper Tool's technique (PLUGINS.md): IK Pass on the layer, the off hand pulled onto an effector under the weapon hand, weighted by a curve in a copy of the clip under `Content/<Kind>/<Name>/Animations/` (never key the vendor FBX). Its component runs a per-instance `Update` and an IK pass, so it is fine on a few unique characters only. IK goals can only be set inside `OnAnimatorIK` on the Animator's GameObject, so a crowd enemy would need a first-party component that reads its weight there (an Animator curve, as `IKHelperToolAnimationCurve` does) with no `Update`. That isn't built: propose it and measure the IK pass cost first. Generic rigs can instead bake the fix into the clip (`tools-unity-animation-rigging`, `RigKit.Bake`), which costs nothing at runtime.
4. **Entity prefab.** For enemies, a builder in `AgentScripts/Characters/<Name>/Build<Name>.cs` (see `BuildGoblin.cs`):
   - **Base prefab:** start from the WanderCapsule enemy contract (aipocalypse-enemy-ai §1), unpacked. Replace its `Capsule` child with the model as child **`Model`**, on the root's layer (`Enemy`). Assign the controller on the model's Animator. Add `EnemyAnimator` (`_animator`, `_fidgets`, `_deaths`, `_attacks`) and `EnemyBody` (`_visual` = the `Model`, which sinks after death), plus the hit colliders on the bones (aipocalypse-enemy-ai §1 → weak spots). Point `EnemyBrain._definition` at the kind's `EnemyDefinition`, whose base stats, `Reaction`, `Wander` and `Animation` are set.
   - **Extra looks:** a **prefab variant** of the base that deletes the base `Model` (Unity 6.6 records it as a removed-GameObject override), adds another `Model` and overrides `EnemyAnimator._animator` and `EnemyBody._visual`. The Goblin's `Goblin_02…06.prefab` work this way. A builder must never rebuild a base prefab that has variants: the base's children would get new file IDs and break the variants' removed-Model overrides, so `BuildGoblin` creates only missing prefabs.
   - **Held weapon:** a first-party prefab holding an unpacked copy of the vendor model with no collider (hits must reach the body), nested on the hand bone of each look's `Model`. Place it from bone positions in the bind pose so it fits any skeleton: blade along little → index knuckles, edge along the fingers, grip a little into the palm (`BuildGoblin.HoldSword`); check with `Snapshot` and in Play (Snapshot samples one clip, so the grip layer's closed fingers show only in Play).
   - Re-running a builder updates in place. Upgrade existing prefabs with `PrefabUtility.LoadPrefabContents` → edit → `SaveAsPrefabAsset` (`BuildGoblin.Upgrade`), which keeps file IDs.
5. **Scene.**
   - A placement script (`PlaceGoblins.cs`) puts instances under `Enemies`, snaps them to the terrain and saves the scene. Saving makes `EnemyInstaller` collect them.
   - Ask before changing scene structure beyond the task.
   - A dirty open scene (vendor demo scenes get dirtied by the Sidekick window) blocks scene switches and `test run`: ask before discarding.
6. **Verify** (§4). 7. **Docs:** CODE_STRUCTURE.md (`Content/` rows; any new script), ASSETS.md (the pack's row now says what uses it), ARCHITECTURE.md → AI when the runtime changes.

## 3. Parameter contract

Every locomotion controller has these parameters, so `EnemyAnimator` drives any vendor's clips:

| Parameter | Type | Meaning |
| --- | --- | --- |
| `Speed` | float | Ground speed in m/s (damped). Base layer: 1D blend tree, idle at 0, then each move clip at its authored speed. |
| `LookX`, `LookY` | float −1..1 | Head (and half-weight body) look: additive 2D trees, a zero-delta neutral at the centre. Standing: random glances. Walking: into the turn. |
| `Lean` | float −1..1 | Additive lean into turns while walking. |
| `Fidget` | trigger | With `FidgetIndex` (int, 0..n−1): plays fidget state n from Locomotion when Speed < 0.2. It ends at 90% or as soon as Speed > 0.3. |
| `Hit` | trigger | With `HitIndex` (int): where the hit came from, 0 front, 1 back, 2 left, 3 right, +4 when heavy (push speed ≥ `EnemyReactionSettings.HeavyHitSpeed`). Plays `Hit_<index>` in the Hit layer from any state; a new hit restarts it. |
| `Dead` | bool | With `DeathIndex` (int): `Death_<side>_<n>` (spec `death.front|back|left|right`, index 10 × (side + 1) + n, `_sideDeaths` counts) falls away from the killing hit; otherwise random 0..`_deaths`−1 → `Death_<index>`. In the base layer from any state. `EnemyAnimator.Die` also zeroes the upper layers' weights and switches culling to AlwaysAnimate until revived. |
| `Combat` | bool | Fighting stance: Locomotion ↔ `Combat` (the same moves with the spec's `combatIdle`, no fidgets). Set by `EnemyBrain.Target` (engaged or not) through `EnemyAnimator.SetCombat`; swings return to `Combat` while it's on. |
| `Appear` | trigger | `Appear` in the base layer from Locomotion (the spec's `appear` clip; none = the trigger does nothing), back to Locomotion at 90% or once Speed > 0.3. Set by `EnemyAnimator.Appear` when the spawner brings an enemy in (`EnemyBrain.Appear`); reset when the appear time ends, so a culled model doesn't play it late. |
| `Celebrate` | bool | With `CelebrateIndex` (int, random 0..`_celebrations`−1): `Celebrate_<index>` (the spec's `celebrate` clips) from Locomotion while Speed < 0.2, repeated until the bool goes off or Speed > 0.3. Set by `EnemyAnimator.Celebrate` (the Melee subtree's `Celebrate` task, after the target died). |
| `Attack` | trigger | With `AttackIndex` (int, random 0..`_attacks`−1): `Attack_<index>` (tag `Attack`) in the base layer from any state, back to Combat (or Locomotion when not fighting) at 90%. Set by `EnemyAnimator.Attack` (melee tasks); the enemy stands while it plays. |

Layers: Base (Locomotion, Combat, fidget, death and attack states), HeadLook (additive, 1), BodyLook (additive, `bodyLook.weight`), Lean (additive, 1), Hit (override, 1, upper-body mask sub-asset `UpperBody`), Grip (override, 1, the spec's hand mask; only with a `grip` line).
- A clip set without look or lean clips just leaves those layers out.
- A Generic rig gets its own controller from the same spec format.
- Adding a parameter means updating `EnemyAnimator`, the builder and the contract test together.

## 4. Verify (don't assume)

- **Model:** `CharacterKit.Describe(prefab)` (bones vs unique bones, blendshapes, readable, culling, avatar). `CharacterKit.Snapshot(prefab, png, clip, time, yaw, size, meshLod)` renders offscreen (no scene touched) posed by a clip, optionally at a forced Mesh LOD; open the PNG.
  - Use a scratchpad path for `png`, never `Assets/`.
  - Yaw ~25 shows the front three-quarters.
- **Controller and prefabs:** `test run --mode editor`. `AnimatedEnemyPrefabs_AreWiredToTheParameterContract` catches a missing Animator, avatar, controller or parameter, root motion turned on, a fidget or death count too high, or missing settings. `EnemyCombatTests` checks every enemy prefab has an `EnemyBody` with its visual and a collider, and every definition has every stat.
- **Play mode** (AGENTS.md editor gotchas):
  - Enter Play, set `Application.runInBackground = true`.
  - Sample with an async probe like `AgentScripts/Characters/Goblin/ProbeGoblins.cs` (`Sample(seconds, every)`): position, mover speed, `Speed` / `LookX` / `Lean`, current state or clip.
  - Look with `ProbeGoblins.Frame` + `capture game_view --camera GoblinCam --source camera`, read inline (base64) rather than `--save_path`, which imports under `Assets/`.
  - Expect Speed ≈ MoveSpeed while walking, fidget clips during stops, glances changing while standing.
  - Damage with `AgentScripts/Characters/Goblin/ProbeCombat.cs`:
    - `Fire(goblin, distance, seconds, item)` stands the player in front, gives the item (AK47, FireAxe…) and holds the attack through a virtual mouse.
    - `Strike(goblin, damage, force, seconds, every)` deals one hit through `DamageService.DealHit` and samples HP, position and the Hit layer state in the same call.
    - `State()` lists every enemy, and `Revive(goblin)` re-enables a dead one.
    - Melee: `Place(goblin, distance, seconds, every)` stands the player `distance` m in front of the goblin (negative = behind) and samples it: `target`, `playerDist`, `facing` (degrees off the player) and the base clip. Expect a chase at ~3.5 m/s, a stop at the attack range, swings with a cooldown, and the target dropped past the lose radius.
    - Off-screen enemies don't animate (`CullCompletely`), so face them before judging a hit reaction.
  - Then stop Play and check `console get --level error` for new entries.

## 5. Performance (crowds)

Per-character cost drivers, how to measure (`PerfSuite.Crowd`), the measured per-goblin timings, why the Mesh LOD bias went back to 0, what was tried without gain, and the next levers, cheapest first: [references/performance.md](references/performance.md). Read it before changing enemy counts, LODs, shadows or Animator layers.

## 6. Traps learned building the Goblins

- **Save only what you wrote** (AGENTS.md editor gotchas). During the Goblin work `SaveAssets()` re-saved WorldMap's terrain; the kits save per asset.
- **Instantiating in the active scene dirties it.** Build models in a throwaway additive scene (`SidekickKit`, `BuildGoblin`) and preview through `PreviewRenderUtility.InstantiatePrefabInScene` (`CharacterKit.Snapshot`).
- **Unpack before restructuring.** A prefab instance's hierarchy can't change, so unpack it first: `DeoptimizeTransformHierarchy` silently does nothing on a connected instance.
- **Optimized hierarchies don't skin outside an updating Animator** (tools-unity-synty-characters §2, `optimizeHierarchy`).
- **Posing:** `AnimationClip.SampleAnimation` poses a humanoid instance in a preview scene; a PlayableGraph evaluated there didn't.
- **Root speed:** `ClipInfo.averageSpeed` is 0 when root motion is baked into the pose. Take speeds from the vendor's documentation.
- **IDs:** `Object.GetInstanceID()` is obsolete (an error) in Unity 6.6. Seed per-instance randomness with `Random.Range`, or use `GetEntityId()`.
