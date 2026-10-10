#if USE_TIMELINE
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace TextStudio.Timeline.Editor
{
    // A clip references its composition directly. Flattening clip timing into layer envelopes
    // would change whole-composition blending, overlap ordering and glyph-range semantics.
    internal static class TextStudioTimelineAuthoring
    {
        const string CreateMenu = "Assets/Create/Text Studio/Animation/Timeline From Composed Effect";

        [MenuItem(CreateMenu, false, 108)]
        static void CreateFromSelection()
        {
            TextStudioComposedEffectAsset source = Selection.activeObject as TextStudioComposedEffectAsset;
            if (source == null) return;
            string path = EditorUtility.SaveFilePanelInProject("Create Text Studio Timeline", source.name + " Timeline", "playable", "Choose where to save the Timeline.");
            if (string.IsNullOrEmpty(path)) return;
            TimelineAsset timeline = CreateTimeline(source, path);
            Selection.activeObject = timeline;
            EditorGUIUtility.PingObject(timeline);
            AssetDatabase.OpenAsset(timeline);
        }

        [MenuItem(CreateMenu, true)]
        static bool CanCreateFromSelection() => Selection.activeObject is TextStudioComposedEffectAsset;

        internal static TimelineAsset CreateTimeline(TextStudioComposedEffectAsset source, string path)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            RequireNewAssetPath(path, ".playable");
            TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            bool persisted = false;
            try
            {
                timeline.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(timeline, path);
                persisted = true;
                TextEffectTrack track = timeline.CreateTrack<TextEffectTrack>(null, "Text Effects");
                TimelineClip clip = track.CreateClip<TextEffectClip>();
                clip.displayName = source.name;
                clip.duration = 2d;
                ((TextEffectClip)clip.asset).effect = source;
                EditorUtility.SetDirty(clip.asset);
                EditorUtility.SetDirty(track);
                EditorUtility.SetDirty(timeline);
                AssetDatabase.SaveAssetIfDirty(timeline);
                Undo.RegisterCreatedObjectUndo(timeline, "Create Text Studio Timeline");
                return timeline;
            }
            catch
            {
                if (persisted) AssetDatabase.DeleteAsset(path);
                else Object.DestroyImmediate(timeline);
                throw;
            }
        }

        internal static TextStudioComposedEffectAsset CreateVariant(TextEffectClip clip, string path)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            if (clip.effect is not TextStudioComposedEffectAsset source)
                throw new ArgumentException("Assign a Composed Effect before creating a variant.", nameof(clip));
            RequireNewAssetPath(path, ".asset");
            TextStudioComposedEffectAsset variant = Object.Instantiate(source);
            bool persisted = false;
            try
            {
                variant.name = Path.GetFileNameWithoutExtension(path);
                SerializedObject serializedVariant = new SerializedObject(variant);
                serializedVariant.FindProperty("_id").stringValue = source.Id + "_" + Guid.NewGuid().ToString("N");
                serializedVariant.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(variant, path);
                persisted = true;
                AssetDatabase.SaveAssetIfDirty(variant);
                Undo.RegisterCreatedObjectUndo(variant, "Create Composed Effect Variant");
                Undo.RecordObject(clip, "Assign Composed Effect Variant");
                clip.effect = variant;
                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssetIfDirty(clip);
                return variant;
            }
            catch
            {
                if (persisted) AssetDatabase.DeleteAsset(path);
                else Object.DestroyImmediate(variant);
                throw;
            }
        }

        static void RequireNewAssetPath(string path, string extension)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase) ||
                !AssetDatabase.IsValidFolder(Path.GetDirectoryName(path)?.Replace('\\', '/')) ||
                File.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new ArgumentException("Choose a new asset path in an existing Assets folder.", nameof(path));
        }
    }
}
#endif
