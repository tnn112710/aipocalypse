---
name: tools-unity-animation-rigging
description: "Unity Animation Rigging 6.6.0 (com.unity.animation.rigging) and Control Rig Pro (Ash Assets) in aipocalypse. Rig structure built from code (RigBuilder, Rig, TwoBoneIK, MultiAim, MultiParent, Damped Transform, Twist Correction, Chain IK), evaluation order, automatic vs manual evaluation (the GunAnimation pattern, a central tick placing targets), baking a rig into a plain clip from the CLI with scripts/RigKit.cs (Generic rigs only; Humanoid is unsupported), reading a rig with RigKit.Describe, Control Rig Pro's builders, jiggle constraints and FootGrounder with their traps, crowd rules, and first-person forearms on the floating hands. Use for any IK, aim, look-at, hand-on-weapon, foot grounding, secondary motion (hair, tails, straps), arm or forearm, or rig-driven clip authoring work, together with aipocalypse-characters for character models and oss-unity:animation-authoring for the generic clip and Animator API."
---

# Animation Rigging (aipocalypse)

Installed: **Animation Rigging 6.6.0** (`UnityEngine.Animations.Rigging`, editor `UnityEditor.Animations.Rigging`; source and docs in `Library/PackageCache/com.unity.animation.rigging@*/`, `Documentation~/`, `Runtime/DocCodeExamples/`). `Aipocalypse.Core` already references it. **Control Rig Pro** (`Assets/Plugins/Ash Assets/Control Rig Pro`, PLUGINS.md: Could) is an editor builder on top of it, plus a few runtime components. Vendor code: never edit it.

Read first, as needed:
- [HANDS.md → Weapon animation authoring](../../../docs/technicals/HANDS.md#weapon-animation-authoring): the rigs already in the game (each gun's `AnimationRoot`: RigBuilder plus Multi-Parent hand targets keyed by clips).
- `aipocalypse-characters`: model vs entity prefab, the animator contract, crowd budget, the IK Helper Tool rule.
- `oss-unity:animation-authoring`: generic clip, controller and Animator API.
- PLUGINS.md rows: Control Rig Pro, Kevin Iglesias IK Helper Tool.

## 1. Pick the tool

| Need | Use |
| --- | --- |
| IK, aim, or parent onto something moved by code, on a unique object (gun hands, forearms, a companion's head) | An Animation Rigging rig, **evaluated manually** right after the code places its targets (§3) |
| Fix or author a clip with IK controls on a **Generic** rig | Rig + keyed targets, then `RigKit.Bake`; ship the plain clip with no rig at runtime (§4) |
| The same on a **Humanoid** clip | No bake: the package's transfer doesn't support Humanoid and `RigKit.Bake` refuses it. At runtime use `OnAnimatorIK` (crowds: a first-party component with no `Update`, see `aipocalypse-characters`) or the IK Helper Tool (unique characters only) |
| Anything on **crowd enemies** | Never a RigBuilder: each one is a PlayableGraph plus an `Update`. Bake (Generic) or `OnAnimatorIK` |
| Secondary motion (hair, ears, tails, straps) on a unique character | `DampedTransform` (package) or Control Rig Pro's `JiggleBoneConstraint` / `JiggleChainConstraint` (job constraints in the same rig, no own `Update`) |
| Foot grounding on a unique humanoid | Control Rig Pro `FootGrounder` (`OnAnimatorIK` + `LateUpdate`, raycasts every frame, needs IK Pass on the layer, no pelvis drop), or a first-party version |
| Quick constraint hierarchy on a model | Code (§2), or Control Rig Pro's menu items (§5) |

## 2. Building a rig from code

Structure (verified):

```text
Character (Animator, RigBuilder: layers = [Rig])   Animator needs no avatar or controller
  bones...                                          constrained bones must be under the Animator
  Rig (Rig: weight)
    ArmIK (TwoBoneIKConstraint)                     one GameObject per constraint
      Target, Hint                                  sources may also live anywhere else in the scene
```

```csharp
var ik = ikGo.AddComponent<TwoBoneIKConstraint>();
ik.data.root = upper; ik.data.mid = lower; ik.data.tip = hand;
ik.data.target = target; ik.data.hint = hint;
ik.data.targetPositionWeight = 1f; ik.data.targetRotationWeight = 1f; ik.data.hintWeight = 1f;
character.AddComponent<RigBuilder>().layers.Add(new RigLayer(rig, true));
```

- **Order:** RigBuilder layers run in list order; constraints in a Rig run depth-first in hierarchy order. A later constraint sees the earlier ones' result.
- **Bindings are captured at `Build`:** changing which transforms a constraint uses (`data.root`, `data.target`, a source list) needs `Build()` again. Moving a target, and weights, are read each `SyncLayers` (verified with a target outside the hierarchy).
- **Maintain offset** flags are captured at `Build` from the current pose.
- **Animatable:** Rig weight, constraint weight and source weights (`WeightedTransformArray`). The guns key Multi-Parent source weights to switch the left hand between contacts.
- **Prefabs:** edit with `PrefabUtility.LoadPrefabContents` → change → `SaveAsPrefabAsset` → `UnloadPrefabContents`. Never `HideFlags.DontSave` a root you save: `SaveAsPrefabAsset` then logs "No objects were found for saving" and writes nothing.
- `RigKit.Describe(prefabPath)` prints every rig, constraint, weight and data field, to check wiring without guessing.

## 3. Evaluation: automatic vs manual

**Automatic:** an enabled RigBuilder builds its own graph in `OnEnable` (play mode only) and runs in the Animator pass, after `Update` and before `LateUpdate`. Targets moved later (in `LateUpdate`, or in a central tick that runs after the Animator) are applied **one frame late**. That shows as lag on anything tied to the camera.

**Manual:** use this whenever code places the targets. Two verified forms:
- Rig only (package example `CustomRigBuilderEvaluator`): `builder.enabled = false; builder.Build(); builder.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);` then call `builder.Evaluate(dt)` in your tick, after the targets are placed.
- Rig plus your own animation (`GunAnimation`): create a Manual graph, add your controller or clip playable and an `AnimationPlayableOutput` to the Animator, call `builder.Build(graph)`, then each tick: set the time, `builder.SyncLayers()`, `graph.Evaluate(0f)`. Detach `animator.runtimeAnimatorController` while you own the graph, or the Animator's own pass overwrites the pose.
- `Build(graph)` adds **one extra output per rig chain**. Evaluate the whole graph: `AnimationMode.SamplePlayableGraph(graph, 0, t)` samples only output 0 and silently skips the rig.
- Clean up: `Clear()` on the builder, `Destroy()` your graph (on disable or pool return).
- Player frame order: `HeldItemDriver.Tick` → `held.LateTick()` (gun clip + constraints) → `_hands.Tick` (hands snap to grips). Anything that follows the hands (forearms) evaluates after that, in the same tick.

## 4. Baking a rig into a clip (`scripts/RigKit.cs`)

```bash
unity command script run --file .agents/skills/tools-unity-animation-rigging/scripts/RigKit.cs \
  --entry RigKit.Bake --args '["<prefab>","<sourceClip.anim>","Assets/_Game/Content/<Kind>/<Name>/Animations/<Clip>_Baked.anim",30,false]' \
  --project-path "$(cygpath -w "$PWD")"
```

- Plays the source clip on an isolated copy of the prefab with its rigs, records every bone (all transforms under the Animator outside a Rig), and keeps the loop settings and events. Overwriting keeps the GUID, so controllers stay wired. Nothing in the open scene changes.
- Verified: an IK-driven clip baked this way reproduces the IK pose to within 1 mm with the rig at weight 0.
- Workflow: copy the vendor clip to `Content/.../Animations/` (never key a vendor FBX; an FBX sub-clip is copied with `AssetDatabase.CreateAsset(Object.Instantiate(clip), path)`) → key the IK targets and weights in the copy (`animation.clip set_curve`, or the Animation window) → `Bake` → point the controller or spec at the baked clip → remove the rig from the shipped prefab.
- **Don't drive the package's own "Transfer motion to skeleton" from the CLI.** `BakeUtils` needs the Animation window open, bound to the selection, in preview with an active clip. The window only binds on a later editor update, and stays unbound while Unity is unfocused, so script runs fail with "There is no clip to work on" or "AnimationMode.BeginSampling may only be called in animation mode". A read-only clip opens a Save dialog that stalls the CLI. If a script ever starts `AnimationMode`, stop it in a `finally`.
- Humanoid: not supported. A muscle bake through `HumanPoseHandler` would be first-party work: propose it, it isn't built.

## 5. Control Rig Pro: what helps, what bites

Its scripts have no asmdef, so they compile into `Assembly-CSharp-firstpass` / `-Editor-firstpass`. `_Game` asmdef code can't reference its types; prefabs can still carry its components. Find them by type name in tests.

- **Quick constraint menu** (verified for 2-Bone IK): set `Selection.activeTransform` to the tip bone, then `EditorApplication.ExecuteMenuItem("GameObject/Control Rig Pro/Add 2-Bone IK")`. It adds the RigBuilder, `<Animator name>_Rig` (Rig), `Left/Right Hand Rig` groups, and the constraint with `Target` / `Hint`, and fills root, mid and tip from the parents. Other items: Multi-Parent, Multi-Aim, Twist Chain, Chain IK, Damped, Override, Multi-Rotation/Position, Jiggle Bone/Chain, finger controls. Traps:
  - Side and limb are guessed from bone names ("left", "l_", "hand", "foot").
  - 2-Bone IK sets `maintainTargetRotationOffset = true`.
  - Multi-Aim hard-codes the aim axis Z and up axis Y: check them against the bone.
  - There's no Undo: run it on prefab contents or a copy.
- **Humanoid Rig Builder: don't use it on game prefabs.**
  - It assigns its `Resources/Test Anim Controller` when the Animator has none.
  - It **clears all RigBuilder layers**.
  - It instantiates its `Resources/Humanoid Rig` prefab with the vendor `HumanoidRig_References` component.
  - It pins arms and legs at weight 1, and places the head target at world +Z.

  Copy only its idea: arm and leg 2-Bone IK, hip Multi-Parent, spine Twist Chain, head Multi-Aim.
- **Build break:** `Scripts/HumanoidRig_References.cs` has `using UnityEditor;` in a runtime folder, so player builds fail while it's there. The fix is a vendor patch: ask the user, then note it in PLUGINS.md like the `RandomTargetMover` one.
- Its `Resources/` folder (`Humanoid Rig.prefab`, `Test Anim Controller`) is included in every build.
- Demo scene, `DemoScripts`, the Simple Human model and the animation library stay out of `_Game`.

## 6. First-person forearms (the floating hands)

The hands are placed by `FirstPersonHands` from item grips (HANDS.md). Forearms **follow the hands**, so gun clips never change. Decide the look first:

| Look | Rig |
| --- | --- |
| Floating forearm (elbow to wrist, no upper arm) | No IK. The forearm root sits at the elbow anchor (`HandSet` elbows, view space) and aims at the hand's wrist; add `TwistCorrection` for twist bones. Plain transform math is enough; a rig helps only for twist distribution |
| Full arm (shoulder to elbow to wrist) | One `TwoBoneIKConstraint` per arm: root = upper arm, mid = forearm, tip = wrist, target = a wrist socket on the hand (outside the arm hierarchy is fine), hint = the `HandSet` elbow point |

- Evaluate manually (§3, rig-only form), called from the hands' tick after each hand is placed. Never in `LateUpdate`, and never with the RigBuilder enabled.
- The socket's rotation must match the arm's wrist bone, so `targetRotationWeight = 1` leaves no seam. A cuff or sleeve hides the joint.
- A reach beyond arm length straightens the arm and opens a gap at the wrist (shells from the belt, far reaches). Slide the shoulder anchor toward the target, or check that no clip exceeds the reach.
- Art: no forearm mesh is declared in ASSETS.md. Ask which pack (check the Undecided rows first).
- Verify:
  - an EditMode test: wrist within 1 mm of the socket after `Evaluate`, elbow on the hint side;
  - a probe stepping every `GunClip` of every gun plus the axe, logging the worst wrist bend and any shortfall;
  - `capture game_view` during reload and repair.

  Update HANDS.md ("Known gaps") and CODE_STRUCTURE.md.

## 7. Performance

- One RigBuilder = one PlayableGraph + per-frame `SyncLayers` + one job per constraint. Fine for the player's view and a few unique characters; profile anything above that (`unity-optimization-pc`).
- Crowds: never (§1). A baked clip costs nothing extra.
- `BoneRenderer` and rig effectors are editor gizmos: harmless, but leave them off shipped prefabs.
