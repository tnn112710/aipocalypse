---
name: tools-unity-6-new-apis
description: Unity 6.4 / 6.5 / 6.6 features and API changes that post-date older training data, filtered for aipocalypse (PC FPS, URP, uGUI) — built-in Dictionary serialization and the serialization analyzer (UAC codes), code-lifecycle attributes (OnEnteringPlayMode, OnCodeInitializing…) and AutoStaticsCleanup, EntityId replacing InstanceID, removed component shortcuts, DEVELOPMENT_BUILD/UNITY_64 deprecation and Managed Code Variant, built-in Unity.Mathematics, AnimationEventInfo, uGUI Raycast Receiver / LayoutElement max size / TMP SetText(span), Mesh/Texture Read/Write build requirements, URP Render Graph only, PSO warm-up, Shader Graph Expression/Switch nodes, Project Auditor, project-local Editor.log. Use before writing C# that touches serialization, statics, init/play-mode hooks, object IDs, build defines, animation events or uGUI layout, and when a remembered Unity API fails to compile or a "how does Unity do X" answer comes from memory.
---

# Unity 6.4–6.6 new APIs (aipocalypse)

Project editor: **6000.6.x** (check `ProjectSettings/ProjectVersion.txt`). Agents trained on older Unity versions miss these. Sources: *What's new* pages for [6.6](https://docs.unity3d.com/Manual/WhatsNewUnity66.html), [6.5](https://docs.unity3d.com/Manual/WhatsNewUnity65.html), [6.4](https://docs.unity3d.com/Manual/WhatsNewUnity64.html) and the [6.5](https://docs.unity3d.com/Manual/UpgradeGuideUnity65.html) / [6.6](https://docs.unity3d.com/Manual/UpgradeGuideUnity66.html) upgrade guides (read 2026-10-24). 2D, XR, mobile, web, console, multiplayer and ECS-internal changes are left out on purpose.

Items marked **(doc-checked)** were read on their detail page. The rest come from the *What's new* summaries only. Check the Script Reference (`https://docs.unity3d.com/6000.6/Documentation/ScriptReference/<Type>.html`) or a `run_script --dry_run` compile before relying on exact signatures.

## 1. Serialization

- **Built-in `Dictionary<TKey, TValue>` serialization (6.6, doc-checked).** Add `[SerializeField]` (opt-in, also needed on public fields) on a `MonoBehaviour`, `ScriptableObject` or `[Serializable]` type. The Inspector shows a two-column table. **Project rule: use this for every key→value field.** Don't use a list of key/value structs, a custom `SerializableDictionary`, or Odin serialization (see AGENTS.md → Code style). [Manual](https://docs.unity3d.com/6000.6/Documentation/Manual/script-serialization-dictionaries.html)
  - Keys and values: primitives, enums of 32 bits or less, Unity structs, `[Serializable]` types, `UnityEngine.Object` refs. Values may be collections (`Dictionary<int, List<int>>`). Not allowed: interface or abstract types (except `UnityEngine.Object`), `IEnumerable` keys (except `string`), `[SerializeReference]` on the field.
  - Custom struct or class keys need `Equals` and `GetHashCode` overrides (`IEquatable<T>` avoids boxing).
  - Duplicate or null keys stay visible in the Inspector. At runtime they're dropped with one warning, and only the first duplicate is kept.
  - No `List<Dictionary<>>` or `Dictionary<>[]`: wrap the dictionary in a `[Serializable]` type. No multi-object editing, no entry search. Prefab overrides are recorded by entry index, so overriding entries on prefab instances is fragile.
  - Display: `[DictionaryDisplay(keyLabel, valueLabel, keyColumnFraction, layout)]` on the field. Layouts are `TwoColumns`, `OneColumnWithValueVisible` and `OneColumnWithValueFoldout`. `[assembly: DictionaryDisplayForType]` sets it per type.
  - Range attributes such as `[Min]` don't apply per entry. Validate in code.
  - **Migrating from a list, or retyping any serialized field:** Unity doesn't convert the old data. Record the values, change the field, recompile, write the values back with a `run_script` editor script, save, and check the YAML. Or add the new field next to the old one, copy, verify, then delete the old field.
- **Serialization rules analyzer (6.5, doc-checked):** Roslyn diagnostics `UAC1000`–`UAC1022` in the IDE and at compile time. Examples: missing `[Serializable]` (UAC1001/1002), unsupported collections such as `HashSet`, `Queue` and nested lists (UAC1009), `[SerializeField]` on a type that can't be serialized (UAC1010), tuples ignored (UAC1017), dictionary rules (UAC1012–1016, UAC1019–1022 are errors). For a vendor base class you can't edit, use `[assembly: MakeSerializable]`. Treat these warnings as data-loss bugs. Don't suppress them. [Manual](https://docs.unity3d.com/6000.6/Documentation/Manual/script-serialization-analyzer.html)
- `[SerializeReference]` targets whose class or an ancestor lacks `[Serializable]` now warn (6.4).
- **YAML (6.6):** word wrapping is off and "Reduce Version Control Noise" is gone, so touching old assets can produce unrelated diffs. Don't mass-reserialize unasked (`AssetDatabase.ForceReserializeAssets` exists).
- The Unity Serialization package is now a core package.

## 2. Code lifecycle, statics, play mode

- **Lifecycle attributes (6.5, doc-checked).** Put them on `static void` methods with no parameters. Prefer them in new code over `[InitializeOnLoad]`, `[RuntimeInitializeOnLoadMethod]` and `EditorApplication.playModeStateChanged`. [Manual](https://docs.unity3d.com/6000.6/Documentation/Manual/programming-code-lifecycle.html)

  | Attribute | Namespace | Runs |
  |---|---|---|
  | `[OnEnteringPlayMode]` / `[OnExitingPlayMode]` | `UnityEngine` (Editor + Player, no `#if` needed) | Entering/exiting Play mode, player start/quit, around a code reload during play |
  | `[OnEnteringEditMode]` / `[OnExitingEditMode]` | `UnityEditor.Scripting.LifecycleManagement` (Editor only) | Back to Edit mode / before Play mode or a reload |
  | `[OnCodeInitializing]` | `Unity.Scripting.LifecycleManagement` | After assemblies load and serialized data is restored, **before** `Awake`/`OnEnable` and before assets fully load. Replaces `InitializeOnLoad(Method)` and `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` |
  | `[OnCodeLoaded]` / `[OnCodeUnloading]` / `[OnCodeDeinitializing]` | `Unity.Scripting.LifecycleManagement` | Around code load and reload |

  Existing code still uses `InitializeOnLoadMethod` + `playModeStateChanged` (`EnvironmentDirector`, `HandPreview`). It works as it is. Migrate only when touching that code for another reason.
- **Statics with domain reload off (doc-checked).** On a static *field* in a `partial` type, `[AutoStaticsCleanup]` re-runs the field initializer on each Play mode entry (or calls `Clear()` on a `readonly` collection) and `[NoAutoStaticsCleanup]` opts the field out. Static constructors are not re-run. Analyzer `UAL0010`–`UAL0014` is off by default; turn it on with `<asmdef>.globalconfig` → `build_property.UnityEnableAutoStaticsCleanupAnalysis = true`. [Manual](https://docs.unity3d.com/6000.6/Documentation/Manual/domain-reloading.html)
  - **Project state:** `EditorSettings` has Enter Play Mode Options on with flags `0`, so the domain and scene still reload. Statics reset today. Game code has no singletons (VContainer), so statics should be rare anyway. If fast play mode (no domain reload) is enabled later, audit statics with these attributes first.
- New projects default to **Reload Scene only** (6.6). That doesn't change this project's existing setting.

## 3. Removed / changed APIs (compile errors if remembered from older Unity)

- **`EntityId` replaces `InstanceID` (6.4 deprecated, 6.5 errors, doc-checked).** `go.GetInstanceID()` → `go.GetEntityId()`, `Resources.InstanceIDToObject` → `Resources.EntityIdToObject`, and `Selection.instanceIDs` is obsolete. `EntityId` is 64 bits: never cast it to `int`, never use its hash as a key, and never infer creation order or "is asset" from it. Use `EntityId` itself as the dictionary key. `ToString()` output changed width in 6.5, so don't persist it in save data. Usually prefer the object reference itself.
- **Component shortcut properties removed (6.5):** `.rigidbody`, `.collider`, `.camera`, `.light`, `.renderer`, `.audio`, `.animation`, `.particleSystem`, `.hingeJoint`, `.constantForce`, and `AddComponent(string)`. Use `GetComponent<T>()` / `AddComponent<T>()`. Also removed: `GameObject.SampleAnimation`, which becomes `AnimationClip.SampleAnimation(go, t)`.
- **`DEVELOPMENT_BUILD` and `UNITY_64` deprecated (6.6, analyzer `UAC0009` / `UAC0008`).** For dev-only runtime gating (cheats, the `Debug` input map) use `Debug.isDebugBuild` or `UNITY_EDITOR`. For asserts and safety checks use `UNITY_ENABLE_CHECKS`. For profiler markers and diagnostic logging use `UNITY_INCLUDE_INSTRUMENTATION`. These follow the new per-platform **Managed Code Variant** Player setting (`PlayerSettings.SetManagedCodeVariant`; Checked ≈ old dev build). `BuildOptions.ForceEnableAssertions` is obsolete.
- `UNITY_6000_5_OR_NEWER` / `UNITY_6000_6_OR_NEWER` exist for version gating. Not needed here, since the project targets one version.
- **URP Compatibility Mode removed (6.4):** custom passes must use the **Render Graph** API (`RecordRenderGraph`, no `Execute`). `FirstPersonRenderFeature` already does. Check new features with the `unity:validate-urp-render-graph-renderer-feature` skill.
- Dynamic batching is obsolete (6.6). Don't enable it or rely on it. GPU Resident Drawer and the SRP Batcher are the path.
- The legacy Rendering Debugger APIs (`DebugState*`, `DebugUIDrawer`) no longer compile. Custom `DebugUI.Widget` returns a UI Toolkit `VisualElement` from `Create()`.
- UI Toolkit: the legacy UXML Factory/Traits system is removed; use `[UxmlElement]`. (The game UI is uGUI; this matters only for editor tools.)
- Unity ships its own `System.Text.Json`, `System.Collections.Immutable`, `System.Reflection.Metadata` and `Unsafe`. User-supplied copies are dropped from builds.

## 4. Math

- **Unity.Mathematics is built in (6.5).** It still appears in `packages-lock.json` as 1.2.6 here. *What's new* lists implicit conversions between `Vector3`/`Matrix4x4` and `float3`/`float4x4`. Use `float3`/`math` for Burst/Jobs and A* `FollowerEntity` data. Keep `Vector3` in ordinary MonoBehaviour code. Burst is now a built-in module (6.6).

## 5. Animation

- **`AnimationEventInfo` (6.5, doc-checked):** declare `void OnStep(AnimationEventInfo info)` instead of `AnimationEvent` and the event fires with no allocation. It's a read-only `ref struct`, so don't store it. Members: `intParameter`, `floatParameter`, `objectReferenceParameter`, `time`, `animatorStateInfo`, `animatorClipInfo`, `isFiredByAnimator`. Reading `stringParameter` or `functionName` allocates. Use it for footsteps and weapon clip events.
- **Require Receiver** (animation event Inspector option, 6.5): logs an error when no method receives the event. Turn it on for gameplay-critical events.
- Animator Controller **Evaluate Entry Transitions On Start** (6.4): starts in the right state with no one-frame default-state delay. On for new controllers, off for existing ones.
- Animation Rigging and Timeline are now core packages (6.6). The Animation window has a property search bar.

## 6. uGUI / TextMeshPro (game UI is uGUI, see UI.md)

- **Raycast Receiver** component (6.5, doc-checked): an invisible hit area with Raycast Target and Raycast Padding. Use it instead of a transparent `Image` to block or catch clicks; it's cheaper.
- **`LayoutElement` Max Width / Max Height** (`maxWidth` / `maxHeight`); **`ContentSizeFitter` "Clamped"** fit mode; layout groups compute max size from children (6.6).
- **`TMP_Text.SetText(ReadOnlySpan<char>)`** (6.6): format HUD numbers into a `Span<char>`/`stackalloc` buffer for allocation-free updates.
- `TMP_Text` callbacks `OnCharacterRequest`, `OnFontMaterialRequest`, `OnColorGradientAssetRequest` (6.5).
- `RectTransform.TryFitInsideCoplanarRectTransform` / `IsCoplanarWith` (6.6): keeps tooltips and popups inside a parent rect.
- `SafeArea` component (6.6): mostly mobile. Ignore it.

## 7. Assets, graphics, performance

- **Build-time Read/Write requirements:** meshes used by a Particle System **Shape** module, **Terrain detail meshes**, or a **MeshCollider** need Read/Write on (6.6), or the build fails and the Inspector warns. Textures used by the Particle Shape module or Terrain Paint Detail need Read/Write on (6.5), because it's no longer forced at build time. Check this when adding vegetation, VFX or collision meshes.
- **Terrain:** Render Pipeline Core no longer pulls in the Terrain module. `com.unity.modules.terrain` is already explicit in `Packages/manifest.json`; keep it. Terrain Layers and materials can be dragged onto a terrain. The Shader Graph **Terrain Properties** node works in Terrain Lit graphs (6.4).
- **Shader stutter / PSO warm-up:** `GraphicsStateCollection` tracing and warm-up (Editor capture with no code, `WarmUp(traceCacheMisses)`, built from Mesh + Material arrays), and `Shaders.VariantsUploadedToGpuLastFrame` to detect hitches. Candidates for a loading-screen prewarm. `VisualEffectAsset.PrewarmComputeShaders` does the same for VFX Graph.
- Shader constant values can be set globally or per build profile without adding variants (6.6).
- URP: `UniversalAdditionalLightData.additionalLightsShadowResolutionTier` (Low/Medium/High) sets per-light shadow resolution at runtime (6.5). Useful for lantern and torch lights.
- Shader Graph: **Expression** node (multi-term math in one node), **Switch** node (cases or an enum property), HLSL function reflection turns `.hlsl` functions into nodes (see the `unity:shader-graph-create-custom-node` skill), subgraph deprecation flag, Preprocessor Directives in Graph Settings, particle Shader Graphs (with mesh-particle GPU instancing) for the Built-in Particle System.
- Lighting: the **Unity Compute Light Baker** is the new default option. Progressive CPU is deprecated. There's a new Lighting Search window.
- Profiling: built-in **Project Auditor** (`unity command audit`; rules may need `com.unity.project-auditor-rules`), redesigned **Rendering Statistics** (draw calls per SRP Batcher / GRD / BRG / instancing), GPU Resident Drawer profiler details, Frame Debugger constant-buffer values, Profiler screenshots and pinning up to four modules, the **mimalloc** native allocator, and Performance Testing (`Unity.PerformanceTesting`) as a core package. Project profiling procedures: `tools-unity-profiling` in AGENTS.md (its skill folder is missing as of 2026-10-24).
- Mesh LOD (since 6.2) has a Scene-view LOD label gizmo and a preview bar (6.4). It could replace hand-made LOD groups for props.
- **Content directories + `Unity.Loading.Loadable<T>`** (6.6) replace AssetBundle builds. Not used (no Addressables for content, see ARCHITECTURE.md). Revisit only if streaming is needed later.
- Windows DirectStorage for `AsyncReadManager` (6.4/6.5): possible load-time gain later.

## 8. Editor workflow (useful to agents)

- **Editor log is project-local (6.5):** `Logs/Editor.log` in the repo root folder (gitignored), not `%LOCALAPPDATA%`. In code use `Application.consoleLogPath`.
- The new Hierarchy window is the default (6.6): faster in big scenes, with columns and public `Unity.Hierarchy` APIs.
- Selection history: Ctrl+Shift+[ / ] (6.6). Grid & Snap overlay redesign and custom grid transform (6.4). Build Analysis window + `Library/BuildHistory` (6.6).
- Custom tools: `EditorTool` / `ISupportsEditorTools` in custom windows (6.6). Graph Toolkit (`Unity.GraphToolkit.Editor`, 6.4+) for node-graph editor tools. Behavior trees stay on Behavior Designer Pro.
- `OnMouseDown`/`OnMouseDrag`/`OnMouseUp` work with the Input System (6.4). **Don't use them in game code:** input goes through `InputService` only.
- `BuildProfile.CreateBuildProfile` creates build profiles from script (6.5).
