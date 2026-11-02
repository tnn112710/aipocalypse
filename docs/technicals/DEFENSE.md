# Defense: fences and gates

Fences are made of segments, one per panel slot. Each segment has its own health and blocks the way while it stands. Gates are segments that also open and close. Enemies with no path to what they hunt break segments to get in; the player repairs them with a hammer. Code: `_Core/DefenseSystem/` (script map in [CODE_STRUCTURE.md](CODE_STRUCTURE.md)); content: `Content/Structures/`; built by `AgentScripts/Defense/DefenseSetup.cs`. Polish level: Medium ([ROADMAP.md](../ROADMAP.md)).

## How a segment works

`BarrierSegment` sits on each segment's root (local X along the fence, Z across it) with its `BarrierDefinition`:

| Part | What it does |
| --- | --- |
| Health (`BarrierHealth`) | Damage through `DamageService` only. At 0 it breaks; a broken segment takes no damage. Repairs add health while broken too, and it stands again once it has `StandAt` (30%) of its maximum back and nothing alive stands in its line |
| Navmesh cut (A\* `NavmeshCut`) | On while it blocks (standing, and on a gate: shut). The panels' own colliders carry a `RecastNavmeshModifier` set to *Always exclude*, so the baked navmesh runs through the fence and the cut stands for it. Cuts are shortened by about the agent radius at both ends; A\* widens them by it again, so neighbours overlap by 0.3 m (cuts that only touch leak) and a broken panel opens about 2 m |
| Climb blocker | A 2.4 m tall box on **Ignore Raycast**, on while it blocks. The player (jump 1.1 m) can't get over; bullets and the interact cast pass through it and hit the rails, so the player can shoot through the gaps |
| Look (`BarrierLook`) | The model, spawned from the definition (or already nested in the prefab). It leans further below 67% and 34% health, away from the side the blows come from, shakes on each hit and falls flat when broken (its colliders off). A fence look has three Lakefell panels as variants, one shown per segment (picked by position, maybe turned round) |
| Target (`IBreachable`, `TargetKind.Fence`) | Registers in the scene's `TargetRegistry` while enabled; targetable only while it blocks |
| Interaction | Tap E on a damaged segment: **Repair Fence 63%** (or **Rebuild** when broken). See Gates for gates |

**No friendly fire:** damage whose `Source` is the player (`IPlayerTarget`) is ignored, so the player's bullets and swings never dent the fence.

**Events:** `BarrierBroken` and `BarrierRestored` (position, `IsGate`). `BreachAlertHud` shows "The fence is down behind you" (the side seen from the player's view, [UI.md](UI.md)). The break sound plays at the barrier.

## Gates

`FenceGate` beside a `BarrierSegment` makes it a gate. A gate covers one panel slot (`WoodGate.prefab`) or two (`WoodGate_Double.prefab`, two leaves meeting in the middle, about 4.5 m clear so animals can pass), so it can replace any segments of a fence.

- **Closed by default** (`StartsOpen` off). E toggles it; the leaves swing away from whoever opens it (DOTween, `SwingTime` 0.6 s, `OpenAngle` 100°). Opening frees the path at once; closing blocks it once the leaves are home.
- It won't close on someone alive in the doorway: the prompt reads **Close Gate (blocked)** and E does nothing.
- A `Doorway` trigger covers the opening, so looking into an open doorway still finds the gate.
- A damaged gate offers **Hold E: Repair Gate 63%** under its open / close prompt (`IHoldInteractable`: while a hold use is offered the tap fires on release). A broken gate is repaired like a segment and stands again shut.
- Its look is a taller frame than the fence (2.7 m posts and a beam across the top) round two-rail plank leaves; its hinges are listed on the `BarrierLook`.

## How enemies break in

`TargetSelector.Choose` picks the best target as before (the player). If the enemy has no path to it (`INavmeshQuery.IsReachable`, a cheap connected-area check on the A\* graph) and its definition has a **Fence** weight, it attacks a barrier instead: of the **`MeleeSettings.BreachCandidates` (5) nearest standing barriers, the one with the least health left** (`ChooseBreach`). It re-asks every `RetargetInterval` (1 s), so it sticks to the segment it is denting, moves on when that one falls, and runs for the player as soon as a gap or an open gate gives it a path.

- Attackers per segment: `EnemySystemSettings.AttackersPerTarget` Fence 3; the rest wait at `HoldDistance`.
- Attackers stand at `PointFor`: `StandOff` (0.75 m) off the centre line on their own side, and their blows land there.
- A barrier plays its own hit sound (the goblin's flesh impact is skipped).
- Goblins: `Goblin.asset` Targets Player 1, Fence 1. A kind without a Fence weight keeps chasing the closest reachable point instead.
- Raid spawn points must have a path to the zone's `Origin`: `Raid/Spawn Zone (Front)` sets `OriginOffset` 3 m so that point lies outside the closed gate ([RAIDS.md](RAIDS.md)).

Measured in WorldMap (`DefensePlayCheck`, 6 goblins against the shut fence, player inside): all six went for the fence, the first segment fell after about 18 s and goblins were in the yard right after.

## Repair

Press E on a damaged segment (hold E on a gate). `PlayerRepair` (the player's `IRepairer`) puts the held item away, raises the first-person hammer (`RepairTool`, `Player.prefab` → `HeldItems/RepairHammer`, Animpic `SM_Hammer` held in the right hand) and knocks every `RepairKnockInterval` (0.7 s): each knock is `IRepairable.Knock`, adding `RepairPerKnock` health (wood fence 30, gate 40, double gate 50) with the repair sound and a puff of chips. It goes on until the segment is whole, then raises the held item again.

It stops after the current swing when the player looks more than `RepairLookAngle` (45°) away for `RepairLookAwayGrace` (0.4 s), walks past the interact range + `RepairReachSlack` (0.75 m), or presses interact, fire, aim, jump, a slot or drop. While repairing, the item keys do nothing else. **Free for now:** the cost hooks in at `BarrierSegment.Knock` when currency exists.

## Outline

`InteractHighlighter` moves one Highlight Plus effect (`Player.prefab` → `Interact Highlight`: warm white outline, no glow, 0.1 s fade) to whatever the interact prompt is for, or to the segment being repaired. Every interactable gets it with nothing added to it (guns, ammo crate, gates, damaged segments). Highlight Plus' render pass is on `PC_Renderer` (PLUGINS.md).

## Upgrades and new kinds

Everything a kind is lives in its `BarrierDefinition`: health, damage scale per type, repair per knock, look, effects, sounds, `OnStruck` effects and `UpgradesTo`. `BarrierSegment.Upgrade()` moves to the next tier keeping the health share and swaps the look if it differs (`fence.upgrade` does it for all).

- **Wood fence** (300) → **Spiked fence** (450, `ThornsEffect` 8 back on every melee blow; still the wood look until it has its own).
- **New behaviour on a blow:** a new `BarrierEffect` asset type (slow, knockback, fire) added to the definition's `OnStruck`.
- **New material** (better wood, rock wall): a new look prefab with a `BarrierLook` (pivots, shakes, optional variants and hinges) and a definition pointing at it. EmaceArt Fences 2026 has rock walls and a rock gate (tagged Maybe in ASSETS.md: ask first).
- **A gate anywhere:** replace one or two segments with `WoodGate.prefab` / `WoodGate_Double.prefab` (or a builder's `Put`), then re-bake the navmesh (Navigation below) and save the scene so `DefenseInstaller` lists it.

## Sounds

`Data/Audio/Defense` library, 3D (`Defense_Spatial`: 2–45 m): `Fence_Hit` (PlaceHolder Inc Wood Chop ×9, at most 4 at once), `Fence_Break` (Wood 11-1, 11-3, 16-1; picked by waveform, audition them), `Fence_Repair` (Nail Wood 1-6…1-10), `Gate_Open` (Gamemaster old wooden door squeaks), `Gate_Close` (old wooden door close thuds).

## In WorldMap

`Environment/Landmarks/Home Base/Fence`: 52 `WoodFence` segments, the double gate over slots 8–9 of the front side (centred seg/2 to +X of `HomeGate`), Lakefell posts ×1.5 between slots. `DefenseInstaller` on the `GameLifetimeScope`.

- **Rebuild the fence only:** `Landmarks.Fence(7)` in `AgentScripts/Maps/WorldMap/WorldMapLayout.cs`, then `Navigation.Bake(0.3)`, then `DefenseSetup.Scene` (lists the barriers, sets the raid zone's origin offset, saves). With the gate shut the yard is its own navmesh island (`homeAreaShare` ≈ 0.006 in the bake report); a value near 0.9 means a cut leaks.
- **Content:** `DefenseSetup.All` (or `Audio`, `Effects`, `Content`, `Player`, `Hud`, `EnemyData`). `Content` rewrites the look and segment prefabs, so run `Landmarks.Fence` again after it.
- **Checks in play:** `DefensePlayCheck.Run` (cut-off yard, gate and broken-segment paths, no friendly fire, a goblin breach) and `DefenseInteractCheck.Run` (prompts, outline, toggle, hold repair, look-away stop).
- **Known:** the back corner slot `Fence 0-14` opens no path when broken: the flowering bush outside it (`Dressing/Home Trees/Bush_3_Flowers`) closes the gap. Enemies that break it just pick another segment. A yard table stands about 2 m inside the gate.

## Console

`fence.status`, `fence.damage <amount> [count]` (nearest the player, as world damage), `fence.breach [count]`, `fence.repair`, `fence.upgrade`, `fence.gate <true|false>` ([CONSOLE.md](CONSOLE.md)).

## Tuning

| To change | Edit |
| --- | --- |
| A kind's health, repair speed, stand-up share, sounds, effects, next tier | `Content/Structures/<Kind>/<Kind>.asset` (`BarrierDefinition`) |
| How it leans, shakes and falls | its look prefab's `BarrierLook` |
| Gate swing angle, time, starting open | `FenceGate` on the gate prefab |
| How many segments an enemy compares | the kind's `MeleeSettings.BreachCandidates` |
| Attackers per segment | `Data/Enemies/EnemySystemSettings.asset` → Attackers Per Target → Fence |
| Repair rhythm, when it stops | `Data/Player/PlayerInteractionTuning.asset` → Repair |
| Hammer poses | `Player.prefab` → `HeldItems/RepairHammer/Poses` (Preview buttons on `RepairTool`) |
| Outline look | `Player.prefab` → `Interact Highlight` (`HighlightEffect`) |
| Breach line words and timing | `Data/UI/BreachAlertStyle.asset` |
