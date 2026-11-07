# Bestiary (enemy ecosystem proposal, 2026-11-08)

A review page for picking enemies: 57 enemies, minibosses and bosses in eight candidate raid groups, with stats, shots-to-kill, asset options, prices and design references. A proposal, not GDD: agreed picks go to `docs/gdd/IDEAS.md` first.

- `aipocalypse-bestiary.html`: the page, self-contained (images embedded). Open it in a browser. The live copy with saved Keep / Maybe / Cut picks is the artifact https://claude.ai/artifact/2MKA2cXXDxLWVvPS3aN3mQ (picks are stored there, in its `reviews` collection, not in this file).
- Offline picks: opened from disk, the page shows **Save picks to file** (downloads `bestiary-picks.json`; keep it here, next to the page) and **Load picks** (reads that file back). Picks also stay in the browser between visits.
- `source/`: the data and builder. `data.py` holds groups, units and owned assets; `store_map.py` the pack cards, Synty-pass and Asset Store options; `refs.py` the page text, principles and questions; `page.html` the template; `build.py` writes the page. The builder reads renders and store images from a temporary session folder that is not kept, so a rebuild needs those images re-rendered (Unity offscreen renders) and re-downloaded.
- `research/`: what the research agents found: design references and principles, Synty packs and gaps, Asset Store candidates and rejects, the six named publishers.

Prices were read on 2026-11-08 and change with sales.
