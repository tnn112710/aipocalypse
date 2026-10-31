# Feel feedback catalog: project status

Every feedback type in the installed Feel, grouped by status. **Use** = fine on any MMF_Player. **Care** = allowed, with the note's caveat. **No** = banned in this project (see SKILL.md §2 for replacements). Source folders: `MMFeedbacks/MMFeedbacks/Feedbacks/` (core) and `MMFeedbacks/MMFeedbacksForThirdParty/<Module>/Feedbacks/`.

## Transform / motion (view models, UI, props, unique entities)

| Feedback | Status | Note |
| --- | --- | --- |
| `MMF_Position`, `MMF_Rotation`, `MMF_Scale`, `MMF_PositionShake`, `MMF_RotationShake`, `MMF_ScaleShake` | Use | Weapon view kick, pickups, UI punches. Never on the camera transform |
| `MMF_PositionSpring`, `MMF_RotationSpring`, `MMF_ScaleSpring`, `MMF_SquashAndStretch`, `MMF_SquashAndStretchSpring` | Use | Springs give good weapon recoil visuals |
| `MMF_Wiggle`, `MMF_LookAt`, `MMF_RotatePositionAround`, `MMF_DestinationTransform` | Use | |
| `MMF_SetParent` | Care | Only on our own view objects |
| `MMF_Rigidbody`, `MMF_Rigidbody2D` | Care | Visual props only. Never on A* agents or the player controller |

## Renderer / material / light

| Feedback | Status | Note |
| --- | --- | --- |
| `MMF_Flicker`, `MMF_Material`, `MMF_MaterialSetProperty`, `MMF_TextureOffset`, `MMF_TextureScale`, `MMF_ShaderGlobal` | Use | Unique entities and the player only. Crowd enemy hit flash is our shader code (SKILL.md §6) |
| `MMF_Light` | Use | Muzzle flash light |
| `MMF_Skybox` | Care | Conflicts with the time of day (`EnvironmentDirector` drives the sky, DAYNIGHT.md) |
| `MMF_Fog` (MMTools) | Care | Same conflict: `EnvironmentDirector` owns the fog |
| `MMF_Sprite*`, `MMF_SpriteSheetAnimation` | Use | Rare in a 3D FPS |
| `MMF_Blink` (MMTools) | Use | |

## Particles / VFX

| Feedback | Status | Note |
| --- | --- | --- |
| `MMF_Particles` | Use | Plays pre-placed child systems (muzzle, shell eject) |
| `MMF_VisualEffect`, `MMF_VisualEffectSetProperty` | Use | VFX Graph on pre-placed objects |
| `MMF_ParticlesInstantiation`, `MMF_InstantiateObject`, `MMF_Destroy` | No | Spawning goes through our pools |
| `MMF_TrailRenderer`, `MMF_LineRenderer` (MMTools) | Use | |

## Camera (Cinemachine 3)

| Feedback | Status | Note |
| --- | --- | --- |
| `MMF_CinemachineImpulse` | Use | Default screen shake. Needs `CinemachineImpulseListener`. All impulses on Scaled mode |
| `MMF_CinemachineImpulseClear` | Use | Clear shakes on death or menu |
| `MMF_CameraFieldOfView` + `MMCinemachineFieldOfViewShaker` | Care | Must not fight the controller's FOV (ADS/sprint) |
| `MMF_CinemachineCameraDutch` | Use | Roll on big hits |
| `MMF_CinemachineTransition`, `MMF_CinemachineImpulseSource` | Care | Camera ownership stays with the player rig |
| `MMF_CameraShake` (+ `MMCinemachineCameraShaker`) | No | Use impulses |
| `MMF_CameraZoom`, `MMF_CameraClippingPlanes`, `MMF_CameraOrthographicSize` | No | Not needed for the FPS rig. Zoom/ADS is controller logic |

## Post-processing (URP, needs shakers on a feedback volume; none exists yet, SKILL.md §4)

| Feedback | Status |
| --- | --- |
| `MMF_Vignette_URP`, `MMF_ChromaticAberration_URP`, `MMF_LensDistortion_URP`, `MMF_ColorAdjustments_URP`, `MMF_Bloom_URP`, `MMF_FilmGrain_URP`, `MMF_DepthOfField_URP`, `MMF_MotionBlur_URP`, `MMF_WhiteBalance_URP`, `MMF_ChannelMixer_URP`, `MMF_PaniniProjection_URP` | Use |
| `MMF_GlobalPPVolumeAutoBlend_URP` | Care: blends a whole volume, so keep it on the feedback volume |
| `MMF_Light2D_URP` | No (2D) |
| PPv2 `MMF_Bloom`, `MMF_Vignette`, `MMF_ChromaticAberration`, `MMF_ColorGrading`, `MMF_DepthOfField`, `MMF_LensDistortion`, `MMF_GlobalPPVolumeAutoBlend`, `MMF_PPMovingFilter` | No (not compiled) |
| `*_HDRP` | No (not compiled) |

## UI (uGUI + TMP)

| Feedback | Status |
| --- | --- |
| `MMF_Image`, `MMF_ImageAlpha`, `MMF_ImageFill`, `MMF_ImageMaterial`, `MMF_ImageSprite`, `MMF_ImageTextureOffset`, `MMF_ImageTextureScale`, `MMF_ImageRaycastTarget`, `MMF_Graphic`, `MMF_GraphicCrossFade`, `MMF_CanvasGroup`, `MMF_CanvasGroupBlocksRaycasts`, `MMF_RectTransformAnchor`, `MMF_RectTransformOffset`, `MMF_RectTransformPivot`, `MMF_RectTransformSizeDelta`, `MMF_Text`, `MMF_TextColor`, `MMF_TextFontSize`, `MMF_Fade` | Use (UI players on Unscaled when they must animate while paused) |
| `MMF_TMPAlpha`, `MMF_TMPColor`, `MMF_TMPCountTo`, `MMF_TMPCountToLong`, `MMF_TMPText`, `MMF_TMPTextReveal`, `MMF_TMPFontSize`, `MMF_TMPDilate`, `MMF_TMPSoftness`, `MMF_TMPOutlineColor`, `MMF_TMPOutlineWidth`, `MMF_TMPCharacterSpacing`, `MMF_TMPLineSpacing`, `MMF_TMPParagraphSpacing`, `MMF_TMPWordSpacing` | Use. `MMF_TMPTextReveal` only inside an authored MMF_Player for a feedback-style reveal; a code-driven typewriter uses TMP `maxVisibleCharacters` + DOTween (Text Studio only if adopted: PLUGINS.md, Text Studio row) |
| `MMF_UIToolkit*` | No (UI is uGUI) |
| `MMF_FloatingText` | No (Damage Numbers Pro) |

## Animation

| Feedback | Status | Note |
| --- | --- | --- |
| `MMF_Animation` (Animator parameter), `MMF_AnimatorPlayState`, `MMF_AnimationCrossfade`, `MMF_AnimatorSpeed` | Care | Only for view-only animation (view model flourish). State the gameplay depends on is driven by our code |

## Audio

| Feedback | Status |
| --- | --- |
| `MMF_Sound`, `MMF_MMSoundManagerSound`, `MMF_MMSoundManagerSoundControl`, `MMF_MMSoundManagerSoundFade`, `MMF_MMSoundManagerTrackControl`, `MMF_MMSoundManagerTrackFade`, `MMF_MMSoundManagerAllSoundsControl`, `MMF_MMSoundManagerSaveLoad`, `MMF_MMSoundManagerSoundData`, `MMF_Playlist`, `MMF_AudioSource`, `MMF_AudioSourcePitch`, `MMF_AudioSourceVolume`, `MMF_AudioSourceStereoPan`, `MMF_AudioFilter*`, `MMF_AudioMixerSnapshotTransition` | No (BroAudio, via custom `MMF_BroSound`) |

## Time / flow / control

| Feedback | Status | Note |
| --- | --- | --- |
| `MMF_Pause`, `MMF_HoldingPause`, `MMF_Looper`, `MMF_LooperStart` | Use | Sequencing inside a player |
| `MMF_Feedbacks`, `MMF_PlayerControl` | Use | Play or stop another MMF_Player by direct reference |
| `MMF_PlayerChain` | Care | By direct reference only, not by channel event |
| `MMF_Events` | Care | UnityEvents into our view components only |
| `MMF_Enable`, `MMF_SetActive`, `MMF_Collider`, `MMF_Collider2D`, `MMF_Layer` | Care | Visual objects only. Never toggle gameplay colliders or entities from a feedback |
| `MMF_RandomEvents`, `MMF_ReferenceHolder`, `MMF_FloatController`, `MMF_ShaderController`, `MMF_Property` | Use | |
| `MMF_DebugLog`, `MMF_DebugComment`, `MMF_DebugBreak` | Care | Remove before commit |
| `MMF_TimescaleModifier`, `MMF_FreezeFrame` | No | TimeService owns timeScale. Use custom `MMF_HitStop` |
| `MMF_LoadScene`, `MMF_UnloadScene` | No | Scene flow is owned by game code (a planned `SceneLoader`) |
| `MMF_Broadcast`, `MMF_BroadcastProxy`, `MMF_RadioSignal`, `MMF_MMGameEvent` | No | Static event buses. Use MessagePipe |
| `MMF_VideoPlayer` | Use | |
| `MMF_Haptics`, `MMF_NV*` | No | Not installed / PC |

## MMTools components to keep out of scenes

`MMTimeManager`, `MMSoundManager`, `MMSceneLoadingManager`, `MMGameEvent` / `MMEventManager` listeners in our code, `MMObjectPooler` / `MMMiniObjectPooler`, `MMFloatingTextSpawner`, `MMDebugMenu` (we have our own debug panel), `MMSaveLoadManager`, any `MMSingleton`/`MMPersistentSingleton` subclass. Shakers (`MM*Shaker`, `MM*Shaker_URP`, `MMCinemachine*Shaker`) are fine: they're the receiving end of feedbacks.
