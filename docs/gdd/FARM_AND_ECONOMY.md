# Farm & Economy

Current direction from the design discussions on 2026-10-30 and 2026-10-31. [CORE.md](CORE.md) describes the daily loop. Confirmed direction is distinguished below from proposals and open rules; this is a design draft, not implementation status.

## Farm and meadow

- The yard contains the player's crops and plants; the animals live in a fenced paddock beside it (a map change, not built yet). The surrounding meadow is safe during an ordinary day: enemies stay in the outer zone unless they are chasing the player, and a chaser can follow across the bridge. Eclipse days are an exception to the ordinary daytime rhythm.
- Animals can stay in the paddock or roam freely in the meadow. Both produce; free-roaming animals produce faster.
- Animals sleep at night and produce nothing while asleep. On quiet nights, animals left outside sleep where they are.
- Before a raid, the player rings a bell and the animals walk home on their own, slowly. Interacting with an animal makes it hurry. Animals left outside during a raid can be stolen by goblins.
- Later, a dog that stays home can help bring the animals home. The player may instead take the dog exploring; exact commands and training progression remain open.

## Animal products

The animals discussed include chickens, cows, sheep and pigs. Their availability and starting roster are not fixed.

| Animal | Product direction | Still open |
| --- | --- | --- |
| Chicken | Eggs as physical drops | Rate, stack size and grazing multiplier |
| Cow | Milk as physical drops | Pickup presentation, rate and grazing multiplier |
| Sheep | Wool as physical drops | Rate, uses and grazing multiplier |
| Pig | Product not yet chosen | Foraged goods, crop-related output or another role |

- Products drop as physical pickups near the animal; the player walks over to collect them, including products left out in the meadow.
- **Drops never despawn.** Each animal stops producing while its uncollected drops are at a cap, so products never pile up endlessly. The cap size is open.
- **Collection automation later:** a collector robot gathers drops. The dog's job stays bringing animals home.
- Product timing, feeding requirements, animal growth and breeding have not been designed. Neither petting nor a separate happiness meter is required by the current direction.

## Crops, plants and fishing

- Crops and plants grow inside the fence. Harvests can contribute food or be sold for money.
- Animal products and harvested ingredients support cooking, eating where applicable, and selling. Exact recipes, edible ingredients and meal effects are open.
- Fishing provides fish that can be cooked or sold. Night fishing is a quiet-night activity.
- Crop selection, growth times, replanting, watering, feed crops and fertilizer are undecided. Adding farming to the core loop does not commit to every farming-simulator subsystem.

## Trading service

- A trading station at home handles selling products and buying supplies, including replacement animals.
- Present the service through a cute robot, using the expressive service-robot direction discussed with the visual reference. The exact model, asset pack, name and station layout are not selected.
- **Sales pay immediately.** The player can use money from today's produce to buy supplies for the same night's defense.
- **Daily stock limit:** every item the robot sells, ammunition included, has a limited stock per day. Ammunition also drops from enemies; crafting remains open.
- **Deposits:** coins and items deposited with the service robots are safe from death and lost raids. Coins and items the player carries are at risk. Whether Blood Moons and eclipses can take part of the deposits is to be considered later.
- **Purchase delivery timing: TBD.** Immediate sale payment does not establish instant delivery of purchases or a courier schedule.
- **Upgrades** are the main way the player and home improve now that XP skills are extras (swimming survival, sanity per hour of sleep, better daily boxes…). The upgrade service and a storage station, inspired by Escape from Duckov's home facilities, are open in form, unlocks and whether they share a structure.

## Daily box

A small, free daily pick with a gacha feel.

- **The offer:** every morning, three closed boxes wait at the trading robot. Open one and the other two vanish. Unopened boxes stay until the next morning, then get replaced.
- **Picking wisely:** each box shows its category (ammunition, food/healing, materials, coins) and a rarity glow, but not what is inside. The player picks by what tonight needs; the roll inside is the gacha moment.
- **Reward for fighting:** a defended raid raises one box's rarity; a won Blood Moon or eclipse guarantees a rare box the next morning.
- **Better through upgrades:** picking more than one box, better box quality, rerolls.
- **Fairness:** a rare box at least every few days, so bad luck never lasts long. No streaks, no penalty for a missed day, no real money.
- **Open:** the categories and their contents, rarity tiers, the bad-luck interval, what counts as a defended raid, and how the boxes are presented (the robot's delivery locker is a proposal).
- Ref: Risk of Rain 2 — the multishop offers three terminals, and buying from one closes the others. Hades — exit doors show the type of reward, not the exact reward.

### Presentation proposals — not confirmed rules

- A robot accepts goods through a tray or intake, scans them, and presents a sale total; purchases and the daily boxes appear through a cargo compartment or delivery locker.
- A trader and mechanic can have distinct tools, expressions and work areas while belonging to the same robot family. The number of robots is not fixed.
- A small shelter, crates and work surfaces can connect the robots to farm life. New tools and storage can make later station improvements visible.
- Short reactions and occasional customer-service jokes can carry the AI humor. The guide drone remains a separate guide character.
- Station names, exact animations, decorative props and specific robot silhouettes remain presentation choices.

## How the activities connect

The confirmed connection is simple: collect produce, harvests, fish and exploration loot; keep useful goods for food or other uses, or sell them for money; deposit what must be safe; use purchases, upgrades and the daily box to support the next day and its defense.

```text
Animals / crops / fishing -> ingredients -> food
                         -> sales -> coins -> purchases (daily stock) and upgrades
Exploration -> resources and loot -> home supplies and progression
Night defense -> protect animals, player and farm -> enemy loot, better daily box
Deposits -> keep coins and items safe from death and raids
```

### Balance proposals — to validate

| Source | Proposed economic role |
| --- | --- |
| Animals | Recurring income; faster meadow production rewards managing their exposure |
| Crops | Harvests that reward planning and supply cooking ingredients |
| Fishing | Active income when the player chooses to spend time earning more |
| Exploration | Resources, equipment and discoveries that make going beyond the river worthwhile |
| Defense | Preserve productive assets; enemy loot and a better daily box |

- Aim for useful progress after reasonable supply, ammunition and repair spending. Ordinary survival should leave room to improve the home.
- Preserve a route to recovery after a bad night. Replacement animals and safe deposits help; other safety nets are proposals.
- Keep raw sales worthwhile. Cooking should offer a reason to keep ingredients without making processing every item compulsory.
- Stable prices, optional product orders and rare exploration materials as upgrade requirements are suggestions, not approved economy rules.
- Make the first bridge repair achievable with resources available before crossing it. Costs and the unlock sequence still need design.

## Decisions still needed

| Area | Open decision |
| --- | --- |
| Trading | Prices, daily stock sizes, purchase delivery timing and transaction rules |
| Daily box | Categories, contents, rarity tiers, bad-luck interval and presentation |
| Animal production | Rates inside and outside the paddock, feed requirements, product packaging and the drop cap |
| Animal safety | Paddock protection after a breach, rescuing stolen animals, replacement prices |
| Animal progression | Starting species, pig products, animal acquisition and whether breeding exists |
| Crops | Starter plants, growth, replanting and maintenance |
| Food | Recipes, meal effects and whether cooked products can be sold |
| Upgrades and storage | Which services exist, what they do and when they unlock |
| Economy balance | Income, daily costs, recovery and the relationship between coins and exploration materials |
