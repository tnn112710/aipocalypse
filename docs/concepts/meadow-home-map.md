# Meadow home map concept

Date: 2026-10-18. Status: visual concept, not to scale. This records the layout developed with the user; it is not a Unity scene or a finalized gameplay specification.

![Version 3 overview with the forest expanded farther east and most of the great lake beyond the right edge](meadow-home-top-down-v3.png)

## Revision 3

The forest expands again into the additional green-marked lake area, reaching across most of the upper map. The great lake moves farther east, with only its western portion visible in this overview. The southern river channel stays open between the forest and meadow. The base, garden, bridge, and meadow remain the established near-bank destinations.

Previous revision: [Version 2 overview](meadow-home-top-down-v2.png).

## Revision 2

The forest now extends farther east and northeast into the former lake area marked by the user's green strokes. This adds contiguous forest land, rather than only increasing tree density. The great lake moves farther east, while the river remains open along the forest's southern edge and connected to the lake. The home base and southern meadow remain in place.

Previous image: [Version 1 top-down plan](meadow-home-top-down.png).

## Layout

- **Home base:** southwest corner, backed by mountains on the west and south. The base sits on a smooth, gently raised grassy hill, with a usable yard rather than a rocky terrace.
- **Cabin and yard:** a wooden log cabin faces diagonally northeast toward the central campfire and main gate. A wooden defensive fence encloses the cabin, yard, and garden. The garden sits to the right of the cabin, rotated farther clockwise.
- **Meadow:** open, gently rolling flower hills surround the base and extend east along the near bank. Keep space for movement and later additions.
- **Waterfall and pond:** the western mountains feed a small scenic pond, which drains into the river.
- **River and crossing:** the river separates the home meadow from the forest. A broken wooden bridge needs repair to cross; its central span is missing.
- **Forest:** a large pine forest covers the far bank and now extends across most of the upper map into the former eastern lake area, forming the dominant land zone in the concept.
- **Mountain lake:** the river opens directly into a large lake shifted farther east to accommodate the expanded forest. Only its western portion appears in version 3; its enclosing far-shore mountains lie mostly outside this view. This replaces the earlier river-gorge proposal.
- **Lakeside viewpoint:** a meadow path leads east to a natural rocky viewpoint beside the lake.

## Reading the image

North points toward the top of this overview. Earlier perspective concepts look diagonally outward from home toward the river and lake opening. Version 3 is an illustrated overhead concept with some angled object rendering; use its shorelines and area relationships as guidance rather than a measured orthographic drawing.

The paths and vegetation illustrate spatial relationships. Map dimensions, travel times, slopes, collision boundaries, forest contents, and encounter placement still need to be designed and tested in a blockout. Decorative details do not establish additional mechanics.

Related design context: [Core GDD](../gdd/CORE.md). This concept follows the user's mountain-backed home direction rather than the GDD's earlier sea-facing ridge description.

## Image generation

Created with the built-in image generation tool. All three PNG versions are preserved here as concept references, outside Unity's asset folders. Versions 2 and 3 use the user's corresponding annotated maps as references.

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
