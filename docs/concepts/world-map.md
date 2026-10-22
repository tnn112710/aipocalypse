# World map concept

Date: 2026-10-19. Status: visual concept with approximate scale. This records the layout developed with the user; it is not a Unity scene or a finalized gameplay specification.

![Version 13 world layout: 700 × 700 m map centred on a 1000 × 1000 m terrain](world-map-top-down-v13.png)

## Built scene

Revision 13 is built in `Assets/_Game/Maps/WorldMap/WorldMap.unity`; see [ENVIRONMENT.md](../technicals/ENVIRONMENT.md) for how it is generated. Differences from the drawing, as requested by the user: the bridge is complete (not broken), and the boundary mountains are rock and mountain models (the terrain is not raised) with invisible walls along the playable edge. The meadow follows the BK Pure Nature Meadows look. Style references are in [refs/](refs/README.md).

Later rounds with the user changed the built scene further:

- The Great Lake is rounder and bigger, with five dressed islands, and an outlet river leaves its north-east tip. The swamp and forest are bigger. The river widens into a flared mouth where it meets the lake.
- Rivers and streams are wider and deeper, with noise in their width and depth. The ponds are 2.5–3 times bigger. The home pond copies the Komorebi demo pond's outline, with a central island.
- As in the concept, grey rocky mountains enclose the valley right at its edge: a near range of massifs (ANGRY MESH granite domes south of the meadow and along the lake's south shore; Grasslands cliffs east and north; Komorebi stones west), with pines on their slopes and the snow peaks behind. The home backs onto the west and south-west massifs.
- The home base moved to the meadow's west end at (267, 282), turned 50° so the door, an open gate and the spawn face the bridge (92 m away); the 34 × 27 m yard sits on a rise (pad 29.5 m) with a few trees and bushes around it. The meadow runs south again to the foot rocks, the tree band along its old south edge is gone, and knolls and hollows make it roll; the old yard site is a meadow hollow.
- A beauty pass: a lower, warmer sun, sun shadows to 150 m and ambient occlusion; a taller, two-tone lawn with knee-high clumps, flower drifts and lupine-like spikes; a palette per kind of water with reflections, reed beds on the lake shore and a wider waterfall with spray; green-tinted highland firs; grass caps and planted feet on the grey rock walls; seed fluff, sunbeam motes and fireflies; frayed trails with tufts and pebbles.

## Revision 13

The terrain stays **1000 × 1000 m**; the whole map now fits in the central **700 × 700 m** (about 150 m to 850 m on both axes), so every distance from version 12 shrinks to 87.5% and the outer 150-m band is unwalkable boundary mountains. The meadow was also too big next to the base, so, following the user's marked-up v12, the southern mountains push north over the south half of the meadow, from the waterfall to the lake's south shore, with a band of pine foothills at their foot. The meadow is now roughly **200 × 70 m**.

The fenced base stays **about 30 × 30 m**, backed against the new mountain foot. It sits just east of the user's mark, **about 85 m from the broken bridge**; exactly on the mark it would be about 65 m, below the 80–100 m target.

## Revision 12

The terrain stays **1000 × 1000 m**, but the whole map, rim mountains included, now fits in the central **800 × 800 m** (about 100 m to 900 m on both axes). Every distance from version 11 shrinks to 80%; the meadow becomes roughly **225 × 105 m**. The outer 100-m band holds boundary mountains: backdrop, not walkable. A dashed line marks the playable edge.

The fenced home base stays at true scale, **about 30 × 30 m**, and is placed again **about 90 m south of the broken bridge**. The inset and the 30 m fence bar are unchanged.

Like version 11, this is an edit of the version 10 artwork: the map was shrunk and centred, and the outer band was tiled from the image's own rim mountains, desaturated so it reads as backdrop. Previous image: [Version 11 overview](world-map-top-down-v11.png).

## Revision 11

The user scaled the whole layout down to **1000 × 1000 m**, including lake and boundary mountains, so every distance from version 10 halves (the meadow becomes roughly **280 × 130 m**). The fenced home base (cabin, yard, campfire and garden) shrinks to true scale at **about 30 × 30 m** and moves north into the meadow, so the base is **about 90 m from the broken bridge** (user target: 80–100 m). Its old spot at the mountain foot becomes forest. At map scale the base is tiny, so an inset shows it at readable size with a 30 m bar across the fence.

Version 11 is an edit of the version 10 image, not a new generation: rulers relabelled 0–1000 m in 125-m steps, compound cut out, scaled and moved, old spot filled with nearby forest texture. Geography, biomes and trails are unchanged. Previous image: [Version 10 overview](world-map-top-down-v10.png).

## Revision 10

Following the user's yellow annotations, additional narrow footpaths branch through the forest hills, follow the swamp fringe and reach the southern forest shoreline. Grey annotations become a rocky mountain boundary along the eastern edge, connecting the northern and southern mountain rim beside the swamp and lake. The highland ponds, streams, expanded swamp and southwest home meadow remain.

Generation prompt: [Version 10 prompt](world-map-v10-prompts.md). Previous image: [Version 9 overview](world-map-top-down-v9.png). Exact path routes, stream crossings and terrain dimensions remain blockout decisions.

## Revision 9

Following the user's blue annotations, two ponds sit among the highland rocks: one near the northern ridges and one on the western highland edge. Small streams follow the marked routes beneath the highlands, along the swamp margins and southeast through the forest toward the lake. The northeastern swamp expands slightly south and west. Forest trails, rolling hills, the mountain enclosure and the compact southwest home meadow remain.

Generation prompt: [Version 9 prompt](world-map-v9-prompts.md). Previous image: [Version 8 overview](world-map-top-down-v8.png). Stream widths, connections and water levels are illustrative and require measured blockout verification.

## Revision 8

The northeastern swamp expands moderately into the neighboring forest, mainly southward. A connected mountain ridge extends from the northwest highlands across the northern edge, enclosing the forest and swamp from behind. The proposed 2000 × 2000 m footprint, compact southwest home meadow, southern river, forest trails and rolling hills remain the reference layout.

Generation prompt: [Version 8 prompt](world-map-v8-prompts.md). Previous image: [Version 7 overview](world-map-top-down-v7.png). Terrain dimensions, slopes and routes remain approximate concept guidance.

## Revision 7

Based on the user's annotated version 6 image, retaining its proposed **2000 × 2000 m** footprint. The two green-marked river routes through the central and eastern forest are filled with woodland, including their southbound outlet. The swamp's southern fringe becomes forest, leaving a smaller northeastern wetland. The original western waterfall, pond, southern river and broken bridge remain.

Narrow winding footpaths extend from the forest side of the bridge toward the highlands and swamp fringe. Low wooded hills, grassy slopes, shallow saddles and scattered outcrops give the forest visible relief. These are visual exploration proposals; exact routes and elevations remain blockout decisions.

Generation deviation: the unmarked stream along the southern highlands edge was shortened to a small western reach, despite the prompt requesting its preservation.

Generation prompt: [Version 7 prompt](world-map-v7-prompts.md). Previous image: [Version 6 overview](world-map-top-down-v6.png).

## Revision 6

The user increased the proposed total footprint to **2000 × 2000 m**, including lake and boundary mountains, while retaining a compact meadow below approximately **600 × 300 m**. Version 6 targets **560 × 260 m including the home compound**. The meadow's elongated southwest shape follows the yellow annotation; the cabin, campfire, fenced yard and garden move together toward the red-marked position along its southern edge. The surrounding southern land becomes woodland and rocky terrain, with paths toward the broken bridge and lakeside viewpoint.

The highlands remain northwest, swamps northeast, pine forest central and northern, and lake east and southeast. Both edge rulers now run from 0 to 2000 m in 250-m increments. The image remains an approximate concept rather than a measured plan; verify actual meadow and compound dimensions in a blockout.

Generation prompts: [Version 6 prompts](world-map-v6-prompts.md). Previous image: [Version 5 overview](world-map-top-down-v5.png).

## Revision 5

Redrawn from the user's resketched world layout. The original home and meadow remain in the southwest, with a broad central pine forest, highlands in the northwest, swamps in the northeast, and the great lake extending along the eastern edge. Branching swamp channels connect to the main river, while a winding forest tributary joins the southern water system. Mountains frame the west and south.

At this revision, the user's proposed total footprint was **1500 × 1500 m**, including lake and boundary mountains. The image adds horizontal and vertical rulers marked every 250 m, a light grid, a north arrow and a small scale bar. These are generated concept guides, not surveyed measurements; landmark symbols are enlarged. Exact geography, slopes, water levels, crossings and travel distances remain blockout decisions.

Generation prompts: [Version 5 prompts](world-map-v5-prompts.md). Earlier visual reference: [Version 4 nature study](world-map-top-down-v4.png).

## Revision 3

The forest expands again into the additional green-marked lake area, reaching across most of the upper map. The great lake moves farther east, with only its western portion visible in this overview. The southern river channel stays open between the forest and meadow. The base, garden, bridge, and meadow remain the established near-bank destinations. [Version 3 overview](world-map-top-down-v3.png).

Previous revision: [Version 2 overview](world-map-top-down-v2.png).

## Revision 2

The forest now extends farther east and northeast into the former lake area marked by the user's green strokes. This adds contiguous forest land, rather than only increasing tree density. The great lake moves farther east, while the river remains open along the forest's southern edge and connected to the lake. The home base and southern meadow remain in place.

Previous image: [Version 1 top-down plan](world-map-top-down.png).

## Layout

- **Home base:** southwest region, in the meadow against the southern mountain foot, about 85 m from the broken bridge (80–100 m target, version 13). The base sits on a smooth, gently raised grassy hill, with a usable yard rather than a rocky terrace.
- **Cabin and yard:** a wooden log cabin faces diagonally northeast toward the central campfire and main gate. A wooden defensive fence encloses the cabin, yard, and garden in about **30 × 30 m**. The garden sits to the right of the cabin, rotated farther clockwise.
- **Meadow:** a compact elongated flower clearing around and east of the base, roughly 200 × 70 m including the compound (version 13), hemmed in by the southern mountains. Woodland and rocky slopes surround it, with narrow paths continuing outside the clearing.
- **Waterfall and pond:** the western mountains feed a small scenic pond, which drains into the river.
- **River and crossing:** the river separates the home meadow from the forest. A broken wooden bridge needs repair to cross; its central span is missing.
- **Forest:** a continuous pine forest covers the far bank and central/eastern map, with narrow footpaths, low wooded hills and small grassy slopes. Version 7 removes the two annotated river routes that divided it.
- **Highlands:** a rugged elevated region in the northwest, backed by the western mountain boundary.
- **Swamps:** a reduced northeastern wetland with internal channels and pools; its former southern fringe becomes forest in version 7. Exact drainage and elevations are not specified.
- **Mountain lake:** the southern rivers open into a large southeastern lake with a long northern arm along the eastern edge. This replaces the earlier river-gorge proposal.
- **Lakeside viewpoint:** a path leaves the meadow and continues east through wooded shore terrain to a natural rocky viewpoint beside the lake.

## Reading the image

North points toward the top of this overview. Earlier perspective concepts look diagonally outward from home toward the river and lake opening. Version 7 is an illustrated overhead concept with some angled object rendering; use its shorelines and area relationships as guidance rather than a measured orthographic drawing.

The paths and vegetation illustrate spatial relationships. The proposed map is 700 × 700 m inside a 1000 × 1000 m terrain whose outer band is unwalkable boundary mountains, with the meadow roughly 200 × 70 m, a 30 × 30 m fenced base and about 85 m from base to bridge; final dimensions, travel times, slopes, collision boundaries, forest contents, and encounter placement still need to be designed and tested in a blockout. Decorative details do not establish additional mechanics.

Related design context: [Core GDD](../gdd/CORE.md). This concept follows the user's mountain-backed home direction rather than the GDD's earlier sea-facing ridge description.

## Image generation

Created with the built-in image generation tool. All seven PNG versions are preserved here as concept references, outside Unity's asset folders. Versions 2 and 3 use the user's corresponding annotated maps as references; version 5 uses the user's resketched biome and waterway layout; version 6 uses the user's annotated meadow and home relocation image; version 7 uses the user's green river-removal annotation and requests for trails and forest relief.

<details>
<summary>Version 3 image generation prompt</summary>

```text
EDIT ONLY THE FOREST LAND FOOTPRINT AND EASTERN LAKE POSITION in the user's latest annotated MEADOW HOME TOP-DOWN PLAN 02 image. The GREEN SCRIBBLES across the RIGHTMOST water are explicit land-expansion instructions. Fill ALL green-marked upper-right lake area with contiguous pine-forested TERRAIN attached to the existing forest. Expand that forest all the way to the old map's RIGHT EDGE over the entire upper two-thirds; the forest eastern shore must move BEYOND old right border, not stay in the old place. This must be a substantial added landmass roughly an extra quarter of existing forest width, NOT more trees on unchanged land, NOT a small shoreline adjustment. Render a slightly WIDER CANVAS to the right so new forest land and a narrow portion of displaced lake are visible. Most great lake is now farther EAST OFF FRAME; show an open blue lake wedge along far-right lower area and a sliver beyond new forest eastern shore so connection is clear. New northern/eastern forest landmass is broad and deep, dominates map, with dense pines, scattered rock outcrops, natural clearings, subtle branching trails. Remove prior deeply indented north/east lake bays within green-marked area. KEEP southern river channel untouched, flowing from western waterfall and pond under BROKEN BRIDGE along southern forest boundary then opening toward lake at far EAST/right. DO NOT fill river, join forest to meadow, move existing bridge, or narrow original near meadow. Base cabin, roof angle and northeast porch orientation, central campfire, clockwise garden, enclosing palisade, gate, southwest corner placement and proportions must remain same as user reference. Southern flower meadow and its path to lakeside viewpoint stay same, do not shrink these to make forest appear larger. Keep smooth grassy base hill, western/southern rocky boundaries. Great lake still surrounded by distant rocky mountains farther right, mostly off-page if necessary; no new mountain wall through forest. Final imagery has no green strokes. ONE clean cartographic overhead map, flat overhead roof and canopy footprints, no perspective view panel, no horizon. Match prior painterly game-map palette and clean labels. Title 'MEADOW HOME — TOP-DOWN PLAN 03'. Compass N up. Labels with tidy leader lines 'HOME BASE', 'CAMPFIRE', 'GARDEN', 'FLOWER MEADOW', 'EXPANDED PINE FOREST', 'WATERFALL & POND', 'BROKEN BRIDGE', 'GREAT MOUNTAIN LAKE', 'LAKESIDE VIEWPOINT'. Small note 'Concept only • Not to scale'. No added buildings, crossings, islands, gorge, enemies or portals. Highest priority demonstrate significant additional forest land in entire green-marked water and move lake right, preserving accepted southern layout. Landscape format.
```

</details>

<details>
<summary>Version 2 image generation prompt</summary>

```text
Edit the user-attached annotated meadow home overview. Green strokes indicate the LAND AREA to add to PINE FOREST, not water decoration. Enlarge forest footprint strongly toward EAST/right and NORTHEAST into ALL green-scribbled former lake region: replace that water with substantial contiguous forested ground with rocks, canopy, a few clearings and subtle winding trail, attached seamlessly to existing forest. Shift great mountain lake farther RIGHT/EAST, widening map canvas east if needed to show it still as a large lake. The lake should no longer curl deeply behind forest across top-center: that former upper-right bay becomes forest land. Keep large lake east of expanded forest with rocky mountains enclosing far eastern and northeastern shore. New forest eastern shoreline should lie far to right of original peninsula, almost at right edge of original annotation, so added acreage is clearly substantial, not merely increased tree density. Preserve river channel along SOUTH edge of expanded forest and its continuous opening into eastern lake, never fill channel or join northern forest to southern meadow. Geographic chain remains western waterfall -> small pond -> river under one broken bridge -> river continues east along now-expanded forest bank -> large eastern lake. Preserve southern flower meadow, southwest base, cabin, central campfire, diagonal clockwise garden, palisade, gate, bridge location, near-shore meadow trail and lakeside viewpoint. Do not shrink meadow or change base proportions. Forest should be largest land zone with broad eastward exploration room. Remove all green scribbles. Output SINGLE TRUE TOP-DOWN PLAN with camera 90 degrees straight down, no horizon or oblique projection: trees as overhead circular/star-shaped canopy symbols, cabin only roof footprint with no walls visible, mountains as overhead rocky ridge contour shapes not upright peaks, fences as thin plan lines. Attractive clean hand-painted cartographic masterplan style. Labels sparse with leader lines: 'HOME BASE', 'CAMPFIRE', 'GARDEN', 'FLOWER MEADOW', 'EXPANDED PINE FOREST', 'WATERFALL & POND', 'BROKEN BRIDGE', 'GREAT MOUNTAIN LAKE', 'LAKESIDE VIEWPOINT'. Title 'MEADOW HOME — TOP-DOWN PLAN 02'. North arrow up, small 'Concept only • Not to scale'. Landscape format, no additional perspective panels. No new buildings, bridges, gorge, portals or mechanics. Main intended change must be larger FOREST LANDMASS filling marked northern/eastern former water, lake displaced east; not just relabeling or densifying existing trees.
```

</details>

<details>
<summary>Version 1 image generation prompt</summary>

```text
CORRECT CAMERA / DRAWING PROJECTION of this map. User asked TOP DOWN; current image is still oblique. Redraw same exact landscape arrangement as FLAT TWO-DIMENSIONAL ORTHOGRAPHIC SITE PLAN. Look STRAIGHT VERTICALLY DOWN at ground, like a flat architectural landscape masterplan or overhead satellite map. NO perspective, NO isometric view. Every tree MUST be a circular/star-shaped overhead canopy blob (conifers seen from tip straight down), NEVER upright triangle silhouettes, NO tree trunks visible. House MUST be roof rectangle only with chimney viewed from above, NO visible walls, doors, windows or facade. Fence is thin plan-view line of dots/posts, NOT tall vertical palisade wall. Mountains MUST be flat irregular gray rock contour shapes with shaded contour lines, NEVER upright mountain silhouettes. Waterfall is a narrow short white frothy patch along overhead stream in western rock contours, NEVER upright white falling-water wall. Labels upright page text. Preserve all geographic layout from input: southwest home compound with diagonal roof, central campfire, clockwise-rotated rectangular garden to its right; open flower meadow in lower half extending east, trail from gate to one broken bridge across river; western small pond at waterfall base -> river southeast along southern edge of very large forest peninsula -> eastern large lake which wraps around northeast forest; broad dense forest covers northern/central land, rocky mountain boundary west/south and far lake northeast/east; near meadow shore path leads to eastern viewpoint. Water bodies exact shore footprints like input. Forest color green filled land with small overhead canopy rosettes, grass pale green with tiny pink flower dots, rocks gray overhead ridge contour hatching. Flat clean hand-painted CARTOGRAPHIC MASTERPLAN aesthetic with restrained watercolor washes on cream paper, very minimal relief. Title 'MEADOW HOME — TOP-DOWN PLAN'. North arrow up, small note 'Concept only • Not to scale'. Clearly labeled main areas: 'HOME BASE', 'CAMPFIRE', 'GARDEN', 'MEADOW', 'PINE FOREST', 'WATERFALL & POND', 'BROKEN BRIDGE', 'MOUNTAIN LAKE', 'LAKESIDE VIEWPOINT'. One map no panels, landscape format. Critical visual test: cannot see side of ANY house, tree, mountain, or fence; EVERYTHING a footprint from directly overhead. Sacrifice decorative depth to meet true flat site-plan readability.
```

</details>
