# Campfire and firelight — design

Design from the 2026-11-28 conversation. Status: **approved by the user, 2026-11-28**, with the three open questions answered (below, "Answered"). **Built 2026-11-28** (uncommitted; docs/technicals/CAMPFIRE.md describes what exists). Builds on the survival stats design ([2026-11-28-survival-stats-design.md](2026-11-28-survival-stats-design.md)) and the darkness captures picked the same day (IDEAS.md → Firelight and darker nights).

## Intent

The yard campfire becomes a real fire the player tends: it burns wood, lights the yard, keeps the Dark (sanity drain) away while it burns, and goes out when neglected. Nights get darker so light sources matter. Later the fire is the yard's hub: cooking, and things thrown in that change it (buffs, colours, summons).

**Why:** today the fire is a weak point light at ground level on a bright blue night; sanity's "safe in the light" rule has nothing visible behind it, and there is nothing to do at home at dusk.

## Goals, non-goals, anti-goals

- **Goals:** a fire that visibly lights ~20 m; a darker night; wood → fuel → burn time the player watches; lighting, feeding and putting out as plain interactions; the fuel level readable at the pit and on the HUD when it matters; rain burns fuel faster; sound; saved; the whole thing modular (its own installer, one settings asset, removable).
- **Non-goals (later):** wood chopping (wood is a finite pile for now), cooking and the "throw things in" reactions (the interaction shape leaves room for them), outer fire pits and player-built fires, a roof over the fire, raiders attacking the fire, fuel materials other than wood (the fuel is in hours so they fit later), the fire as a beacon seen from the bridge.
- **Anti-goals:** a fire that needs feeding every few minutes (Don't Starve's chore); a yard so dark that fence fights are unreadable; the radius creeping in while you fight.

## Decisions (user, 2026-11-28)

### Look

- **Night darkness:** ambient × 0.3, moon × 0.5 (level E of the captures), applied to the Night and Midnight looks; Blue hour gets half of it so dawn reads as relief; Dusk unchanged. The exposure part (−0.5 EV) goes into the Night grade.
- **Flame:** Gabriel Aguiar Stylized Fire 01 v1 (VFX Graph), colour warmed toward orange (it reads pink under the night grade), on the HIGHLANDS pit and logs. Its size follows the fuel (below).
- **Light:** a point light raised ~1 m with a two-octave flicker (10–30% swing, tunable) and a wider soft fill light, no shadows; together they light **about 20 m**. The sanity-safe radius is **30 m** (× 1.5; an upgrade later makes it bigger).
- **Sound:** a crackle loop while lit, through `AudioService` (new BroAudio entity, approved by the user).
- **Position:** moved toward the centre of the yard (today it sits to the right); exact spot is the builder's, keeping the door path and the benches sensible.
- **Collider:** the pit blocks the player and NPCs (nobody walks through the fire).

### Fuel

- A **fuel meter in hours**: `Fuel` 0 … `MaxFuel`. Start: `MaxFuel` 6 h (= 4 logs × 1.5 h). A log adds `FuelHours` 1.5. These are tuning values; the user expects to rebalance to 1–2 h a log once more materials exist. `MaxFuel` is upgradeable later (a `StatId` or a campfire upgrade; not built now).
- **Burning:** while lit, fuel drops 1 h per game hour × the rain multiplier (below). At 0 the fire goes out by itself. There is **no auto put-out at dawn**: a lit fire burns through the day unless the player puts it out.
- **Rain:** burns × 1.25 in light rain, × 1.5 medium, × 2 heavy (from the weather's rain amount: 0.2 / 0.5 / 1), none under a roof later.
- **Flame size and radius:** above 25% fuel the flame, the lit area and the safe radius are constant. Below 25% the flame shrinks and both radii shrink gradually toward zero at 0, so a dying fire is visible before it dies.
- **Unlit = unsafe:** an unlit fire gives no light and no sanity safety. The two sanity drains already stack this way (NEEDS.md): lack of sleep always runs; darkness adds on top outside every light.

### Interactions (at the pit, `IInteractable` + `IHoldInteractable`)

| State | Holding | Tap E | Hold E |
| --- | --- | --- | --- |
| Unlit, fuel 0 | anything | — ("needs wood") | — |
| Unlit, fuel > 0 | anything | **Light fire** (free, no item needed) | — |
| Any | wood | **Add wood** (one log; refused when it would pass `MaxFuel`: "Fire is full") | Add all that fits |
| Lit | not wood | — | **Put out** (keeps the fuel in the pit) |

- Wood is a stackable item (1 kg a piece, Common, low value) with a world model; a firewood pile in the yard is a pickup stack of 8, finite until chopping exists. `cheat.give wood [n]` for testing.
- The prompt shows the fuel: "Add wood (Fire 3.0 / 6 h)" or "Light fire (3.0 h)".
- **The fire consumes what you hold** is the shape later reactions reuse: a `FireReaction` per item (fuel hours now; cook recipe, buff, colour, summon later), so a new idea is a new entry, not a new verb.

### HUD

- **At the pit:** the interact prompt carries the fuel (above).
- **On the HUD:** a small fire widget (icon + bar, fuel / max) that shows only when it matters: the fire is lit and under 25%, or the fire is out at night. Hidden otherwise. Position near the need rings (builder's call within UI.md's layout).

### Saved

Lit state and fuel hours (a `CampfireSaveSection`, SAVE.md → Adding a system to the save). Wood in the inventory and on the ground is already saved as items.

### Modular

`CampfireSystem` (`_Core/CampfireSystem/`): `Campfire` (the pit's component: state, interactions, light, VFX, sound), `CampfireSettings` asset (max fuel, hours per log, rain curve, shrink-from share, lit radius, safe radius, flicker), `CampfireInstaller` (scene installer: collects campfires, ticks them on `WorldClock` hours, registers the save section and console commands). No installer = the fire is a prop. The `SafeLight` on the pit is enabled only while lit.

## Approaches considered

| Approach | Trade-off | Verdict |
| --- | --- | --- |
| Stock URP lights (core + fill, no shadows), Forward+ | cheap; a 20 m pool needs a bright core, so the fill does the reach | **picked** |
| GigaLights pack | ray-traced shadows, heavy on an RTX 3060, integration risk with COZY and the first-person pass; solves a light-count problem we don't have | rejected |
| Darkness as distance-from-light fog/exposure pass | shows the rule without lighting cost | later (survival stats design, "the Dark thickens with distance") |
| Fuel in pieces | breaks when planks / coal arrive | rejected: hours |
| Auto put-out at dawn | user changed their mind: burning by day is the player's cost | rejected |
| Fire stages (embers / fire / bonfire) | three sizes of the same graph; maps onto the fuel share | partly: one continuous size below 25% now, tiers later if wanted |

## Constraints

- AGENTS.md: prompt is the spec; smallest thing; own installer and settings asset; `AudioService` only; `InputService` only; Dictionary serialization; tests first; docs in the same change (NEEDS, DAYNIGHT, ENVIRONMENT, EQUIPMENT (wood item), UI, SAVE, CONSOLE, ASSETS, CODE_STRUCTURE).
- Perf: 60 fps at 1080p on an RTX 3060. One VFX Graph fire and two shadowless point lights; measure the fire against the old Shuriken one in the editor benchmark.
- COZY owns the ambient and moon lights: the darker night is baked through `TOD_*` looks → `CozySetup.Atmosphere()`, not hand-edited in COZY.
- The map generator (`WorldMapLayout`, Landmarks) must place the new campfire prefab at the new spot so a rebuild keeps it.

## Facts from the codebase (builder check, 2026-11-28)

- Dusk, Blue hour, Night and Midnight all use the `PP_Night` grade, so "−0.5 EV at night, Dusk unchanged, Blue hour half" means a cloned grade for Night / Midnight (and one for Blue hour), not an edit of `PP_Night`.
- The pit's collider needs an A* `NavmeshCut` (or a graph rescan) so enemies, robots, animals and the dog path around it.
- `SafeLight.Radius` is serialized; the shrink below 25% needs a runtime radius the fire sets.
- The Landmarks generator step rebuilds the whole home base, so the campfire's new spot is set in the generator constant and the scene object is moved by hand to match.
- A sleep skips the clock; the fire burns through the skip like the needs do.

## Answered (user, 2026-11-28)

1. **No wood in hand.** Wood stays in the inventory (never raised). It goes on the fire two ways: E at the pit puts one log from the inventory on it, or the player throws wood (G / Drop) into the pit and it burns as fuel. Tap E: add a log when the player carries wood and the fire has room, else light the fire; hold E: put out a lit fire. No "add all".
2. **New game:** the pit starts full (max fuel) and unlit; the player only lights it.
3. **Economy:** a log is now **2 h** (max 4 logs = **8 h**). The yard pile refills to 8 each morning (a stand-in for chopping, removed when chopping arrives). The fire burns through a sleep skip.

These replace the numbers above: `FuelHours` 2, `MaxFuel` 8.

## Left to the builder

- Exact light intensities, fill height, flicker noise, the orange tint of the flame, smoke amount.
- The new yard position; where the fuel widget sits; the wood model (an existing log mesh from a declared pack) and icon.
- How the fire ticks (the installer on `WorldClock` vs its own `Advance`), as long as EditMode tests can step it.
- Which existing crackle clip (an ambience pack) backs the new BroAudio entity.
