# Third-party plugins & tools

Everything third-party in the project: what it is, where it lives, and whether we use it. **Check this before building a system** (an installed plugin may already do it), and **update it in the same change** whenever a plugin is added, removed, upgraded, or used in a new way.

Status:
- **Core**: the architecture depends on it.
- **Use**: approved for game code, following the rules below.
- **Content**: art/audio/animation we reference or make variants of.
- **Editor**: editor-only tooling, never referenced by runtime game code.
- **Undecided**: installed but no decision yet. Ask before relying on it.

Vendor folders (`Assets/Plugins/`, `Assets/_StoreAssets/`, `Assets/Synty/`, `Packages/com.*` local packages) are never modified. Make variants or wrappers under `Assets/_Game/`.

## General rules

- No plugin singletons or static event systems in gameplay code (`MMEventManager`, `X.Instance`, …). Wrap a plugin behind a service registered in VContainer when game code needs it. Cross-system events go through MessagePipe.
- `TimeService` owns `Time.timeScale`. No plugin may set it directly (Feel's time-scale feedbacks, BD's `Time/SetTimeScale`, …).
- UI is uGUI. Code tweens use DOTween.
- Prefer each plugin's pooling, batching and caching (Damage Numbers Pro and BroAudio pools, GPUI instancing). Keep per-instance plugin components off crowd entities. Exceptions: BD's `BehaviorTree` (Manual mode, centrally ticked) and A*'s `FollowerEntity`.
- When unsure how a plugin works, read its docs or source instead of guessing. Some ship their own guide, e.g. `Assets/Plugins/BroAudio/CLAUDE.md`.

## Code libraries (UPM, from git)

| Library | Version | Status | Notes |
|---|---|---|---|
| VContainer (`jp.hadashikick.vcontainer`) | 1.19.0 (pinned tag) | Core | DI. Lifetime scopes, per-system installers, `EntityScope` on entity prefabs. Root via `_Core/_Bootstrap/VContainerSettings.asset` (Preloaded Assets) → `ProjectLifetimeScope.prefab`. `DisableScriptModifier` is on: otherwise VContainer overwrites any newly created `*LifetimeScope.cs` with its template. |
| MessagePipe (+ `.vcontainer`) | 1.8.2 | Core | Cross-system events. Registered once in the root scope (open generics on, no per-type registration). Skill: `tools-unity-messagepipe`. |
| UniTask | 2.5.11 | Core | Async. Use instead of coroutines. |
| UIEffect (`com.coffee.ui-effect`) | 5.11.7 | Use | uGUI effects. |

MessagePipe, UniTask and UIEffect track the repo HEAD (no tag pinned), so a package resolve can upgrade them. Versions above are what `Library/PackageCache` holds.

## Runtime plugins

| Plugin | Version | Location | Status | Notes |
|---|---|---|---|---|
| A* Pathfinding Project | 5.4.7 | `Packages/com.arongranberg.astar` | Core | Enemy navigation with `FollowerEntity` agents (driven through `IAstarAI`). `AstarPath.active` only inside the `INavigation` service. Scenes with agents need the `Navigation` prefab (AstarPath + RVOSimulator). Skill: `tools-unity-astar`. |
| Behavior Designer Pro (+ Opsive Graph Designer 3.3.4, Opsive Shared 2.3.2) | 3.3.3 | `Packages/com.opsive.*`, `Assets/Plugins/Opsive` | Core | Behaviour trees for **all** AI: crowd enemies, companions, drone, bosses. Crowd trees run in `Manual` mode, ticked time-sliced by `EnemyManager`; tasks delegate to our parts. Skill: `tools-unity-behavior-designer`. |
| GPU Instancer Pro (Core, Prefab, Terrain) | 0.12.20 | `Packages/com.gurbu.gpui-pro*`, `Assets/Plugins/GPUInstancerPro` | Use | Foliage and props instancing. |
| GPU Instancer Pro – Crowd Animations | 0.10.5 | `Packages/com.gurbu.gpui-pro.crowd-animations` | Undecided | Candidate for crowd enemy rendering. |
| DOTween + DOTween Pro (+ DemiLib) | — | `Assets/Plugins/Demigiant` | Use | Code tweens, including scaled repair-result UI feedback and additive weapon kick. Core references DOTween.Modules for uGUI extensions. |
| Feel (MoreMountains) | — | `Assets/_StoreAssets/GameFeel/Feel` | Use | Game feel / feedbacks. Views play `MMF_Player`s in response to events, never services or weapons directly. No time-scale, sound, scene-loading, floating-text or broadcast feedbacks, and no `MMF_Player` on crowd enemies. Hit stop goes through our `MMF_HitStop` (→ `TimeService.RequestHitStop`, via `FeedbackContext`). Camera shake: `MMF_CinemachineImpulse` (Scaled timescale mode on every impulse feedback) heard by the `CinemachineImpulseListener` on the player's Cinemachine camera. First use: `Player/GunJamFeedback` (gun jam / repair beats). `Aipocalypse.Core` references `MoreMountains.Tools`. Skill: `tools-unity-feel` (feedback catalog in its references). |
| Damage Numbers Pro | — | `Assets/_StoreAssets/GameFeel/DamageNumbersPro` | Use | Hit numbers: `_Core/UI/Prefabs/Widgets/DamageNumber.prefab` (built with DNP's own menu item, pooling on, prewarmed by `HitFeedbackHud`). DNP's setup rewrites its font's material, so our prefab uses the first-party copy `Data/UI/DamageNumberFont.asset` and the vendor font stays untouched. Same hook rule as Feel: a view spawns numbers in response to `DamageDealt`. |
| BroAudio | — | `Assets/Plugins/BroAudio` | Use | All game audio. Read its `CLAUDE.md`. Game code plays through `AudioService` (`_Core/AudioSystem`, root scope), never the static `BroAudio` facade. One-shots use `Play` / `PlayAt`; a sound the caller keeps (the player's breath loop) uses `Start`, then `SetVolume` / `Pause` / `UnPause` / `Stop` (fades on volume and stop) on the returned `IAudioPlayer`. Set Loop on the entity for loops. Libraries (`AudioAsset`) and their entities live in `Data/Audio/` (`Guns`, `Melee`, `RepairFeedback`, `Player`, `Combat`); each entity holds its clips, volume, random clip / pitch settings. Edit them in the Library Manager (`Tools > BroAudio > Library Manager`); data fields reference them as `SoundID`. The entities were first created from the old `AudioClip` fields by an editor script using BroAudio's public `AudioEntity.CreateNewInstance`. |
| Easy Save 3 | — | `Assets/Plugins/Easy Save 3` | Use | Persistence (save system postponed, see ARCHITECTURE.md). |
| Odin Inspector | — | `Assets/Plugins/Sirenix` | Use | Inspector attributes in game code. Mostly editor-side. |
| COZY 3: Stylized Weather (+ Blocks 1.7.2, Habits 1.4.0, Horizon 1.0.0, Plume 1.4.3) | 3.6.23 | `Packages/com.distantlands.cozy.*` | Undecided | Sky, weather, time of day, clouds. Must not own game time if a day/night system is ours. |
| Kronnect Beautify, Volumetric Fog 2, Global Snow, Highlight Plus, Transitions Plus | — | `Assets/_StoreAssets/Rendering/Kronnect` | Undecided | Post FX, fog, snow, outlines, screen transitions. Overlaps with URP post-processing and COZY fog. |
| Stylized Grass, Stylized Water 3 (Staggart) | — | `Assets/_StoreAssets/Rendering/Staggart Creations` | Use | Grass and water shaders. |
| Malbers Animal Controller (+ Animals packs) | — | `Assets/_StoreAssets/Animals/Malbers Animations` | Undecided | Heavy per-instance controller. Fine for a few unique animals (dog, cat); never for crowds. |
| Lean (CW: LeanCommon, LeanGUI, LeanTexture(+), LeanTransition) | — | `Assets/Plugins/CW` | Undecided | LeanTransition overlaps with DOTween; prefer DOTween. |
| Text Studio | 1.2.0 | `Assets/Plugins/TextStudio` | Undecided | Text effects. |
| Advanced FPS Counter (CodeStage) | — | `Assets/Plugins/CodeStage` | Use | Debug overlay only (dev builds). |

## Unity packages worth noting

Default Unity modules are omitted. Full list: `Packages/manifest.json`.

| Package | Version | Status | Notes |
|---|---|---|---|
| Input System | 1.20.0 | Core | New Input System only. `Assets/Settings/InputSystem_Actions.inputactions`, read only through `InputService` via the generated wrapper `GameInputActions` (`_Core/InputSystem/`, regenerated on import). The asset is also the project-wide actions, which only feed UI for now. Editor: keyboard/mouse reach the game only while the Game view has focus. |
| URP / Shader Graph / VFX Graph | 17.6.0 | Core | Rendering. First-person held models / muzzle flashes use a HeldWeapon-only Overlay camera with Clear Depth in the player camera stack; world effects keep normal depth. |
| Cinemachine | 6.6.0 | Core | Owns the camera. The player rig (`CinemachineBrain` + a `CinemachineCamera` hard-locked to the player's camera pivot) lives inside the Player prefab; scenes have no camera of their own. |
| Test Framework | 1.8.0 | Core | `unity command run_tests`. |
| Unity Pipeline (`com.unity.pipeline`) | 0.8.0-exp.1 | Editor | Unity CLI bridge. Skill: `unity-pipeline`. |
| Entities / Entities Graphics | 6.6.0 | Undecided | Required by BD Pro. Our own code stays MonoBehaviour (ARCHITECTURE.md). |
| Unity Behavior | 1.0.16 | Remove | Behavior Designer Pro was chosen. Don't use; remove the package. |
| AI Navigation | 2.0.15 | Undecided | Overlaps with A*. Not used for enemies. |
| Visual Scripting | 1.9.12 | Undecided | Probably a dependency of another package or unused. |
| Addressables | 2.11.2 | Undecided | |
| Localization | 1.5.13 | Use | |
| Animation Rigging | 6.6.0 | Use | AK47, MP5, pistol, pump shotgun and double barrel: Multi-Parent Constraints drive shared-hand targets from gun parts or an authored free-hand control. GunAnimation evaluates the rig with its manual controller graph in the central weapon tick; no per-hand Update. Editor Animation-window previews use RigBuilder normally. See HANDS.md. |
| Splines | 2.9.1 | Use | |
| Terrain Tools | 5.3.3 | Editor | |
| Timeline | 6.6.0 | Use | |
| Recorder / Memory Profiler / FBX Exporter | 5.1.7 / 1.1.12 / 5.1.6 | Editor | |

## Editor-only tools

| Tool | Version | Location | Notes |
|---|---|---|---|
| MicroVerse (+ Ambiance, Objects, Splines, Vegetation, Demo) | 1.7.0 | `Packages/com.jbooth.microverse*` | Terrain authoring, stripped from builds (only baked TerrainData ships). BK Pure Nature 2 presets in `Assets/_StoreAssets/Env/MicroVerse-Presets` (only the 6 installed BK biomes resolve). Ambiance (`AmbientArea`/`AmbianceMgr`) runs at runtime as a static singleton: don't use it (BroAudio owns audio). Skill: `tools-unity-microverse`. |
| Spline Spawner (Staggart) | 1.2.0 | `Packages/xyz.staggart-creations.spline-spawner` | Spawn objects along splines. |
| Vegetation Spawner (Staggart) | — | `Assets/_StoreAssets/Rendering/Staggart Creations` | Terrain vegetation placement. |
| Rowlan height stamps (Alpine Mountains, Highlands, Meadows, Swamp, Valleys) | — | `Assets/_StoreAssets/Env/Rowlan/Terrain/Stamps` | 4K R16 heightmaps with MicroVerse content collections. Keep `Brushes.zip` zipped (Terrain Tools loads every brush into memory). |
| Rowlan Genesis for MicroVerse | 3.5.0 | `Assets/_StoreAssets/Env/Rowlan/Genesis` | MicroVerse wizards (heightmap → collection converter, stamp placement/validation, terraform). The Genesis wizard writes to `Assets/Rowlan/Genesis Data` and replaces MicroVerse setups. Gameplay (legacy Input rig) and Visual Ambience (built-in post-processing) must not be used. |
| Rowlan Fullscreen Editor Play Mode | 1.9.0 | `Assets/Plugins/Rowlan` | |
| Asset Usage Detector | 2.6.3 | UPM git (yasirkula) | Find asset references. |
| Inspect Plus | 2.1.1 | UPM git (yasirkula) | |
| Audio Tools | 1.2.0 | `Assets/Plugins/AudioTools` | |
| Audio Preview Tool | — | `Assets/Plugins/WarpedImagination` | |
| Easy Collider Editor | — | `Assets/Plugins/EasyColliderEditor` | Collider authoring. |
| KINEMATION Retarget Pro | — | `Assets/Plugins/KINEMATION` | Animation retargeting. |
| FImpossible Creations shared tools | — | `Assets/Plugins/FImpossible Creations` | |
| Auxilium Dev Toolbox | — | `Assets/Plugins/Auxilium Dev Toolbox` | |
| Scene List Tool | — | `Assets/Plugins/SceneListTool` | |
| Colorize Palette Modifier (Smitesoft) | — | `Assets/Plugins/Smitesoft` | |
| Smart Mouse (TelePresent) | — | `Assets/Plugins/TelePresent` | |
| Better Inspector (Tiny Giant Studio) | — | `Assets/Plugins/Tiny Giant Studio` | |
| Ultimate Preview / Ultimate Thumbnails (Voxel Labs) | — | `Assets/Plugins/Voxel Labs` | |

## Content packs

| Pack | Location |
|---|---|
| Synty Polygon Generic, Polygon Prototype, Sidekick Characters | `Assets/Synty` |
| Synty Animation Base Locomotion, Goblin Locomotion | `Assets/Synty` |
| Synty Interface Core, Interface Apocalypse HUD | `Assets/Synty`. HUD art: **sprites and the SairaCondensed TMP fonts only**, never its prefabs or sample scripts. Our widgets reference the sprites through style assets ([UI.md](UI.md)); the repair QTE uses Apocalypse ring/bar/panel sprites with first-party timing views; item icons come from `Icons_Weapons` and the hotbar key cap from Interface Core's `Icons_Input` |
| Low Poly Weapon Bundle Packs 1–3 | `Assets/_StoreAssets/Weapons`. Gun models: nest the store prefab as the `Model` child of a `Gun_Base` variant (`_Core/WeaponSystem/Prefabs/Guns/`), shadows off, no colliders ([WEAPONS.md](WEAPONS.md#adding-a-gun)). In use: AR_B (AK-47), Pistol_E |
| VFX: Gabriel Aguiar Unique Projectiles Vol 1, Hovl Studio packs | `Assets/_StoreAssets/VFX`. Our effect prefabs (`_Core/WeaponSystem/Prefabs/Effects/`: muzzle flash, tracer, impact) use the Unique Projectiles materials; they're pooled through `EffectPool`, never instantiated per shot. Hovl Studio: not used yet |
| Animpic Polygon Mega Survival (Kit, Construction, Tools, Medical, Weapons), Poly-Survival Food / Melee / Workshop | `Assets/_StoreAssets/Env/Animpic`. Melee models are used the same way as the gun models |
| BK Pure Nature (Asian Valley, Jungle, Meadows, Mesa Desert, Mountains, Redwood) | `Assets/_StoreAssets/Env/BK` |
| ANGRY MESH Stylized Pack – Meadow | `Assets/_StoreAssets/Env/ANGRY MESH` |
| NewLua Stylized Farm Crops | `Assets/_StoreAssets/Env/NewLua Studios` |
| Malbers Animals Packs | `Assets/_StoreAssets/Animals/Malbers Animations` |
| TextMesh Pro samples (fonts, shaders) | `Assets/Samples/TextMesh Pro` |

## Open overlaps to decide

The capsule wander demo in `Assets/_Game/Scenes/Prototype.unity` uses a shared Behavior Designer Pro subtree (`Data/Enemies/Wander.asset`), with custom choose/walk/pause tasks and Manual trees ticked by `EnemyManager`. A* FollowerEntity handles movement and local avoidance; `Prefabs/Navigation.prefab` contains AstarPath, a small terrain-following grid scanned on startup, and RVOSimulator. The `Enemy` layer keeps the capsules out of terrain navigation and grounding queries.

- **A\* vs AI Navigation.** A* is the choice for enemies; AI Navigation can probably go.
- **Tweening: DOTween vs LeanTransition vs Feel's tweens.** DOTween for code; Feel only through feedbacks.
- **Atmosphere: COZY vs Kronnect Volumetric Fog / Global Snow vs URP volumes.** Decide who owns fog, sky and weather.
