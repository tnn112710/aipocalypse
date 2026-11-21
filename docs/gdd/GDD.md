# Game Design Document — aipocalypse

Release title: **Hasta la Vista, M.A.Y.B.E**; "aipocalypse" is the development codename and namespace ([official/IDENTITY.md](official/IDENTITY.md)). Backup names and naming notes: [ONE_PAGE.md](ONE_PAGE.md).

Design intent, split into topic files. Current direction revised 2026-11-07. Technical implementation is documented in [ARCHITECTURE.md](../technicals/ARCHITECTURE.md); the development schedule is in [ROADMAP.md](../ROADMAP.md).

Start with [CORE.md](CORE.md). The current loop combines a farm and safe daytime meadow, exploration unlocked by repairing the bridge, and required defense on raid nights against thieves and attackers. Cute robot services provide trading, deposits and a free daily box; more stations come from robots found in the meadow and rescued beyond the river.

A developer burned out by compulsory AI productivity hears emergency sirens, is swallowed by light from his PC and wakes on a farm, unaware it is an AI sandbox. The AI claims he owes for tokens he "wasted" in real life. **Tokens** are the universal currency for supplies, upgrades and cumulative debt repayments.

There are two ways to win. Repayment, with no deadline, earns Ending 1: return to AI-controlled reality and discover humanity's enslavement as sandbox training subjects, then choose resistance outside or return to the sandbox. Defeating the ultimate AI boss, at the end of a Megabonk-style run in the final zone, needs no savings and offers Ending 2 (reality in 2018 and another chance) or Ending 3 (stay in the home he built), whether or not the player reached Ending 1 first. The debt amount, time-travel mechanism, detailed presentation and replay rules remain open. See [STORY.md](STORY.md) for the premise, reveal order and ending structure.

Beyond the river, open regions ranked by difficulty each have their own terrain, enemy mix and a summonable boss; a final zone opens once every region boss is beaten. Broken robots rescued out there open stations at home. Raid and quiet nights are known each morning; ordinary raids end at midnight. Enemy types the player provokes (zombies, robots…) hold a grudge and join the raids, and a Threat level that follows the player's results sets how hard they are ([RAIDS.md](RAIDS.md)). Blood Moons occur every 10 days, with an eclipse replacing the Blood Moon every 30 days. XP skills, perk cards, Luck and the cat are extras for later. See the [major-event calendar](CORE.md#major-event-calendar).

## Reading the draft

- [official/](official/README.md) holds the finalized design: rules the user confirmed as final, with no open items. Everything else in this folder is working draft.
- Current direction records the user's choices. Sections labelled **proposals**, **retained draft** or **open** are not additional confirmed rules.
- [IDEAS.md](IDEAS.md) contains unapproved suggestions, extras for later and a clearly marked historical notebook. The fire choice, opt-in fights and the full-cozy path no longer exist.
- A design decision does not mean that the feature is implemented. Follow the roadmap and technical docs for implementation status, and confirm gameplay work against the user's prompt.

## Topics

| File | Covers | Status |
| --- | --- | --- |
| [ONE_PAGE.md](ONE_PAGE.md) | The whole game on one page: concept, loop, win/lose, controls, scope, look, audience, done criteria, milestones | Summary of CORE; scope, done and milestones are proposals |
| [CORE.md](CORE.md) | Pitch, pillars, places, loop, bridge and river, day/night, raid schedule, Blood Moons, eclipses, sleep and sanity, player, HUD earned back through upgrades, companions, failure and endings | Current direction; detailed rules open |
| [STORY.md](STORY.md) | Compulsory AI productivity and siren opening, token-waste debt, the two ways to win, hidden sandbox training, occupied reality, resistance/return and the 2018/stay endings | Opening and ending structure agreed; detailed staging, time travel and replay rules open |
| [RAIDS.md](RAIDS.md) | Who raids and how hard: enemy types and grudge, wave power and the Threat level, tribute bowls, raids at the farm while the player is away, losing a raid, region bosses in raids, anti-grinding | Agreed 2026-11-07; balance values open |
| [FARM_AND_ECONOMY.md](FARM_AND_ECONOMY.md) | Tokens and cumulative debt repayment, roaming animals, physical product drops, crops, food, fishing, robot trading, deposits, daily stock, the daily box, robots and simple crafting | Direction agreed; rates, prices and service rules open |

Further topic files (player, weapons, enemies, night fights, world, UI, art and audio) get added here as each area is designed. Keep unresolved choices explicit rather than filling them with assumed mechanics or balance numbers.
