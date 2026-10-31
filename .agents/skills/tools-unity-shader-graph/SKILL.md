---
name: tools-unity-shader-graph
description: Shader Graph 17.6 in aipocalypse (URP 17.6, Forward+, depth priming, GPU Resident Drawer). Use for any shader or material effect work - creating or editing a .shadergraph / .shadersubgraph, copying a vendor graph (Synty Sidekick, Raygeas, AllIn1 demos) into _Game, hit flash, dissolve, rim / fresnel, intersection glow, hologram, scrolling or vertex wobble, custom HLSL nodes, AllIn1 Shader Nodes, per-object shader values driven from C#, keywords and variants - and when a shader is magenta, dark at night, missing in a build, or breaks batching. Also whenever an agent must build or wire graph nodes without the Shader Graph window.
---

# Shader Graph (aipocalypse)

Shader Graph **17.6.0** on URP 17.6. Agents can't use the Shader Graph window and the CLI has no graph commands, so graphs are edited with **`scripts/ShaderGraphKit.cs`**: it runs through `unity command script run` and drives Shader Graph's own internal `GraphData`, with the same calls its importer and editor window make. Never hand-edit `.shadergraph` JSON.

**Not here:** uGUI effects use UIEffect (PLUGINS.md), particles and VFX the VFX packs (ASSETS.md), water / grass / terrain their own skills, post-processing the URP volume. Hand-written `.shader` files are only for skies and forks (`_Core/EnvironmentSystem/Shaders/`); everything else is a Shader Graph, which emits the passes, keywords and DOTS instancing this renderer needs.

## The renderer a graph must suit

`_Game/Settings/PC_RPAsset.asset` + `PC_Renderer.asset`:

| Setting | What it means for a graph |
| --- | --- |
| **Forward+**, additional lights + shadows | Use the URP **Lit** target (Unlit only for unlit looks). Lit handles the lantern, the muzzle-flash light, shadows, SSAO, decals and fog by itself. Custom lighting in an Unlit graph (or AllIn1's lighting-model nodes) sees only the main light: black at night. |
| Night is dark | Lit base colour is multiplied by the lights. Anything that must read at night (a hit flash, a glow) also goes to **Emission**. |
| **Depth Priming: Forced** | Opaque graphs are fine (Shader Graph emits DepthOnly / DepthNormals). An opaque shader without a depth pass renders nothing. |
| Depth + opaque textures on | Scene Depth / Scene Color nodes work (intersection glow, soft edges, refraction) in transparent graphs. |
| **SRP Batcher** on | A `MaterialPropertyBlock` takes the renderer off it. |
| **GPU Resident Drawer** (Instanced Drawing, BRG variants Keep All) | Static `MeshRenderer`s are drawn through BRG; a `MaterialPropertyBlock` silently drops them out. Keep DOTS instancing (Shader Graph's default). Skinned meshes aren't on it. |
| URP strips unused variants | A keyword enabled only from code at runtime may be missing in a build (magenta / invisible). |

## Where graphs live and how they're named

- Content rules (`docs/technicals/ARCHITECTURE.md` → Folder layout): one thing → `Content/<Kind>/<Name>/`; several of one kind → `Content/<Kind>/_Shared/`; the environment kit → `Environment/`; driven by a Core system's code → `_Core/<System>/Shaders/`. A graph shared across kinds (every Sidekick character): ask, no folder exists yet.
- **Vendor graphs are never edited in place** (`Assets/Synty`, `_StoreAssets`, `Plugins`, `Packages`). Copy the graph into `_Game` (keep the pack prefix: `Sidekick_…`), give it the menu path `Aipocalypse/<Area>` (shader `Aipocalypse/<Area>/<File>`, like the sky shaders), point first-party material copies at it, and note the fork in the system doc: vendor updates are merged by hand.
- Properties: reference `_PascalCase` (`_HitFlashColor`), display name in words. C# caches `static readonly int HitFlashColorId = Shader.PropertyToID("_HitFlashColor");`.
- HLSL for a Custom Function node sits next to its graph (`EnemyFx.hlsl`).

## Editing a graph: ShaderGraphKit

```bash
K=.agents/skills/tools-unity-shader-graph/scripts/ShaderGraphKit.cs
unity command script run --file $K --entry ShaderGraphKit.Describe --args '["Assets/_Game/.../X.shadergraph"]'
```

| Entry | Does |
| --- | --- |
| `Describe(graph)` | JSON: targets + settings, properties, keywords, every node (`id`, type, settings, ports with defaults and links), master-stack blocks (`block:BaseColor`). Read it before editing. |
| `Apply(graph, opsFile)` | Runs an ops file in order, saves once, returns the new node ids and a compile check. One failing op saves nothing. |
| `Create(graph, "Lit"\|"Unlit", "Aipocalypse/<Area>")` | New URP graph, as Create > Shader Graph > URP. |
| `ListNodes(filter)` | Built-in node classes with their menu path (`"fresnel"` → `FresnelNode: Math/Vector/Fresnel Effect`). |
| `Check(graph)` | Reimports and compiles (one tiny synchronous render); returns compile errors with file:line. |
| `Preview(graphOrMat, png, size)` | Renders a sphere offscreen with the shader or a material; open the PNG. |

Op formats (add, property, custom, connect, disconnect, set, remove, graph, target) are in the kit's header. The loop:

1. Copy a vendor graph (`script eval 'return UnityEditor.AssetDatabase.CopyAsset(src, dst);'`) or `Create` one.
2. `Describe` it; note the node id / port feeding the block you'll change.
3. Write the ops file, `Apply`. Ports are matched by name (spaces and case ignored); `connect` replaces what's in an input. For a fork of a vendor graph, keep the ops file in `AgentScripts/<Area>/` (like the map generators), so the fork can be rebuilt from a fresh copy after a vendor update; one-off edits can use the scratchpad.
4. Read `check.messages` from Apply. **An import alone doesn't surface HLSL errors** (shaders compile lazily), so never trust a clean import or `ShaderUtil.ShaderHasError` alone: `Check` or `Preview`.
5. Material: copy the existing one with the new shader so textures carry over (`new Material(src) { shader = s }` → `AssetDatabase.CreateAsset` in the content folder); set values with `material set_properties`. Sidekick looks: `SidekickKit` rebuilds a look's material from the vendor `M_BaseMaterial`, so a rebuild reverts the shader; reapply it (or teach the kit) and guard it with a wiring test.
6. In the scene: `capture scene_view` / game view by day and night, then `console get`: passes the preview didn't draw (shadow caster, other keyword sets) only compile when the scene renders them.

**Custom Function nodes** (`custom` op) are File mode. Shader Graph calls `<name>_$precision`, so write **both** `Name_float` and `Name_half` (vendor graphs such as Sidekick are half precision; with only `_float` you get "undeclared identifier Name_half"). Guard engine-only values with `#ifdef SHADERGRAPH_PREVIEW`.

**Kit limits:** no keywords, groups, sticky notes, new sub graphs or reflected-HLSL (provider) nodes; ask the user to do those in the Shader Graph window. `Apply` refuses a graph that is open in a Shader Graph window: the window would raise a modal "Graph has changed on disk" (which stalls CLI commands) and its own copy could be saved over the edit, so ask the user to save and close it. The kit uses internal API; after a Shader Graph update, an error like "X has no member Y" means: read `Library/PackageCache/com.unity.shadergraph@*/Editor` and fix the kit.

## Tested example: goblin hit flash + burn dissolve

On a copy of `Sidekick_ShaderGraph` (the goblins' graph; `<lerp>` is the node `Describe` shows feeding `block:BaseColor`). Per-goblin values come from the renderer's user value (next section), so all goblins share one material.

```hlsl
// EnemyFx.hlsl: bits 0-7 flash, 8-15 dissolve (packed in C#)
void EnemyFx_float(out float Flash, out float Dissolve)
{
#ifdef SHADERGRAPH_PREVIEW
    Flash = 0; Dissolve = 0;
#else
    uint v = unity_RendererUserValue;
    Flash = (v & 0xFF) / 255.0;
    Dissolve = ((v >> 8) & 0xFF) / 255.0;
#endif
}
void EnemyFx_half(out half Flash, out half Dissolve) { float f, d; EnemyFx_float(f, d); Flash = f; Dissolve = d; }
```

```json
{"ops":[
 {"op":"graph","name":"path","value":"Aipocalypse/Characters"},
 {"op":"custom","id":"fx","name":"EnemyFx","file":"<folder>/EnemyFx.hlsl","outputs":"Flash:Float,Dissolve:Float","x":-900,"y":120},
 {"op":"property","id":"flashColor","kind":"HDRColor","name":"Hit Flash Color","reference":"_HitFlashColor","value":"1,1,1,1","x":-900,"y":20},
 {"op":"add","id":"hit","node":"subgraph:Assets/Plugins/AllIn1Assets/AllIn1ShaderNodes/Editor/Nodes/SG/Color Effects/Hit.shadersubgraph","x":-500,"y":-40},
 {"op":"connect","from":"<lerp>.Out","to":"hit.Input Color"},
 {"op":"connect","from":"flashColor","to":"hit.Hit Color"},
 {"op":"connect","from":"fx.Flash","to":"hit.Blend"},
 {"op":"set","port":"hit.Glow","value":"1"},
 {"op":"add","id":"glow","node":"Multiply","x":-500,"y":160},
 {"op":"connect","from":"flashColor","to":"glow.A"},
 {"op":"connect","from":"fx.Flash","to":"glow.B"},
 {"op":"connect","from":"glow.Out","to":"block:Emission"},
 {"op":"target","name":"alphaClip","value":"true"},
 {"op":"add","id":"pos","node":"Position","x":-500,"y":360},
 {"op":"set","port":"pos.@space","value":"Object"},
 {"op":"add","id":"burn","node":"subgraph:Assets/Plugins/AllIn1Assets/AllIn1ShaderNodes/Editor/Nodes/SG/Alpha Effects/Fade - Burn.shadersubgraph","x":-250,"y":220},
 {"op":"connect","from":"fx.Dissolve","to":"burn.Fade Amount"},
 {"op":"connect","from":"pos.Out","to":"burn.UV"},
 {"op":"set","port":"burn.Tiling","value":"2,2"},
 {"op":"set","port":"burn.Fade Tex","value":"Assets/Plugins/AllIn1Assets/AllIn1ShaderNodes/DefaultTextures/NoiseTextureDefault.png"},
 {"op":"connect","from":"hit.Out","to":"burn.Input RGB"},
 {"op":"connect","from":"burn.Out_RGB","to":"block:BaseColor"},
 {"op":"connect","from":"burn.Out_Alpha","to":"block:Alpha"}
]}
```

- `target` sets a setting on **every** active target: the Sidekick graph also has a Built-In target, and `alphaClip` on only one leaves URP unclipped. It adds the Alpha / AlphaClipThreshold blocks, as the editor would.
- The flash also feeds Emission, so it shows at night. The burn edge (`burn.Out_RGB` minus `hit.Out`) is base colour only here: send it to Emission too if it should glow at night.
- Synty UV0 indexes a small colour palette, so noise from UV0 is blocky: the dissolve samples object-space position instead. Its xy stretches on side faces and swims while the mesh animates; start the dissolve once the death pose has settled, or use 3D noise.
- Always-on alpha clipping adds `clip()` to the depth and shadow passes of every goblin. Measure it; if it costs, swap to a clipped material only at death.

## Per-object values at runtime

- **First choice: the renderer's shader user value.** `MeshRenderer` / `SkinnedMeshRenderer.SetShaderUserValue(uint)` (not on `Renderer`). A graph reads `unity_RendererUserValue` in a Custom Function. URP keeps it in the per-draw buffer, with a DOTS-instanced path, so the SRP Batcher and the GPU Resident Drawer both keep working and every object shares one material. Tested on a `MeshRenderer` and on the skinned goblin (GPU skinning on). 32 bits: pack small values (8 bits each for flash, dissolve…) and write only when the packed value changes.
- **When it doesn't fit** (colours, textures, more than 32 bits): a material instance made once when the object is created (or taken from its pool), kept for its life and destroyed with it, written with cached property ids.
- **Never** a `MaterialPropertyBlock` (SRP Batcher, GPU Resident Drawer), and no `renderer.material` in a hot path.
- **Crowds** (enemies, animals): write from a central tick like `EnemyBodyManager` / `EnemyAnimationManager`, reacting to `DamageDealt`; no `Update`, tween or `MMF_Player` per enemy (`aipocalypse-enemy-ai`, `tools-unity-feel` §6).
- **On/off at runtime = a value** the graph uses in a lerp / step, not a keyword. Keywords are for per-material choices set in the editor (Shader Feature, local scope).
- **Time:** the Time node is scaled game time: it stops when paused and slows in hit stop (right for world effects). HUD or pause-menu animation needs an unscaled time global; none exists yet, so add one through `TimeSystem`, not AllIn1's `SetGlobalTime` / `ShaderGlobalTimeController`.

## AllIn1 Shader Nodes (PLUGINS.md: Could)

75 effect sub graphs in `Assets/Plugins/AllIn1Assets/AllIn1ShaderNodes/Editor/Nodes/SG/<Category>/<Name>.shadersubgraph`, added with `"node":"subgraph:<path>"`. `Describe` a sub graph to read its ports; the demo graphs in `AllIn1ShaderNodes/Demo/Graphs/` show each one wired (Describe them too).

- Use the effect nodes (alpha / dissolve, colour, rim, hologram, intersection glow, UV, vertex) inside a URP Lit graph.
- **Not** the lighting-model nodes (diffuse / specular models, Full Light Model): main light only, so they ignore the lantern and muzzle flash at night. Not the runtime helpers in `AllIn1Shared` (`WindController`, `FastLightConfigurator`, `ShaderGlobalTimeController`…): lights and wind belong to `EnvironmentDirector`, time to `TimeService`.
- `Fade - Burn` needs alpha clipping and a noise texture. Its `Fade Amount` is steep: with the default ports, about 0.4 already leaves only specks on a goblin, so ease the value in C# (or remap it in the graph) before wiring gameplay to it.

## Verify

`Check` → no messages; `Preview` the material and look at it (a real `MeshRenderer` in a `PreviewRenderUtility` scene is needed to see user values); in the scene `capture scene_view` at day and at night; `console get`; for many objects, Frame Debugger: SRP Batch / BRG draws (`unity-optimization-pc`).

## Common mistakes

| Mistake | Instead |
| --- | --- |
| Editing the vendor graph in place | Copy into `_Game`, set the `Aipocalypse/…` path |
| Hand-editing `.shadergraph` JSON | `ShaderGraphKit.Apply` |
| Trusting a clean import or `ShaderHasError` | `Check` / `Preview` compile it; HLSL errors appear only then |
| Custom function with only `_float` | Also `_half` |
| `alphaClip` set on one target of a two-target graph | The kit's `target` op sets all; `Describe` shows each target |
| `MaterialPropertyBlock`, or a material per enemy, for a hit flash | `SetShaderUserValue` + `unity_RendererUserValue` |
| Flash only in base colour | Also Emission, or it vanishes at night |
| Noise from UV0 on Synty meshes | Object-space position |
| Unlit graph with custom lighting, or AllIn1 lighting nodes | Lit target; night lights work |
| A keyword toggled from C# | A value the graph reads |
| Time node for a pause-menu effect | An unscaled time global (to add) |
