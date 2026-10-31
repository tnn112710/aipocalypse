# Applying the selected ideas to aipocalypse

This is a project-specific application of the general references, prepared 2026-10-31 from `AGENTS.md`, `docs/ROADMAP.md`, `docs/gdd/GDD.md`, and `docs/gdd/CORE.md`. It is a set of proposed review directions, **not observed playtest findings or authorization to implement GDD rules**. Re-read current files before acting; this snapshot does not replace them.

## Fit the actual milestone

The roadmap describes a core-feel prototype stage. Movement, shooting, equipment, day/night, and basic enemy behavior exist in the WorldMap prototype; the connected farm/exploration/defense loop remains design direction rather than a completed playable loop. Do not describe planned systems as implemented or redesign their unresolved values from the book.

The strongest immediate application is to validate the existing momentary actions and their interactions before expanding the roster or production map. The user's polish allocation gives shooting and the inner zone Very High effort, movement/camera and enemies High, many supporting systems Medium, and some activities Low. Re-read that allocation instead of applying equal polish everywhere.

## Proposed priorities and tests

| Area | Book-informed review direction | Small useful test | Evidence to collect |
| --- | --- | --- | --- |
| Shooting | Distinguish each gun's job and confirm the full action-impact-reaction sequence | Compare existing guns against representative targets at useful ranges, including reload, jam, miss, and kill | Reasons for choosing each gun; hit certainty; visibility; control and recovery |
| Movement and camera | Validate dimensions, responsiveness, and comfortable view changes before relying on them in encounters | Exercise current locomotion, jumps, slopes, aiming, bob, and landing with real input | Unclear footing, unexpected restrictions, missed inputs, excessive involuntary motion |
| Enemies | Give each behavioral role a legible intention and response | Test an existing role alone, then a complementary combination when available | Recognized tells; understood priorities; credible counterplay; navigation failures |
| Home and meadow | Make repeated activity and orientation pleasant while preserving the cozy character | Review service/landmark visibility and representative local routes in current geometry | Wrong turns, unnecessary handling, satisfying small interactions, meaningful quiet |
| Exploration | Use recognizable destinations, choices, and return orientation | Review a scoped outbound/return route without rebuilding the map | Landmark recall, apparent blocked routes, empty repetition, visibility after dark |
| Full daily loop | Verify that activities support one another when the required systems are authorized and playable | Create a beat chart from confirmed direction; later test a minimal representative cycle | Purpose of each activity, transition friction, anticipation, recovery, meaningful next goal |
| Raids and theft | Make objectives, arrivals, consequences, and response opportunities readable | Design review first; test the actual approved role/encounter when implemented | Whether players notice and understand the threat and can explain available responses |
| Rewards and losses | Check whether resource pressure still leaves viable recovery | Trace strong, weak, and unlucky scenarios using confirmed rules and labelled assumptions | Affordability, bottlenecks, repeated-loss spirals, useful reasons to explore or work locally |

These are options for the next authorized task, not a demand to build every test immediately. The book's "walking is not gameplay" slogan should not erase peaceful farm life, anticipation, or intentional exploration. Likewise, its action-game preference for fighting every enemy should not remove the player's chosen retreat or rescue decisions.

## Additional review directions from Schell

The following apply the Schell selection (user-scope `game-design` skill, `references/schell-summary.md`) to the same documented direction. They remain proposed questions; the book review did not include a playable audit.

- **Shared experience:** Test whether tending the home, exploring, and defending reinforce the existing "build something worth protecting" pillar. A feature's presence does not establish attachment. Ask what players actually care about after a representative cycle.
- **Choices:** Compare a safe local activity and an outer-zone outing using perceived enjoyment, time, supplies, expected gains, exposure, and recovery. Look for circumstances where preferences change rather than assuming that adding risk automatically creates a good choice. Leave unresolved values unresolved.
- **Pacing:** Review action, outing, and daily-cycle interest separately. Preserve genuinely enjoyable quiet time and quiet nights. Do not force every day's curve into a raid climax or infer engagement from enemy deaths.
- **Enemy depth:** Ask which readable responses existing roles create with terrain and player tools before requesting more archetypes. Do not replace the project's behavior trees with the book's generic state-machine examples.
- **Home as place:** Evaluate landmarks, useful local centers, transitions, scale, and movement through repeated service routes. Ask whether detailed dressing supports comfort and use. Propose small changes through the existing map workflow.
- **Character presence:** Service robots and the drone may become distinct through compatible functions, reactions, relationships, and status cues. Treat exact characterization or role changes as proposals; a new dialogue or relationship system is not implied.
- **Inner contradictions:** Check the potential tension between satisfying shooting and progression that begins with reduced capability or jams. Check the potential tension between cozy local life and repeated compulsory handling. These are questions to test, not reasons to remove the user's intended limits or systems.

Read the matching topic reference for a focused test. The most useful addition at the current milestone is a clearer question for a small prototype, not a requirement to implement all these directions now.

## Respect the project's execution boundaries

- The prompt and session authorize implementation; GDD material supplies context. Propose and confirm any rule, number, or mechanic taken from `docs/gdd/` or `IDEAS.md` before implementing it, as `AGENTS.md` requires.
- Use technical docs to establish implementation status and ownership. Read relevant first-party skills for enemies, characters, architecture, feel, audio, map generation, shaders, and other affected areas.
- Check `PLUGINS.md` before building systems/tools, and `ASSETS.md` before selecting content. Use the approved priorities and ask where the project explicitly requires it. Book examples do not authorize imports or changing audio entities.
- Use the project's Unity CLI workflow for editor changes. A review does not authorize a full map rebuild or unrelated scene restructuring. Preserve existing user work.
- Follow the 60 fps at 1080p RTX 2060-class target. The roadmap's crowd-enemy performance gate must be verified before expanding the roster. Do not infer success from enemy counts or editor screenshots.
- Keep gameplay feedback within the project's architecture: existing feedback, audio, time, input, and event ownership still apply. This engine-neutral skill does not override their implementation rules.

## Useful deliverable for a future review

Pick the requested area and report the relevant current behavior, the intended experience, evidence or missing evidence, a few prioritized changes, their tradeoffs, and a small test. Mark proposals and unknowns explicitly. Update game documentation only when the user actually asks for a design change or authorizes implementation; creating this skill does not revise the GDD.
