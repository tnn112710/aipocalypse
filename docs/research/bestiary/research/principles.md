# Enemy ecosystem principles: scarce-ammo farm-defense FPS

Sources are in `references.json` (same folder). The short names in brackets match its `source` fields.

## Principles

1. **Every enemy asks two questions and shows the answer.** Write how the player dodges it and how they hit it before building it. Every damaging attack gets a wind-up with a pose and a sound; harder raids may shorten wind-ups but never remove the tell. [Stout telegraphing; DOOM Eternal Q&A; Halo GDC 2002 p.6]
2. **One personality per type, every decision announced.** Skittish, relentless or methodical, with a bark or animation for flee, charge and call for help. Robots need readable states too. [Halo GDC 2002 p.6; F.E.A.R. Orkin; Halo 4 Warner; GMTK]
3. **Readable at night distance.** Silhouette-test each type (proportions, held weapon, headgear), and give every special, raid start and boss its own audio cue from the first build. [TF2 paper; Vermintide 2 Hellberg; Darktide; DRG Grabber]
4. **Weak alone, strong together.** Build specials as pairs that punish opposite habits (clumping or straying, hiding or pushing), for example a pinner plus a mover. Barks make the pairing visible. [L4D Weir; F.E.A.R. Orkin; Darktide]
5. **Fodder is a resource; threats are priorities.** Every roster has "kill when you need it" fodder that feeds the economy, and "kill first" threats with a weak point that one gun answers best. [DOOM Eternal Hell Razer; DOOM Eternal Graft; DOOM GDC 2018]
6. **Scarcity must pull players into fights, not away from them.** Tie some resupply to combat, such as melee kills on fodder dropping rounds or scrap, or close kills refunding health. Otherwise players hoard ammo and melee everything. [DOOM GDC 2018; DOOM Eternal Graft; ULTRAKILL]
7. **No bullet sponges.** With fixed HP per unit, bullets-to-kill (body shots vs headshots at ×2, with falloff) is each type's main tuning number. Extra HP belongs on visible elites that pay it back in drops. Tougher enemies read as smarter, so toughness buys credibility cheaply, but it costs the player ammo. [Halo 4 Warner; Halo GDC 2002 p.16; Valheim stars]
8. **Ranged pressure stays fair.** Use lobbed or visible projectiles, and give anything gun-like a laser or charge cue. Accuracy ramps with exposure time and drops with player speed, and the number of ranged sources firing at once is capped. [Halo GDC 2002 p.17; DOOM 2016 AI; HL2 Ep2 commentary; Wagar hitscan]
9. **Cap the damage, not the crowd.** Raid size can grow, but per-type attack tokens gate how many enemies attack at once, and a hard cap limits live enemies. [DOOM 2016 AI; 7DTD Blood Moon; RoR2 Directors]
10. **Flyers and grabbers give windows.** Flyers stop to attack (hover and charge, or swoop then retreat) and carry big weak points. Grabbers and thieves screech first, respect grace periods, and drop their prize after a small damage threshold. [DRG Mactera Spawn; DRG Grabber; Minecraft Phantom]
11. **Swarms and breakers are cheap to answer.** Swarm units announce themselves and die fast, and exploders hurt their own crowd. Breakers get a short, loud, cancellable fuse. [Serious Sam kamikaze; Minecraft Creeper; DRG Swarm]
12. **One rank language for all types.** Goblins, zombies, robots and the rest use the same elite marks (bar color, name prefix, a visual mark), and each elite modifier adds one visible verb. [Destiny Elite; RoR2 Elites; Valheim stars]
13. **Minibosses and bosses are final exams.** They test skills the roster already taught, telegraph big, show damage state on the model, and make clear how they die. A region boss is summoned with an offering farmed in its region. [DOOM Eternal Marauder; HL2 Ep2 Antlion Guard; Valheim Eikthyr]
14. **The director's budget sets difficulty; pacing sets drama.** Price units and elites in points, refuse "too cheap" picks, and spend each budget as an opening wave plus pressure waves. Vary nights with themed mixes and feed the budget from farm value and day count. Shape tempo (build, peak, relax) separately. [RoR2 Directors; DRG Swarm; L4D Booth; RimWorld]
15. **Nights and raids explain themselves.** Telegraph big nights all day (HUD, sky, sound), tie threats to player choices (noise, sleep, wealth, progress), put a time limit on raids, and give night an apex type of its own. [7DTD Blood Moon; 7DTD Screamer; Minecraft Phantom; Valheim raids; They Are Billions; Dying Light]

## Role checklist

| Role | Purpose | Real example |
| --- | --- | --- |
| Fodder | Cheap bodies that make the player feel strong and feed resources | Halo Grunts ("fodder", GDC 2002); DOOM Eternal zombies and Imps |
| Horde / swarm | Many weak melee units that punish standing still and clumping | L4D common-infected mobs; DRG Glyphid swarms |
| Exploder | Punishes letting it get close; can be turned against its own crowd | Serious Sam Beheaded Kamikaze |
| Breaker | Goes for structures behind a cancellable wind-up | Minecraft Creeper (destroys blocks) |
| Ranged pinner | Holds the player in cover so others can move | F.E.A.R. squads laying suppression fire; Darktide riflemen |
| Close assaulter / flanker | Closes the gap while the player is pinned | Darktide 8-12 m assaulters; F.E.A.R. advancing squads |
| Lobber / caster | Arcing, visible projectiles from the back line | HL2 Ep2 antlion workers |
| Disabler / thief | Takes one target (player or animal) out of play until someone rescues it | L4D Smoker and Hunter; DRG Mactera Grabber; Darktide Pox Hound |
| Scrambler | Breaks formation or position with area effects | L4D Boomer; Darktide Bomber and Flamer |
| Flyer | Attacks from an angle the ground roster can't, splitting attention | DRG Mactera Spawn; Minecraft Phantom |
| Shielded | Blocks frontal fire; needs a flank or the right gun | Halo Jackals ("carry shields", GDC 2002 slides) |
| Leader | Steadies the others; killing it routs them | Halo Elites (Grunts flee when it dies) |
| Support / healer | Keeps others alive; risks one-dimensional fights | RoR2 Mending elite; Halo 4 Watcher (as a warning) |
| Summoner / caller | Adds enemies unless killed fast | 7DTD Screamer; DOOM Eternal Arch-vile |
| Heavy | Priority target with a weak point that one gun answers | DOOM Eternal Mancubus and Arachnotron |
| Elite variant | A known unit with a visible mark and one added verb | Destiny orange-bar Elites; RoR2 Blazing; Valheim stars |
| Miniboss / boss | An exam of taught skills; forces a change of strategy | L4D Tank and Witch; DOOM Eternal Marauder; Valheim Eikthyr |
| Night apex | The threat that defines night | Dying Light Volatiles |

## Not verified or left out

- Killing Floor 2 Fleshpound/Scrake rage thresholds, Borderlands Badass tiers and Remnant 2 elite modifiers: only fan-wiki or guide snippets were reachable (fandom and the official KF2 wiki block fetching).
- Vampire Survivors / Megabonk spawn rules: no documented design source found.
- The Forest / Sons of the Forest day-based escalation: only a fan wiki (aggression grows with kills and day count) and forum posts.
- Left 4 Dead "specials designed to split the team": Booth's slides only say Specials "harass". The split/punish reading is Weir's analysis.
- DOOM "fodder / heavy / super heavy" tier names: not found in a verified source (Shacknews uses "heavies" and "weaker enemies").
- "30 seconds of fun": Griesemer's line is confirmed, but its first use (documentary or talk) is not.
- Days Gone horde talk (GDC 2018, "Freak-O-System"): only the abstract was found.
- RimWorld raid formula: verified only through search excerpts of the community wiki; the site blocked direct fetching.
