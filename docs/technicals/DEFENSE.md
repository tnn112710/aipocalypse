# Defense: fences and gates

Fences are made of segments, one per panel slot. Each segment has its own health and blocks the way while it stands. Gates are segments that also open and close. Enemies with no path to what they hunt break segments to get in; the player repairs them with the hammer, a hotbar item. Code: `_Core/DefenseSystem/` (script map in [CODE_STRUCTURE.md](CODE_STRUCTURE.md)); content: `Content/Structures/`; built by `AgentScripts/Defense/DefenseSetup.cs`. Polish level: Medium ([ROADMAP.md](../ROADMAP.md)).

## How a segment works

`BarrierSegment` sits on each segment's root (local X along the fence, Z across it) with its `BarrierDefinition`:

| Part | What it does |
| --- | --- |
| Health (`BarrierHealth`) | Damage through `DamageService` only. At 0 it breaks; a broken segment takes no damage. Repairs add health while broken too, and it stands again once it has `StandAt` (30%) of its maximum back and nothing alive stands in its line |
| Start state | `BarrierSegment > Start Share`: the share of health it starts a new game with (`BarrierHealth.StartAt`); 0 starts it down to debris, to be rebuilt with the hammer. A loaded save overrides it. WorldMap's start: [In WorldMap](#in-worldmap) |
| Navmesh cut (A\* `NavmeshCut`) | On while it blocks (standing, and on a gate: shut). The panels' own colliders carry a `RecastNavmeshModifier` set to *Always exclude*, so the baked navmesh runs through the fence and the cut stands for it. Cuts are shortened by about the agent radius at both ends; A\* widens them by it again, so neighbours overlap by 0.3 m (cuts that only touch leak) and a broken panel opens about 2 m |
| Climb blocker | A 2.4 m tall box on **Ignore Raycast**, on while it blocks. The player (jump 1.1 m) can't get over; bullets and the interact cast pass through it and hit the planks, so the player can shoot through the gaps. The gate leaves use a mesh collider on their planks (the vendor leaf's solid box is off), so their gaps let shots through too (`DefensePlayCheck`: about 30% of rays across the shut double gate pass) |
| Look (`BarrierLook`) | The model, spawned from the definition (or already nested in the prefab). It leans further below 67% and 34% health, away from the side the blows come from, shakes on each hit and falls flat when broken (its colliders off). A fence look has three Lakefell panels as variants, one shown per segment (picked by position, maybe turned round). With `Debris` set (the wood fence), once fallen the panel is hidden and a small hint of broken planks shows instead (first-party boxes in `FenceDebris.mat`, no colliders, turned round per slot); it goes when the segment stands again |
| Posts (`FencePost`) | The Lakefell posts between slots are scene objects, each linked to the one or two segments beside it: a post shows while either of them stands, so a run that is all debris has no lone posts. Hidden posts take their colliders with them; the baked navmesh doesn't change (the cuts decide the way) |
| Target (`IBreachable`, `TargetKind.Fence`) | Registers in the scene's `TargetRegistry` while enabled; targetable only while it blocks |
| Ghost | While broken, looking at it (or rebuilding it) shows a see-through green ghost of it standing where it fell (`IGhostSource`, `BarrierLook.CaptureStanding`, drawn by `Player` with `PlayerInteractionTuning.Ghost`); red while the next knock would be blocked by someone in its line |
| Interaction | With the hammer in hand: **R** on a damaged segment, **Repair Fence 63%** (or **Rebuild** when broken; [Repair](#repair)), and **E** on any segment, **Repair All Fences (5)** while any standing segment is damaged. Empty-handed or holding something else, a plain segment offers nothing. A `Gap` trigger over the slot lets the interact cast find a broken segment once its panel's colliders are off (bullets and bodies ignore triggers). See Gates for gates |
| Highlight | With the hammer in hand, every damaged segment within 45 m gets a see-through tint over its model, through walls, yellow → orange → red as its health drops (a broken one is the red outline of its fallen panel). It fades up close so the segment being mended isn't covered (`RepairHighlight`, `RepairHighlightStyle`) |

**No friendly fire:** damage whose `Source` is the player (`IPlayerTarget`) is ignored, so the player's bullets and swings never dent the fence.

**Events:** `BarrierBroken` and `BarrierRestored` (position, `IsGate`). `BreachAlertHud` shows "The fence is down behind you" (the side seen from the player's view, [UI.md](UI.md)). The break sound plays at the barrier.

## Gates

`FenceGate` beside a `BarrierSegment` makes it a gate. A gate covers one panel slot (`WoodGate.prefab`) or two (`WoodGate_Double.prefab`, two leaves meeting in the middle, about 4.5 m clear so animals can pass), so it can replace any segments of a fence.

- **Closed by default** (`StartsOpen` off). E toggles it; the leaves swing away from whoever opens it (DOTween, `SwingTime` 0.6 s, `OpenAngle` 100°). Opening frees the path at once; closing blocks it once the leaves are home.
- It won't close on someone alive in the doorway: the prompt reads **Close Gate (blocked)** and E does nothing.
- A `Doorway` trigger covers the opening, so looking into an open doorway still finds the gate.
- With the hammer in hand, a damaged gate offers **R: Repair Gate 63%**, and a standing gate **Hold E: Repair All Fences** under its open / close prompt (`IHoldInteractable`: while a hold use is offered the tap fires on release), since a tap opens it. A broken gate is repaired like a segment and stands again shut.
- Its look is a taller frame than the fence (2.7 m posts and a beam across the top) round two-rail plank leaves; its hinges are listed on the `BarrierLook`.

## How enemies break in

`TargetSelector.Choose` picks the best target as before (the player). Then, for a kind with a **Fence** weight:

- **Fence shut** (no path to the player: `INavmeshQuery.IsReachable`, a cheap connected-area check on the A\* graph): it attacks a barrier: of the **`MeleeSettings.BreachCandidates` (5) nearest standing barriers, the one with the least health left** (`ChooseBreach`).
- **Ways in** are runs of side-by-side barriers that don't block (broken, or a gate open): two broken neighbours make one opening 4.5 m wide. An opening counts for an enemy only if it's at least as wide as its body (twice its `FollowerEntity` radius: goblins 1 m, so one broken panel, about 2.25 m, lets them through).
- **Only ways in too narrow for it** (a large enemy at a single broken panel): it widens the nearest one, attacking the weaker standing barrier beside it, then goes through once it's wide enough. It also counts a too-narrow opening as a wall, so it doesn't try to squeeze through one straight ahead.
- **A way in it fits through within `GapNear` (8 m)**: it always goes through. None of the rules below applies to it.
- **A way in it fits through, further off**: most go for the player through it, walking round to it if needed. Some keep breaking instead, so the player has more than one gap to hold:
  - **Gap on the far side:** when a standing segment is between the goblin and the player and the walk through the nearest open way in is at least **`GapDetour` (15 m)** longer than going straight, it breaks its own way in (a 34 × 27 m yard: a gap on the opposite side).
  - **A breaker share:** of the goblins walled off beyond `GapNear`, at the fence or still on their way, the ones for whom the open way in is furthest keep breaking: **`BreakerShare` (a third)** of all the goblins hunting, at least **`MinBreakers` (1)**, never every one (a lone goblin takes the gap). Six goblins spread along the fence: two keep breaking, four go in; six crowded round the first gap all go in; of a wave arriving later, a third make their own gap. A breaker that stops being among the furthest goes for the gap on its next choice, unless its segment is about to fall.
  - **New gaps keep apart:** a breaker skips segments within **`GapSpacing` (6 m)** of an open way in, so it makes a second gap instead of widening the first.
  - **Almost broken:** a goblin whose segment would fall to **`FinishBreachBlows` (2) of its own blows** or fewer (30 HP for goblins) finishes it first.

  Detours are straight-line estimates (`TargetSelector.Detour`: to the middle of the way in, then to the player), and "a standing segment is between" is a line test against each standing barrier's `Span`, so this costs no path queries. The openings are rebuilt only when a barrier starts or stops blocking (barrier ends closer than 0.25 m are joined; WorldMap's ring of 53 meets exactly). The breaker ranking uses where each goblin stood at its last choice (`TargetSelector` keeps one record per hunter; `Forget` on death and disable).

It re-asks every `RetargetInterval` (1 s) and **at once whenever a barrier starts or stops blocking**: the segment applies its navmesh cut immediately (`ForceUpdate` + `FlushWorkItems`, a few ms, only on a break, a stand-up or a gate swing) and bumps `TargetRegistry.BarrierVersion`, which makes every hunter choose again. Without that, the goblins at the fence would see the old navmesh for a frame and pick the next segment instead of the new gap.

- Attackers per segment: `EnemySystemSettings.AttackersPerTarget` Fence 3; the rest wait at `HoldDistance`.
- Attackers stand at `PointFor`: `StandOff` (0.75 m) off the centre line on their own side, and their blows land there.
- A barrier plays its own hit sound (the goblin's flesh impact is skipped).
- Goblins: `Goblin.asset` Targets Player 1, Fence 1. A kind without a Fence weight keeps chasing the closest reachable point instead.
- A raider that hasn't noticed the player ([ARCHITECTURE.md → AI](ARCHITECTURE.md#ai) → Awareness) goes for the **Farm**, the middle of the fence ring (`TargetRegistry.Farm`), and breaks in toward it by the same rules; once inside it roams the yard until it notices the player.
- Raid spawn points must have a path to the zone's `Origin`: `Raid/Spawn Zone (Front)` sets `OriginOffset` 3 m so that point lies outside the closed gate ([RAIDS.md](RAIDS.md)).

Measured in WorldMap (`DefensePlayCheck`, 6 goblins, player inside): against the shut fence all six went for it and the first segment fell after about 19 s; 2 s later two were still breaking a segment 6.8 m from the gap and four went in; that second gap fell about 5 s later. With the back fence already broken, goblins arriving at the front broke a new gap there (27 m from the back gap) after 18 s and none walked round.

Since `GapNear` (2026-11-11, scratch probe: 6 goblins against the shut front fence): when the first segment fell, all six went through it within 6 s; before it, two of them, standing 2–3 m from the gap, walked off to break a segment 6.8 m away. Two goblins made 2.6 m wide (their `FollowerEntity` radius raised in play, as a stand-in for a large kind) at a single broken panel attacked the segment beside it, and went in once it fell. `DefensePlayCheck`'s goblin steps spawn in front of the player, so they only test the breach when the player faces out of the yard.

Since spotting (2026-11-11, scratch probe, the player 22 m from the gate inside): four raiders spawned 10 m outside the gate came for the fence unaware, three with slots on one segment, the fourth waiting; it fell after 18 s and all four walked to the farm and roamed the yard, none having noticed the player.

## Repair

The hammer is a hotbar item (`Content/Tools/Hammer`, a `RepairToolDefinition`, category `Tool`), picked up like a weapon. A copy lies on the yard table with the guns. Its held prefab is a `RepairTool`: Animpic `SM_Hammer` in the right hand, knocking between pose markers.

**One segment (R):** with the hammer up and a damaged segment or gate in view, the HUD shows **[R] Repair Fence 63%** above the E prompt. R starts `PlayerRepair` (the player's `IRepairer`). It knocks every `RepairKnockInterval` (0.7 s): each knock is `IRepairable.Knock`, adding `RepairPerKnock` health (wood fence 30, gate 40, double gate 50) with the repair sound and a puff of chips. It goes on until the segment is whole.

It stops after the current swing when the player looks more than `RepairLookAngle` (45°) away for `RepairLookAwayGrace` (0.4 s), walks past the interact range + `RepairReachSlack` (0.75 m), or presses R, E, fire, aim or jump. Switching slot or dropping the hammer stops it at once. While it knocks, R, E, fire and aim do nothing else.

**Repair All (E):** on any segment (hold E on a standing gate) it mends every damaged **standing** barrier in the scene at once (`BarrierRegistry.RepairAll` → `BarrierSegment.RepairFully`): a puff of chips on each, one repair sound at the segment used. Broken ones are rebuilt one at a time with R (since 2026-11-22, so a fence that starts half down can't come back with one key; the `fence.repair` cheat still restores everything). It is instant for now; it is meant to cost time later.

**Free for now:** a cost hooks in at `BarrierSegment.Knock` and `RepairFully` when currency exists.

## Upgrades and new kinds

Everything a kind is lives in its `BarrierDefinition`: health, damage scale per type, repair per knock, look, effects, sounds, `OnStruck` effects and `UpgradesTo`. `BarrierSegment.Upgrade()` moves to the next tier keeping the health share and swaps the look if it differs (`fence.upgrade` does it for all).

- **Wood fence** (300) → **Spiked fence** (450, `ThornsEffect` 8 back on every melee blow; still the wood look until it has its own).
- **New behaviour on a blow:** a new `BarrierEffect` asset type (slow, knockback, fire) added to the definition's `OnStruck`.
- **New material** (better wood, rock wall): a new look prefab with a `BarrierLook` (pivots, shakes, optional variants and hinges) and a definition pointing at it. EmaceArt Fences 2026 has rock walls and a rock gate (tagged Maybe in ASSETS.md: ask first).
- **A gate anywhere:** replace one or two segments with `WoodGate.prefab` / `WoodGate_Double.prefab` (or a builder's `Put`), then re-bake the navmesh (Navigation below) and save the scene so `DefenseInstaller` lists it.

## Effects

`Content/Structures/_Shared/Effects`, pooled through `EffectPool`:

- **`WoodHit`:** a few chips and a puff where a blow lands.
- **`WoodBreak`:** 14 tumbling planks (lit cube mesh particles, `WoodPlank.mat`) that bounce on the ground and lie in the gap for about 3 s, 45 splinters and a dust cloud.
  - It flies away from the attacker, into the yard when goblins break in.
  - It emits from a 1 m wide box that `BarrierSegment.Break` stretches to the barrier's width (Shape scaling), so the double gate bursts across both leaves.
  - Planks use High collision quality: they raycast the `Default` layer (the terrain). Medium's voxel cache let them fall through.

## Sounds

`Data/Audio/Defense` library, 3D (`Defense_Spatial`: 2–45 m): `Fence_Hit` (PlaceHolder Inc Wood Chop ×9, at most 4 at once), `Fence_Break` (Wood 11-1, 11-3, 16-1; picked by waveform, audition them), `Fence_Repair` (Nail Wood 1-6…1-10), `Gate_Open` (Gamemaster old wooden door squeaks), `Gate_Close` (old wooden door close thuds).

## In WorldMap

`Environment/Landmarks/Home Base/Fence`: 52 `WoodFence` segments, the double gate over slots 8–9 of the front side (centred seg/2 to +X of `HomeGate`), Lakefell posts ×1.5 between slots (`FencePost`). `DefenseInstaller` on the `GameLifetimeScope`.

**Start of a new game (user, 2026-11-22):** the front half is down to debris: all of the front side (`Fence 2-*`, 13 segments) and the halves of the side walls that meet it (`Fence 1-06…11`, `Fence 3-00…05`), 25 of 52. The gate and the back half stand; 23 posts are hidden. Rebuilding a debris segment takes 3 knocks to stand (90 of 300) and brings its posts back.

- **Start state and posts only:** `Landmarks.FenceStart` (Start Share from `FenceStartShare`) and `Landmarks.FencePosts` (links posts and segments) on the fence already in the scene; save the scene after. `Landmarks.Fence` does both when it rebuilds. The debris on the look prefab: `DefenseSetup.FenceDebris` (`Content` builds it too).
- **Rebuild the fence only:** `Landmarks.Fence(7)` in `AgentScripts/Maps/WorldMap/WorldMapLayout.cs`, then `Navigation.Bake(0.3)`, then `DefenseSetup.Scene` (lists the barriers, sets the raid zone's origin offset, saves). With the gate shut the yard is its own navmesh island (`homeAreaShare` ≈ 0.006 in the bake report); a value near 0.9 means a cut leaks.
- **Content:** `DefenseSetup.All` (or `Audio`, `Effects`, `Content`, `Player`, `Hud`, `EnemyData`). `Content` rewrites the look and segment prefabs, so run `Landmarks.Fence` again after it. `FenceGap` only adds the `Gap` trigger to an existing `WoodFence.prefab`. The hammer item, the 4th hotbar slot, the HUD's R line, the highlight look and the table hammer: `AgentScripts/Defense/HammerSetup.cs` (`All`, or `Item`, `Player`, `Config`, `Hud`, `Highlight`, `Scene`).
- **Checks in play:** `DefensePlayCheck.Run` (cut-off yard, gate and broken-segment paths, no friendly fire, shots through the shut gate, a goblin breach and what they do once a gap opens) and `DefenseInteractCheck.Run` (empty-handed prompts and toggle, picking the hammer up, R on a gate and a segment, look-away stop, rebuilding a broken segment, E repairing all).
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
| When an enemy finishes a segment instead of taking the gap | the kind's `MeleeSettings.FinishBreachBlows` |
| How far out of the way a gap may be before an enemy breaks its own; the share that keeps breaking; spacing between gaps; how close a gap is always taken | the kind's `MeleeSettings.GapDetour`, `BreakerShare`, `MinBreakers`, `GapSpacing`, `GapNear` |
| How wide a gap an enemy needs | its prefab's `FollowerEntity` radius (body width = 2 × radius; a bigger radius also needs its own navmesh graph, ENVIRONMENT.md → Navigation) |
| Attackers per segment | `Data/Enemies/EnemySystemSettings.asset` → Attackers Per Target → Fence |
| When an enemy notices the player, alarms others, forgets them | the kind's `EnemyDefinition` → Senses (`SpotRange`, `AlertRadius`, `LoseSightTime`, `SightBlockers`) |
| Where an unaware raider counts as arrived at the farm; how long a waiting enemy keeps its spot | the kind's `MeleeSettings.ObjectiveRadius`, `HoldShuffleTime` |
| Repair rhythm, when it stops | `Data/Player/PlayerInteractionTuning.asset` → Repair |
| Hammer poses | `Content/Tools/Hammer/Hammer.prefab` → `Poses` (Preview buttons on `RepairTool`); how it lies on the table: its `World/Model` |
| Damage highlight: colours by health, range, fade up close | `Data/Defense/RepairHighlightStyle.asset` (material `RepairHighlight.mat`, shader `Aipocalypse/Ghosts/GhostXray`); off when `DefenseInstaller` has no style |
| Breach line words and timing | `Data/UI/BreachAlertStyle.asset` |
| The broken-segment ghost's colours | `Data/Ghosts/Ghost_Valid.mat`, `Ghost_Invalid.mat` (or `AgentScripts/Ghosts/GhostSetup.cs`, which rewrites them) |
