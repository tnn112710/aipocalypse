using System;
using System.Collections.Generic;
using System.IO;
using ImpossibleRobert.Common;
using TextStudio.BuiltIns;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TextStudio.Editor
{
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextAnimatorMigrationAssetScanner
    {
        static readonly string[] TextAssetExtensions =
        {
            ".txt",
            ".json",
            ".csv",
            ".ink",
            ".yarn",
            ".uss",
            ".uxml",
            ".asset",
            ".unity"
        };

        static readonly HashSet<string> s_strongTextAnimatorAngleTags =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "pend",
                "incr",
                "rot",
                "rainb",
                "slide",
                "slideh",
                "slidex",
                "slidev",
                "slidey",
                "waitfor",
                "waitinput",
                "short"
            };

        static readonly string[] KnownReferenceProperties =
        {
            "databaseEffects",
            "styleSheet",
            "sharedSettings",
            "actionsDatabase",
            "timingsScriptableBase",
            "globalEffectsDatabase",
            "globalActionsDatabase",
            "globalStyleSheet",
            "globalPlaybacksDatabase",
            "fallbackPlayback",
            "fallbackStateCurve",
            "settings.globalEffectsDatabase",
            "settings.globalActionsDatabase",
            "settings.globalStyleSheet",
            "settings.globalPlaybacksDatabase",
            "settings.fallbackPlayback",
            "settings.fallbackStateCurve"
        };

        public static bool TryApplyCandidate(TextAnimatorMigrationCandidate candidate, string convertedMarkup)
        {
            if (candidate == null ||
                !candidate.CanApply ||
                string.IsNullOrEmpty(candidate.PropertyPath) ||
                candidate.Target == null)
            {
                return false;
            }

            SerializedObject serializedObject = new SerializedObject(candidate.Target);
            SerializedProperty property = serializedObject.FindProperty(candidate.PropertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.String)
                return false;

            Undo.RecordObject(candidate.Target, "Apply Text Studio migration");
            property.stringValue = convertedMarkup ?? string.Empty;
            bool changed = serializedObject.ApplyModifiedProperties();
            if (changed)
            {
                EditorUtility.SetDirty(candidate.Target);
                Component component = candidate.Target as Component;
                if (component != null && component.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            }

            return changed;
        }

        static void AddObjectCandidates(
            UnityEngine.Object obj,
            TextAnimatorMigrationSourceKind sourceKind,
            List<TextAnimatorMigrationCandidate> candidates)
        {
            if (obj == null)
                return;

            TextAsset textAsset = obj as TextAsset;
            if (textAsset != null)
            {
                AddTextAssetCandidate(AssetDatabase.GetAssetPath(textAsset), candidates);
                return;
            }

            GameObject gameObject = obj as GameObject;
            if (gameObject != null)
            {
                AddGameObjectCandidates(gameObject, sourceKind, candidates);
                return;
            }

            Component component = obj as Component;
            if (component != null)
                AddComponentCandidate(component, sourceKind, candidates);
        }

        static void AddGameObjectCandidates(
            GameObject gameObject,
            TextAnimatorMigrationSourceKind sourceKind,
            List<TextAnimatorMigrationCandidate> candidates)
        {
            if (gameObject == null)
                return;

            Dictionary<int, string> textStudioMarkup = null;
            TextStudioText[] textStudioTexts = gameObject.GetComponentsInChildren<TextStudioText>(true);
            for (int i = 0; i < textStudioTexts.Length; i++)
            {
                int count = candidates.Count;
                AddTextStudioCandidate(
                    textStudioTexts[i],
                    sourceKind,
                    candidates,
                    HasTextAnimatorComponent(textStudioTexts[i].gameObject));
                if (candidates.Count > count)
                {
                    textStudioMarkup ??= new Dictionary<int, string>();
                    textStudioMarkup[textStudioTexts[i].gameObject.GetStableId()] = candidates[candidates.Count - 1].SourceMarkup;
                }
            }

            TMP_Text[] tmpTexts = gameObject.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < tmpTexts.Length; i++)
            {
                // An object whose Text Studio and TextMeshPro components hold the same markup is listed once.
                if (textStudioMarkup != null &&
                    textStudioMarkup.TryGetValue(tmpTexts[i].gameObject.GetStableId(), out string markup) &&
                    string.Equals(markup, tmpTexts[i].text, StringComparison.Ordinal))
                {
                    continue;
                }

                AddTmpCandidate(
                    tmpTexts[i],
                    sourceKind,
                    candidates,
                    HasTextAnimatorComponent(tmpTexts[i].gameObject));
            }
        }

        static void AddComponentCandidate(
            Component component,
            TextAnimatorMigrationSourceKind sourceKind,
            List<TextAnimatorMigrationCandidate> candidates)
        {
            if (IsTextAnimatorType(GetTypeName(component)))
            {
                AddCompanionTextCandidates(component.gameObject, sourceKind, candidates, true);
                return;
            }

            TextStudioText textStudioText = component as TextStudioText;
            if (textStudioText != null)
            {
                AddTextStudioCandidate(
                    textStudioText,
                    sourceKind,
                    candidates,
                    HasTextAnimatorComponent(textStudioText.gameObject));
                return;
            }

            TMP_Text tmpText = component as TMP_Text;
            if (tmpText != null)
            {
                AddTmpCandidate(
                    tmpText,
                    sourceKind,
                    candidates,
                    HasTextAnimatorComponent(tmpText.gameObject));
            }
        }

        static void AddCompanionTextCandidates(
            GameObject gameObject,
            TextAnimatorMigrationSourceKind sourceKind,
            List<TextAnimatorMigrationCandidate> candidates,
            bool textAnimatorContext)
        {
            if (gameObject == null)
                return;

            string textStudioMarkup = null;
            TextStudioText textStudioText = gameObject.GetComponent<TextStudioText>();
            if (textStudioText != null)
            {
                int count = candidates.Count;
                AddTextStudioCandidate(textStudioText, sourceKind, candidates, textAnimatorContext);
                if (candidates.Count > count)
                    textStudioMarkup = candidates[candidates.Count - 1].SourceMarkup;
            }

            TMP_Text tmpText = gameObject.GetComponent<TMP_Text>();
            if (tmpText != null && !string.Equals(textStudioMarkup, tmpText.text, StringComparison.Ordinal))
                AddTmpCandidate(tmpText, sourceKind, candidates, textAnimatorContext);
        }

        static void AddTextStudioCandidate(
            TextStudioText text,
            TextAnimatorMigrationSourceKind sourceKind,
            List<TextAnimatorMigrationCandidate> candidates,
            bool textAnimatorContext)
        {
            AddSerializedStringCandidate(text, "_markup", sourceKind, candidates, textAnimatorContext);
        }

        static void AddTmpCandidate(
            TMP_Text text,
            TextAnimatorMigrationSourceKind sourceKind,
            List<TextAnimatorMigrationCandidate> candidates,
            bool textAnimatorContext)
        {
            AddSerializedStringCandidate(text, "m_text", sourceKind, candidates, textAnimatorContext);
        }

        static void AddSerializedStringCandidate(
            UnityEngine.Object target,
            string propertyPath,
            TextAnimatorMigrationSourceKind sourceKind,
            List<TextAnimatorMigrationCandidate> candidates,
            bool textAnimatorContext)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.String)
                return;

            string value = property.stringValue ?? string.Empty;
            if (!LooksLikeTextAnimatorMarkup(value, textAnimatorContext))
                return;

            candidates.Add(new TextAnimatorMigrationCandidate(
                target.name + " / " + propertyPath,
                ResolveAssetPath(target, sourceKind),
                sourceKind,
                target,
                propertyPath,
                value));
        }

        static void AddTextAssetCandidate(string path, List<TextAnimatorMigrationCandidate> candidates)
        {
            if (string.IsNullOrWhiteSpace(path) || !IsSupportedTextAssetPath(path))
                return;

            TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (textAsset == null || !LooksLikeTextAnimatorMarkup(textAsset.text))
                return;

            candidates.Add(new TextAnimatorMigrationCandidate(
                textAsset.name,
                path,
                path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                    ? TextAnimatorMigrationSourceKind.Scene
                    : TextAnimatorMigrationSourceKind.TextAsset,
                null,
                string.Empty,
                textAsset.text));
        }

        static bool IsSupportedTextAssetPath(string path)
        {
            string extension = System.IO.Path.GetExtension(path);
            for (int i = 0; i < TextAssetExtensions.Length; i++)
            {
                if (string.Equals(extension, TextAssetExtensions[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        static void AddObjectArtifacts(
            UnityEngine.Object obj,
            TextAnimatorMigrationSourceKind sourceKind,
            List<TextAnimatorMigrationArtifactRecord> artifacts,
            Dictionary<string, TextAnimatorMigrationArtifactRecord> artifactKeys)
        {
            if (obj == null)
                return;

            GameObject gameObject = obj as GameObject;
            if (gameObject != null)
            {
                AddGameObjectArtifacts(gameObject, sourceKind, artifacts, artifactKeys);
                return;
            }

            Component component = obj as Component;
            if (component != null)
            {
                AddComponentArtifacts(component, sourceKind, artifacts, artifactKeys);
                return;
            }

            string path = AssetDatabase.GetAssetPath(obj);
            AddScriptableArtifact(path, obj, sourceKind, artifacts, artifactKeys);
        }

        static void AddGameObjectArtifacts(
            GameObject gameObject,
            TextAnimatorMigrationSourceKind sourceKind,
            List<TextAnimatorMigrationArtifactRecord> artifacts,
            Dictionary<string, TextAnimatorMigrationArtifactRecord> artifactKeys)
        {
            if (gameObject == null)
                return;

            Component[] components = gameObject.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
                AddComponentArtifacts(components[i], sourceKind, artifacts, artifactKeys);
        }

        static void AddComponentArtifacts(
            Component component,
            TextAnimatorMigrationSourceKind sourceKind,
            List<TextAnimatorMigrationArtifactRecord> artifacts,
            Dictionary<string, TextAnimatorMigrationArtifactRecord> artifactKeys)
        {
            if (component == null)
                return;

            string typeName = GetTypeName(component);
            string scriptGuid = GetScriptGuid(component);
            bool isSoundWriter = TextAnimatorMigrationCompanionUtility.IsTAnimSoundWriter(typeName, scriptGuid);
            bool isTextAnimator = IsTextAnimatorType(typeName);
            if (!isTextAnimator && !isSoundWriter)
                return;

            SerializedObject serializedObject = new SerializedObject(component);
            if (isSoundWriter)
            {
                AddArtifact(
                    artifacts,
                    artifactKeys,
                    TextAnimatorMigrationArtifactKind.SoundWriterComponent,
                    component.gameObject.name + " / " + component.GetType().Name,
                    string.Empty,
                    ResolveAssetPath(component, sourceKind),
                    sourceKind,
                    component,
                    "Text Animator sample audio player can be converted to Text Studio Audio Player settings. Source audio clips are referenced, not copied.");
                return;
            }

            bool isTypewriter = IsTypewriterType(typeName);
            AddArtifact(
                artifacts,
                artifactKeys,
                isTypewriter ? TextAnimatorMigrationArtifactKind.TypewriterComponent : TextAnimatorMigrationArtifactKind.TextAnimatorComponent,
                component.gameObject.name + " / " + component.GetType().Name,
                string.Empty,
                ResolveAssetPath(component, sourceKind),
                sourceKind,
                component,
                isTypewriter
                    ? "Text Studio reveals this text once it is added. Check start and skip behavior before removing this typewriter."
                    : "Add Text Studio to this text, check the result, then remove the Text Animator component.");

            if (isTypewriter && TextAnimatorMigrationCompanionUtility.HasTypewriterEventListeners(serializedObject))
            {
                AddArtifact(
                    artifacts,
                    artifactKeys,
                    TextAnimatorMigrationArtifactKind.TypewriterEventComponent,
                    component.gameObject.name + " / Typewriter Events",
                    string.Empty,
                    ResolveAssetPath(component, sourceKind),
                    sourceKind,
                    component,
                    TextAnimatorMigrationCompanionUtility.BuildTypewriterEventNote(serializedObject));
            }

            for (int i = 0; i < KnownReferenceProperties.Length; i++)
                AddReferencedArtifact(serializedObject, KnownReferenceProperties[i], sourceKind, artifacts, artifactKeys);

            AddTagArtifact(
                serializedObject,
                IsTypewriterActionType(typeName) ? TextAnimatorMigrationArtifactKind.Action : TextAnimatorMigrationArtifactKind.Other,
                component.name,
                ResolveAssetPath(component, sourceKind),
                sourceKind,
                component,
                artifacts,
                artifactKeys);
        }

        static void AddScriptableArtifact(
            string path,
            UnityEngine.Object obj,
            TextAnimatorMigrationSourceKind sourceKind,
            List<TextAnimatorMigrationArtifactRecord> artifacts,
            Dictionary<string, TextAnimatorMigrationArtifactRecord> artifactKeys)
        {
            if (obj == null)
                return;

            string typeName = GetTypeName(obj);
            if (!IsTextAnimatorType(typeName))
                return;

            SerializedObject serializedObject = new SerializedObject(obj);
            TextAnimatorMigrationArtifactKind kind = ClassifyScriptable(typeName, serializedObject);
            string note = BuildArtifactNote(kind);

            if (kind != TextAnimatorMigrationArtifactKind.Other)
            {
                AddArtifact(
                    artifacts,
                    artifactKeys,
                    kind,
                    obj.name,
                    ExtractTagId(serializedObject),
                    path,
                    sourceKind,
                    obj,
                    note);
            }

            AddTagArtifact(
                serializedObject,
                kind == TextAnimatorMigrationArtifactKind.Other ? ClassifyTagProvider(typeName) : kind,
                obj.name,
                path,
                sourceKind,
                obj,
                artifacts,
                artifactKeys);

            AddDatabaseEntries(serializedObject, path, sourceKind, kind, artifacts, artifactKeys);
            AddStyleEntries(serializedObject, path, sourceKind, artifacts, artifactKeys);
            for (int i = 0; i < KnownReferenceProperties.Length; i++)
                AddReferencedArtifact(serializedObject, KnownReferenceProperties[i], sourceKind, artifacts, artifactKeys);
        }

        static void AddReferencedArtifact(
            SerializedObject serializedObject,
            string propertyPath,
            TextAnimatorMigrationSourceKind sourceKind,
            List<TextAnimatorMigrationArtifactRecord> artifacts,
            Dictionary<string, TextAnimatorMigrationArtifactRecord> artifactKeys)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                return;

            UnityEngine.Object reference = property.objectReferenceValue;
            if (reference == null)
                return;

            string path = AssetDatabase.GetAssetPath(reference);
            TextAnimatorMigrationArtifactKind kind = ClassifyReferenceProperty(propertyPath, GetTypeName(reference));
            AddArtifact(
                artifacts,
                artifactKeys,
                kind,
                reference.name,
                string.Empty,
                path,
                sourceKind,
                reference,
                BuildArtifactNote(kind));

            AddScriptableArtifact(path, reference, sourceKind, artifacts, artifactKeys);
        }

        static void AddDatabaseEntries(
            SerializedObject serializedObject,
            string path,
            TextAnimatorMigrationSourceKind sourceKind,
            TextAnimatorMigrationArtifactKind databaseKind,
            List<TextAnimatorMigrationArtifactRecord> artifacts,
            Dictionary<string, TextAnimatorMigrationArtifactRecord> artifactKeys)
        {
            SerializedProperty data = serializedObject.FindProperty("data");
            if (data == null || !data.isArray)
                return;

            TextAnimatorMigrationArtifactKind entryKind = EntryKindForDatabase(databaseKind);
            for (int i = 0; i < data.arraySize; i++)
            {
                SerializedProperty entry = data.GetArrayElementAtIndex(i);
                UnityEngine.Object reference = entry.propertyType == SerializedPropertyType.ObjectReference
                    ? entry.objectReferenceValue
                    : null;
                if (reference != null)
                {
                    AddScriptableArtifact(AssetDatabase.GetAssetPath(reference), reference, sourceKind, artifacts, artifactKeys);
                    continue;
                }

                string tagId = ExtractTagId(entry);
                if (string.IsNullOrWhiteSpace(tagId))
                    continue;

                AddArtifact(
                    artifacts,
                    artifactKeys,
                    entryKind,
                    tagId,
                    tagId,
                    path,
                    sourceKind,
                    serializedObject.targetObject,
                    "Embedded Text Animator database entry is not imported automatically.");
            }
        }

        static void AddStyleEntries(
            SerializedObject serializedObject,
            string path,
            TextAnimatorMigrationSourceKind sourceKind,
            List<TextAnimatorMigrationArtifactRecord> artifacts,
            Dictionary<string, TextAnimatorMigrationArtifactRecord> artifactKeys)
        {
            SerializedProperty styles = serializedObject.FindProperty("styles");
            if (styles == null || !styles.isArray || styles.propertyType == SerializedPropertyType.String)
                return;

            for (int i = 0; i < styles.arraySize; i++)
            {
                SerializedProperty style = styles.GetArrayElementAtIndex(i);
                SerializedProperty tag = style.FindPropertyRelative("styleTag");
                if (tag == null || tag.propertyType != SerializedPropertyType.String || string.IsNullOrWhiteSpace(tag.stringValue))
                    continue;

                AddArtifact(
                    artifacts,
                    artifactKeys,
                    TextAnimatorMigrationArtifactKind.Style,
                    tag.stringValue,
                    tag.stringValue,
                    path,
                    sourceKind,
                    serializedObject.targetObject,
                    "Text Animator style can be expanded only after importing its style sheet.");
            }
        }

        static void AddTagArtifact(
            SerializedObject serializedObject,
            TextAnimatorMigrationArtifactKind kind,
            string label,
            string path,
            TextAnimatorMigrationSourceKind sourceKind,
            UnityEngine.Object target,
            List<TextAnimatorMigrationArtifactRecord> artifacts,
            Dictionary<string, TextAnimatorMigrationArtifactRecord> artifactKeys)
        {
            string tagId = ExtractTagId(serializedObject);
            if (string.IsNullOrWhiteSpace(tagId))
                return;

            AddArtifact(
                artifacts,
                artifactKeys,
                kind,
                string.IsNullOrEmpty(label) ? tagId : label,
                tagId,
                path,
                sourceKind,
                target,
                BuildArtifactNote(kind));
        }

        static void AddArtifact(
            List<TextAnimatorMigrationArtifactRecord> artifacts,
            Dictionary<string, TextAnimatorMigrationArtifactRecord> artifactKeys,
            TextAnimatorMigrationArtifactKind kind,
            string label,
            string tagId,
            string assetPath,
            TextAnimatorMigrationSourceKind sourceKind,
            UnityEngine.Object target,
            string note)
        {
            string key = kind + "|" + (assetPath ?? string.Empty) + "|" + (tagId ?? string.Empty) + "|" +
                         (target != null ? target.GetStableId().ToString() : label ?? string.Empty);
            if (artifactKeys.ContainsKey(key))
                return;

            TextAnimatorMigrationArtifactRecord artifact = new TextAnimatorMigrationArtifactRecord(
                kind,
                label,
                tagId,
                assetPath,
                sourceKind,
                target,
                note);
            artifactKeys.Add(key, artifact);
            artifacts.Add(artifact);
        }

        static string ExtractTagId(SerializedObject serializedObject)
        {
            string tagId = ExtractTagId(serializedObject.FindProperty("tagId"));
            if (!string.IsNullOrWhiteSpace(tagId))
                return tagId;

            return ExtractTagId(serializedObject.FindProperty("tagID"));
        }

        static string ExtractTagId(SerializedProperty property)
        {
            if (property == null)
                return string.Empty;

            if (property.propertyType == SerializedPropertyType.String)
                return property.stringValue ?? string.Empty;

            SerializedProperty tagId = property.FindPropertyRelative("tagId") ??
                                       property.FindPropertyRelative("tagID");
            if (tagId != null && tagId.propertyType == SerializedPropertyType.String)
                return tagId.stringValue ?? string.Empty;

            return string.Empty;
        }

        static TextAnimatorMigrationArtifactKind ClassifyScriptable(string typeName, SerializedObject serializedObject)
        {
            if (typeName.EndsWith(".AnimationsDatabase", StringComparison.Ordinal))
                return TextAnimatorMigrationArtifactKind.EffectDatabase;
            if (typeName.EndsWith(".Actions.ActionDatabase", StringComparison.Ordinal))
                return TextAnimatorMigrationArtifactKind.ActionDatabase;
            if (typeName.EndsWith(".PlaybacksDatabase", StringComparison.Ordinal))
                return TextAnimatorMigrationArtifactKind.PlaybackDatabase;
            if (typeName.EndsWith(".Styles.StyleSheetScriptable", StringComparison.Ordinal))
                return TextAnimatorMigrationArtifactKind.StyleSheet;
            if (typeName.EndsWith(".TextAnimatorSettings", StringComparison.Ordinal))
                return TextAnimatorMigrationArtifactKind.GlobalSettings;
            if (typeName.EndsWith(".AnimatorSettingsScriptable", StringComparison.Ordinal))
                return TextAnimatorMigrationArtifactKind.AnimatorSettings;
            if (typeName.EndsWith(".TypewriterSettingsScriptable", StringComparison.Ordinal))
                return TextAnimatorMigrationArtifactKind.TypewriterSettings;
            if (typeName.EndsWith(".TypingsTimingsScriptableBase", StringComparison.Ordinal) ||
                typeName.Contains(".Components.Typewriter.Scriptable.") ||
                typeName.Contains("TypingDelays", StringComparison.OrdinalIgnoreCase) ||
                typeName.Contains("TypingsTimings", StringComparison.OrdinalIgnoreCase))
            {
                return TextAnimatorMigrationArtifactKind.TimingProfile;
            }

            return ClassifyTagProvider(typeName);
        }

        static TextAnimatorMigrationArtifactKind ClassifyTagProvider(string typeName)
        {
            if (typeName.Contains(".Actions."))
                return TextAnimatorMigrationArtifactKind.Action;
            if (typeName.Contains(".Playback", StringComparison.OrdinalIgnoreCase) ||
                typeName.Contains("Playback", StringComparison.OrdinalIgnoreCase))
                return TextAnimatorMigrationArtifactKind.Playback;
            if (typeName.Contains(".Effects.", StringComparison.Ordinal) ||
                typeName.Contains("Effect", StringComparison.OrdinalIgnoreCase) ||
                typeName.Contains("Curve", StringComparison.OrdinalIgnoreCase))
                return TextAnimatorMigrationArtifactKind.Effect;
            return TextAnimatorMigrationArtifactKind.Other;
        }

        static TextAnimatorMigrationArtifactKind ClassifyReferenceProperty(string propertyPath, string typeName)
        {
            string normalizedProperty = propertyPath.StartsWith("settings.", StringComparison.Ordinal)
                ? propertyPath.Substring("settings.".Length)
                : propertyPath;
            switch (normalizedProperty)
            {
                case "databaseEffects":
                case "globalEffectsDatabase":
                    return TextAnimatorMigrationArtifactKind.EffectDatabase;
                case "actionsDatabase":
                case "globalActionsDatabase":
                    return TextAnimatorMigrationArtifactKind.ActionDatabase;
                case "globalPlaybacksDatabase":
                    return TextAnimatorMigrationArtifactKind.PlaybackDatabase;
                case "styleSheet":
                case "globalStyleSheet":
                    return TextAnimatorMigrationArtifactKind.StyleSheet;
                case "sharedSettings":
                    return typeName.Contains("Typewriter", StringComparison.OrdinalIgnoreCase)
                        ? TextAnimatorMigrationArtifactKind.TypewriterSettings
                        : TextAnimatorMigrationArtifactKind.AnimatorSettings;
                case "timingsScriptableBase":
                    return TextAnimatorMigrationArtifactKind.TimingProfile;
                case "fallbackPlayback":
                    return TextAnimatorMigrationArtifactKind.Playback;
                case "fallbackStateCurve":
                    return TextAnimatorMigrationArtifactKind.Effect;
            }

            return ClassifyTagProvider(typeName);
        }

        static TextAnimatorMigrationArtifactKind EntryKindForDatabase(TextAnimatorMigrationArtifactKind databaseKind)
        {
            switch (databaseKind)
            {
                case TextAnimatorMigrationArtifactKind.EffectDatabase:
                    return TextAnimatorMigrationArtifactKind.Effect;
                case TextAnimatorMigrationArtifactKind.ActionDatabase:
                    return TextAnimatorMigrationArtifactKind.Action;
                case TextAnimatorMigrationArtifactKind.PlaybackDatabase:
                    return TextAnimatorMigrationArtifactKind.Playback;
                default:
                    return TextAnimatorMigrationArtifactKind.Other;
            }
        }

        static string BuildArtifactNote(TextAnimatorMigrationArtifactKind kind)
        {
            switch (kind)
            {
                case TextAnimatorMigrationArtifactKind.EffectDatabase:
                case TextAnimatorMigrationArtifactKind.ActionDatabase:
                    return "Text Animator database is listed for impact review; entries are not imported automatically.";
                case TextAnimatorMigrationArtifactKind.PlaybackDatabase:
                    return "Used Text Animator playbacks are converted by the Import playbacks step of the migration window.";
                case TextAnimatorMigrationArtifactKind.StyleSheet:
                    return "Used styles are converted by the Import style aliases step of the migration window.";
                case TextAnimatorMigrationArtifactKind.Effect:
                    return "Custom Text Animator effect must be recreated as a Text Studio effect.";
                case TextAnimatorMigrationArtifactKind.Action:
                    return "Custom Text Animator action must be recreated as a Text Studio action.";
                case TextAnimatorMigrationArtifactKind.Playback:
                    return "Supported Text Animator playback timing can be imported as a native, registered Text Studio Playback asset.";
                case TextAnimatorMigrationArtifactKind.Style:
                    return "Used styles become Text Studio tag aliases through the Import style aliases step.";
                case TextAnimatorMigrationArtifactKind.AnimatorSettings:
                case TextAnimatorMigrationArtifactKind.TypewriterSettings:
                case TextAnimatorMigrationArtifactKind.GlobalSettings:
                case TextAnimatorMigrationArtifactKind.TimingProfile:
                case TextAnimatorMigrationArtifactKind.OtherSettings:
                    return "Default syntax already accepts supported Text Animator braces. The Import typing speed step converts supported character timings; review product-specific settings when testing.";
                case TextAnimatorMigrationArtifactKind.SoundWriterComponent:
                    return "Text Animator sample audio can be mapped to Text Studio Audio Player settings; sample clips are not copied into Text Studio.";
                case TextAnimatorMigrationArtifactKind.TypewriterEventComponent:
                    return "Text Animator typewriter events can be relayed through Text Studio Typewriter Event Relay.";
                default:
                    return "Text Animator artifact is not imported automatically.";
            }
        }

        static bool IsTextAnimatorType(string typeName)
        {
            return !string.IsNullOrEmpty(typeName) &&
                   typeName.StartsWith("Febucci.TextAnimator", StringComparison.Ordinal);
        }

        static bool IsTypewriterType(string typeName)
        {
            return typeName.IndexOf("Typewriter", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool IsTypewriterActionType(string typeName)
        {
            return typeName.IndexOf("Typewriter", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   typeName.IndexOf("Action", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool HasTextAnimatorComponent(GameObject gameObject)
        {
            if (gameObject == null)
                return false;

            Component[] components = gameObject.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (IsTextAnimatorType(GetTypeName(components[i])))
                    return true;
            }

            return false;
        }

        static string GetScriptGuid(Component component)
        {
            MonoBehaviour monoBehaviour = component as MonoBehaviour;
            if (monoBehaviour == null)
                return string.Empty;

            MonoScript script = MonoScript.FromMonoBehaviour(monoBehaviour);
            if (script == null)
                return string.Empty;

            string path = AssetDatabase.GetAssetPath(script);
            return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }

        // Project subclasses of Text Animator types are classified by the Text Animator type they extend.
        static string GetTypeName(UnityEngine.Object obj)
        {
            if (obj == null)
                return string.Empty;

            for (Type cursor = obj.GetType(); cursor != null; cursor = cursor.BaseType)
            {
                string fullName = cursor.FullName;
                if (fullName != null && fullName.StartsWith("Febucci.TextAnimator", StringComparison.Ordinal))
                    return fullName;
            }

            return obj.GetType().FullName ?? obj.GetType().Name;
        }

        static string ResolveAssetPath(UnityEngine.Object target, TextAnimatorMigrationSourceKind sourceKind)
        {
            Component component = target as Component;
            if (component != null && component.gameObject.scene.IsValid())
                return component.gameObject.scene.path ?? string.Empty;

            string path = AssetDatabase.GetAssetPath(target);
            if (!string.IsNullOrEmpty(path))
                return path;

            return sourceKind == TextAnimatorMigrationSourceKind.Scene ? string.Empty : path;
        }

        static bool LooksLikeTextAnimatorMarkup(string text, bool textAnimatorContext = false)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            IReadOnlyList<TextAnimatorMarkupToken> tokens = TextAnimatorMarkupScanner.Scan(text);
            for (int i = 0; i < tokens.Count; i++)
            {
                TextAnimatorMarkupToken token = tokens[i];
                if (token.IsBrace)
                    return true;

                if (textAnimatorContext)
                {
                    if (!IsNativeRichTextToken(token))
                        return true;

                    continue;
                }

                if (s_strongTextAnimatorAngleTags.Contains(token.Name))
                    return true;

                if (token.RawParameter.IndexOf("playback", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        static bool IsNativeRichTextToken(TextAnimatorMarkupToken token)
        {
            if (string.IsNullOrEmpty(token.Name))
                return false;

            bool hasParameter = !string.IsNullOrEmpty(token.RawParameter);
            return BuiltInStyles.IsNativeRichTextFormatting(token.Name, hasParameter) ||
                   BuiltInStyles.IsSelfContainedNativeRichTextFormatting(token.Name, hasParameter);
        }
    }
}
