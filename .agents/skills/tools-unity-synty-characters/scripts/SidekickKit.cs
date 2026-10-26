// Builds Synty Sidekick characters without the Sidekick Character Tool window, for `unity command script run`.
// Read-only on the vendor package: never calls SidekickRuntime.PopulateToolData (it writes file_exists into the .db)
// and never paints the shared M_BaseMaterial textures. Outputs go to a first-party folder under Assets/_Game/.
//
//   ListPresets(species)                           species, part / body / color preset names (JSON)
//   WriteSk(skPath, name, species, head, upper, lower, body, speciesColors, outfitColors, attachmentColors)
//                                                  a .sk from presets ("" = skip that group); open it in the tool to tweak
//   Build(skPath, outFolder, name, keepFacialBlendShapes, optimizeHierarchy, exposedBones)
//                                                  .sk -> <outFolder>/<name>_Model.prefab + mesh, avatar, material, color map
//                                                  (keep optimizeHierarchy false: see the skill's §2)
//   Check a build with CharacterKit.Describe / Snapshot (aipocalypse-characters skill).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Synty.SidekickCharacters;
using Synty.SidekickCharacters.API;
using Synty.SidekickCharacters.Database;
using Synty.SidekickCharacters.Database.DTO;
using Synty.SidekickCharacters.Enums;
using Synty.SidekickCharacters.Serialization;
using Synty.SidekickCharacters.Utils;
using Unity.VisualScripting.YamlDotNet.Serialization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class SidekickKit
{
    const string BaseModel = "Meshes/SK_BaseModel";
    const string BaseMaterial = "Materials/M_BaseMaterial";
    const string BaseColorMap = "Assets/Synty/SidekickCharacters/Resources/Textures/T_ColorMap.png";
    static readonly int ColorMapId = Shader.PropertyToID("_ColorMap");
    // Mesh LOD levels past LOD0: deeper ones tear (the generator ignores skin weights). LOD2 is ~35% of the triangles and
    // holds up at combat range; the bias puts crowds on it (aipocalypse-characters §5 has the numbers).
    const int MeshLods = 2;
    const float MeshLodBias = 2f;

    public static string ListPresets(string species)
    {
        return WithDb(db =>
        {
            SidekickSpecies sp = SidekickSpecies.GetByName(db, species);
            if (sp == null)
                return Fail($"no species '{species}'; known: {string.Join(", ", SidekickSpecies.GetAll(db, false).Select(s => s.Name))}");
            var sb = new StringBuilder("{");
            sb.Append($"\"species\":{Json(sp.Name)},\"allSpecies\":{JsonList(SidekickSpecies.GetAll(db).Select(s => s.Name))}");
            foreach (PartGroup group in Enum.GetValues(typeof(PartGroup)))
                sb.Append($",\"parts{group}\":{JsonList(SidekickPartPreset.GetAllBySpeciesAndGroup(db, sp, group).Select(p => p.Name))}");
            sb.Append($",\"body\":{JsonList(SidekickBodyShapePreset.GetAll(db).Select(b => $"{b.Name} ({b.BodyType}/{b.BodySize}/{b.Musculature})"))}");
            foreach (ColorGroup group in new[] { ColorGroup.Species, ColorGroup.Outfits, ColorGroup.Attachments, ColorGroup.Materials })
            {
                var presets = SidekickColorPreset.GetAllByColorGroup(db, group).Where(p => p.PtrSpecies == sp.ID || p.PtrSpecies <= 0);
                sb.Append($",\"colors{group}\":{JsonList(presets.Select(p => p.Name))}");
            }
            return sb.Append('}').ToString();
        });
    }

    public static string WriteSk(string skPath, string name, string species, string headPreset, string upperPreset, string lowerPreset,
        string bodyPreset, string speciesColors, string outfitColors, string attachmentColors)
    {
        return WithDb(db =>
        {
            SidekickSpecies sp = SidekickSpecies.GetByName(db, species);
            if (sp == null)
                return Fail($"no species '{species}'");
            var parts = new Dictionary<CharacterPartType, string>();
            // Preset names repeat across groups ("Goblin Fighters 04" is a head, an upper and a lower body preset): look each
            // up within its own group.
            var groups = new[] { (PartGroup.Head, headPreset), (PartGroup.UpperBody, upperPreset), (PartGroup.LowerBody, lowerPreset) };
            foreach ((PartGroup group, string presetName) in groups)
            {
                if (string.IsNullOrEmpty(presetName))
                    continue;
                SidekickPartPreset preset = SidekickPartPreset.GetAllBySpeciesAndGroup(db, sp, group).FirstOrDefault(p => p.Name == presetName);
                if (preset == null)
                    return Fail($"no {group} preset '{presetName}' for {sp.Name}");
                foreach (SidekickPartPresetRow row in SidekickPartPresetRow.GetAllByPreset(db, preset))
                    if (!string.IsNullOrEmpty(row.PartName))
                        parts[Enum.Parse<CharacterPartType>(CharacterPartTypeUtils.GetTypeNameFromShortcode(row.PartType))] = row.PartName;
            }

            var shape = new SerializedBlendShapeValues { BodyTypeValue = 0, BodySizeValue = 0, MuscleValue = 0 };
            if (!string.IsNullOrEmpty(bodyPreset))
            {
                SidekickBodyShapePreset body = SidekickBodyShapePreset.GetAll(db).FirstOrDefault(b => b.Name == bodyPreset);
                if (body == null)
                    return Fail($"no body preset '{bodyPreset}'");
                shape = new SerializedBlendShapeValues { BodyTypeValue = body.BodyType, BodySizeValue = body.BodySize, MuscleValue = body.Musculature };
            }

            // Start from the default color set, then let each chosen preset override its properties.
            SidekickColorSet defaultSet = SidekickColorSet.GetDefault(db);
            var rows = SidekickColorRow.GetAllBySet(db, defaultSet).ToDictionary(r => r.PtrColorProperty, r => new SerializedColorRow(r));
            var colorGroups = new[] { (ColorGroup.Species, speciesColors), (ColorGroup.Outfits, outfitColors), (ColorGroup.Attachments, attachmentColors) };
            foreach ((ColorGroup group, string presetName) in colorGroups)
            {
                if (string.IsNullOrEmpty(presetName))
                    continue;
                SidekickColorPreset preset = SidekickColorPreset.GetAllByColorGroup(db, group)
                    .FirstOrDefault(p => p.Name == presetName && (p.PtrSpecies == sp.ID || p.PtrSpecies <= 0));
                if (preset == null)
                    return Fail($"no {group} color preset '{presetName}' for {sp.Name}");
                foreach (SidekickColorPresetRow row in SidekickColorPresetRow.GetAllByPreset(db, preset))
                    rows[row.PtrColorProperty] = new SerializedColorRow(SidekickColorRow.CreateFromPresetColorRow(row));
            }

            var colorSet = new SerializedColorSet();
            colorSet.PopulateFromSidekickColorSet(defaultSet, sp);
            var character = new SerializedCharacter
            {
                Name = name,
                Species = sp.ID,
                Parts = parts.Select(p => new SerializedPart(p.Value, p.Key, "1")).ToList(),
                ColorSet = colorSet,
                ColorRows = rows.Values.OrderBy(r => r.ColorProperty).ToList(),
                BlendShapes = shape
            };
            // EmitDefaults: the tool's own writer drops zeros, and a dropped BodyType/Muscle reloads as 50.
            string yaml = new SerializerBuilder().EmitDefaults().Build().Serialize(character);
            Directory.CreateDirectory(Path.GetDirectoryName(skPath));
            File.WriteAllBytes(skPath, Encoding.ASCII.GetBytes(yaml));
            AssetDatabase.ImportAsset(skPath);
            return $"{{\"ok\":true,\"sk\":{Json(skPath)},\"parts\":{parts.Count},\"colorRows\":{rows.Count}}}";
        });
    }

    public static string Build(string skPath, string outFolder, string name, bool keepFacialBlendShapes, bool optimizeHierarchy, string exposedBones)
    {
        if (!File.Exists(skPath))
            return Fail($"no .sk at {skPath}");
        if (!outFolder.StartsWith("Assets/_Game/"))
            return Fail("outFolder must be under Assets/_Game/ (vendor folders are read-only)");
        SerializedCharacter character = new Deserializer().Deserialize<SerializedCharacter>(Encoding.ASCII.GetString(File.ReadAllBytes(skPath)));

        return WithDb(db =>
        {
            var warnings = new List<string>();
            var parts = new List<SkinnedMeshRenderer>();
            SerializedBlendShapeValues shape = character.BlendShapes ?? new SerializedBlendShapeValues();
            bool torsoUsesWrap = false;
            SerializedPart wrap = null;
            foreach (SerializedPart part in character.Parts)
            {
                if (part.PartType == CharacterPartType.Wrap)
                {
                    wrap = part;
                    continue;
                }
                SkinnedMeshRenderer renderer = LoadPart(db, part, warnings, out SidekickPart dto);
                if (renderer != null)
                    parts.Add(renderer);
                if (part.PartType == CharacterPartType.Torso && dto != null)
                    torsoUsesWrap = dto.UsesWrap;
            }
            // Same rule as the tool: the wrap only goes on a torso that uses it, on a feminine-leaning body.
            if (wrap != null && torsoUsesWrap && shape.BodyTypeValue > 0)
            {
                SkinnedMeshRenderer renderer = LoadPart(db, wrap, warnings, out _);
                if (renderer != null)
                    parts.Add(renderer);
            }
            if (parts.Count == 0)
                return Fail("no parts could be loaded: " + string.Join("; ", warnings));

            EnsureFolder(outFolder);
            Texture2D colorMap = WriteColorMap(db, character, $"{outFolder}/T_{name}ColorMap.png");
            Material material = WriteMaterial(colorMap, $"{outFolder}/{name}.mat");

            var runtime = new SidekickRuntime(Resources.Load<GameObject>(BaseModel), material, null, db)
            {
                BodyTypeBlendValue = shape.BodyTypeValue,
                BodySizeHeavyBlendValue = shape.BodySizeValue > 0 ? shape.BodySizeValue : 0,
                BodySizeSkinnyBlendValue = shape.BodySizeValue < 0 ? -shape.BodySizeValue : 0,
                MusclesBlendValue = shape.MuscleValue
            };
            // Built in a throwaway additive scene so the open scene (WorldMap) isn't dirtied. The window deletes scene objects
            // named "Combined Character": never build under that name.
            Scene active = SceneManager.GetActiveScene();
            Scene temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(temp);
            try
            {
                GameObject model = runtime.CreateCharacter(name, parts, true, true, null, false, keepFacialBlendShapes);
                return SaveModel(model, material, outFolder, name, optimizeHierarchy, exposedBones, warnings);
            }
            finally
            {
                if (active.IsValid())
                    SceneManager.SetActiveScene(active);
                EditorSceneManager.CloseScene(temp, true);
                EditorUtility.UnloadUnusedAssetsImmediate();
            }
        });
    }

    static SkinnedMeshRenderer LoadPart(DatabaseManager db, SerializedPart part, List<string> warnings, out SidekickPart dto)
    {
        dto = SidekickPart.SearchForByName(db, part.Name);
        GameObject model = dto?.GetPartModel();
        SkinnedMeshRenderer renderer = model != null ? model.GetComponentInChildren<SkinnedMeshRenderer>() : null;
        if (renderer == null)
            warnings.Add($"{part.PartType} '{part.Name}' not found (pack not imported?)");
        return renderer;
    }

    // The tool's per-character map: the shared 32x32 atlas with every saved color row painted in (2x2 texels per property).
    // Only the color map is per character, like the tool's export; metallic/smoothness/emission stay the shared maps.
    static Texture2D WriteColorMap(DatabaseManager db, SerializedCharacter character, string path)
    {
        var source = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorMap);
        var texture = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
        texture.SetPixels(source.GetPixels());
        Dictionary<int, SidekickColorProperty> properties = SidekickColorProperty.GetAll(db).ToDictionary(p => p.ID);
        foreach (SerializedColorRow row in character.ColorRows ?? new List<SerializedColorRow>())
        {
            if (!properties.TryGetValue(row.ColorProperty, out SidekickColorProperty property))
                continue;
            if (!ColorUtility.TryParseHtmlString("#" + row.MainColor, out Color color))
                continue;
            int u = property.U * 2, v = property.V * 2;
            texture.SetPixel(u, v, color);
            texture.SetPixel(u + 1, v, color);
            texture.SetPixel(u, v + 1, color);
            texture.SetPixel(u + 1, v + 1, color);
        }
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 32;
        importer.isReadable = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material WriteMaterial(Texture2D colorMap, string path)
    {
        var material = new Material(Resources.Load<Material>(BaseMaterial)) { name = Path.GetFileNameWithoutExtension(path) };
        material.SetTexture(ColorMapId, colorMap);
        return SaveOrReplace(material, path);
    }

    static string SaveModel(GameObject model, Material material, string outFolder, string name, bool optimizeHierarchy, string exposedBones, List<string> warnings)
    {
        var renderer = model.GetComponentInChildren<SkinnedMeshRenderer>();
        Mesh mesh = renderer.sharedMesh;
        mesh.name = name;
        mesh = SaveOrReplace(mesh, $"{outFolder}/{name}_Mesh.asset");
        MeshLodUtility.GenerateMeshLods(mesh, MeshLods);
        // No CPU copy at runtime; skinning and rendering don't need one.
        var serializedMesh = new SerializedObject(mesh);
        serializedMesh.FindProperty("m_IsReadable").boolValue = false;
        serializedMesh.ApplyModifiedPropertiesWithoutUndo();
        renderer.sharedMesh = mesh;
        renderer.sharedMaterial = material;
        renderer.updateWhenOffscreen = false;
        renderer.skinnedMotionVectors = false;
        renderer.localBounds = mesh.bounds;
        renderer.meshLodSelectionBias = MeshLodBias;

        var animator = model.GetComponent<Animator>();
        animator.applyRootMotion = false;
        // Safe without root motion (the mover moves the body): off-screen enemies skip their animation entirely.
        animator.cullingMode = AnimatorCullingMode.CullCompletely;
        // An avatar of this exact hierarchy (the shared SK_BaseModelAvatar lacks the parts' *_dyn_* bones and the moved
        // attach bones): same human map, this model's skeleton as its rest pose. Optimize Hierarchy needs it to skin.
        HumanDescription human = animator.avatar.humanDescription;
        human.skeleton = model.GetComponentsInChildren<Transform>(true)
            .Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale })
            .ToArray();
        Avatar avatar = AvatarBuilder.BuildHumanAvatar(model, human);
        if (!avatar.isValid || !avatar.isHuman)
            return Fail($"avatar for {name} is not a valid humanoid");
        avatar.name = name + "Avatar";
        animator.avatar = SaveOrReplace(avatar, $"{outFolder}/{name}_Avatar.asset");
        animator.Rebind();
        if (optimizeHierarchy)
        {
            string[] exposed = (exposedBones ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
            AnimatorUtility.OptimizeTransformHierarchy(model, exposed);
        }

        string prefabPath = $"{outFolder}/{name}_Model.prefab";
        PrefabUtility.SaveAsPrefabAsset(model, prefabPath, out bool saved);
        if (!saved)
            return Fail($"could not save {prefabPath}");
        // Only what this wrote: SaveAssets() would also write unrelated dirty assets (e.g. the WorldMap terrain).
        foreach (Object asset in new Object[] { mesh, material, animator.avatar })
            AssetDatabase.SaveAssetIfDirty(asset);
        return $"{{\"ok\":true,\"prefab\":{Json(prefabPath)},\"vertices\":{mesh.vertexCount},\"bones\":{renderer.bones.Length},\"blendShapes\":{mesh.blendShapeCount},\"warnings\":{JsonList(warnings)}}}";
    }

    // Overwrites in place so the asset keeps its GUID and every prefab pointing at it stays wired.
    static T SaveOrReplace<T>(T asset, string path) where T : Object
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
        EditorUtility.CopySerialized(asset, existing);
        existing.name = Path.GetFileNameWithoutExtension(path);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    // The DB connection is static and shared with the tool window: open it if needed, close it only if we opened it
    // and the window isn't using it (an open handle locks the .db on Windows).
    static string WithDb(Func<DatabaseManager, string> work)
    {
        var db = new DatabaseManager();
        bool opened = db.GetCurrentDbConnection() == null;
        if (opened)
            db.GetDbConnection(false);
        try
        {
            return work(db);
        }
        catch (Exception e)
        {
            return Fail(e.GetType().Name + ": " + e.Message);
        }
        finally
        {
            if (opened && !EditorWindow.HasOpenInstances<ModularCharacterWindow>())
                db.CloseConnection();
        }
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    static string Fail(string message) => $"{{\"ok\":false,\"error\":{Json(message)}}}";
    static string Json(string s) => s == null ? "null" : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    static string JsonList(IEnumerable<string> items) => "[" + string.Join(",", items.Select(Json)) + "]";
}
