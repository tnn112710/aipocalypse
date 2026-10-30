# Third-party plugins & tools

Every third-party code library, plugin and editor tool in the project, grouped by purpose, with which one to use for each. Art, animation, UI, VFX and audio packs are in [ASSETS.md](ASSETS.md). **Check this before building a system** (an installed plugin may already do it), and **update it in the same change** whenever a plugin is added, removed, upgraded, or used in a new way.

Vendor folders (`Assets/Plugins/`, `Assets/_StoreAssets/`, `Assets/Synty/`, `Packages/com.*` local packages) are never modified. Make variants or wrappers under `Assets/_Game/`.

## Priority tags

Each package is declared once, with one tag. **Notes** say what it's used for and any part that's off-limits (e.g. Feel: "no time-scale feedbacks").

| Tag | Meaning |
| --- | --- |
| **Must** | The only choice for what its row says it's used for; the architecture or a project rule depends on it. Don't use alternatives for that. |
| **Should** | The default pick, approved for game code (even if nothing uses it yet). Using another instead needs a reason, written in its row. |
| **Could** | Fallback, only for what the Must/Should choice doesn't cover, or an optional convenience. |
| **Maybe** | Last resort, only when nothing tagged Must/Should/Could covers it, and ask first. |
| **Avoid** | Reference only: no new game code, asset or scene may use it. Kept installed (a dependency, or removal not decided) unless the row says remove. |
| **Undecided** | Not ranked yet. Ask before relying on it. The row may suggest a tag. |

Editor-only tools never ship and are never referenced by runtime code; their tag only says which tool to reach for.

## General rules

- No plugin singletons or static event systems in gameplay code (`MMEventManager`, `X.Instance`, …). Wrap a plugin behind a service registered in VContainer when game code needs it. Cross-system events go through MessagePipe.
- `TimeService` owns `Time.timeScale`. No plugin may set it directly (Feel's time-scale feedbacks, BD's `Time/SetTimeScale`, …).
- UI is uGUI. Code tweens use DOTween. All audio goes through BroAudio (`AudioService`): no plugin plays its own audio (MicroVerse Ambiance, SW3 `RiverAudioManager`, Malbers' controller sounds).
- Prefer each plugin's pooling, batching and caching (Damage Numbers Pro and BroAudio pools, GPUI instancing). Keep per-instance plugin components off crowd entities. Exceptions: BD's `BehaviorTree` (Manual mode, centrally ticked) and A*'s `FollowerEntity`.
- When unsure how a plugin works, read its docs or source instead of guessing. Some ship their own guide, e.g. `Assets/Plugins/BroAudio/CLAUDE.md`.

MessagePipe, UniTask, UIEffect and the yasirkula tools track their repo HEAD (no tag pinned), so a package resolve can upgrade them. Unity's own packages (Input System, URP, Cinemachine, …) are not listed: see `Packages/manifest.json` and the system docs. Versions below are what `Library/PackageCache` or the package's `package.json` holds; "—" means the asset has no version file.

## Architecture, async, data

| Plugin | Version | Location | Tag | Notes |
| --- | --- | --- | --- | --- |
| VContainer (`jp.hadashikick.vcontainer`) | 1.19.0 (pinned tag) | UPM git | Must | DI. Lifetime scopes, per-system installers, `EntityScope` on entity prefabs. Root via `_Core/_Bootstrap/VContainerSettings.asset` (Preloaded Assets) → `ProjectLifetimeScope.prefab`. `DisableScriptModifier` is on: otherwise VContainer overwrites any newly created `*LifetimeScope.cs` with its template. Skill: `aipocalypse-architecture`. |
| MessagePipe (+ `.vcontainer`) | 1.8.2 | UPM git | Must | Cross-system events. Registered once in the root scope (open generics on, no per-type registration). Awaitable events use `IAsyncPublisher` / `IAsyncSubscriber` (`PlayerRespawnFading` and `PlayerRespawning`, awaited by `PlayerRespawnFlow`; the death screen subscribes). |
| UniTask | 2.5.11 | UPM git | Must | Async. Use instead of coroutines. `UNITASK_DOTWEEN_SUPPORT` is off. |
| Odin Inspector | — | `Assets/Plugins/Sirenix` | Could | Inspector attributes in game code, mostly editor-side. **Never use Odin serialization** (`SerializedMonoBehaviour`, `SerializedScriptableObject`, `[OdinSerialize]`, Odin serialized collections): use Unity serialization, including the built-in `[SerializeField] Dictionary<,>` (Unity 6.6) for key→value data. |
| Easy Save 3 | — | `Assets/Plugins/Easy Save 3` | Should | Persistence. Not used yet (save system postponed, see ARCHITECTURE.md). |

## AI & navigation

The WorldMap navmesh setup (Navigation prefab, cached recast graph, water exclusion mesh) is in ENVIRONMENT.md → Navigation and the `aipocalypse-enemy-ai` skill.

| Plugin | Version | Location | Tag | Notes |
| --- | --- | --- | --- | --- |
| Behavior Designer Pro (+ Opsive Graph Designer 3.3.4, Opsive Shared 2.3.2) | 3.3.3 | `Packages/com.opsive.*`, `Assets/Plugins/Opsive` | Must | Behaviour trees for **all** AI: crowd enemies, companions, drone, bosses. Crowd trees run in `Manual` mode, ticked time-sliced by `EnemyManager`; tasks delegate to our parts. Skill: `aipocalypse-enemy-ai`. |
| A* Pathfinding Project | 5.4.7 | `Packages/com.arongranberg.astar` | Must | Enemy navigation with `FollowerEntity` agents, driven directly by `EnemyMover`. The scene's `AstarPath` is injected through `EnemyInstaller` (no `AstarPath.active` in game code). Scenes with agents need the `Navigation` prefab (AstarPath + RVOSimulator). Standalone defines `ASTAR_NO_GUI` (A*'s documented optimization): it compiles out `AstarPath.OnGUI`, the only `OnGUI` left in builds, so IMGUI doesn't run and allocate every frame. |
| Fishing for Animal Controller (Sunfish Creations) | — | `Assets/_StoreAssets/Animation/Sunfish Creations` | Undecided | Malbers add-on (Malbers: ASSETS.md). Not used. |

## Weapons & animation

| Plugin | Version | Location | Tag | Notes |
| --- | --- | --- | --- | --- |
| KINEMATION FPS Animation Framework, Procedural Recoil Animation System | — | `Assets/_StoreAssets/Animation/KINEMATION` | Avoid | Our weapon animation and recoil are first-party (HANDS.md, WEAPONS.md). Retarget Pro is under Editor tools; the FPS Animation Pack in ASSETS.md. |
| Kevin Iglesias IK Helper Tool | — | `Assets/_StoreAssets/Animation/Kevin Iglesias/IKHelperTool` | Could | Fixes a hand that misses a two-handed hold after retargeting onto a model with different arm lengths: `IKHelperTool` (on the Animator's GameObject) pulls one hand onto an effector parented to the other hand, weighted by an `IK Switch` child whose `localPosition.y` a duplicated clip keys (0 off, 1 on); `IKHelperToolAnimationCurve` takes the weight from an Animator float instead and adds an elbow hint. Needs IK Pass on the layer. Guide: `IKHelperTool Documentation.pdf`; examples in `IKHelperTool Examples.unity` (bazooka, greatsword, rifle, spear). Each instance runs its own `Update` and IK pass, so only on a few unique characters (companions, bosses), **never crowd enemies**: those would need a first-party component without the `Update` (skill `aipocalypse-characters`). Keyed clips are copies under `_Game` (never key the vendor FBX). Not used. Clips: Kevin Iglesias rows in ASSETS.md. |

## Rendering & post-processing

| Plugin | Version | Location | Tag | Notes |
| --- | --- | --- | --- | --- |
| Stylized Water 3 (Staggart) | 3.3.3 | `Assets/_StoreAssets/Rendering/Staggart Creations/Stylized Water 3` | Must | Water. Draws the world map water: flat bodies are `WaterObject`s on generated meshes at the water level; rivers and streams are `RiverModeler` ribbons on spline knots. Materials are first-party copies in `Environment/Water/`; script-set toggles also need their keywords (`[Toggle]` drawers don't run from code). Underwater Rendering is used: one `UnderwaterArea` per flat water level (ENVIRONMENT.md → Underwater). Not used: river audio (`RiverAudioManager`), the MicroVerse river integration, `PlanarReflectionRenderer` (re-renders the terrain every frame), Dynamic Effects (~160 MB of buffers). Environment reflections are off in every water material (`_EnvironmentReflectionsOn` 0 plus the `_ENVIRONMENTREFLECTIONS_OFF` keyword) and the Great Lake's realtime ReflectionProbe is deactivated. Waterfall particles: `Particles/Waterfall Impact Splashes`, `WaterRipples_Stationairy` (sortingOrder 1). Skill: `tools-unity-stylized-water`. |
| GPU Instancer Pro (Core, Prefab, Terrain) | 0.12.20 | `Packages/com.gurbu.gpui-pro*`, `Assets/Plugins/GPUInstancerPro` | Undecided | Candidate instancing for foliage and props. **Not on the world map yet**: Unity's terrain renderer draws its trees and details, and a leftover `GPUITerrainBuiltin` sits on the terrain with no GPUI manager. The Raygeas Shader Graphs have no "GPU Instancer Pro Setup" node, so GPUI would draw Komorebi vegetation magenta until `_Game` graph copies get that node. GPUI also auto-converts text shaders by writing `*_GPUIPro.shader` next to the vendor file. Both are open decisions (ENVIRONMENT.md). |
| GPU Instancer Pro – Crowd Animations | 0.10.6 | `Packages/com.gurbu.gpui-pro.crowd-animations` | Undecided | Candidate for crowd enemy rendering. |
| Stylized Grass (Staggart) | 2.1.3 | `Assets/_StoreAssets/Rendering/Staggart Creations/Stylized Grass` | Should | Grass shaders, benders and colour maps. **Use it for all grass and flowers from now on** (new details too, not the vendor BK or Raygeas grass shaders). Draws every grass and flower detail on the world map through first-party material copies (ENVIRONMENT.md → Stylized Grass); the BK and Raygeas grass shaders are no longer used for details. Its `GrassRenderFeature` (bending, range 50) is in `_Game/Settings/PC_Renderer.asset`. 2.1.3's `AddRenderPasses` skips every non-Scene-view camera while the feature's own `hideFlags` aren't `None`, and URP 17.6 saves renderer features `HideInHierarchy` (`ScriptableRendererData.OnValidate`), so without help it never bends grass in game or in builds: `GraphicsService` clears the feature's flags before each frame and restores them on Dispose (no vendor edit; drop that code if an update fixes the check). Bending: the player's own `GrassBender`; crowd movers (enemies, animals) only through the scene's `GrassBendField`, one particle-system bender, never a `GrassBender` per crowd entity (ENVIRONMENT.md → Stylized Grass → Bending). Its `WindController` (a static singleton) isn't used: wind is set on the materials. |
| SC Screen Effects (`xyz.staggart-creations.screen-effects`, local package) | 3.0.2 | `Packages/xyz.staggart-creations.screen-effects` | Undecided | Extra URP volume effects (blur, colour split, fog, …). Not used. Its components appear in `_Game/Settings/DefaultVolumeProfile.asset` only because Unity lists every installed volume component there; a stray `PostProcessingVolumeRenderer` (no volume set) sits on WorldMap's `Post-Processing` object. |
| Kronnect Skybox Plus, Volumetric Fog 2, Dynamic Fog URP, Highlight Plus | — | `Assets/_StoreAssets/Rendering/Kronnect` | Should | Skybox, fog, outlines (e.g. interactables). Skybox Plus is WorldMap's sky (on trial; the Time Of Day sky is kept to switch back): `SkyboxPlus_Sky.mat` on our fork of its Clouds shader (`_Core/EnvironmentSystem/Shaders/SkyStarMap.shader`, adds a star map; vendor updates must be merged by hand), its sun, moon and moon phase fed by `EnvironmentDirector`, not its own `SkyboxPlus` component (DAYNIGHT.md → Sky). The rest not used yet. Sky and fog are owned by `EnvironmentDirector` (DAYNIGHT.md): drive these from it, don't let them run their own time of day. |
| Kronnect Beautify, Global Snow, Transitions Plus | — | `Assets/_StoreAssets/Rendering/Kronnect` | Undecided | Post FX, snow, screen transitions. Overlap URP post-processing and our grade. Beautify appears in `DefaultVolumeProfile.asset` only because Unity lists every installed volume component there. |
| MK Glow | — | `Assets/Plugins/MK` | Could | Glow / bloom post-processing: a URP renderer feature (`MKGlowRendererFeature`) plus a volume component (`MKGlow`). Overlaps URP Bloom in our time-of-day grades (DAYNIGHT.md). Not used yet; not added to `PC_Renderer`. |

## Day/night, sky, weather

Time of day, sky, lighting, fog and grade are first-party (`EnvironmentDirector`, [DAYNIGHT.md](DAYNIGHT.md)). Unluck Stylized Weather is in ASSETS.md.

| Plugin | Version | Location | Tag | Notes |
| --- | --- | --- | --- | --- |
| COZY 3: Stylized Weather (+ Blocks 1.7.2, Habits 1.4.0, Horizon 1.0.0, Plume 1.4.3) | 3.6.23 | `Packages/com.distantlands.cozy.*` | Avoid | Kept installed, not used (user, 2026-10-21). Don't reference COZY scripts, shaders or assets. |

## Game feel, tweening, UI

| Plugin | Version | Location | Tag | Notes |
| --- | --- | --- | --- | --- |
| DOTween + DOTween Pro (+ DemiLib) | — | `Assets/Plugins/Demigiant` | Should | All code tweens. In use: the QTE widget's result feedback and the hit marker (unscaled time, so a hit stop doesn't freeze them), the HUD fade on death. `DOVirtual.EasedValue` gives the pose sequences' eased moves (Ease picked per beat in data, no tween objects); the death screen's eyelids, menu fades and pulse are DOTween sequences linked to its GameObject. Core references DOTween.Modules for uGUI extensions. |
| Feel (MoreMountains) | — | `Assets/_StoreAssets/GameFeel/Feel` | Could | Game feel feedbacks. Its tween feedbacks only inside an `MMF_Player`; code tweens use DOTween. Views play `MMF_Player`s in response to events, never services or weapons directly. No time-scale, sound, scene-loading, floating-text or broadcast feedbacks, and no `MMF_Player` on crowd enemies. Hit stop goes through our `MMF_HitStop` (→ `TimeService.RequestHitStop`, via `FeedbackContext`). Camera shake: `MMF_CinemachineImpulse` (Scaled timescale mode on every impulse feedback) heard by the `CinemachineImpulseListener` on the player's Cinemachine camera. First use: `Player/GunJamFeedback`. `Aipocalypse.Core` references `MoreMountains.Tools`. Skill: `tools-unity-feel`. |
| Damage Numbers Pro | — | `Assets/_StoreAssets/GameFeel/DamageNumbersPro` | Could | Damage numbers: `_Core/UI/Prefabs/Widgets/DamageNumber.prefab` (built with DNP's own menu item, pooling on, prewarmed by `HitFeedbackHud`). DNP's setup rewrites its font's material, so our prefab uses the first-party copy `Data/UI/DamageNumberFont.asset`. A view spawns numbers in response to `DamageDealt`. |
| UIEffect (`com.coffee.ui-effect`) | 5.11.7 | UPM git | Should | uGUI effects (shiny, dissolve, transitions). Not used yet. |
| Text Studio | 1.3.1 | `Assets/Plugins/TextStudio` | Undecided | Animated / typewriter text. Skill: `tools-unity-text-studio`. |
| LeanTransition (CW) | — | `Assets/Plugins/CW/LeanTransition` | Avoid | Overlaps DOTween. |
| LeanGUI, LeanTexture, LeanTexture+ (CW, + LeanCommon) | — | `Assets/Plugins/CW` | Undecided | LeanGUI overlaps our uGUI widgets. Not used. |

## Audio

| Plugin | Version | Location | Tag | Notes |
| --- | --- | --- | --- | --- |
| BroAudio | — | `Assets/Plugins/BroAudio` | Must | All game audio. Read its `CLAUDE.md`. Game code plays through `AudioService` (`_Core/AudioSystem`, root scope), never the static `BroAudio` facade. One-shots use `Play` / `PlayAt`; a sound the caller keeps (the player's breath loop) uses `Start`, then `SetVolume` / `Pause` / `UnPause` / `Stop` on the returned `IAudioPlayer`. Set Loop on the entity for loops. Libraries (`AudioAsset`) and their entities live in `Data/Audio/` (`Guns`, `Melee`, `RepairFeedback`, `Player`, `Combat`, `Ambience`: beds and calls played by `AmbienceService`, `Music`: the death music loop); edit them in the Library Manager (`Tools > BroAudio > Library Manager`); data fields reference them as `SoundID`. Clip sources: ASSETS.md → Audio. Skill: `tools-unity-broaudio`. |

## Debug

| Plugin | Version | Location | Tag | Notes |
| --- | --- | --- | --- | --- |
| Advanced FPS Counter (CodeStage) | 1.5.7 | `Assets/Plugins/CodeStage` | Maybe | FPS overlay. Debug overlay only (editor and dev builds), driven by the dev console (`FpsCounterCommands`): off whenever a scene loads, `afps.show` / `afps.hide`; its own hotkey (default `` ` ``, the console's key) is cleared and it draws at sort order 999, under the console. Release builds don't gate it yet: WorldMap's root `Advanced FPS Counter` would still show there (~0.2 ms main thread and ~0.1 KB GC per frame in the editor, 2026-10-25). `Aipocalypse.Core` references `CodeStage.AFPSCounter.Runtime`. |

## Editor tools

### Terrain and map authoring

| Tool | Version | Location | Tag | Notes |
| --- | --- | --- | --- | --- |
| MicroVerse (+ Objects, Splines, Vegetation, Demo) | 1.7.0 | `Packages/com.jbooth.microverse*` | Must | Terrain authoring through the WorldMap generator, stripped from builds (only baked TerrainData ships). Saved maps keep the MicroVerse component disabled between rebuilds (baked mode, kit `Enable`). **Local patch:** `Scripts/Editor/MicroVerseEditor.cs` `TerrainAssetProcessor.OnWillSaveAssets` skips `SaveBackToTerrain` when MicroVerse is disabled (marked `aipocalypse patch`). Otherwise every save rewrote the LFS TerrainData with GPU-readback noise. Re-apply it after a MicroVerse update. Skill: `tools-unity-map-generation` (library detail: user-scope `unity-microverse`). Height stamps and presets: ASSETS.md. |
| MicroVerse Ambiance | 1.7.0 | `Packages/com.jbooth.microverse.ambiance` | Avoid | `AmbientArea` / `AmbianceMgr` run at runtime as a static singleton; BroAudio owns audio. |
| Rowlan Genesis for MicroVerse | 3.5.5 | `Assets/_StoreAssets/Env/Rowlan/Genesis` | Could | MicroVerse wizards (heightmap → collection converter, stamp placement/validation, terraform). The wizard writes to `Assets/Rowlan/Genesis Data` (not created yet) and replaces MicroVerse setups. Its Gameplay (legacy Input rig) and Visual Ambience (built-in post-processing) parts are Avoid. |
| Spline Spawner (Staggart) | 1.2.0 | `Packages/xyz.staggart-creations.spline-spawner` | Could | Spawn objects along splines. Not used. WorldMap dressing is scripted by the generator: anything placed by hand there is lost on a rebuild unless folded into the generator. |
| Vegetation Spawner (Staggart) | 1.1.0 | `Assets/_StoreAssets/Rendering/Staggart Creations/VegetationSpawner` | Avoid | The generator places vegetation; a second placer would fight it. |
| Rowlan YAPP (prefab painter) | 1.28.0 | `Assets/Plugins/Rowlan/YAPP` | Could | Prefab painting. Not used. WorldMap dressing is scripted by the generator: anything painted there is lost on a rebuild unless folded into the generator. |

### Assets, inspector, workflow

| Tool | Version | Location | Tag | Notes |
| --- | --- | --- | --- | --- |
| Asset Usage Detector | 2.6.3 | UPM git (yasirkula) | Should | Find asset references. |
| Inspect Plus | 2.1.1 | UPM git (yasirkula) | Could | Inspector extras. |
| Better Inspector (Tiny Giant Studio) | — | `Assets/Plugins/Tiny Giant Studio` | Could | Inspector extras; overlaps Inspect Plus and Odin. |
| KINEMATION Retarget Pro | — | `Assets/_StoreAssets/Animation/KINEMATION/RetargetPro` | Should | Animation retargeting. |
| Audio Tools | 1.2.0 | `Assets/Plugins/AudioTools` | Should | Trim, normalize, fade, export clips. Skill: `tools-unity-audio-tools`. |
| Audio Preview Tool | — | `Assets/Plugins/WarpedImagination` | Could | Clip preview in the Project window. |
| Easy Collider Editor | — | `Assets/Plugins/EasyColliderEditor` | Should | Collider authoring. |
| Rowlan Fullscreen Editor Play Mode | 1.9.0 | `Assets/Plugins/Rowlan/Fullscreen` | Could | |
| Ultimate Preview / Ultimate Thumbnails (Voxel Labs) | — | `Assets/Plugins/Voxel Labs` | Could | |
| Colorize Palette Modifier (Smitesoft) | — | `Assets/Plugins/Smitesoft` | Could | |
| Smart Mouse (TelePresent) | — | `Assets/Plugins/TelePresent` | Could | |
| Scene List Tool | — | `Assets/Plugins/SceneListTool` | Could | |
| FolderColor | — | `Assets/Plugins/FolderColor` | Undecided | Editor-only folder icons and colours in the Project window, plus Hierarchy row colours (`Editor/`, own asmdef). Suggested Should: editor-only and already used to colour the project's folders. |
| Auxilium Dev Toolbox | — | `Assets/Plugins/Auxilium Dev Toolbox` | Could | |
| FImpossible Creations shared tools | — | `Assets/Plugins/FImpossible Creations` | Could | |
| SyntyPackageHelper | — | `Assets/Synty/SyntyPackageHelper` | Avoid | Synty package import helper. |
