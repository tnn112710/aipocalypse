# Survival stats: Hunger, Thirst, Sanity and carried weight

Design from the question rounds of 2026-11-28. Status: **approved by the user, 2026-11-28**, with the build-time answers (sounds, screen tool, campfire) and the six calls confirmed or corrected the same day. **Passes 1–3 built 2026-11-28** (uncommitted; docs/technicals/NEEDS.md describes what exists). Pass 4 (nightmare enemies) is not.

Lines marked **(user)** are the user's picks. Lines marked **(call)** are suggestions made while writing this up that the user has not ruled on. Every number is a starting value to tune (user).

## Why

The player has two stats today, Health and Stamina. Three more give the day a shape:

- **Hunger and Thirst** make the farm, cooking and water worth something, and make the player plan a trip.
- **Sanity** makes the night frightening and makes home, the fire, the bed and Loki feel safe.
- **Carried weight** makes the backpack and what you loot a decision.

All five stats must be ready for upgrades and buffs (bigger max, slower drain, faster recovery) **(user)**.

## Goals, non-goals, anti-goals

### Goals

- Meters plan the day. They don't ask for attention every minute.
- Each meter has its own job: no two do the same thing.
- A full meter is a reward, not only the absence of a penalty.
- Every stat exposes its max, drain and recovery to upgrades, items and buffs.
- Each mechanic can be switched off without touching the others.

### Not in the first build

- Food and drink items, and Sanity restored by sleep: for now the cheats refill the meters **(user)**.
- Nightmare enemies at zero Sanity: the next task **(user)**.
- The night-exploration tools (below), comfort food, resting on a bench.
- An upgrade screen. The stats are ready for it; `stat.set` tests them.

### Anti-goals

- Dying from a meter. Hunger, Thirst and Sanity never kill.
- A HUD value that is wrong for Health, Stamina, ammo or the need gauges. Only the clock and the minimap may lie.
- A player who cannot move at all.
- A sixth meter. Tiredness lives inside Sanity; temperature is out.

## Hunger and Thirst

| | Hunger | Thirst |
| --- | --- | --- |
| Refilled by (later) | Food | Drink |
| Lasts, full to empty, at rest | About 1 game day (18 real min) | About two thirds of a day (12 real min) |
| Target habit **(user)** | Two meals a day | Three drinks a day |
| When low **(user)** | Weak melee, shaky aim | Stamina regenerates slower and drains faster |
| When well fed **(user)** | Health regenerates slowly out of combat | Stamina regenerates a little faster |

- **Drain: time plus effort (user).** A steady drain all day, plus more for the stamina the player spends (sprinting, jumping, swinging, shooting). A calm farm day costs little; a raid or an expedition costs more.
- **Tiers.** Penalties start around the halfway mark **(user)**:

| Tier | Meter | Hunger | Thirst |
| --- | --- | --- | --- |
| Well fed | 75% and up | Health regen out of combat | Stamina regen × 1.15 |
| Normal | 50–75% | — | — |
| Low | 25–50% | Melee × 0.85, sway × 1.25 | Stamina regen × 0.8, stamina costs × 1.15 |
| Very low | under 25% | Melee × 0.65, sway × 1.6 | Stamina regen × 0.6, stamina costs × 1.35 |
| Empty | 0 | Melee × 0.5, sway × 2 | Stamina regen × 0.4, stamina costs × 1.5 |

- **Empty never kills (user).** At zero, health drains slowly and stops at 25% of max health. Both empty: the drains add up, the floor stays.
- **Asleep (user):** both drain at about a third of the normal rate, so the player wakes a little hungry and thirsty and breakfast starts the day.
- **Values ease in** when a tier changes, as low-stamina penalties do today, so nothing snaps.
- **A new game starts with all three meters at 100% (user).**

## Sanity

Starts full and drains; full is good, like the other meters **(user)**. The GDD already calls it Sanity.

### What drains it (user)

1. **Darkness.** At night, outside every light, Sanity drains, faster than from lack of sleep (user) and on top of it. **The farther from the nearest light, the faster.**
2. **Not sleeping (user).** Sanity starts to drain slowly 4 game hours after waking, day and night, wherever the player is. From this alone a full meter reaches zero at about 18 hours awake: a starting value that upgrades stretch. Sleeping resets the hours awake.
3. **Special enemy effects.** One entry point ("drain this much, from this source") that an attack, an aura or an event can call. No enemy uses it in the first build.

### Light (user)

- **The campfire at home is the light.** Its radius covers the farm area. Inside it the player is safe and Sanity recovers.
- **The belt lantern gives no protection for now.**
- **Recovery by the fire is slow:** a whole night awake at home restores about a third. Sleep will be the real cure.
- Drain outside: nothing at the light's edge, rising to its full rate about 150 m out. At the full rate a full meter empties in about 4 game hours (3 real minutes). The curve is a setting.
- More than one light can exist: the drain is measured from the nearest one. This is what the later fire pits build on.
- Darkness only exists at night. The lack-of-sleep drain runs by day too (user).
- **(call)** Standing in a light recovers Sanity by day too.
- **Consequence to check:** once the lack-of-sleep drain is running (about 7% a game hour), the fire's recovery (about 3% a game hour) only slows the loss. By the fire the player still loses Sanity late at night, so staying up never replaces sleep. Until sleep restores Sanity (later), only the cheats bring it back up.
- **The yard already has a campfire (user):** it only needs its fire and light. The map generator places a fire effect with a light there, and the scene references that effect; check in the editor whether it shows, and add what's missing. The light's radius for Sanity is a setting, separate from how far the lamp shines.

### What calms it

- **Loki (user):** near the dog the darkness drain is slower (× 0.6), and petting him gives a small boost, at most once every few minutes.
- **Later (user):** sleep, comfort food and drink, sitting on a bench, resting and looking at the landscape.

### What low Sanity does (user)

Screen and sound effects, and a HUD that lies. No worse handling, no reward for being mad.

| Sanity | Screen and sound | HUD |
| --- | --- | --- |
| 60% and up | Nothing | — |
| 40–60% | Faint whispers, slightly darker edges | — |
| 20–40% | Colour drains, a heartbeat | The clock shows wrong times for a moment. The stat bars and gauges flicker and get hard to read; their values stay right |
| Under 20% | Double vision and colour-split pulses, louder whispers | Fake blips on the minimap, compass letters swap |
| 0 | The above at full strength. Nightmare enemies turn real here (next task) | — |

- **Tool (user):** SC Screen Effects for the look, tagged Could in PLUGINS.md, behind a swappable seam (next section). Its frame cost is measured against the 60 fps target.
- The effects blend with the time-of-day grades (DAYNIGHT.md).
- No fast full-screen flashing. Enemies stay readable at every level.

### Screen looks: one seam, swappable tools (user)

The game must be able to switch between SC Screen Effects, URP's own post effects or another tool later.

- **Game code asks for a look by name and strength** (0 to 1): Sanity uneasy, Sanity low, Sanity critical. It never names a tool.
- **A backend draws it.** Each tool is one backend behind the same interface. A factory creates the backend named in the settings asset, so switching tools is one field, and a new tool is one new backend with no change to game code.
- **Two backends in the first build:** SC Screen Effects (the one in use) and URP-only (darker edges, colour draining, colour fringing, grain). The second proves the switch works and gives a frame-cost comparison.
- **Only the SC backend knows the SC package.** Removing the pack means deleting that one backend. With no backend, looks do nothing and the game still runs.
- SC Screen Effects needs its renderer feature added to `PC_Renderer`; it isn't there today.
- **Nothing existing needs a refactor.** The low-health vignette is a UI image, not a post effect. The time-of-day grades are `EnvironmentDirector`'s own volumes and stay as they are; the looks sit above them.

### Nightmare enemies (next task, user)

At zero, a few shadow enemies appear that exist only while the player is at zero, and fade when they reach light or recover. Like Don't Starve's shadow creatures. They need their own design: the model, the behaviour, how many, what they drop.

## Night exploration, mid to late game (later)

With "the farther from light, the faster the drain", these push the player farther. All four are wanted **(user)**; none is in the first build.

1. **Fire pits you light.** Unlit pits stand at points of interest across the river. Lighting one costs wood and makes a new island of light, so a chain of fires is a safe road into the dark. Exploring by day prepares the nights.
2. **Loki carries a lantern.** An upgrade hangs a small light on his collar: a light that walks with the player.
3. **A torch, then a belt lantern.** A hand torch gives a small personal light but takes a hand. A later upgrade moves the light to the belt. **A torch in one hand and a pistol in the other** comes later **(user)**.
4. **Calming consumables.** Tea, coffee or pills: Sanity drains much slower for a couple of game hours.

Suggested extras, not decided: thrown flares as a short-lived light; moon phases changing the drain (a full moon is gentler, a new moon harsher).

## Carried weight

The inventory already tracks kilograms and a limit (25 kg plus the worn bag). Nothing slows the player yet.

- **Two limits (user):** Comfortable and Max.
  - Under Comfortable: normal.
  - Between them: speed drops gradually (to about 60%) and stamina costs rise (to about × 1.5).
  - At Max or above: no sprint, no jump, a crawl at about 15% speed. Never frozen **(user)**.
  - Picking up is never refused for weight.
- Comfortable is today's limit (25 kg + the bag). Max is Comfortable + 10 kg (user). Both grow later through upgrades or a better bag (user).

## HUD

```text
BOTTOM-LEFT                                BOTTOM-RIGHT

  (Hu)  (Th)  (Sa)                              + 30 Rifle Ammo
                                                + 1 Bandage
  +  [##############......]  Health
  >  [#########...........]  Stamina                  [Bag]
                                                    18.4 / 35 kg
```

**Health and Stamina stay as bars (user).** They change every second in a fight, and a bar can grow longer when an upgrade raises its max.

**Hunger, Thirst and Sanity are icon rings above the bars (user).**

- A thin ring around the icon, emptying clockwise. It shows the share of the current max.
- Always visible, dim while fine. A ring brightens when its tier changes or it's refilled, then dims again.
- Colours: white normal, green while well fed, orange low, red very low. One pulse on entering a tier; a slow pulse at empty.
- **Body sounds (user):** only from packs already imported; a sound with no match is left out. Matches found by file name, not listened to yet:

| For | Sound | Found |
| --- | --- | --- |
| Thirst | Dry cough | Gamemaster `Voice/Human Male A/voice_male_coughing_*` (Could) |
| Sanity | Heartbeat | Already a sound entry (the low-health heartbeat): reuse it |
| Sanity | Whispers | Gamemaster `Voice/Ghost/ghost_witch_voice_hiss_*` (Could); a hiss, so listen before using |
| Sanity | Low drone under the effects | Gamemaster `Cinematic Sounds/cinematic_LowDrone1`, `Collectibles_Items_Powerup/points_horror_negative_loop` (Could) |
| Hunger | Stomach growl | Nothing found: left out |
| Later, eating and drinking | — | PlaceHolder Inc Survival Lite `Miscellaneous/Food Eat 01`, `Food Eat 06`, `Food Drink 02` (Should) |

**The bag gauge sits bottom-right, by the pickup feed (user).**

- A bag icon that fills from the bottom, with "current / max kg" under it.
- Colours **(user)**: white normal, yellow near Comfortable (from 80% of it), orange from Comfortable to Max, red at Max and above.
- A small notch on the bag marks Comfortable (user). It dims while white, like the rings. The inventory screen's weight bar uses the same colours.

The dog bar and the effects row move up to make room for the rings.

## Ready for upgrades and buffs

Every value below is a stat that an upgrade, an item or a timed buff can change, through the stat system that Health and Stamina already use. All are testable with `stat.set`.

| Stat | Max | Drains slower | Recovers faster | Other |
| --- | --- | --- | --- | --- |
| Health | Max health (exists) | — | Health regen | — |
| Stamina | Max stamina (exists) | Sprint, walk, jump and attack costs (exist) | Stamina regen (exists) | Low-stamina penalty size (exists) |
| Hunger | Max hunger | Hunger drain | — (food, later) | Penalty size |
| Thirst | Max thirst | Thirst drain | — (drink, later) | Penalty size |
| Sanity | Max sanity | Sanity drain | Sanity recovery | — |
| Weight | Comfortable and Max weight | — | — | — |
| Death | — | — | — | The safe floor after death |

**After death (user):** any of the three meters under 50% is raised to 50%; higher ones are kept. Dying never refills. Upgrades can raise that floor later.

**One system for all of it (user).** Every row above goes through the stat modifier layer that exists: a source (an upgrade, a level, a bag, a buff) adds to or multiplies a stat under its own name, and removing the source removes its change. The modifiers are saved. Nothing about a stat is changed any other way, so levelling up, upgrading or buffing any of the five stats or the weight limits is the same one call.

## Cheats

- `cheat.meter <health|stamina|hunger|thirst|sanity|all> [percent]`: set one meter, or all five, to a percent (100 when left out). One command for every meter, so a new meter needs no new cheat.
- `cheat.needs`: the three need meters with their values, tiers and hours awake.
- `cheat.needsfreeze [on|off]`: freeze every need drain (recovery and cheats still work).

## Switching it off

One settings asset holds the numbers above, with a switch each for Hunger, Thirst, Sanity and the weight slowdown. With all four off the game plays as it does today.

## Approaches

1. **One shared meter, tiers as data (chosen, user).** One small "need" rule (value, max, drain, tiers) used three times. A tier is a row of stat multipliers, read through the same stat seam that consumable buffs use. Sanity is the same meter plus its own light and darkness rule and its effect views. Least code; a new penalty or a new upgrade is a data change.
2. **Three hand-written classes**, like `Stamina` and `Fatigue`. Easy to read one at a time, but the tiers, save and upgrade hooks get written three times.
3. **Rebuild all five stats under one system**, Health and Stamina included. The tidiest end state, but it rewrites working, tested code for no gain in play.

## Build order

Four passes, each playable and testable by itself:

1. **Hunger and Thirst:** the shared meter, tiers, the new stats, the health floor, the well-fed bonus, the sleep rate, the death floor, saving, cheats, the three rings (Sanity's ring shows a meter that only cheats change), the cough.
2. **Carried weight:** the two limits, the slowdown, the bag gauge, the inventory bar colours.
3. **Sanity:** lights and the darkness drain, hours awake, Loki, the enemy hook, the screen-look seam with its two backends, the sounds, the lying clock and minimap.
4. **Nightmare enemies**, after their own design.

## Where this differs from the GDD and roadmap

- **Thirst is new.** CORE.md lists HP, Hunger, Stamina and Sanity.
- CORE.md says Sanity drains in darkness and is restored by sleep, and that campfires and torches give light. This design agrees and adds the distance rule.
- ROADMAP.md leaves hunger and sanity out of the vertical slice on purpose. Building them now is the user's choice.
- The belt lantern item exists and widens what the player sees in the dark. It does nothing for Sanity for now.

## Saved

The three meters and the hours awake are saved with the player.

## Left to the builder

- Class names, where the code lives, and how the views are split.
- The exact numbers: every one above is a starting point in the settings asset.
- Which icons (the HUD's Layer Lab set first) and which campfire prop.
- How the fake minimap blips and the lying clock are drawn.
- Tests: the drain, the tiers, the health floor, the sleep rate, the death floor, the light-distance curve, the weight speed curve, and that every meter has every tier.
