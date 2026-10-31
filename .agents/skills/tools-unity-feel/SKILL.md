---
name: tools-unity-feel
description: Feel (More Mountains, MMF_Player feedbacks) for aipocalypse — where game feel belongs (views reacting to MessagePipe events, never services or weapons directly), which feedbacks are allowed vs banned here (time scale, sound, scene loading, floating text, static event buses), Cinemachine 3 impulses, URP volume shakers, custom feedbacks bridging to TimeService/BroAudio, crowd-enemy limits, pooling and timescale modes. Use for any juice/feedback/screen shake/hit stop/flash/recoil-visual/UI punch work. NOT the legacy MMFeedbacks component (removed in Feel v4).
---

# Feel (aipocalypse)

Installed: **Feel 6.1** (MMF_Player era; the version its bundled Skills state; the `.meta` files carry package version 6.3) at `Assets/_StoreAssets/GameFeel/Feel/` (MMFeedbacks + MMTools only, no demos). It's vendor code, so don't modify it. Custom feedbacks go in `Assets/_Game/`.

- **One assembly:** all Feel runtime code (core, feedbacks, third-party feedbacks) compiles into **`MoreMountains.Tools`** via asmrefs. Our asmdef references `MoreMountains.Tools`. Namespaces: `MoreMountains.Feedbacks` (MMF_Player, MMF_Feedback, built-ins), `MoreMountains.FeedbacksForThirdParty` (URP, Cinemachine, TMP), `MoreMountains.Tools`.
- **Active integration defines** (from the asmdef's versionDefines): `MM_CINEMACHINE3`, `MM_URP`, `MM_UGUI2` (TMP feedbacks), `MM_VISUALEFFECTGRAPH`, `MM_INPUTSYSTEM`, `MM_UI`. **Inactive:** `MM_POSTPROCESSING` (PPv2: `MMF_Bloom`, `MMF_Vignette`… are dead, so use the `_URP` variants) and `MM_HDRP`.
- Sources to read when unsure: `MMFeedbacks/MMFeedbacks/Core/MMF_Player/MMF_Player.cs`, `MMF_Feedback.cs` (custom feedback API), each feedback in `MMFeedbacks/MMFeedbacks/Feedbacks/` and `MMFeedbacksForThirdParty/*/Feedbacks/`. Docs: https://feel-docs.moremountains.com/.

Feedback catalog with project status: [references/feedbacks.md](references/feedbacks.md).

## Feel's own skills (vendor how-to)

Feel ships agent skills (verified against 6.1) in `Assets/_StoreAssets/GameFeel/Feel/Skills/`. No agent loads them on its own, so read the one that matches the task for menu paths, inspector steps and API detail. **This skill's rules win wherever they disagree:** check §2 before adding anything a vendor skill suggests.

| Vendor skill | Read when | Here |
| --- | --- | --- |
| `feel-add-feedbacks/` (+ `references/feedback-catalog.md`) | Creating an MMF_Player, adding feedbacks, triggering from code / animation events / Timeline, loops, runtime changes, completion callbacks, target acquisition | Use, but skip its FreezeFrame / `MMTimeManager` and sound parts (§2, §5) |
| `feel-screen-shake/` | Camera / Cinemachine / UI shake, zoom, fade, flash, shake accessibility toggle | Use with §3 (Cinemachine 3 impulses) |
| `feel-post-processing-effects/` (+ `references/shaker-map.md`) | Pulsing vignette, bloom, chromatic aberration, lens distortion | Use the URP variants only (§4) |
| `feel-springs/` (+ `references/spring-components.md`) | Bouncy / springy motion and values | Use (views and UI only, not crowd enemies, §6) |
| `feel-custom-feedback/` (+ `references/shaker-pattern.md`) | Writing an `MMF_Feedback` subclass or a shaker | Use with §5 (bridges to our services through `FeedbackContext`) |
| `feel-sound-manager/`, `feel-sound-and-playlist-setup/` | — | **Banned:** MMSoundManager. BroAudio owns audio (`tools-unity-broaudio`) |
| `feel-haptics/` | — | **Not applicable:** Nice Vibrations not installed, PC only |

## 1. Where Feel fits in this architecture

PLUGINS.md (Feel row): *"Views play `MMF_Player`s in response to events, never services or weapons directly."*

```text
Rules code (DamageService, GunJam…)  ──publishes──>  MessagePipe event (DamageDealt, PlayerDied, GunJamBeat, GunRepairCompleted…)
                                         │
View MonoBehaviour (subscribes, injected) ──> MMF_Player.PlayFeedbacks(position, intensity)
```

- **Services never reference Feel.** No `MMF_Player` field in a service, no Feel calls from rules code.
- **Weapon logic never plays feedbacks.** The gun's jam and repair code publishes `GunJamBeat` / `GunRepairCompleted`, and `GunJamFeedback` (a view on the gun user's entity) subscribes and plays its MMF_Players. A muzzle/kick player would work the same way from a shot event (none exists yet; add one only if a prompt needs it). Gameplay recoil (aim offset, spread) is WeaponSystem data and code. Feel only does the *visual* kick.
- **Feedback rules live on the view:** which player, intensity from event data (damage → intensity 0–1), positional plays at the hit point.
- **Feel is not a tween library for code.** Code tweens use DOTween (PLUGINS.md). Feel's position/scale/rotation feedbacks are fine *inside* an MMF_Player authored in the inspector.

```csharp
// On the Player prefab (unique entity, injected by PlayerScope).
public sealed class PlayerHitFeedback : MonoBehaviour
{
    [SerializeField] MMF_Player _hurt;          // camera impulse + vignette (the hurt sound goes through AudioService, not Feel)
    [SerializeField] MMF_Player _heavyHurt;     // adds hit stop
    [SerializeField] float _heavyThreshold = 30f;
    IDamageable _self;
    IDisposable _subscription;

    [Inject]
    void Construct(ISubscriber<DamageDealt> damage, IDamageable self)
    {
        _self = self;
        _subscription = damage.Subscribe(OnDamage, e => ReferenceEquals(e.Target, _self));
    }

    void OnDamage(DamageDealt e)
    {
        var player = e.Info.Amount >= _heavyThreshold ? _heavyHurt : _hurt;
        player.PlayFeedbacks(e.Info.Point, Mathf.Clamp01(e.Info.Amount / 50f));
    }

    void OnDestroy() => _subscription?.Dispose();
}
```

(`PlayerHitFeedback` is a sketch, not in the repo; `DamageDealt` and its fields are the real ones from `_Core/_Common/Events/CombatEvents.cs`.)

## 2. Banned and redirected feedbacks

These conflict with systems that already own the concern. Don't add them to players. If you find them on a prefab, replace them.

| Don't use | Why | Use instead |
| --- | --- | --- |
| `MMF_TimescaleModifier`, `MMF_FreezeFrame` (+ `MMTimeManager` in scenes) | **`TimeService` owns `Time.timeScale`.** Both drive `MMTimeManager`, which sets `Time.timeScale` directly. FreezeFrame's "Automatic Shaker Setup" button *adds* an `MMTimeManager`, so don't click it | Custom `MMF_HitStop` feedback → TimeService (§5) |
| `MMF_Sound`, `MMF_MMSoundManager*`, `MMF_Playlist`, `MMF_AudioSource*`, `MMF_AudioMixerSnapshotTransition`, `MMF_AudioFilter*` | **BroAudio owns audio** (pooling, mixing, rules) | Custom `MMF_BroSound` feedback (§5) |
| `MMF_FloatingText` (+ `MMFloatingTextSpawner`) | **Damage Numbers Pro** owns hit numbers (pooled) | DNP from the damage view |
| `MMF_LoadScene`, `MMF_UnloadScene` | Scene flow is owned by game code (a planned `SceneLoader`, ARCHITECTURE.md → Scopes and app flow) | Call the scene flow through game code |
| `MMF_Broadcast`, `MMF_BroadcastProxy`, `MMF_RadioSignal`, `MMF_MMGameEvent`, `MMF_PlayerChain` via channels, triggering `MMF_PlayerEvent` from code | Static event buses (MMEventManager / MMRadio). Cross-system events go through MessagePipe | MessagePipe event → view plays the player |
| `MMF_InstantiateObject`, `MMF_ParticlesInstantiation`, `MMF_Destroy` | Spawning is ours (pools, ARCHITECTURE.md) | `MMF_Particles` on pre-placed child systems; world VFX through our VFX pool |
| `MMF_Bloom`, `MMF_Vignette`, `MMF_ChromaticAberration`… (no `_URP` suffix) | PPv2, not compiled (`MM_POSTPROCESSING` off) | The `_URP` variants |
| `MMF_UIToolkit*` | UI is uGUI (PLUGINS.md) | uGUI feedbacks (`MMF_Image`, `MMF_CanvasGroup`, `MMF_RectTransform*`, TMP) |
| `MMF_Haptics`, `MMF_NV*` | Nice Vibrations not installed, PC only | — |

`MMF_Events` (UnityEvents) is allowed for calling a method on **our view component** (e.g. tell the HUD to blink). Don't use it to call into services or plugin singletons.

## 3. Camera (Cinemachine 3)

The camera rig lives inside the Player prefab (ARCHITECTURE.md).
- **Screen shake / kicks / explosions:** `MMF_CinemachineImpulse` plus a **`CinemachineImpulseListener`** extension on the player's `CinemachineCamera`. Impulses add on top of other noise and respect distance. With `MM_CINEMACHINE3` the feedback calls `m_ImpulseDefinition.CreateEvent(position, Velocity * intensity)` directly, so no source component is needed. Set a non-zero `Velocity` (a zero velocity means no shake).
- Every impulse play sets the **global** `CinemachineImpulseManager.Instance.IgnoreTimeScale` from that feedback's timescale mode. Keep **all** impulse feedbacks on the same mode (Scaled), or the last one played flips it for everyone.
- **FOV punch (sprint, explosions, ADS kick):** `MMF_CameraFieldOfView` plus `MMCinemachineFieldOfViewShaker` on the `CinemachineCamera`. Match the channel. If the player controller also sets FOV (ADS, sprint), let the shaker run additively (relative values) or it will fight the controller.
- Don't use `MMF_CameraShake` + `MMCinemachineCameraShaker` (perlin-noise shaker) together with impulses. Pick impulses for the FPS camera.
- Cinemachine owns the camera (PLUGINS.md). Never move the camera transform with position or rotation feedbacks.

## 4. Post-processing (URP volume shakers)

`MMF_Vignette_URP`, `MMF_ChromaticAberration_URP`, `MMF_LensDistortion_URP`, `MMF_ColorAdjustments_URP`, `MMF_DepthOfField_URP`… send events to a **shaker on a Volume** (`MMVignetteShaker_URP` etc.). The volume's profile must contain that override (Vignette, …) **enabled**.
- **No feedback volume exists yet.** When a feedback first needs one, add one dedicated global Volume holding the overrides and their shakers, with the override values neutral at rest and a priority above the time-of-day grades (`Grade From` 10, `Grade To` 11; there is no base volume, DAYNIGHT.md). Don't put shakers on the grade volumes: the day/night director blends the same overrides and would fight Feel.
- Use **channels** (`MMChannel` assets, or int channels) when several shakers of the same type exist, e.g. "PlayerHurt" vs "LowHealth" vignette.
- Don't use the feedback's "Automatic Shaker Setup" in World. It creates a new volume. Wire shakers onto the feedback volume by hand.
- Put that volume with the camera rig (Player prefab) so it works in any scene.

## 5. Custom feedbacks (bridging to our services)

Feedbacks are plain serialized objects inside `MMF_Player`, so **VContainer can't inject them** (same problem as BD tasks). Bridge through an injected component on the player's GameObject or a parent:

```csharp
namespace Aipocalypse.Core.Feedback
{
    // Gives Feel feedbacks (plain serialized objects VContainer can't inject) access to our services. Put it on or above the MMF_Players.
    public sealed class FeedbackContext : MonoBehaviour
    {
        public TimeService Time { get; private set; }

        [Inject]
        void Construct(TimeService time) => Time = time;
    }
}
```

```csharp
using MoreMountains.Feedbacks;
using UnityEngine;

namespace Aipocalypse.Core.Feedback
{
    [AddComponentMenu("")]
    [System.Serializable]
    [FeedbackPath("Aipocalypse/Hit Stop")]
    [FeedbackHelp("Asks TimeService for a short hit stop. Never sets Time.timeScale itself.")]
    public class MMF_HitStop : MMF_Feedback
    {
        public static bool FeedbackTypeAuthorized = true;

        [MMFInspectorGroup("Hit Stop", true, 12)]
        [Tooltip("Real seconds")] public float Duration = 0.07f;
        [Range(0f, 1f)] public float TimeScale = 0.05f;

        FeedbackContext _context;

        public override float FeedbackDuration { get => ApplyTimeMultiplier(Duration); set => Duration = value; }

        protected override void CustomInitialization(MMF_Player owner)
        {
            base.CustomInitialization(owner);
            _context = owner.GetComponentInParent<FeedbackContext>();
        }

        protected override void CustomPlayFeedback(Vector3 position, float feedbacksIntensity = 1.0f)
        {
            if (!Active || !FeedbackTypeAuthorized || _context == null) return;
            _context.Time.RequestHitStop(FeedbackDuration * feedbacksIntensity, TimeScale);   // TimeService runs the timer on unscaled time
        }
    }
}
```

- Both are implemented in `_Core/_Common/Feedback/` (`FeedbackContext.cs`, `MMF_HitStop.cs`). `TimeService.RequestHitStop(duration, scale)` runs in real time, pause beats hit stop, overlapping stops keep the longest time and lowest scale. Don't let a feedback or Feel's `MMTimeManager` own it.
- **`MMF_BroSound`** (not built yet; write it when a feedback needs sound), same shape: `[SerializeField] SoundID _sound;` and `CustomPlayFeedback` calls `AudioService.PlayAt(_sound, position)` reached through `FeedbackContext` (add `AudioService` to it), never BroAudio's static facade (`tools-unity-broaudio`).
- Override points (from `MMF_Feedback.cs`): `CustomInitialization(MMF_Player owner)`, `CustomPlayFeedback(position, intensity)` (abstract), `CustomStopFeedback`, `CustomSkipToTheEnd`, `CustomRestoreInitialValues`, `CustomReset`, `CustomPlayerComplete`, `FeedbackDuration`. Useful members: `Owner`, `Active`, `Timing`, `FeedbackTime` / `FeedbackDeltaTime` (respect the timescale mode), `InScaledTimescaleMode`, `NormalPlayDirection`, `ComputeIntensity(intensity, position)`.
- Put custom feedbacks in `Assets/_Game/_Core/_Common/Feedback/` (Core) or a feature's folder. The asmdef references `MoreMountains.Tools`.

## 6. Crowd enemies and performance

AGENTS.md: keep per-instance plugin components off crowd entities.
- **No `MMF_Player` on crowd enemy prefabs.** Hundreds of players with initialized feedback lists is the per-instance overhead we avoid.
- Enemy **hit flash**: the enemy's renderer gets a shader user value (`SetShaderUserValue`, read by the graph; not a MaterialPropertyBlock, which takes it off the SRP Batcher; `tools-unity-shader-graph`) from a central enemy tick or the damage router. It's cheap and needs no Feel.
- Enemy **hit and death VFX**: our VFX pool. **Damage numbers**: Damage Numbers Pro (pooled).
- **Player-side reaction to hitting enemies** (hit marker punch, small impulse on kill, hit stop on headshot; the hit sound goes through `AudioService`): *one* view on the player subscribed to player-sourced `DamageDealt` (`Result.Killed` for kills, as `HitFeedbackHud` reads it), playing shared players with `PlayFeedbacks(hitPoint, intensity)`. One player serves every enemy.
- Unique entities (dog, cat, drone, bosses) may carry their own MMF_Players.
- Spammy events (automatic fire, many hits per frame): keep `CanPlayWhileAlreadyPlaying = true` for shots, and set `CooldownDuration` on players that shouldn't stack (camera impulse on every hit of a shotgun blast should play once).
- Turn on `PerformanceMode` on players with many feedbacks (skips inspector refresh). Disable `DebugActive`.

## 7. MMF_Player control (runtime API)

From `MMF_Player.cs` / `MMFeedbacks.cs`:
- Play: `PlayFeedbacks()`, `PlayFeedbacks(Vector3 position, float intensity = 1, bool forceChangeDirection = false)`, `PlayFeedbacksInReverse()`. Stop or reset: `StopFeedbacks()`, `ResetFeedbacks()`, `RestoreInitialValues()`, `SkipToTheEnd()`. Pause: `PauseFeedbacks()` / `ResumeFeedbacks()`.
- State: `IsPlaying`, `TotalDuration`, `HasFeedbackStillPlaying()`, `TimesPlayed`.
- Settings fields: `InitializationMode` (default `Start`), `AutoPlayOnStart`, `AutoPlayOnEnable`, `CanPlay`, `CanPlayWhileAlreadyPlaying`, `CooldownDuration`, `InitialDelay`, `ChanceToPlay`, `FeedbacksIntensity`, `PlayerTimescaleMode`, `ForceTimescaleMode` / `ForcedTimescaleMode`, `DurationMultiplier`, `PlayMode` (Parallel/Sequential), `StopFeedbacksOnDisable`, `RestoreInitialValuesOnDisable`, `PerformanceMode`.
- Retarget before playing: `GetFeedbackOfType<MMF_Flicker>().BoundRenderer = r;` (or `GetFeedbackOfType<T>(label)`). Cache the feedback reference once and don't search per play.
- Adding feedbacks at runtime: `AddFeedback(typeof(T))`, then `Initialization()`. Prefer authoring in the inspector.
- Await completion: `PlayFeedbacksCoroutine` exists. With UniTask, wait on `IsPlaying` (`await UniTask.WaitWhile(() => player.IsPlaying, cancellationToken: ct)`).

## 8. Time scale and pause

- Gameplay feedbacks: **Scaled** timescale mode, so they freeze with pause (TimeService sets `timeScale = 0`).
- UI and menu feedbacks (pause menu, death screen): **Unscaled**, so they still animate while paused.
- During a hit stop (timeScale ≈ 0.05), Scaled feedbacks slow down too. That's usually the desired "impact" look. Non-impulse feedbacks that must play at full speed through a hit stop can be Unscaled. Impulses can't (§3, global flag).

## 9. Pooled and reused objects

For unique entities that despawn and respawn (companions, boss phases) or pooled weapon view models: set `StopFeedbacksOnDisable` and `RestoreInitialValuesOnDisable` so a feedback interrupted mid-play doesn't leave a scaled, offset or colored object behind.

## 10. Debugging

- Each MMF_Player inspector has a Play button in Play mode and per-feedback progress bars. Feedbacks with missing targets show a setup warning (`EvaluateRequiresSetup`).
- Nothing happens: `CanPlay` off, cooldown active, `ChanceToPlay` < 100, feedback `Active` off, shaker missing on the volume or camera, channel mismatch between feedback and shaker, timescale paused with a Scaled feedback.
- Vignette or other post stuck after a stop: shaker `ResetShakerValuesAfterShake` off, or the override's base value isn't neutral.
- Time scale wrong after a hit: an `MMTimeManager` slipped into the scene, or someone used `MMF_FreezeFrame`/`MMF_TimescaleModifier` (§2).
