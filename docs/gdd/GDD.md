# Game Design Document — aipocalypse

Design intent, split into topic files. Current direction revised 2026-10-30. Technical implementation is documented in [ARCHITECTURE.md](../technicals/ARCHITECTURE.md); the development schedule is in [ROADMAP.md](../ROADMAP.md).

Start with [CORE.md](CORE.md). The current loop combines a farm and safe daytime meadow, exploration unlocked by repairing the bridge, and night defense against thieves and attackers. Cute robot services provide the trading direction, with upgrade and storage services considered for later.

Player biome portals are removed; the outer-zone map remains to be designed. Quiet nights allow activities such as night fishing; ordinary raids end at midnight with attacks stopping and surviving raiders retreating. Blood Moons occur every 10 days, with an eclipse replacing the Blood Moon every 30 days. Trading sales pay immediately. See the [major-event calendar](CORE.md#major-event-calendar).

## Reading the draft

- Current direction records the user's choices. Sections labelled **proposals**, **retained draft** or **open** are not additional confirmed rules.
- [IDEAS.md](IDEAS.md) contains unapproved suggestions and a clearly marked historical notebook. Earlier fire-only attacks, farming as an extra, and the prohibition on enemy attack portals no longer define the current loop.
- A design decision does not mean that the feature is implemented. Follow the roadmap and technical docs for implementation status, and confirm gameplay work against the user's prompt.

## Topics

| File | Covers | Status |
| --- | --- | --- |
| [CORE.md](CORE.md) | Pitch, pillars, farm/exploration/defense loop, bridge, day/night, Blood Moons, eclipses, player, companions, failure and endings | Current direction; detailed rules open |
| [FARM_AND_ECONOMY.md](FARM_AND_ECONOMY.md) | Roaming animals, physical product drops, crops, food, fishing, robot trading and possible upgrade/storage services | Direction agreed; rates, prices and service rules open |

Further topic files (player, weapons, enemies, night fights, world, story, UI, art and audio) get added here as each area is designed. Keep unresolved choices explicit rather than filling them with assumed mechanics or balance numbers.
