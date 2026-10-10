using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEngine;

namespace TextStudio.Editor
{
    internal sealed class TextStudioEffectCodePreview
    {
        public string Name = string.Empty;
        public string Id = string.Empty;
        public int LayerCount;
        public int BundledAssetCount;
        public int RequiredEffectCount;
        public string WriterVersion = string.Empty;
        public string Summary = string.Empty;
    }

    internal static class TextStudioEffectCodeImporter
    {
        internal static bool TryCreatePreview(
            TextStudioEffectCodePackage package,
            out TextStudioEffectCodePreview preview,
            out string error)
        {
            preview = null;
            error = string.Empty;
            if (!TryPreflight(package, out _, out error))
                return false;
            TextStudioEffectCodeAsset root = package.Root;
            int layerCount = root?.Composition?.Layers.Count ?? 0;
            preview = new TextStudioEffectCodePreview
            {
                Name = root != null ? root.Name : string.Empty,
                Id = root != null ? root.EffectId : string.Empty,
                LayerCount = layerCount,
                BundledAssetCount = Math.Max(0, package.Assets.Count - 1),
                RequiredEffectCount = package.RequiredEffects.Count,
                WriterVersion = package.WriterVersion
            };
            preview.Summary = BuildSummary(preview, root);
            return true;
        }

        internal static bool TryImport(
            TextStudioEffectCodePackage package,
            string folder,
            string rootTagOverride,
            out TextStudioEffectDefinitionAsset importedRoot,
            out string error)
        {
            importedRoot = null;
            error = string.Empty;
            if (!TryPreflight(package, out Type[] types, out error))
                return false;
            if (!TryNormalizeAssetFolder(folder, out string normalizedFolder, out error))
                return false;
            if (!TryNormalizeTagOverride(rootTagOverride, out string normalizedTagOverride, out error))
                return false;

            UnityEngine.Object[] instances = new UnityEngine.Object[package.Assets.Count];
            string createdPath = string.Empty;
            string createdSettingsPath = string.Empty;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Import Text Studio Effect Code");
            try
            {
                for (int i = 0; i < package.Assets.Count; i++)
                {
                    ScriptableObject instance = ScriptableObject.CreateInstance(types[i]);
                    instance.name = string.IsNullOrWhiteSpace(package.Assets[i].Name)
                        ? types[i].Name
                        : package.Assets[i].Name;
                    instances[i] = instance;
                }

                for (int i = 0; i < package.Assets.Count; i++)
                {
                    TextStudioEffectCodeAsset record = package.Assets[i];
                    if (record.Kind == TextStudioEffectCodeAssetKind.Composed)
                        PopulateComposition((TextStudioComposedEffectAsset)instances[i], record.Composition, instances);
                    else
                        EditorJsonUtility.FromJsonOverwrite(record.Json, instances[i]);
                }

                PatchSerializedReferences(package, instances);
                Dictionary<string, string> renamedIds = AssignUniqueEffectIds(
                    instances,
                    package.RootIndex,
                    normalizedTagOverride);
                RewriteLocalEffectReferences(instances, renamedIds);

                importedRoot = instances[package.RootIndex] as TextStudioEffectDefinitionAsset;
                if (importedRoot == null)
                    throw new InvalidOperationException("The root object in this code is not a Text Studio effect definition.");

                EnsureFolderExists(normalizedFolder);
                string fileName = SanitizeFileName(
                    string.IsNullOrWhiteSpace(importedRoot.name) ? importedRoot.Id : importedRoot.name);
                createdPath = AssetDatabase.GenerateUniqueAssetPath(normalizedFolder + "/" + fileName + ".asset");
                AssetDatabase.CreateAsset(importedRoot, createdPath);
                Undo.RegisterCreatedObjectUndo(importedRoot, "Import Text Studio Effect Code");
                for (int i = 0; i < instances.Length; i++)
                {
                    if (i == package.RootIndex)
                        continue;
                    AssetDatabase.AddObjectToAsset(instances[i], createdPath);
                    Undo.RegisterCreatedObjectUndo(instances[i], "Import Text Studio Effect Code Dependency");
                }
                AssetDatabase.SetMainObject(importedRoot, createdPath);

                TextStudioProjectSettings settings = TextStudioProjectSettingsEditorUtility.FindActiveOrFirst();
                if (settings == null)
                {
                    bool hadExistingSettings =
                        TextStudioProjectSettingsEditorUtility.FindAll().Count > 0;
                    settings =
                        TextStudioProjectSettingsEditorUtility.GetOrCreateCanonical();
                    if (!hadExistingSettings)
                    {
                        createdSettingsPath =
                            TextStudioProjectSettings.CanonicalAssetPath;
                    }
                }
                else
                {
                    Undo.RecordObject(settings, "Register Imported Text Studio Effect");
                }

                RegisterImportedAssets(settings, instances);
                EditorUtility.SetDirty(settings);
                EditorUtility.SetDirty(importedRoot);
                AssetDatabase.SaveAssetIfDirty(importedRoot);
                AssetDatabase.SaveAssetIfDirty(settings);
                TextStudioProjectSettingsEditorUtility.ActivateAndRebuildRegistry(settings);
                ValidateImportedCompositions(instances);
                Selection.activeObject = importedRoot;
                EditorGUIUtility.PingObject(importedRoot);
                Undo.CollapseUndoOperations(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                error = "Text Studio could not import this Effect Code. No partial effect was kept. " + exception.Message;
                if (!string.IsNullOrEmpty(createdPath))
                    AssetDatabase.DeleteAsset(createdPath);
                if (!string.IsNullOrEmpty(createdSettingsPath))
                    AssetDatabase.DeleteAsset(createdSettingsPath);
                for (int i = 0; i < instances.Length; i++)
                {
                    if (instances[i] != null && !EditorUtility.IsPersistent(instances[i]))
                        UnityEngine.Object.DestroyImmediate(instances[i]);
                }
                Undo.RevertAllDownToGroup(undoGroup);
                TextStudioProjectSettingsEditorUtility.ActivateDeterministic(true);
                importedRoot = null;
                return false;
            }
        }

        static bool TryPreflight(
            TextStudioEffectCodePackage package,
            out Type[] types,
            out string error)
        {
            types = null;
            error = string.Empty;
            if (package == null || package.Root == null)
            {
                error = "The Effect Code has no importable root effect.";
                return false;
            }

            TextStudioRegistry.EnsureInitialized();
            for (int i = 0; i < package.RequiredEffects.Count; i++)
            {
                TextStudioEffectCodeRequirement requirement = package.RequiredEffects[i];
                if (!TextStudioRegistry.Effects.TryGetQuiet(
                        requirement.EffectId,
                        out ITextStudioEffectDefinition definition))
                {
                    error = "This code requires the custom effect '" + requirement.EffectId +
                            "'. Install and register it before importing.";
                    return false;
                }
                if (!RequirementMatches(requirement, definition.GetType()))
                {
                    error = "The registered effect '" + requirement.EffectId +
                            "' has a different implementation than the one required by this code.";
                    return false;
                }
            }

            types = new Type[package.Assets.Count];
            for (int i = 0; i < package.Assets.Count; i++)
            {
                TextStudioEffectCodeAsset asset = package.Assets[i];
                Type type;
                if (asset.Kind == TextStudioEffectCodeAssetKind.Composed)
                {
                    type = typeof(TextStudioComposedEffectAsset);
                }
                else
                {
                    type = ResolveType(asset.TypeName, asset.AssemblyName);
                    if (type == null)
                    {
                        error = "This code requires the custom type '" + asset.TypeName +
                                "' from assembly '" + asset.AssemblyName + "'. Install that code before importing.";
                        return false;
                    }
                }
                if (!typeof(ScriptableObject).IsAssignableFrom(type) || type.IsAbstract)
                {
                    error = "The bundled type '" + type.FullName + "' is not an importable ScriptableObject.";
                    return false;
                }
                types[i] = type;

                for (int j = 0; j < asset.References.Count; j++)
                {
                    TextStudioEffectCodeReference reference = asset.References[j];
                    if (reference.LocalAssetIndex >= 0)
                        continue;
                    if (!TryResolveExternalReference(reference, out _))
                    {
                        error = "A project asset required by this code is missing (" + reference.TypeName +
                                ", GUID " + reference.Guid + ").";
                        return false;
                    }
                }
            }
            if (!typeof(TextStudioEffectDefinitionAsset).IsAssignableFrom(types[package.RootIndex]))
            {
                error = "The Effect Code root is not a Text Studio effect definition.";
                return false;
            }
            for (int i = 0; i < package.Assets.Count; i++)
            {
                TextStudioEffectCodeComposition composition = package.Assets[i].Composition;
                if (composition == null)
                    continue;
                for (int layerIndex = 0; layerIndex < composition.Layers.Count; layerIndex++)
                {
                    int playbackIndex = composition.Layers[layerIndex].CustomPlaybackIndex;
                    if (playbackIndex >= 0 && !typeof(EffectPlaybackAsset).IsAssignableFrom(types[playbackIndex]))
                    {
                        error = "A composed layer points to a bundled asset that is not a Text Studio Playback asset.";
                        return false;
                    }
                    int curveIndex = composition.Layers[layerIndex].CustomCurveAssetIndex;
                    if (curveIndex >= 0 && !typeof(TextStudioCurveProfile).IsAssignableFrom(types[curveIndex]))
                    {
                        error = "A composed layer points to a bundled asset that is not a Text Studio Curve asset.";
                        return false;
                    }
                    int writingStyleIndex =
                        composition.Layers[layerIndex].WritingStyleIndex;
                    if (writingStyleIndex >= 0 &&
                        !typeof(TextStudioWritingStyleAsset).IsAssignableFrom(
                            types[writingStyleIndex]))
                    {
                        error = "A composed Writing layer points to a bundled asset that is not a Text Studio Writing Style.";
                        return false;
                    }
                }
            }
            return true;
        }

        static void PopulateComposition(
            TextStudioComposedEffectAsset asset,
            TextStudioEffectCodeComposition composition,
            UnityEngine.Object[] instances)
        {
            asset.Initialize(composition.Id, null);
            for (int i = 0; i < composition.Layers.Count; i++)
            {
                TextStudioEffectCodeLayer layer = composition.Layers[i];
                if (layer.Kind == TextStudioCompositionLayerKind.Channel)
                {
                    if (!string.IsNullOrEmpty(layer.RenderPropertyId))
                        asset.AddRenderPropertyLayer(layer.RenderPropertyId);
                    else
                        asset.AddChannelLayer(layer.Channel);
                }
                else if (layer.Kind == TextStudioCompositionLayerKind.Writing)
                {
                    TextStudioWritingStyleAsset writingStyle =
                        layer.WritingStyleIndex >= 0
                            ? instances[layer.WritingStyleIndex] as
                                TextStudioWritingStyleAsset
                            : null;
                    asset.AddWritingLayer(writingStyle);
                }
                else
                    asset.AddEffectLayer(layer.EffectId);
            }

            SerializedObject serialized = new SerializedObject(asset);
            serialized.FindProperty("_id").stringValue = composition.Id;
            serialized.FindProperty("_authoringSpace").enumValueIndex = (int)composition.AuthoringSpace;
            serialized.FindProperty("_revealActiveBlendDuration").floatValue = composition.RevealActiveBlendDuration;
            SerializedProperty tag = serialized.FindProperty("_tagStack");
            tag.FindPropertyRelative("_enabled").boolValue = composition.TagEnabled;
            tag.FindPropertyRelative("_openingMarkup").stringValue = composition.OpeningMarkup;
            tag.FindPropertyRelative("_closingMode").enumValueIndex = (int)composition.ClosingMode;
            tag.FindPropertyRelative("_customClosingMarkup").stringValue = composition.CustomClosingMarkup;
            SerializedProperty layers = serialized.FindProperty("_layers");
            for (int i = 0; i < composition.Layers.Count; i++)
                WriteLayer(layers.GetArrayElementAtIndex(i), composition.Layers[i], instances);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WriteLayer(
            SerializedProperty property,
            TextStudioEffectCodeLayer layer,
            UnityEngine.Object[] instances)
        {
            property.FindPropertyRelative("_stableId").stringValue = Guid.NewGuid().ToString("N");
            property.FindPropertyRelative("_name").stringValue = layer.Name;
            property.FindPropertyRelative("_enabled").boolValue = layer.Enabled;
            property.FindPropertyRelative("_kind").enumValueIndex = (int)layer.Kind;
            property.FindPropertyRelative("_channel").enumValueIndex = (int)layer.Channel;
            SerializedProperty renderPropertyId = property.FindPropertyRelative("_renderPropertyId");
            if (renderPropertyId != null)
                renderPropertyId.stringValue = layer.RenderPropertyId ?? string.Empty;
            property.FindPropertyRelative("_effectId").stringValue = layer.EffectId;
            property.FindPropertyRelative("_effectParameters").stringValue = layer.EffectParameters;
            SerializedProperty parameterEntries = property.FindPropertyRelative("_effectParameterEntries");
            if (parameterEntries != null)
            {
                parameterEntries.arraySize = layer.Parameters.Count;
                for (int i = 0; i < layer.Parameters.Count; i++)
                {
                    TextStudioEffectCodeParameter source = layer.Parameters[i];
                    SerializedProperty destination = parameterEntries.GetArrayElementAtIndex(i);
                    destination.FindPropertyRelative("_name").stringValue = source.Name;
                    destination.FindPropertyRelative("_value").stringValue = source.Value;
                    destination.FindPropertyRelative("_operation").enumValueIndex = (int)source.Operation;
                    destination.FindPropertyRelative("_baseline").stringValue = source.Baseline;
                }
            }
            SerializedProperty snapshotInitialized =
                property.FindPropertyRelative("_effectParameterSnapshotInitialized");
            SerializedProperty snapshotEffectId =
                property.FindPropertyRelative("_effectParameterSnapshotEffectId");
            if (snapshotInitialized != null)
                snapshotInitialized.boolValue = layer.Parameters.Count > 0;
            if (snapshotEffectId != null)
                snapshotEffectId.stringValue = layer.Parameters.Count > 0 ? layer.EffectId : string.Empty;
            property.FindPropertyRelative("_phases").intValue = (int)layer.Phases;
            property.FindPropertyRelative("_playback").enumValueIndex = (int)layer.Playback;
            property.FindPropertyRelative("_customPlayback").objectReferenceValue =
                layer.CustomPlaybackIndex >= 0 ? instances[layer.CustomPlaybackIndex] : null;
            property.FindPropertyRelative("_curve").enumValueIndex = (int)layer.Curve;
            SerializedProperty customCurveAsset = property.FindPropertyRelative("_customCurveAsset");
            if (customCurveAsset != null)
            {
                customCurveAsset.objectReferenceValue = layer.CustomCurveAssetIndex >= 0
                    ? instances[layer.CustomCurveAssetIndex]
                    : null;
            }
            SerializedProperty writingStyle =
                property.FindPropertyRelative("_writingStyle");
            if (writingStyle != null)
            {
                writingStyle.objectReferenceValue = layer.WritingStyleIndex >= 0
                    ? instances[layer.WritingStyleIndex]
                    : null;
            }
            property.FindPropertyRelative("_customCurve").animationCurveValue = layer.CustomCurve;
            property.FindPropertyRelative("_vector").vector3Value = layer.Vector;
            property.FindPropertyRelative("_bottomLeftCorner").vector2Value = layer.BottomLeft;
            property.FindPropertyRelative("_topLeftCorner").vector2Value = layer.TopLeft;
            property.FindPropertyRelative("_topRightCorner").vector2Value = layer.TopRight;
            property.FindPropertyRelative("_bottomRightCorner").vector2Value = layer.BottomRight;
            property.FindPropertyRelative("_colorMode").enumValueIndex = (int)layer.ColorMode;
            property.FindPropertyRelative("_colorGradient").gradientValue = layer.ColorGradient;
            property.FindPropertyRelative("_scalar").floatValue = layer.Scalar;
            property.FindPropertyRelative("_unsigned").uintValue = layer.Unsigned;
            property.FindPropertyRelative("_intensity").floatValue = layer.Intensity;
            property.FindPropertyRelative("_timeOffset").floatValue = layer.TimeOffset;
            SerializedProperty timing = property.FindPropertyRelative("_timing");
            timing.FindPropertyRelative("charOffset").floatValue = layer.Timing.charOffset;
            timing.FindPropertyRelative("wordOffset").floatValue = layer.Timing.wordOffset;
            timing.FindPropertyRelative("lineOffset").floatValue = layer.Timing.lineOffset;
            timing.FindPropertyRelative("speed").floatValue = layer.Timing.speed;
            SerializedProperty sequence = property.FindPropertyRelative("_sequence");
            if (sequence != null)
            {
                sequence.FindPropertyRelative("Order").enumValueIndex = (int)layer.Sequence.Order;
                sequence.FindPropertyRelative("Direction").intValue = (int)layer.Sequence.Direction;
                sequence.FindPropertyRelative("Spread").floatValue = layer.Sequence.Spread;
                sequence.FindPropertyRelative("Seed").intValue = layer.Sequence.Seed;
            }
            SerializedProperty area = property.FindPropertyRelative("_area");
            area.FindPropertyRelative("_mode").enumValueIndex = (int)layer.Area.Mode;
            area.FindPropertyRelative("_groupSize").intValue = layer.Area.GroupSize;
            property.FindPropertyRelative("_blendMode").enumValueIndex = (int)layer.BlendMode;
            SerializedProperty surfaceMask = property.FindPropertyRelative("_surfaceMask");
            if (surfaceMask != null)
                surfaceMask.intValue = (int)layer.SurfaceMask;
            SerializedProperty spatialInfluence = property.FindPropertyRelative("_spatialInfluence");
            if (spatialInfluence != null && !string.IsNullOrEmpty(layer.SpatialInfluenceJson))
            {
                TextStudioInfluenceFieldStack stack =
                    JsonUtility.FromJson<TextStudioInfluenceFieldStack>(layer.SpatialInfluenceJson);
                if (stack != null)
                    spatialInfluence.boxedValue = stack;
            }
            SerializedProperty sourceColorSelector = property.FindPropertyRelative("_sourceColorSelector");
            if (sourceColorSelector != null &&
                !string.IsNullOrEmpty(layer.SourceColorSelectorJson))
            {
                TextStudioSourceColorSelector selector =
                    JsonUtility.FromJson<TextStudioSourceColorSelector>(
                        layer.SourceColorSelectorJson);
                if (selector != null)
                    sourceColorSelector.boxedValue = selector;
            }
            SerializedProperty logicalSelector = property.FindPropertyRelative("_selector");
            if (logicalSelector != null && !string.IsNullOrEmpty(layer.SelectorJson))
                logicalSelector.boxedValue = JsonUtility.FromJson<TextStudioApplicationSelector>(layer.SelectorJson);
        }

        static void PatchSerializedReferences(
            TextStudioEffectCodePackage package,
            UnityEngine.Object[] instances)
        {
            for (int i = 0; i < package.Assets.Count; i++)
            {
                TextStudioEffectCodeAsset asset = package.Assets[i];
                if (asset.References.Count == 0)
                    continue;
                SerializedObject serialized = new SerializedObject(instances[i]);
                for (int j = 0; j < asset.References.Count; j++)
                {
                    TextStudioEffectCodeReference reference = asset.References[j];
                    SerializedProperty property = serialized.FindProperty(reference.PropertyPath);
                    if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                        throw new InvalidOperationException(
                            "The installed custom effect schema no longer contains '" + reference.PropertyPath + "'.");
                    UnityEngine.Object value;
                    if (reference.LocalAssetIndex >= 0)
                        value = instances[reference.LocalAssetIndex];
                    else if (!TryResolveExternalReference(reference, out value))
                        throw new InvalidOperationException("A referenced project asset disappeared during import.");
                    property.objectReferenceValue = value;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        internal static bool TryNormalizeTagOverride(
            string value,
            out string normalized,
            out string error)
        {
            normalized = value != null ? value.Trim() : string.Empty;
            error = string.Empty;
            if (string.IsNullOrEmpty(normalized))
                return true;

            for (int i = 0; i < normalized.Length; i++)
            {
                char c = normalized[i];
                if (!char.IsWhiteSpace(c) && c != '<' && c != '>' && c != '/' && c != '=')
                    continue;
                error = "Tag names cannot contain spaces or the characters <, >, /, or =.";
                return false;
            }
            return true;
        }

        static Dictionary<string, string> AssignUniqueEffectIds(
            UnityEngine.Object[] instances,
            int rootIndex,
            string rootTagOverride)
        {
            HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, ITextStudioEffectDefinition> pair in TextStudioRegistry.Effects.GetAll())
                used.Add(pair.Key);
            HashSet<string> bundledIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> renamed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < instances.Length; i++)
            {
                if (instances[i] is not TextStudioEffectDefinitionAsset effect)
                    continue;
                string original = string.IsNullOrWhiteSpace(effect.Id) ? "sharedEffect" : effect.Id.Trim();
                if (!bundledIds.Add(original))
                    throw new InvalidOperationException(
                        "The Effect Code contains more than one bundled effect with the id '" + original + "'.");
            }

            for (int i = 0; i < instances.Length; i++)
            {
                if (i == rootIndex)
                    continue;
                AssignUniqueEffectId(instances[i], string.Empty, used, renamed);
            }
            if (rootIndex >= 0 && rootIndex < instances.Length)
                AssignUniqueEffectId(instances[rootIndex], rootTagOverride, used, renamed);
            return renamed;
        }

        static void AssignUniqueEffectId(
            UnityEngine.Object instance,
            string requestedId,
            HashSet<string> used,
            Dictionary<string, string> renamed)
        {
            if (instance is not TextStudioEffectDefinitionAsset effect)
                return;

            string original = string.IsNullOrWhiteSpace(effect.Id) ? "sharedEffect" : effect.Id.Trim();
            string preferred = string.IsNullOrEmpty(requestedId) ? original : requestedId;
            string unique = MakeUniqueId(preferred, used);
            used.Add(unique);
            if (string.Equals(original, unique, StringComparison.Ordinal))
                return;

            SerializedObject serialized = new SerializedObject(effect);
            SerializedProperty id = serialized.FindProperty("_id");
            if (id == null || id.propertyType != SerializedPropertyType.String)
            {
                throw new InvalidOperationException(
                    "The imported effect tag '" + original +
                    "' cannot be renamed safely because its custom type does not serialize an id.");
            }
            id.stringValue = unique;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            renamed[original] = unique;
        }

        static void RewriteLocalEffectReferences(
            UnityEngine.Object[] instances,
            Dictionary<string, string> renamedIds)
        {
            if (renamedIds.Count == 0)
                return;
            for (int i = 0; i < instances.Length; i++)
            {
                if (instances[i] is not TextStudioComposedEffectAsset composed)
                    continue;
                SerializedObject serialized = new SerializedObject(composed);
                SerializedProperty layers = serialized.FindProperty("_layers");
                for (int layerIndex = 0; layerIndex < layers.arraySize; layerIndex++)
                {
                    SerializedProperty effectId = layers.GetArrayElementAtIndex(layerIndex)
                        .FindPropertyRelative("_effectId");
                    if (renamedIds.TryGetValue(effectId.stringValue, out string renamed))
                        effectId.stringValue = renamed;
                }
                SerializedProperty tag = serialized.FindProperty("_tagStack");
                SerializedProperty opening = tag.FindPropertyRelative("_openingMarkup");
                SerializedProperty customClosing = tag.FindPropertyRelative("_customClosingMarkup");
                foreach (KeyValuePair<string, string> pair in renamedIds)
                {
                    opening.stringValue = RewriteMarkupEffectId(opening.stringValue, pair.Key, pair.Value);
                    customClosing.stringValue = RewriteMarkupEffectId(customClosing.stringValue, pair.Key, pair.Value);
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static string RewriteMarkupEffectId(string markup, string original, string replacement)
        {
            if (string.IsNullOrEmpty(markup))
                return markup;
            string openingPattern = "(<\\s*)" + Regex.Escape(original) + "(?=\\s|>)";
            string closingPattern = "(<\\s*/\\s*)" + Regex.Escape(original) + "(?=\\s*>)";
            string rewritten = Regex.Replace(
                markup,
                openingPattern,
                match => match.Groups[1].Value + replacement,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return Regex.Replace(
                rewritten,
                closingPattern,
                match => match.Groups[1].Value + replacement,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        static string MakeUniqueId(string original, HashSet<string> used)
        {
            if (!used.Contains(original))
                return original;
            for (int suffix = 2; suffix < 100000; suffix++)
            {
                string candidate = original + suffix;
                if (!used.Contains(candidate))
                    return candidate;
            }
            throw new InvalidOperationException("Text Studio could not create a unique effect id for '" + original + "'.");
        }

        static void RegisterImportedAssets(TextStudioProjectSettings settings, UnityEngine.Object[] instances)
        {
            settings.effectCatalog ??= new List<TextStudioEffectDefinitionAsset>();
            settings.curveCatalog ??= new List<TextStudioCurveProfile>();
            settings.playbackCatalog ??= new List<EffectPlaybackAsset>();
            for (int i = 0; i < instances.Length; i++)
            {
                if (instances[i] is TextStudioEffectDefinitionAsset effect && !settings.effectCatalog.Contains(effect))
                    settings.effectCatalog.Add(effect);
                if (instances[i] is TextStudioCurveProfile curve && !settings.curveCatalog.Contains(curve))
                    settings.curveCatalog.Add(curve);
                if (instances[i] is EffectPlaybackAsset playback && !settings.playbackCatalog.Contains(playback))
                    settings.playbackCatalog.Add(playback);
            }
        }

        static void ValidateImportedCompositions(UnityEngine.Object[] instances)
        {
            for (int i = 0; i < instances.Length; i++)
            {
                if (instances[i] is TextStudioComposedEffectAsset composed &&
                    !composed.TryCompileForValidation(out string error))
                    throw new InvalidOperationException(
                        "The imported composition is not valid in this project. " + error);
            }
        }

        static bool TryResolveExternalReference(
            TextStudioEffectCodeReference reference,
            out UnityEngine.Object resolved)
        {
            resolved = null;
            if (string.IsNullOrWhiteSpace(reference.Guid))
                return false;
            string path = AssetDatabase.GUIDToAssetPath(reference.Guid);
            if (string.IsNullOrEmpty(path))
                return false;
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                UnityEngine.Object candidate = assets[i];
                if (candidate == null ||
                    !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(candidate, out string _, out long localId) ||
                    localId != reference.LocalFileId)
                    continue;
                Type expectedType = ResolveType(reference.TypeName, reference.AssemblyName);
                if (expectedType != null && !expectedType.IsInstanceOfType(candidate))
                    continue;
                resolved = candidate;
                return true;
            }
            return false;
        }

        static Type ResolveType(string typeName, string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return null;
            IReadOnlyList<System.Reflection.Assembly> assemblies = UnityUtils.GetLoadedAssemblies();
            for (int i = 0; i < assemblies.Count; i++)
            {
                System.Reflection.Assembly assembly = assemblies[i];
                if (!string.IsNullOrWhiteSpace(assemblyName) &&
                    !string.Equals(assembly.GetName().Name, assemblyName, StringComparison.Ordinal))
                    continue;
                Type type = assembly.GetType(typeName, false, false);
                if (type != null)
                    return type;
            }
            return null;
        }

        static bool RequirementMatches(TextStudioEffectCodeRequirement requirement, Type type)
        {
            if (string.IsNullOrWhiteSpace(requirement.TypeName))
                return true;
            return type != null &&
                   string.Equals(type.FullName, requirement.TypeName, StringComparison.Ordinal) &&
                   (string.IsNullOrWhiteSpace(requirement.AssemblyName) ||
                    string.Equals(type.Assembly.GetName().Name, requirement.AssemblyName, StringComparison.Ordinal));
        }

        static bool TryNormalizeAssetFolder(string folder, out string normalized, out string error)
        {
            normalized = (folder ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
                normalized = "Assets/Text Studio/Shared Effects";
            if (!string.Equals(normalized, "Assets", StringComparison.Ordinal) &&
                !normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                error = "Choose a destination folder inside this project's Assets folder.";
                return false;
            }
            if (normalized.IndexOf("..", StringComparison.Ordinal) >= 0)
            {
                error = "The destination folder contains an invalid parent path.";
                return false;
            }
            return true;
        }

        static void EnsureFolderExists(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        static string BuildSummary(TextStudioEffectCodePreview preview, TextStudioEffectCodeAsset root)
        {
            string type = root != null && root.Kind == TextStudioEffectCodeAssetKind.Composed
                ? preview.LayerCount + (preview.LayerCount == 1 ? " composed layer" : " composed layers")
                : "Custom effect asset";
            string dependencies = preview.BundledAssetCount == 0 && preview.RequiredEffectCount == 0
                ? "Self-contained"
                : preview.BundledAssetCount + " bundled dependencies · " +
                  preview.RequiredEffectCount + " installed dependencies required";
            return type + "\n" + dependencies;
        }

        static string SanitizeFileName(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "Shared Effect" : value.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
                result = result.Replace(invalid[i], '_');
            return result;
        }
    }
}
