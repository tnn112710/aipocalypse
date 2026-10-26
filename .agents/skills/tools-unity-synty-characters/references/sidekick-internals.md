# Sidekick Characters internals (tool 1.2.7, DB 1.0.2)

Checked against the vendor source. Paths are relative to `Assets/Synty/SidekickCharacters/` and line numbers are from 1.2.7: re-check them after a tool update (`Scripts/Editor/version.txt`).

## Contents
1. Runtime API (`SidekickRuntime`)
2. Database
3. `.sk` format
4. The tool's export ("Export Character as FBX")
5. Rig, avatar, material
6. Costs and leaks

## 1. Runtime API — `Scripts/Runtime/API/SidekickRuntime.cs`, namespace `Synty.SidekickCharacters.API`

**Constructor** (:245)
- `new SidekickRuntime(GameObject model, Material material, RuntimeAnimatorController controller = null, DatabaseManager db = null)` opens the DB if it isn't connected.
- It loads nothing by default; callers pass `Resources.Load("Meshes/SK_BaseModel")` and `Resources.Load("Materials/M_BaseMaterial")`.

**`CreateCharacter`** (:290)
```
CreateCharacter(string name, List<SkinnedMeshRenderer> parts, bool combineMesh, bool processBoneMovement,
                GameObject existingModel = null, bool keepBodyBlendShapes = true, bool keepFacialBlendShapes = true)
```
- `parts` are the SkinnedMeshRenderers of the part FBX: `SidekickPart.GetPartModel()` (a `Resources.Load` of the part's location) `.GetComponentInChildren<SkinnedMeshRenderer>()`.
- `existingModel` is unused: it always creates a new object.
- `keep* = false` bakes the current values into the mesh (`BakeAndFilterBlendShapes` :590).
- Bone movement shifts only the `*Attach*` bones (DB `sk_blend_shape_rig_movement`). Calling it does a DB read.
- `ResolveBaseModel` (:965) uses the head part's own model as the rig donor when that part's avatar isn't `SK_BaseModelAvatar` (e.g. alien heads). Goblin heads copy the base avatar, so they build on `SK_BaseModel`.

**Body shape properties**
- `BodyTypeBlendValue`: −100 masculine … +100 feminine.
- `BodySizeHeavyBlendValue` / `BodySizeSkinnyBlendValue`: 0..100. The `.sk` BodySize is one value: > 0 means heavy, < 0 means skinny = −value.
- `MusclesBlendValue`: −100..100.

**Wrap rule:** the Wrap part is used only when the Torso part has `UsesWrap` **and** BodyType > 0.

**`PopulateToolData`** (:261) must run before `MappedPartDictionary` / preset maps are used.
- In the editor it scans `Assets` for `SK_*_*_*_*_*.fbx` and **writes `file_exists` back to the .db**.
- SidekickKit avoids it: it finds parts by name with `SidekickPart.SearchForByName(db, name)`.

**`UpdateColor`** (:1070) paints 2×2 texels of the material's 32×32 maps in place, calling `Apply()` per call. With the shared `M_BaseMaterial`, this mutates `Resources/Textures/T_ColorMap.png` in memory for the editor session.

## 2. Database — `Scripts/Runtime/Database/DatabaseManager.cs`

**Connection**
- Path `Assets/Synty/SidekickCharacters/Database/Side_Kick_Data.db`, version const "1.0.2".
- **One static connection** shared by every `DatabaseManager`, and by the tool window (which closes it in `OnDestroy`).
- `GetDbConnection(checkDbOnLoad)`: `true` may INSERT a default color set or CREATE tables (`InitialiseDatabase`).

**Player builds** read `Resources/Database/Side_Kick_Data.bytes`. `Scripts/Editor/Build/DatabaseBuildSync.cs` (`IPreprocessBuildWithReport`) copies it there before each build.

**Queries** are static methods on the DTOs in `Scripts/Runtime/Database/DTO/`:

| DTO | Methods |
| --- | --- |
| `SidekickSpecies` | `GetAll(db, excludeSpeciesWithNoParts)`, `GetByName`, `GetByID` |
| `SidekickPart` | `SearchForByName`, `GetAllForSpecies`, `GetPartModel()`, `UsesWrap` |
| `SidekickPartPreset` | `GetAllBySpeciesAndGroup(db, species, PartGroup)`, `GetByName` |
| `SidekickPartPresetRow` | `GetAllByPreset`; `.PartType` is a short code, so use `CharacterPartTypeUtils.GetTypeNameFromShortcode` |
| `SidekickBodyShapePreset` | `GetAll` (BodyType / BodySize / Musculature ints) |
| `SidekickColorPreset` | `GetAllByColorGroup`, `GetByName`; `ColorGroup` is Species, Outfits, Attachments, Materials or Elements |
| `SidekickColorPresetRow` | `GetAllByPreset` → `SidekickColorRow.CreateFromPresetColorRow` |
| `SidekickColorSet` | `GetDefault` (species −1) → `SidekickColorRow.GetAllBySet` |
| `SidekickColorProperty` | `GetAll` (209 properties, each with U/V in the 16×16 cell grid) |

**Contents**
- Species: Human (1), Goblin (2), plus Skeleton, Elves, Zombie, Robots, Unrestricted (all parts) and "TO BE FIXED" with no parts installed.
- Goblin presets:
  - head: "Species Goblins 01–10" and "Goblin Fighters 01–20"
  - upper and lower body: "Goblin Fighters 01–10"
  - colors: 10 each for Species, Outfits and Attachments
- Body presets: 12 global ones, e.g. "Masculine - Heavy" (−100/60/−10).

## 3. `.sk` format

- **Type and serializer:** `Scripts/Runtime/Serialization/SerializedCharacter.cs`. The tool writes YAML with `Unity.VisualScripting.YamlDotNet.Serialization.Serializer` (editor-only DLL from com.unity.visualscripting), ASCII. A JSON variant also exists (`_Demos/SavedCharacters/SK_SampleFace.sk`), and the YAML deserializer reads it.

```
Name: GoblinFighter_01
Species: 2                       # sk_species.id
Parts:                           # one per equipped CharacterPartType
- Name: SK_GOBL_BASE_01_01HEAD_GO01
  PartType: Head
  PartVersion: 1
ColorSet: {Species, Name, SourceColorPath, ...}
ColorRows:                       # one per sk_color_property (209)
- ColorProperty: 1
  MainColor: 88824C              # RRGGBB, no '#'; also Metallic, Smoothness, Reflection, Emission, Opacity
BlendShapes: {BodyTypeValue: 100, BodySizeValue: 80, MuscleValue: -50}
```

- **Load in the tool:** `ModularCharacterWindow.OpenAndLoadCharacter(path)` is public static. Double-clicking a `.sk` calls it. It only rebuilds the scene preview; it doesn't export.

## 4. The tool's export — `Scripts/Editor/ModularCharacterWindow.cs`

1. The button "Export Character as FBX" calls `CreateCharacterPrefab` (:5515).
2. `SaveFilePanelInProject` opens a modal dialog. Picking `X/Foo.fbx` writes into `X/Foo/`:
   - `Foo.sk`
   - `Textures/T_FooColorMap.png`
   - `Materials/Foo.mat` (existing ones are `File.Delete`d first, so the material gets a new GUID)
   - `Meshes/Foo.fbx` (FBX Exporter; Human rig, `optimizeGameObjects` off, readable)
   - `Foo.prefab`
3. `ValidateExportedAvatar` can open a modal dialog.
4. All exceptions are swallowed into a warning.

**Options** are kept in EditorPrefs:
- `SK_Combine_meshes` (default on)
- "Combine Body / Facial Blend Shapes" (default on, meaning keep them)
- Loading a `.sk` forces all of them on.

The window auto-opens once per editor session (EditorPrefs `syntySkAutoOpenState`).

## 5. Rig, avatar, material

**Rig:** `Resources/Meshes/SK_BaseModel.fbx`, Humanoid, avatar `SK_BaseModelAvatar`.
- Bones: `root`, `pelvis`, `spine_01..03`, `neck_01`, `head`, `jaw`, `eye_l/r`, `clavicle_*`, `upperarm_*`, `lowerarm_*`, `hand_*` (+ fingers, `prop_l/r`), `thigh_*`, `calf_*`, `foot_*`, `ball_*`, twist bones.
- Attach bones: `headAttach`, `faceAttach`, `backAttach`, `shoulderAttach_*`, `elbowAttach_*`, `hipAttach*`, `kneeAttach_*`.
- `ik_hand_*`, `ik_foot_*`.
- Parts add `<mesh>_dyn_<chain>_<n>_<side>` bones (hair, ears, attachments) for secondary motion.

**Human map:** Hips `pelvis`, Spine `spine_01`, Chest `spine_02`, UpperChest `spine_03`, Neck `neck_01`, Head `head`, Jaw `jaw`, Toes `ball_*`, fingers. Base Locomotion's and Goblin Locomotion's `SidekickSyntyCharacter` avatars share it.

**Material:** `Resources/Shaders/Sidekick_ShaderGraph.shadergraph`.
- URP Lit (+ Built-in), opaque.
- One material per character with a 32×32 color atlas (`_ColorMap`) plus shared `_MetallicMap`, `_SmoothnessMap` and `_EmissionMap`.
- The shader has no `_ReflectionMap` / `_OpacityMap`, though the runtime writes them.
- Motion-vector pass disabled.

## 6. Costs and leaks

- `MeshUtils.CopyMesh` and the Combiner allocate Meshes that are never destroyed. After batch builds, call `EditorUtility.UnloadUnusedAssetsImmediate` (SidekickKit does).
- The Combiner sorts the caller's part list in place.
- **`StateMachineBehaviour`s** `PerlinNoiseAnimatorState` and `RandomStateBehaviour` run every frame. They're only used by `Animations/New Animator Controller.controller`.
- **Network:**
  - `[InitializeOnLoad] DownloaderBackgroundService` → `ToolDownloader` GETs `api.github.com/repos/SyntyStudios/SidekicksToolRelease/releases/latest` on each domain reload.
  - "Download and Install" imports `Sidekicks.unitypackage` + `SidekicksDatabase.unitypackage` silently over the vendor folder.
  - `Scripts/Editor/Network/UrlManager.cs` has no callers.
