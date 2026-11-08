# Character crowd performance (aipocalypse)

Moved out of SKILL.md (same numbering) to keep it short.

## 5. Performance (crowds)

Per character: one Animator (state machine + 4 layers), one SkinnedMeshRenderer with ~100–115 bones, and the vertex count.
- Measure with `PerfSuite.Crowd(counts, seconds, hour, profile, keep, ab)` (`AgentScripts/Perf/`, in Play).
  - It hides the scene's enemies, spawns goblins in front of the player and reports each count facing the crowd and facing away, with enemy, Animator and skinning markers.
  - `ab` measures runtime variants at the largest count, each restored after: `cull`, `nodebug`, `nolayers`, `noshadow`, `lodN`, `thrX`, `biasX`, `base`.
- **Measured 2026-10-27** (editor Play, 1080p, RTX 5070 Ti Laptop + Core Ultra 7 255HX; linear from 0 to 300 goblins):

  | Per goblin | Before | Now (LOD2 bias 2, `CullCompletely`) |
  | --- | --- | --- |
  | Main thread, on screen | ~27 µs | ~28 µs |
  | Main thread, off screen | ~17 µs | ~13 µs |
  | GPU, on screen | ~15 µs | ~12 µs |
  | Triangles drawn (depth, color, shadows) | ~72k | ~25k |

  - Main thread at 80 goblins, on screen, per frame:
    - Animator ~1 ms (`DirectorUpdateAnimationBegin` + `End`; ~0.1 ms when off screen with `CullCompletely`);
    - `EnemyAnimationManager` ~0.3 ms;
    - Behavior Designer ~0.2 ms;
    - physics ~0.2 ms;
    - skinning ~0.1 ms;
    - `EnemyManager.Think` ~0.03 ms.
  - Baseline without enemies: ~7 ms PlayerLoop and ~2.3 ms GPU. Editor numbers: confirm in a development build.
  - Budget: 80 goblins are ~2.3 ms of main thread and ~1 ms of GPU here (≈2.5–3 ms on a 2060) with bias 2.
- **Bias back to 0 (2026-10-28):** bias 2 kept every goblin on LOD2 even at arm's length: blocky faces, arms broken into segments, very visible in the T-pose of edit mode and in melee range. With bias 0, screen size picks the level (LOD0 close, LOD1/2 farther). Cost at 80 goblins clustered in view (`ab` `bias2` vs `base`): 14.1 M vs 10.2 M triangles, about +0.6 ms GPU here (≈+1.5–2 ms on a 2060), main thread unchanged; facing away, identical. Bias 1 sits between (12.3 M). If crowds need the GPU back, raise the bias only for far enemies (a distance tier), never on close ones.
- **Tried, no gain:**
  - FollowerEntity `debugFlags` off: nothing measurable. Goblin.prefab has it off anyway.
  - Project-wide Mesh LOD Threshold: works (8 ≈ bias 4), but it changes every Mesh LOD asset and every distance. Prefer a per-renderer bias on far enemies only.
- Next levers, cheapest first:
  - Fewer layers for distant enemies. All three look / lean layers off saved ~25% of the Animator cost.
  - Shadows off for far enemies (−0.36 ms GPU at 80 with all of them off).
  - **GPU Instancer Pro – Crowd Animations** for hundreds (PLUGINS.md, Undecided: ask); not needed at the 50-enemy cap.
  - Optimize Transform Hierarchy is unverified here (tools-unity-synty-characters §2).
- `EnemyAnimationManager` itself is a loop of `SetFloat`s with cached hashes: zero allocations. Its per-call costs (`SetFloat`, `FollowerEntity.velocity`) are under 1 µs, so it's not worth optimizing.
