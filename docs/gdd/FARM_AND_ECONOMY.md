# Farm & Economy

Current direction from the design discussion on 2026-10-30. [CORE.md](CORE.md) describes the daily loop. Confirmed direction is distinguished below from proposals and open rules; this is a design draft, not implementation status.

## Farm and meadow

- The fenced farm contains the player's crops, plants and animal area. The surrounding meadow is safe during an ordinary day; enemies do not cross the river to actively attack the player unless provoked. Eclipse days are an exception to the ordinary daytime rhythm.
- Animals can remain within the farm or roam freely in the meadow. Both produce; free-roaming animals produce faster.
- Animals drop their products as physical pickups near themselves. The player walks over to collect them, including products left out in the meadow.
- Production is not restricted to a morning collection at a barn or coop. The earlier suggestion of fixed nests, milking stalls and next-morning grazing rewards is replaced by physical drops and a faster grazing production rate.
- Before night attacks, bring roaming animals back inside the farm to protect them. Animals left outside during raids are exposed to goblin theft. Quiet-night animal routines and the protection provided by fences and shelters after a breach remain open.
- Later, a dog assigned to the inner zone can help automate gathering animals. The player may instead take the dog exploring; exact commands and training progression remain open.

## Animal products

The animals discussed include chickens, cows, sheep and pigs. Their availability and starting roster are not fixed.

| Animal | Product direction | Still open |
| --- | --- | --- |
| Chicken | Eggs as physical drops | Rate, stack size and grazing multiplier |
| Cow | Milk as physical drops | Pickup presentation, rate and grazing multiplier |
| Sheep | Wool as physical drops | Rate, uses and grazing multiplier |
| Pig | Product not yet chosen | Foraged goods, crop-related output or another role |

Product timing, feeding requirements, animal growth and breeding have not been designed. Neither petting nor a separate happiness meter is required by the current direction.

Pickup lifetime, persistence across sleep, stacking, maximum uncollected production and any later collection automation also need decisions. Do not assume that uncollected produce disappears at dusk or is automatically moved into storage.

## Crops, plants and fishing

- Crops and plants grow inside the fence. Harvests can contribute food or be sold for money.
- Animal products and harvested ingredients support cooking, eating where applicable, and selling. Exact recipes, edible ingredients and meal effects are open.
- Fishing provides fish that can be cooked or sold.
- Crop selection, growth times, replanting, watering, feed crops and fertilizer are undecided. Adding farming to the core loop does not commit to every farming-simulator subsystem.

## Trading service

- A trading station at home handles selling products and purchasing supplies.
- Present the service through a cute robot, using the expressive service-robot direction discussed with the visual reference. The exact model, asset pack, name and station layout are not selected.
- An upgrade machine/service and a storage station, inspired by the home facilities in Escape from Duckov, are possible later additions. Their functions, unlocks and whether they share a structure are undecided.
- Buying and crafting ammunition remain part of the broader design; crafting recipes and the trading station's full stock are not fixed.
- **Sales pay immediately.** The player can use money from today's produce to purchase supplies for the same night's defense.
- **Purchase delivery timing: TBD.** Immediate sale payment does not establish instant delivery of purchases or a courier schedule.

### Presentation proposals — not confirmed rules

- A robot accepts goods through a tray or intake, scans them, and presents a sale total; purchases appear through a cargo compartment or delivery locker.
- A trader and mechanic can have distinct tools, expressions and work areas while belonging to the same robot family. The number of robots is not fixed.
- A small shelter, crates and work surfaces can connect the robots to farm life. New tools and storage can make later station improvements visible.
- Short reactions and occasional customer-service jokes can carry the AI humor. The guide drone remains a separate guide character.
- Station names, exact animations, decorative props and specific robot silhouettes remain presentation choices.

## How the activities connect

The confirmed connection is simple: collect produce, harvests, fish and exploration loot; keep useful goods for food or other uses, or sell them for money; use purchases and improvements to support the next day and its defense.

```text
Animals / crops / fishing -> ingredients -> food
                         -> sales -> coins -> purchases
Exploration -> resources and loot -> home supplies and progression
Night defense -> protect animals, player and farm
```

### Balance proposals — to validate

| Source | Proposed economic role |
| --- | --- |
| Animals | Recurring income; faster meadow production rewards managing their exposure |
| Crops | Harvests that reward planning and supply cooking ingredients |
| Fishing | Active income when the player chooses to spend time earning more |
| Exploration | Resources, equipment and discoveries that make going beyond the river worthwhile |
| Defense | Preserve productive assets, with rewards and salvage still to be defined |

- Aim for useful progress after reasonable supply, ammunition and repair spending. Ordinary survival should leave room to improve the home.
- Preserve a route to recovery after a bad night. Protected income, recoverable theft or other safety nets are proposals; their exact form is not decided.
- Keep raw sales worthwhile. Cooking should offer a reason to keep ingredients without making processing every item compulsory.
- Stable prices, optional product orders and rare exploration materials as upgrade requirements are suggestions, not approved economy rules.
- Make the first bridge repair achievable with resources available before crossing it. Costs and the unlock sequence still need design.

## Decisions still needed

| Area | Open decision |
| --- | --- |
| Trading | Purchase delivery timing, stock, restocking, prices and transaction rules; sale payment is immediate |
| Animal production | Rates inside and outside the farm, feed requirements, product packaging and pickup persistence |
| Animal safety | Theft behavior, protection inside the fence, recovery of stolen animals and permanent-loss rules |
| Animal progression | Starting species, pig products, animal acquisition and whether breeding exists |
| Crops | Starter plants, growth, replanting and maintenance |
| Food | Recipes, meal effects and whether cooked products can be sold |
| Upgrades and storage | Which services exist, what they do and when they unlock |
| Economy balance | Income, daily costs, recovery and the relationship between coins and exploration materials |
