# Service robots pass 2: reactions, roles, door, call prompt

Asked 2026-11-22 after pass 1: "do 1,2,3,4,5,6,7" (the animation ideas), "prepare for storage, shop, upgrade and worker
robots (collecting coins, guarding), no need to implement them", "check, fix bugs, refactor", "whistling at the green robot in
the house doesn't bring it; show Q Call when aiming at a robot in range". Spec of pass 1:
`docs/superpowers/specs/2026-11-22-service-robots-design.md`. Executed inline; rulings in
`.superpowers/sdd/2026-11-22-service-robots-pass2/progress.md`.

## What gets built

| # | Piece | Rule |
| --- | --- | --- |
| 1 | Knockdown | 3 hits within 2.5 s: falls (back when hit from the front), crossed eyes while down, gets up, sulks 10 s (angry mood, a look gets a head shake, not a wave). A pat ends the sulk. |
| 2 | Pat | Hold E (IHoldInteractable "Pat"): heart eyes, a laugh, a chirp; faces the player. |
| 3 | Raid | RaidStarted: a brave boxing pose (~1.3 s, angry), then worried; trots to its shelter (RobotSpot shelter point, else its spot) and cowers there (worried, looks around now and then) until RaidEnded. A whistle during a raid gets a worried head shake. |
| 4 | After the raid | RaidEnded Cleared/Dawn with no barrier broken since RaidStarted: win + dance, excited face. Defeat or a barrier broke: cry, sad. Stopped: nothing. |
| 5 | Welcome home | The player was outside the yard ≥ 60 s and comes back in: awake robots not busy look around, spot the player, trot over (a call with a welcome reason) and wave or hop on arrival. Not at night or during a raid. |
| 6 | Bedtime, morning | Going to standby: a yawn face, then a slower walk home. Woken by the clock: a hop with a happy face and the wake beep; woken by the player: surprised. |
| 7 | Speech mouth | While its face shows Talk, the mouth quad swaps for the scrolling speech mouth (`Rob15_mouthSpeech`), scrolled by RobotFace. |
| D | Door | A robot-only NodeLink2 through the house door (A* tag 1 "Robots only"); robots hop the 1 m step (Jump emote, parabola). EnemyMover excludes that tag for every other walker (enemies, animals). |
| Q | Call prompt | `[Q] Call Bolt` when the whistle aim (50 m) is on a robot that isn't the E focus. IWhistleTarget gets a prompt; PlayerWhistle refreshes each frame; InteractPromptHud shows it on a new view. |
| R | Roles | `RobotRole` (abstract SO: name, verb, icon). `RobotService : RobotRole` (Menu: Shop, Storage, Upgrades): E publishes `RobotServiceRequested`. `RobotJob : RobotRole` (abstract): `CreateWork(Robot)` → `IRobotWork` run by a Work branch above home life. No concrete job yet. |
| F | Fixes, refactor | Reviewer findings: step-aside abort/stale request, face-the-spot cancelled by Halt, a glance at night wakes, head drift while culled, NoGo fallback, per-frame boxing, stroll sphere, replanning each think, restart after a step, reactions queued behind a call, disable/enable, beep gaps live, hits while off, dead members, hard-coded timings, whistle buffer, allocation in step-aside, the self-filter test, staggered thinks, comment length. Request and reaction timers move to a plain C# `RobotState` (tested). |

## Tree order

flinch/knockdown > step aside > scared (raid) > answer the call > nervous > greet/pat > celebrate or cry > welcome > standby
> work (job) > home life.

## Checks

EditMode: RobotState rules (glance at night, knockdown count and window, sulk, pat, step expiry, raid outcome, welcome
timer), PlayerWhistle prompt, role and data wiring. Play: Pip comes out through the door when whistled and hops back home;
enemies still can't path in; Q prompt; each reaction by probe; screenshots.
