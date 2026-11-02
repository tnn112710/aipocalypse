# Builds

Player builds come from **build configs**: `BuildConfig` assets in `Assets/_Game/Data/Build/`, one per kind of build, built from **Autobuild > Build Window** (or the config's own Build button). Code: `Assets/_Game/_Editor/Build/` (asmdef `Aipocalypse.Editor`, editor only).

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
- **Zip**: also writes `<name>.zip` next to the folder, the files inside a `<name>/` folder, without the `*_DoNotShip` / `*_ButDontShipItWithYourGame` symbol folders.

Name: `<product>_<version>_<platform>_<distribution|development>[_profiler][_cheat][_pipeline]`, product and version from Player Settings. Output: `_builds/<name>/` (gitignored), cleaned before each build. Scenes: the enabled Build Settings scenes. A config's own Pipeline / cheat choice wins over Player Settings: the build removes a stray `ENABLE_CHEAT` or Pipeline toggle from them for its duration.

## Cheats in code

`Cheats.Enabled` (`Aipocalypse.Core.Debugging`) is true in the editor and in `ENABLE_CHEAT` builds. It gates the console, its commands, the cheat hotkeys (`Debug` input map) and `ScreenLog`; register dev-only entry points inside it ([CONSOLE.md → Adding a command](CONSOLE.md#adding-a-command)). `Debug.isDebugBuild` stays only for development tooling that isn't a cheat (the MessagePipe diagnostics provider). Never `DEVELOPMENT_BUILD` (deprecated in 6.6).

## Command line

```bash
Unity.exe -batchmode -quit -projectPath <project> -executeMethod Aipocalypse.Editor.Build.AutoBuild.BuildFromCommandLine \
  -buildConfig Assets/_Game/Data/Build/WindowsDistribution.asset [-buildOutput <folder>]
```

Exits 1 when the config is missing or the build fails.
