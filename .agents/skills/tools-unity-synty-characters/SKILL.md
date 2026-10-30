---
name: tools-unity-synty-characters
description: Synty character content in aipocalypse. Covers the Sidekick Character Creator (tool 1.2.7, its SQLite DB, .sk files, species / part / body / color presets) and building Sidekick characters without the window using scripts/SidekickKit.cs (no DB writes, first-party model prefabs with their own avatar). Also covers the Sidekick character packs (Goblin Fighters / Species), the Polygon character packs (Goblin War Camp, Generic), the Synty animation packs (Base Locomotion and Goblin Locomotion — A_MOD_ vs POLY clips, the misleading _RM_ names, authored gait speeds, the two-frame additive look / lean clips), the rigs and avatars, and vendor traps (180 MB Resources in builds, the tool's update check, banned sample scripts, the EULA's generative-AI clause). Use whenever a task creates, customizes, rebuilds or animates a Synty character, picks Synty clips, or imports a new Sidekick or Synty character / animation pack. Turning the model into an enemy or NPC (prefab contract, controller, EnemyAnimator) is aipocalypse-characters; behaviour is aipocalypse-enemy-ai.
---

# Synty characters and animations (aipocalypse)

Vendor content: never edit anything under `Assets/Synty/`. Everything we make goes to `Assets/_Game/Content/<Kind>/<Name>/`.
The project side (folder layout, entity prefab, animator contract, verification) is **aipocalypse-characters**: read it too.
Source-level facts (DB tables, runtime API, the tool's export path, file:line): [references/sidekick-internals.md](references/sidekick-internals.md).

## 0. Status: ask before using

ASSETS.md tags every pack below **Undecided** (Polygon Generic and Prototype are **Avoid**). The Goblin enemy (`Content/Enemies/Goblin/`) uses Sidekick + Goblin Locomotion as a test the user asked for; any other use asks first (AGENTS.md → Ask the user first), then updates the pack's row.

| Pack | Folder | What |
| --- | --- | --- |
| Sidekick Characters (tool 1.2.7, DB 1.0.2) | `Assets/Synty/SidekickCharacters` | Modular character builder: one Humanoid rig, ~530 part FBX in `Resources/`, SQLite DB of parts / presets / colors |
| Sidekick character packs | `SidekickCharacters/Characters/<Pack>/<Name>/` | Ready characters: `.sk` + prefab + mesh/avatar assets (GoblinFighters, GoblinSpecies today; more get imported) |
| Animation Base Locomotion | `Assets/Synty/AnimationBaseLocomotion` | 346 Sidekick + 346 Polygon clips, Masculine / Feminine sets + neutral additives |
| Animation Goblin Locomotion | `Assets/Synty/AnimationGoblinLocomotion` | 194 Sidekick + 194 Polygon clips, neutral only, + idle fidgets and a stumble |
| Polygon Goblin War Camp | `Assets/Synty/PolygonGoblinWarCamp` | 15 fixed `SM_Chr_*` characters (Polygon rig) + camp props |

## 1. Pick the source

- **Sidekick** (preferred for humanoids we build): one rig and one avatar layout for every part, so every Sidekick clip fits every Sidekick character. Customizable by parts, body shape and colors; built into our own clean prefab by SidekickKit.
- **Sidekick pack characters** (`Characters/<Pack>/<Name>/`): use their **`.sk`** as the starting point, never their prefabs. The shipped prefabs come from an older tool: SkinnedMeshRenderer bone arrays of 1.7k–2.9k entries for ~115 real bones, readable 5–15 MB meshes, 82 facial blendshapes, `updateWhenOffscreen` on, culling AlwaysAnimate (`CharacterKit.Describe` shows it).
- **Polygon packs** (Goblin War Camp, Generic): fixed prefabs on the classic Polygon rig (Hips = `Root`, 40 humanoid bones), one avatar per pack, several body meshes per prefab. They need the Polygon clips (`A_*` / `A_POLY_*`), not the Sidekick ones. When making an avatar for a Polygon model, map the third "mitt" finger chain to **Middle** (Unity guesses Little and the finger animation breaks).

## 2. Make a Sidekick character (no window needed)

The **`.sk` file is the source of truth** and lives with the content: `Content/<Kind>/<Name>/Model/<Look>.sk`. Get one by:
1. copying a pack's `.sk` (`AssetDatabase.CopyAsset`), as the Goblin did with `GoblinFighter_01…05.sk`;
2. writing one from presets: `SidekickKit.ListPresets("<Species>")`, then `SidekickKit.WriteSk(skPath, name, species, headPreset, upperPreset, lowerPreset, bodyPreset, speciesColors, outfitColors, attachmentColors)` (`""` skips a group); the Goblin's look 06 came from this. Preset names repeat across groups, so WriteSk looks each one up within its group;
3. the user designing it in **Synty → Sidekick Character Tool** and saving with **Save Character** into that folder (double-clicking a `.sk` opens it in the tool). You can't click the tool: ask the user when a look needs hand tweaking.

Then build the model prefab (run from the repo root; Git Bash needs `--project-path "$(cygpath -w "$PWD")"`):

```bash
unity command script run --file .agents/skills/tools-unity-synty-characters/scripts/SidekickKit.cs --entry SidekickKit.Build \
  --args '["Assets/_Game/Content/Enemies/Goblin/Model/Goblin_01.sk","Assets/_Game/Content/Enemies/Goblin/Model","Goblin_01",false,false,""]' --timeout 300
```

`Build(skPath, outFolder, name, keepFacialBlendShapes, optimizeHierarchy, exposedBones)` writes, in `outFolder`:
`<name>_Model.prefab` (root Animator + `mesh` SkinnedMeshRenderer + `root` bones), `<name>_Mesh.asset`, `<name>_Avatar.asset`, `<name>.mat`, `T_<name>ColorMap.png`. Rebuilding overwrites in place and keeps every GUID, so prefabs that nest the model stay wired. Then check it with `CharacterKit.Snapshot` / `Describe` (aipocalypse-characters §6).

What Build does differently from the tool's own export, and why:
- **Reads the vendor package only.** It never calls `SidekickRuntime.PopulateToolData` (that writes `file_exists` into `Side_Kick_Data.db`, a binary diff in git) and never paints the shared `M_BaseMaterial` textures (the runtime paints them in place for the whole editor session).
- **Builds in a throwaway additive scene**, so the open scene isn't dirtied.
- **Avatar of this exact hierarchy** (`<name>_Avatar.asset`): the human map of `SK_BaseModelAvatar` with this model's skeleton as rest pose. The shared base avatar lacks the parts' `*_dyn_*` bones and the moved attach bones.
- **Crowd-friendly defaults:** one combined mesh, body blendshapes baked, facial blendshapes dropped (`keepFacialBlendShapes=true` only for talking NPCs), mesh not readable, `updateWhenOffscreen` and skinned motion vectors off, culling `CullCompletely`, no root motion, no controller (the entity prefab assigns it).
- **Mesh LODs:** `MeshLodUtility.GenerateMeshLods` to LOD2 (~35% of the triangles), renderer `meshLodSelectionBias` 0 (screen size picks the level; bias 2 kept close-up goblins on LOD2: blocky faces, segmented arms). Deeper levels tear (spikes, a lost head) because the generator ignores skin weights. Check a level with `CharacterKit.Snapshot(..., meshLod)`.
- **Per-character color map only** (32×32, point filtered), like the tool. Metallic / smoothness / emission stay the shared maps, so per-character metallic or emission edits from the tool are not carried over.
- **`optimizeHierarchy` stays `false`.** With `true` the renderer keeps its bindposes but no bones, so nothing renders until an Animator updates. Editor previews and thumbnails then render nothing and log "Bones do not match bindpose", and in-game use is unverified. Try it only as a measured crowd optimization, with a Play-mode check.

## 3. Sidekick traps

- `RandomStateBehaviourAutoRefresh` (in `SidekickCharacters.dll`) loads every AnimatorController in the project and calls `SaveAssets` after each domain reload and Play press, which took ~12 s here. `_Core/_Common/Utils/SidekickAutoRefreshBlocker.cs` unhooks it. If a controller ever uses `RandomStateBehaviour`, press its inspector "Refresh Destinations" button after editing transitions.

- The tool's prefab button is now **"Export Character as FBX"**: modal save dialog, FBX export, modal validation dialog, exceptions swallowed into a warning. It can't be scripted; use SidekickKit.
- The tool's `.sk` writer **drops zero values**: a saved BodyType or Muscle of 0 reloads as **50** (`SerializedBlendShapeValues` defaults). `SidekickKit.WriteSk` writes them (`EmitDefaults`). The `.sk` `SourceColorPath` is stale (points at the previous export); it's only read when `ColorRows` is empty.
- The DB connection is **static and shared** with the tool window. Close it only if you opened it and the window isn't open (SidekickKit does this); a handle left open locks the `.db` on Windows.
- The window deletes any scene object named **"Combined Character"**: never use that name.
- **Build size:** `SidekickCharacters/Resources/` (~180 MB of part FBX and textures) ships in every player build because it's a Resources folder, plus the 2.5 MB DB that `DatabaseBuildSync` copies into `Resources/Database/` before each build. Our characters only need their own baked assets. Raise this with the user before a release build. Excluding it would mean a build step that moves the folder out, and that's a decision for them.
- **Network:** `ToolDownloader` does an HTTP GET to the GitHub releases API on every domain reload. Its "Download and Install" re-imports the tool and DB over the vendor folder: never trigger it. Packs newer than DB 1.0.2 may need that DB update: ask the user.
- `FacialController`, `SidekickFacialController` and `CharacterBlendshapeEditor` are compiled **editor-only** (`#if UNITY_EDITOR` around the whole file): on a prefab they become missing scripts in builds. Keep them off our prefabs.
- **Importing a new Sidekick pack** adds no DB rows; the tool finds parts by FBX file name. Its `Characters/<Pack>` folder gets flagged by `.claude/hooks/check-undeclared-packs.sh` until ASSETS.md names it: suggest a tag and ask.

## 4. Synty animation packs

- **Format must match the rig.** `A_MOD_BL_*` / `A_MOD_GBL_*` = Sidekick (`Animations/Sidekick/…`); `A_*` (Base) and `A_POLY_GBL_*` (Goblin) = Polygon (`Animations/Polygon/…`). All are Humanoid FBX copying the pack's `Meshes/SidekickSyntyCharacter.fbx` / `PolygonSyntyCharacter.fbx` avatar, so they retarget, but they're authored for those proportions.
- **Sets:** Base has Masculine (`_Masc`) and Feminine (`_Femn`) sets plus 4 neutral additives (Lean, BodyLook, HeadLook, TPose). Goblin is neutral only (`_Neut`): idles incl. fidgets Menacing / Scratching / Swipe, a run stumble, Lean L/R, Head/BodyLook L/R/U/D, Idle_Rest additive.
- **Authored gait speeds** (match `EnemyDefinition.MoveSpeed` and blend thresholds to these, or feet slide): Base walk 1.4, run 2.5, sprint 6 m/s; Goblin walk 1.4, run 3.5, sprint 7 m/s. `CharacterKit.ClipInfo` reports `averageSpeed` 0 for these clips because their root motion is baked into the pose.
- **Root motion: don't trust `_RM_` names.** Most `_RM_` clips still bake everything into the pose (84 of 86 Goblin Sidekick ones; every Base Run / Shuffle / Sprint `_RM_` bakes XZ). Movement here comes from the A\* `FollowerEntity`: use the non-RM clips and `applyRootMotion = false`.
- **Additive clips are two-frame poses:** frame 1 is the reference, frame 2 the look or lean. There's no separate reference pose. A blend tree of them needs a zero-delta centre clip. `CharacterKit.BuildLocomotionController` generates one; the vendor used a TPose clip that the Goblin pack doesn't ship.
- **Import settings:** compression **Off**, resampling on, loop set per clip (cycles loop; transitions, jumps and fidgets don't). We can't change vendor import settings. If clip memory shows up in a profile, the fix is first-party copies.
- **Vendor controllers** (`AC_Sidekick_Masculine/Feminine`, Goblin `AC_Sidekick`, `AC_Polygon*`) are driven by 27 parameters from `SamplePlayerAnimationController` (gait ints, strafe / shuffle directions, incline, camera offset). Use them as reference for blend-tree layout only.
- **Sample scripts and prefabs are banned in `_Game`:** `PF_*Player`, `SamplePlayerAnimationController`, `InputReader`, the pack's own `Controls.inputactions`, `SampleCameraController`. They read their own Input System actions (we read input only through `InputService`), move a `CharacterController` from `Update`, and compile into Assembly-CSharp (no asmdef, so `Aipocalypse.Core` can't reference them anyway). Sample materials use the Built-in Standard shader (pink in URP).
- Synty's tutorial videos ("Getting Animated", "Base Locomotion – Character setup guide") only show swapping a character into `PF_Player`. Two points carry over:
  - Keep **one Animator per character**, on the model root, with the character's own avatar. Drop any nested Animator.
  - Keep characters off the **Default** layer, which ground checks use. Our enemies are on `Enemy`.

## 5. License

The Synty EULA summary in these packs' guides forbids using the assets **"with Generative AI Programs"**. Don't hand Synty models, textures or renders to image generation (the Codex image hand-off) as input or reference; ask the user when a task would.
