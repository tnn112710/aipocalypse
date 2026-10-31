# Feel: MMF_Player runtime API and debugging

Moved out of SKILL.md (same numbering) to keep it short.

## 7. MMF_Player control (runtime API)

From `MMF_Player.cs` / `MMFeedbacks.cs`:
- Play: `PlayFeedbacks()`, `PlayFeedbacks(Vector3 position, float intensity = 1, bool forceChangeDirection = false)`, `PlayFeedbacksInReverse()`. Stop or reset: `StopFeedbacks()`, `ResetFeedbacks()`, `RestoreInitialValues()`, `SkipToTheEnd()`. Pause: `PauseFeedbacks()` / `ResumeFeedbacks()`.
- State: `IsPlaying`, `TotalDuration`, `HasFeedbackStillPlaying()`, `TimesPlayed`.
- Settings fields: `InitializationMode` (default `Start`), `AutoPlayOnStart`, `AutoPlayOnEnable`, `CanPlay`, `CanPlayWhileAlreadyPlaying`, `CooldownDuration`, `InitialDelay`, `ChanceToPlay`, `FeedbacksIntensity`, `PlayerTimescaleMode`, `ForceTimescaleMode` / `ForcedTimescaleMode`, `DurationMultiplier`, `PlayMode` (Parallel/Sequential), `StopFeedbacksOnDisable`, `RestoreInitialValuesOnDisable`, `PerformanceMode`.
- Retarget before playing: `GetFeedbackOfType<MMF_Flicker>().BoundRenderer = r;` (or `GetFeedbackOfType<T>(label)`). Cache the feedback reference once and don't search per play.
- Adding feedbacks at runtime: `AddFeedback(typeof(T))`, then `Initialization()`. Prefer authoring in the inspector.
- Await completion: `PlayFeedbacksCoroutine` exists. With UniTask, wait on `IsPlaying` (`await UniTask.WaitWhile(() => player.IsPlaying, cancellationToken: ct)`).

## 10. Debugging

- Each MMF_Player inspector has a Play button in Play mode and per-feedback progress bars. Feedbacks with missing targets show a setup warning (`EvaluateRequiresSetup`).
- Nothing happens: `CanPlay` off, cooldown active, `ChanceToPlay` < 100, feedback `Active` off, shaker missing on the volume or camera, channel mismatch between feedback and shaker, timescale paused with a Scaled feedback.
- Vignette or other post stuck after a stop: shaker `ResetShakerValuesAfterShake` off, or the override's base value isn't neutral.
- Time scale wrong after a hit: an `MMTimeManager` slipped into the scene, or someone used `MMF_FreezeFrame`/`MMF_TimescaleModifier` (§2).
