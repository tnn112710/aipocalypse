---
name: aipocalypse-characters
description: How characters get into aipocalypse, whatever the vendor (Synty, Malbers, PolyartStudio, SICS, anything imported later). Covers enemies, NPCs, companions, animals and bosses. Choosing a source pack by its ASSETS.md tag; the model prefab vs entity prefab split under Content/<Kind>/<Name>/; rig / avatar / import checks. The animator parameter contract (Speed, LookX/LookY, Lean, Fidget/FidgetIndex) and building controllers from a text spec with scripts/CharacterKit.cs. EnemyAnimator, LocomotionAnimation and EnemyAnimationManager (a central tick, no root motion, A* FollowerEntity moves the body). Prefab variants for extra looks, placing characters in WorldMap, verifying with offscreen snapshots, Play-mode probes and the wiring test, and crowd performance levers. Use for any task that adds, swaps, re-skins, animates or tunes a character model in the game, together with the vendor skill (tools-unity-synty-characters for Synty) and aipocalypse-enemy-ai for behaviour trees and navigation.
---

# Characters in aipocalypse

A character is two things kept apart:
- a **model prefab**, which is the vendor's look, rebuilt or swapped at will;
- an **entity prefab**, which holds our components, nests the model, and is what scenes and spawners use.

Vendor skills make models: **tools-unity-synty-characters** for Synty. This skill turns any model into a working character and keeps the vendors interchangeable. Behaviour (trees, navigation, spawning) is **aipocalypse-enemy-ai**.

Worked example, built with this skill: **`Content/Enemies/Goblin/`**, six wandering Sidekick goblins in WorldMap (Goblin 1–6 outside the home gate): looks 01–05 copied from Goblin Fighters `.sk` files, look 06 written from presets. The setup scripts are in `AgentScripts/Characters/Goblin/`.

## 1. What exists

| Piece | Where | Role |
| --- | --- | --- |
| `EnemyAnimator` | `_Core/EnemySystem/` | On the enemy root: `_animator` (the Model's Animator), `_fidgets` (fidget states in the controller). Writes the contract parameters. No `Update`. |
| `LocomotionAnimation` | `_Core/EnemySystem/` | Plain C# rules: where to look, lean, when to fidget, from speed and turn rate. EditMode-tested. |
| `EnemyAnimationManager` | `_Core/EnemySystem/` | `ITickable` registered by `EnemyInstaller`: advances every registered enemy's `EnemyAnimator` each frame on scaled time (pause freezes it). Profiler marker `Aipocalypse.EnemyAnimationManager.Animate`. |
| `EnemyAnimationSettings` | SO, `Data/Enemies/EnemyAnimationSettings.asset` | Glance interval / range, look and lean into turns, fidget delay / chance, damping. Linked from `EnemyDefinition.Animation`. |
| `EnemyMover.Velocity`, `EnemyBrain.Animation` | `_Core/EnemySystem/` | The animator reads movement through the mover; the brain finds the optional `EnemyAnimator` on `Start` and initializes it on activation. |
| `CharacterKit.cs` | `scripts/` here | `Describe`, `Snapshot`, `ClipInfo`, `BuildLocomotionController`: run with `unity command script run`. |
| `EnemyAnimationTests` | `Tests/EditMode/` | Rules, plus `AnimatedEnemyPrefabs_AreWiredToTheParameterContract` over every enemy prefab with an `EnemyAnimator`. |

**Not built** (don't assume): animated NPCs, companions, animals or bosses; LODs; attack / hit / death animations; pooling.
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
2. **Model prefab** in `Content/<Kind>/<Name>/Model/` (one per look). Contract:
   - The root holds the Animator, with an avatar built from **this** hierarchy (humanoid if the clips are humanoid).
   - No controller (the entity assigns it). `applyRootMotion` off. Culling `CullUpdateTransforms`.
   - SkinnedMeshRenderer `updateWhenOffscreen` off; mesh not readable unless something reads it.
   - No vendor MonoBehaviours.
   - Synty Sidekick: `SidekickKit.Build`. Other vendors: a first-party prefab nesting or unpacking the vendor model, never an edited vendor prefab.
3. **Controller.** Write `AgentScripts/Characters/<Name>/<Name>.animspec.txt` (format in the header of `scripts/CharacterKit.cs`; the Goblin's is the example), then:
   ```bash
   unity command script run --file .agents/skills/aipocalypse-characters/scripts/CharacterKit.cs \
     --entry CharacterKit.BuildLocomotionController --args '["AgentScripts/Characters/Goblin/Goblin.animspec.txt"]'
   ```
   This writes `Content/<Kind>/<Name>/<Name>.controller` (rebuilt in place, GUID kept) and `Animations/<Name>_AdditiveNeutral.anim`.
   - Move thresholds are the clips' **authored** speeds.
   - Set `EnemyDefinition.MoveSpeed` near the walk speed so the walk plays at its own pace.
4. **Entity prefab.** For enemies, a builder in `AgentScripts/Characters/<Name>/Build<Name>.cs` (see `BuildGoblin.cs`):
   - **Base prefab:** start from the WanderCapsule enemy contract (aipocalypse-enemy-ai §1), unpacked. Replace its `Capsule` child with the model as child **`Model`**, on the root's layer (`Enemy`). Assign the controller on the model's Animator. Add `EnemyAnimator` (`_animator`, `_fidgets`). Point `EnemyBrain._definition` at the kind's `EnemyDefinition`, whose `Wander` and `Animation` are set.
   - **Extra looks:** a **prefab variant** of the base that deletes the base `Model` (Unity 6.6 records it as a removed-GameObject override), adds another `Model` and overrides `EnemyAnimator._animator`. The Goblin's `Goblin_02…06.prefab` work this way. A builder must never rebuild a base prefab that has variants: the base's children would get new file IDs and break the variants' removed-Model overrides, so `BuildGoblin` creates only missing prefabs.
   - Re-running a builder updates in place.
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

Layers: Base (Locomotion + fidget states), HeadLook (additive, 1), BodyLook (additive, `bodyLook.weight`), Lean (additive, 1).
- A clip set without look or lean clips just leaves those layers out.
- A Generic rig gets its own controller from the same spec format.
- Adding a parameter means updating `EnemyAnimator`, the builder and the contract test together.

## 4. Verify (don't assume)

- **Model:** `CharacterKit.Describe(prefab)` (bones vs unique bones, blendshapes, readable, culling, avatar). `CharacterKit.Snapshot(prefab, png, clip, time, yaw, size)` renders offscreen (no scene touched) posed by a clip; open the PNG.
  - Use a scratchpad path for `png`, never `Assets/`.
  - Yaw ~25 shows the front three-quarters.
- **Controller and prefabs:** `test run --mode editor`. `AnimatedEnemyPrefabs_AreWiredToTheParameterContract` catches a missing Animator, avatar, controller or parameter, root motion turned on, a fidget count too high, or missing settings.
- **Play mode** (AGENTS.md editor gotchas):
  - Enter Play, set `Application.runInBackground = true`.
  - Sample with an async probe like `AgentScripts/Characters/Goblin/ProbeGoblins.cs` (`Sample(seconds, every)`): position, mover speed, `Speed` / `LookX` / `Lean`, current state or clip.
  - Look with `ProbeGoblins.Frame` + `capture game_view --camera GoblinCam --source camera`, read inline (base64) rather than `--save_path`, which imports under `Assets/`.
  - Expect Speed ≈ MoveSpeed while walking, fidget clips during stops, glances changing while standing.
  - Then stop Play and check `console get --level error` for new entries.

## 5. Performance (crowds)

Per character: one Animator (state machine + 4 layers), one SkinnedMeshRenderer with ~100–115 bones, and the vertex count.
- The Goblins are **17–23k vertices each**. That's fine for a few, but 300 of them (the ROADMAP `Proto_Stress` gate) is ~6–7M skinned vertices.
- Measure before the crowd work (unity-optimization-pc). Levers, cheapest first:
  - Culling `CullUpdateTransforms`, or `CullCompletely` (safe without root motion).
  - Fewer layers for distant enemies (look and lean are cosmetic).
  - Fewer vertices: smaller parts, or a vendor LOD pack, since Sidekick ships no LODs.
  - Single combined mesh, no blendshapes (already done by SidekickKit).
  - GPU skinning (Player Settings).
  - **GPU Instancer Pro – Crowd Animations** (PLUGINS.md, Undecided: ask).
  - Optimize Transform Hierarchy is unverified here (tools-unity-synty-characters §2).
- `EnemyAnimationManager` itself is a loop of `SetFloat`s with cached hashes: zero allocations.

## 6. Traps learned building the Goblins

- **Save only what you wrote.** `AssetDatabase.SaveAssets()` writes every dirty asset in the editor. During the Goblin work it re-saved WorldMap's terrain (a 59 MB LFS blob). Use `AssetDatabase.SaveAssetIfDirty(asset)` per asset, as the kits do, and check `git status` for unrelated files before finishing.
- **Instantiating in the active scene dirties it.** Build models in a throwaway additive scene (`SidekickKit`, `BuildGoblin`) and preview through `PreviewRenderUtility.InstantiatePrefabInScene` (`CharacterKit.Snapshot`).
- **Unpack before restructuring.** A prefab instance's hierarchy can't change, so unpack it first: `DeoptimizeTransformHierarchy` silently does nothing on a connected instance.
- **Optimized hierarchies don't skin outside an updating Animator.** Previews show nothing, and thumbnail generators log "Bones do not match bindpose".
- **Posing:** `AnimationClip.SampleAnimation` poses a humanoid instance in a preview scene; a PlayableGraph evaluated there didn't.
- **Root speed:** `ClipInfo.averageSpeed` is 0 when root motion is baked into the pose. Take speeds from the vendor's documentation.
- **IDs:** `Object.GetInstanceID()` is obsolete (an error) in Unity 6.6. Seed per-instance randomness with `Random.Range`, or use `GetEntityId()`.
