---
name: tools-unity-cinemachine
description: Cinemachine 3 (package 6.6, namespace Unity.Cinemachine) for aipocalypse's first-person camera — the player camera rig inside the Player prefab (HardLockToTarget + RotateWithFollowTarget driven by our look code, not PanTilt input), FOV for sprint/ADS/settings, view bob noise, impulses for shake, camera offset for ADS/lean, extra cameras (death cam, cutscenes, drone view) by priority, brain update/blend settings, time scale. Use for any camera work. NOT Cinemachine 2 — CinemachineVirtualCamera, `using Cinemachine`, m_Lens/m_Follow/m_Priority, GetCinemachineComponent<T>() are the old API.
---

# Cinemachine 3 (aipocalypse)

Installed: **`com.unity.cinemachine` 6.6.0**, which is the **Cinemachine 3 API**. Namespace **`Unity.Cinemachine`**, asmdef `Unity.Cinemachine`. Source: `Library/PackageCache/com.unity.cinemachine@*/Runtime` (`Behaviours/`, `Components/`, `Impulse/`, `Helpers/`). Docs: https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/index.html.

Most examples online are Cinemachine 2 and won't compile:

| CM2 (don't write) | CM3 (installed) |
| --- | --- |
| `using Cinemachine;` | `using Unity.Cinemachine;` |
| `CinemachineVirtualCamera` | `CinemachineCamera` (the old class is in `Runtime/Deprecated`, so don't add it) |
| `vcam.m_Follow`, `m_LookAt`, `vcam.Follow =` | `cam.Target.TrackingTarget`, `cam.Target.LookAtTarget` (`Follow`/`LookAt` properties still exist) |
| `vcam.m_Lens.FieldOfView` | `cam.Lens.FieldOfView` (struct field, assignable in place) |
| `vcam.m_Priority = 10` | `cam.Priority = 10` (`PrioritySettings`, implicit from int) or `cam.Prioritize()` |
| `GetCinemachineComponent<T>()` | Components are separate MonoBehaviours on the camera GameObject, so use `GetComponent<T>()` once and cache it |
| `CinemachineComposer`, `CinemachineTransposer`, `CinemachinePOV`, `Cinemachine3rdPersonFollow` | `CinemachineRotationComposer`, `CinemachineFollow`, `CinemachinePanTilt`, `CinemachineThirdPersonFollow` |
| `noise.m_AmplitudeGain` | `noise.AmplitudeGain`, `noise.FrequencyGain` |
| `impulseSource.m_ImpulseDefinition` | `impulseSource.ImpulseDefinition`, `GenerateImpulse(velocity)` / `GenerateImpulseAt(pos, velocity)` |
| `brain.m_DefaultBlend.m_Time` | `brain.DefaultBlend = new CinemachineBlendDefinition(Styles.Cut, 0f)` |

## 1. The player camera rig

ARCHITECTURE.md: the camera rig, `CinemachineBrain` and `AudioListener` live **inside the Player prefab**, so the player works dropped into any scene.

```text
Player (PlayerScope : EntityScope)            ← yaw rotates this root (our look code)
├─ CameraPivot (eye height)                   ← pitch rotates this (our look code); gameplay aim origin
├─ PlayerCamera  [CinemachineCamera]
│     Tracking Target = CameraPivot
│     Position Control: CinemachineHardLockToTarget
│     Rotation Control: CinemachineRotateWithFollowTarget (Damping 0)
│     Noise: CinemachineBasicMultiChannelPerlin (view bob/idle sway, gain driven by speed)
│     Extensions: CinemachineImpulseListener, CinemachineCameraOffset (ADS / lean)
└─ MainCamera    [Camera + CinemachineBrain + AudioListener + UniversalAdditionalCameraData]
```

- **Our look code owns rotation.** `InputService`'s `Look` action → player controller rotates the root (yaw) and `CameraPivot` (pitch, clamped). The Cinemachine camera just copies it (`HardLockToTarget` + `RotateWithFollowTarget`). Don't use `CinemachinePanTilt` + `CinemachineInputAxisController`. They read input themselves (bypassing `InputService` and map switching), and gameplay aim would then have to read rotation back from the camera.
- **Shoot from `CameraPivot`**, not from the rendered camera. Noise, impulses and offsets move the camera, not the pivot, so shake never changes where bullets go. Visual recoil is a camera effect. Gameplay recoil is the WeaponSystem rotating the pivot by data.
- `MainCamera` may sit inside the Player prefab. The brain overwrites its world transform every frame. There must be **one** `CinemachineBrain` and one `AudioListener` in the scene. Prototype scenes must not keep their default Main Camera when the player is dropped in.
- Unity's `Camera` component settings (clear flags, URP renderer, post-processing on) live on `MainCamera`. Lens values (FOV, near/far clip) are driven by the active `CinemachineCamera.Lens`, so set them there.

## 2. FOV (settings, sprint, ADS)

```csharp
// On the Player prefab; ticked by the player's own update path, writes only when the value changes.
public sealed class PlayerCameraLens : MonoBehaviour
{
    [SerializeField] CinemachineCamera _camera;
    float _baseFov, _targetFov, _currentFov;
    float _fovSpeed;                                    // from PlayerTuning

    [Inject]
    void Construct(ISettings settings, PlayerTuning tuning)  // placeholder names
    {
        _baseFov = _targetFov = _currentFov = settings.FieldOfView;
        _fovSpeed = tuning.FovLerpSpeed;
    }

    public void SetMultiplier(float multiplier) => _targetFov = _baseFov * multiplier;   // sprint 1.1, ADS from WeaponDef

    public void Tick(float dt)
    {
        if (Mathf.Approximately(_currentFov, _targetFov)) return;
        _currentFov = Mathf.MoveTowards(_currentFov, _targetFov, _fovSpeed * dt);
        _camera.Lens.FieldOfView = _currentFov;
    }
}
```

- Base FOV comes from the settings service (player option), and multipliers from data (`PlayerTuning`, `WeaponDef`). Nothing hardcoded.
- Feel's FOV shaker (`MMCinemachineFieldOfViewShaker`) captures the FOV at shake start and restores it. It fights this component during ADS transitions, so use relative mode or impulses instead (`tools-unity-feel` §3).
- Weapon view model: render it with a URP Renderer Feature (Render Objects on a "ViewModel" layer with its own FOV) or a camera-stack overlay so it doesn't clip into walls or stretch with FOV changes. That's URP work, not Cinemachine.

## 3. Shake, bob and offsets

| Effect | Tool |
| --- | --- |
| One-shot shake (shot kick, explosion, hurt) | **Impulse**: `CinemachineImpulseListener` on the camera. Sources via Feel's `MMF_CinemachineImpulse` (preferred, see `tools-unity-feel`) or a `CinemachineImpulseSource` on the emitter (`GenerateImpulseAt(position, velocity)`). Use listener `ChannelMask` to separate camera-only impulses |
| Continuous sway / view bob | `CinemachineBasicMultiChannelPerlin` with a handheld-style `NoiseProfile`. Drive `AmplitudeGain`/`FrequencyGain` from player speed (0 when still) |
| ADS / lean / crouch offset | `CinemachineCameraOffset.Offset` (applied after Aim). Crouch height itself is a pivot move by the controller, not a camera offset |
| Dutch roll on big hits | `cam.Lens.Dutch` or Feel's `MMF_CinemachineCameraDutch` |

- Don't animate the `MainCamera` or `CameraPivot` transforms for shake. The brain overwrites the camera, and the pivot is gameplay aim.
- Impulse **distance** matters: `GenerateImpulseAt(position, …)` decays with distance from the listener per the definition's dissipation. Explosions should pass their world position.
- Impulses obey `CinemachineImpulseManager.Instance.IgnoreTimeScale` (global). Feel sets it per play. Keep all impulses on scaled time.

## 4. Other cameras (death cam, cutscenes, drone view, menus)

- Each extra view is its own `CinemachineCamera` (in the relevant prefab or scene). Switch by priority: `cam.Priority = 20` to take over, back to `0` (or disable the GameObject) to release. `Prioritize()` moves a camera to the top among equal priorities.
- The player camera stays at a fixed base priority (e.g. 10). Disabling the `CinemachineCamera` GameObject is the cleanest "off".
- Blends: `brain.DefaultBlend` = **Cut** for gameplay (an FPS camera must never ease between views by accident). Specific pairs (gameplay → death cam: EaseOut 0.5 s) go in a `CinemachineBlenderSettings` asset on `brain.CustomBlends`.
- A camera following another target (drone view, dog view): `CinemachineFollow` or `CinemachineThirdPersonFollow` + `CinemachineRotationComposer`. Don't reuse the player's rig.
- React to camera switches with the `CinemachineBrainEvents` / `CinemachineCameraEvents` helper components (UnityEvents), or `brain.ActiveVirtualCamera` / `brain.IsBlending`. Never poll every frame for gameplay logic.

## 5. Brain settings

- `UpdateMethod`: `SmartUpdate` (default) or `LateUpdate`. If the player moves in `FixedUpdate` with Rigidbody interpolation, SmartUpdate handles it. Jitter usually means the pivot moves in a different update phase than the camera, so fix the controller's update phase rather than reaching for FixedUpdate on the brain.
- `IgnoreTimeScale = false`: damping and noise freeze when `TimeService` pauses, and slow during hit stop. Set it to true only on a menu-only brain.
- `ShowDebugText` / `ShowCameraFrustum` for debugging only.

## 6. Performance

- One live `CinemachineCamera` costs little. Inactive cameras in standby update round-robin (`StandbyUpdate = RoundRobin`). Set `Never` on cameras that are rarely used (cutscene cams).
- `CinemachineDeoccluder`/`Decollider` are third-person tools. The FPS rig doesn't need them.
- Don't write `Lens`, `Priority` or noise gains every frame unless the value changed (§2 pattern).

## 7. Debugging

- Brain inspector shows the live camera and blend. `ShowDebugText` overlays it in the Game view.
- Camera doesn't follow: `Target.TrackingTarget` unset, or two brains in the scene (the prototype's default camera).
- Shake does nothing: no `CinemachineImpulseListener` on the live camera, `ChannelMask` mismatch, zero impulse velocity, or the source is too far for its dissipation distance.
- Aim drifts while shaking: shots use the camera transform instead of `CameraPivot`.

---
Rewritten for this project from `tools-unity-cinemachine` in [tjboudreaux/cc-plugin-unity-gamedev](https://github.com/tjboudreaux/cc-plugin-unity-gamedev) (MIT, see `../THIRD_PARTY_LICENSE-unity-gamedev.txt`). API checked against the installed `com.unity.cinemachine` 6.6.0 source.
