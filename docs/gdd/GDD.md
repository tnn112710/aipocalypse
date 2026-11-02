# Game Design Document — aipocalypse

Design intent, split into topic files. Current direction revised 2026-10-31. Technical implementation is documented in [ARCHITECTURE.md](../technicals/ARCHITECTURE.md); the development schedule is in [ROADMAP.md](../ROADMAP.md).

Start with [CORE.md](CORE.md). The current loop combines a farm and safe daytime meadow, exploration unlocked by repairing the bridge, and required defense on raid nights against thieves and attackers. Cute robot services provide trading, safe deposits and a free daily box; upgrade and storage services are open in form.

Raid and quiet nights are known each morning; ordinary raids end at midnight. Blood Moons occur every 10 days, with an eclipse replacing the Blood Moon every 30 days. Play is endless for now and the grand trial is on hold. XP skills, perk cards, Luck and the cat are extras for later. See the [major-event calendar](CORE.md#major-event-calendar).

## Reading the draft

- Current direction records the user's choices. Sections labelled **proposals**, **retained draft** or **open** are not additional confirmed rules.
- [IDEAS.md](IDEAS.md) contains unapproved suggestions, extras for later and a clearly marked historical notebook. The fire choice, opt-in fights and the full-cozy path no longer exist.
- A design decision does not mean that the feature is implemented. Follow the roadmap and technical docs for implementation status, and confirm gameplay work against the user's prompt.

## Topics

| File | Covers | Status |
| --- | --- | --- |
| [ONE_PAGE.md](ONE_PAGE.md) | The whole game on one page: concept, loop, win/lose, controls, scope, look, audience, done criteria, milestones | Summary of CORE; scope, done and milestones are proposals |
| [CORE.md](CORE.md) | Pitch, pillars, places, loop, bridge and river, day/night, raid schedule, Blood Moons, eclipses, sleep and sanity, player, companions, failure and endings | Current direction; detailed rules open |
| [FARM_AND_ECONOMY.md](FARM_AND_ECONOMY.md) | Roaming animals, physical product drops, crops, food, fishing, robot trading, deposits, daily stock, the daily box and upgrade/storage services | Direction agreed; rates, prices and service rules open |

Further topic files (player, weapons, enemies, night fights, world, story, UI, art and audio) get added here as each area is designed. Keep unresolved choices explicit rather than filling them with assumed mechanics or balance numbers.
