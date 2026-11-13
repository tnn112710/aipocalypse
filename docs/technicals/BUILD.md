# Builds

Player builds come from **build configs**: `BuildConfig` assets in `Assets/_Game/Data/Build/`, one per kind of build. Code: `Assets/_Game/_Editor/Build/` (asmdef `MB13.Aipocalypse.Editor`, editor only).

**Autobuild > Build Window** lists every config with its build name and last build on this machine (time, duration, size; `Library/AutoBuild/History.json`). Build one, or tick several and **Build Selected**: they build one after another (grouped by platform; a cancel stops the rest), then the build folder opens (`_builds/` for several). **Open** / **Run** open the last build's folder or start it. Before building it refuses Play mode, offers to save modified scenes (an unsaved change is not in the build: Unity builds the scenes on disk), and asks before a distribution build with the Pipeline server.

## Configs

| Config | Development | Profiler | Cheat | Zip | Output name |
| --- | --- | --- | --- | --- | --- |
| `WindowsDistribution` | | | | ✓ | `aipocalypse_0.1.0_windows_distribution` |
| `WindowsDistributionCheat` | | | ✓ | ✓ | `aipocalypse_0.1.0_windows_distribution_cheat` |
| `WindowsDevelopment` | ✓ | | ✓ | | `aipocalypse_0.1.0_windows_development_cheat` |
| `WindowsDevelopmentProfiler` | ✓ | ✓ | ✓ | | `aipocalypse_0.1.0_windows_development_profiler_cheat` |

Options (one bool each on the asset; a new option is a new field plus its postfix in `AutoBuild.GetBuildName`):

- **Development**: Unity's Development Build (debugger, profiler hooks). Always has cheats. Its frame times aren't representative: judge the 60 fps target on a distribution build.
- **Profiler**: auto-connects the Profiler at start. Makes a development build.
- **Cheat**: defines `ENABLE_CHEAT` for the build (an extra define, Player Settings untouched). On in every development build.
- **Pipeline**: the Unity Pipeline's runtime server in the player, so the `unity` CLI can drive it (`runtime …` commands, `console get`, method reload). A loopback-only HTTP server that runs code: for QA builds, never one handed out. The Pipeline's build step reads its toggle (`ENABLE_RUNTIME_PIPELINE_DEV` for development builds, `ENABLE_RUNTIME_PIPELINE` for distribution) from Player Settings, so the build sets it there for its duration and restores it; the editor recompiles once afterwards. Server settings: Project Settings > Pipeline > Runtime (`ProjectSettings/Packages/com.unity.pipeline/RuntimePipelineConfig.json`), connecting: the package's `Documentation~/runtime-setup.md`.
- **Delete do-not-ship folders** (on by default): deletes `*_BackUpThisFolder_ButDontShipItWithYourGame` (IL2CPP's generated C++ and symbols) and `*_BurstDebugInformation_DoNotShip` after the build. Turn it off to keep them for a build whose crash call stacks you'll want to symbolicate.
- **Zip**: also writes `<name>.zip` next to the folder, the files inside a `<name>/` folder, without the `*_DoNotShip` / `*_ButDontShipItWithYourGame` symbol folders.

**Addressables first.** Each build first builds the Addressables content for its platform (Localization's string tables and locales: [LOCALIZATION.md](LOCALIZATION.md)) into `Library/com.unity.addressables/aa/`, and stops with an error if that fails: a player without it shows no text. The player build then copies that content into `StreamingAssets/aa`. The project setting "Build Addressables on Player Build" is on, so builds from File > Build Profiles also include it; Autobuild switches it off for the length of its own build so the content isn't built twice. The content build unloads every asset only code holds, so Autobuild keeps its `BuildConfig` assets loaded (`HideFlags.DontUnloadUnusedAsset`, cleared afterwards) while it builds.

Name: `<product>_<version>_<platform>_<distribution|development>[_profiler][_cheat][_pipeline]`, product and version from Player Settings. Output: `_builds/<name>/` (gitignored), cleaned before each build. Scenes: the enabled Build Settings scenes. A config's own Pipeline / cheat choice wins over Player Settings: the build removes a stray `ENABLE_CHEAT` or Pipeline toggle from them for its duration.

## Code stripping

Managed stripping level is **Low** (Player Settings > Other Settings > Optimization; the Windows build profile doesn't override it). At Low the Unity linker keeps every assembly a scene references whole, and in the others keeps public types and members and drops unreachable non-public code; Minimal copied every non-engine assembly whole, used or not.

`Assets/_Game/_Core/link.xml` keeps whole the assemblies that find or create types by reflection or by name: `MB13.Aipocalypse.Core`, VContainer, MessagePipe, the Behavior Designer Pro / Opsive runtime assemblies (trees store task types by full name) and COZY. When code in a player build reaches a type only by reflection or by a string (`Type.GetType`, `Activator.CreateInstance`, a new reflection-based plugin), add its assembly or type there; editor-only code (`#if UNITY_EDITOR`) doesn't count. A stripped type shows up only in the player: `MissingMethodException`, a null `Type.GetType` or a VContainer resolve error in `Player.log`, so check a distribution build's `Player.log` after changing this.

Going further (Medium/High) roots only MonoBehaviours, ScriptableObjects, `[Preserve]` and `link.xml` in non-scene assemblies, and would need public-but-reflected vendor code listed too.

## Build size

The Autobuild window shows each build's total size. To find what makes it big:

- **Assets** (textures, meshes, audio): the build report Unity writes at the end of `Editor.log` (biggest assets and size by category), or the `BuildReport` from the build.
- **Shaders:** use **Shader Control** (Kronnect, PLUGINS.md → Editor tools). It lists every shader in the last build with its keywords and **variant count**, and it covers `Resources` shaders, Always Included shaders and shaders that only materials pull in. Too many variants make builds bigger, slower to build and use more shader memory. It only covers shaders.

### Graphics shader stripping

Project Settings > Graphics > Shader Stripping decides which built-in keyword variants every shader keeps:

- **Fog Modes: Custom, Exponential Squared only.** It's the project's only fog mode (DAYNIGHT.md). Linear and Exponential are stripped, so a scene that uses either one has no fog in builds, while the editor still shows it.
- **Lightmap Modes:** not trimmed yet. No scene uses lightmaps: WorldMap's lights are all Realtime, Realtime GI is off and no lighting has been baked (the day/night cycle moves the sun). So all six modes can be stripped. If a scene ever bakes lighting, re-tick its modes, or its lightmaps are missing in builds.

### Shader Control

Open it from **Window > Kronnect > Shader Control > Browse Shaders…**. Use the **Build View**:

1. Click **Quick Build**, then build any config (Autobuild works too). The build skips shader compilation, so it renders pink: throw it away. Shader Control records each shader's keywords and variants.
2. Sort by variant count. Look for shaders that shouldn't ship, for example a vendor uber shader in `Resources` (Beautify was ~830k variants), or a built-in shader pulled in by a stray material. Look for keywords that multiply the count.
3. Fix it at the source first. Remove the reference, the material or the `Resources` copy. Turn the feature off in the URP asset so URP strips it. In our own shaders, use `shader_feature` instead of `multi_compile`.
4. Make a normal build and compare its size in the Autobuild window. Shader Control's variant counts also refresh on every normal build.

Traps:

- **Quick Build edits files.** It adds a hidden `ShaderControlQuickBuild` GameObject to the active scene and saves the scene. Its save hook calls `AssetDatabase.SaveAssets()`, which can write any dirty asset (see AGENTS.md → "Save only what you wrote"). Before a Quick Build, start from a clean `git status`. Afterwards, revert the scene change and any unrelated assets.
- **Unticking strips silently.** A shader or keyword you untick in the Build View is stripped from every later build, by any config, on any machine that has its data. A variant that's missing shows up only in the player, as pink or wrong materials. Untick only when the source can't be fixed, for example a keyword a vendor shader never uses here. When you do, write it here.
- **Its data is an asset.** It lives in `Assets/Plugins/Kronnect/ShaderControl/Editor/Resources/` (`BuiltShaders.asset`, `BuildConfiguration.asset`). It's editor only, so it never ships. Commit those assets only once they hold strip choices; until then they only hold the last build's statistics.
- **Its Project View edits files too.** "Convert to local" rewrites shader files and material keyword pruning rewrites materials. Use them only on `_Game` shaders and materials, never on vendor ones.

## Build stamp

Each build gets `build_info.txt` next to the exe (and in the zip): `name`, `config`, `commit` (short hash, `-dirty` with uncommitted changes to tracked files) and `built` (date). `BuildStamp` (root scope) reads it at start and logs `Build: …` to `Player.log`; the console's `build` command prints it, and cheat builds show it small in the bottom-right corner (not in the editor). A player without the file says so instead.

## Cheats in code

`Cheats.Enabled` (`MB13.Aipocalypse.Core.Debugging`) is true in the editor and in `ENABLE_CHEAT` builds. It gates the console, its commands, the cheat hotkeys (`Debug` input map) and `ScreenLog`; register dev-only entry points inside it ([CONSOLE.md → Adding a command](CONSOLE.md#adding-a-command)). `Debug.isDebugBuild` stays only for development tooling that isn't a cheat (the MessagePipe diagnostics provider). Never `DEVELOPMENT_BUILD` (deprecated in 6.6).

## Command line

```bash
Unity.exe -batchmode -quit -projectPath <project> -executeMethod MB13.Aipocalypse.Editor.Build.AutoBuild.BuildFromCommandLine \
  -buildConfig Assets/_Game/Data/Build/WindowsDistribution.asset[,<another config>] [-buildOutput <folder>, one config only]
```

Exits 1 when a config is missing or a build fails. No dialogs: scenes build as saved.
