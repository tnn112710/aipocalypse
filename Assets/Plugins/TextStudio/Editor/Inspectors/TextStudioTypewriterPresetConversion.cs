using TextStudio.BuiltIns;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal static class TextStudioTypewriterPresetConversion
    {
        public static Button CreateSaveButton(System.Action save)
        {
            Button button = TextStudioInspectorElements.CreateButton(
                "Save as Custom Asset…",
                "Save these inline typewriter settings as a reusable preset, then switch Source to Custom using the saved asset.",
                save);
            button.name = "ts-typewriter-save-inline-asset";
            return button;
        }

        public static void SaveProfilePreset(TextStudioBehaviorProfile profile, string path)
        {
            if (profile == null || string.IsNullOrEmpty(path))
                return;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Save Inline Typewriter as Custom Asset");
            TextStudioTypewriterPreset preset = ScriptableObject.CreateInstance<TextStudioTypewriterPreset>();
            preset.Settings = TextStudioTypewriterSettings.NormalizeAuthoringSettings(profile.Typewriter.InlineSettings);
            AssetDatabase.CreateAsset(preset, path);
            Undo.RegisterCreatedObjectUndo(preset, "Create Typewriter Preset");
            AssetDatabase.SaveAssetIfDirty(preset);
            Undo.RecordObject(profile, "Assign Typewriter Preset");
            TextStudioBehaviorSettings settings = profile.BehaviorSettings;
            TextStudioTypewriterRecipe recipe = settings.Typewriter;
            recipe.Source = TextStudioTypewriterSource.Custom;
            recipe.CustomPreset = preset;
            settings.Typewriter = recipe;
            profile.ApplyBehaviorSettings(settings);
            EditorUtility.SetDirty(profile);
            Undo.CollapseUndoOperations(undoGroup);
            EditorGUIUtility.PingObject(preset);
        }

        public static bool CanConvert(TextStudioText text)
        {
            return text != null &&
                   !text.UsesBehaviorProfileTypewriter &&
                   TryGetPresetSettings(
                       text.TypewriterSource,
                       text.TypewriterPresetId,
                       text.TypewriterPreset,
                       out _);
        }

        public static bool CanConvert(TextStudioBehaviorProfile profile)
        {
            return profile != null &&
                   profile.Provides(TextStudioBehaviorDomain.Typewriter) &&
                   TryGetPresetSettings(
                       profile.TypewriterSource,
                       profile.TypewriterPresetId,
                       profile.TypewriterPreset,
                       out _);
        }

        public static bool ConvertToInline(TextStudioText text)
        {
            if (!CanConvert(text) ||
                !TryGetPresetSettings(
                    text.TypewriterSource,
                    text.TypewriterPresetId,
                    text.TypewriterPreset,
                    out TextStudioTypewriterSettings settings))
            {
                return false;
            }

            Undo.RecordObject(text, "Convert Typewriter Preset to Inline");
            text.ApplyTypewriterSettings(settings);
            EditorUtility.SetDirty(text);
            return true;
        }

        public static bool ConvertToInline(TextStudioBehaviorProfile profile)
        {
            if (!CanConvert(profile) ||
                !TryGetPresetSettings(
                    profile.TypewriterSource,
                    profile.TypewriterPresetId,
                    profile.TypewriterPreset,
                    out TextStudioTypewriterSettings settings))
            {
                return false;
            }

            Undo.RecordObject(profile, "Convert Typewriter Preset to Inline");
            TextStudioBehaviorSettings behaviorSettings = profile.BehaviorSettings;
            behaviorSettings.Typewriter = TextStudioTypewriterRecipe.Inline(settings);
            profile.ApplyBehaviorSettings(behaviorSettings);
            EditorUtility.SetDirty(profile);
            return true;
        }

        static bool TryGetPresetSettings(
            TextStudioTypewriterSource source,
            string builtInPresetId,
            TextStudioTypewriterPreset customPreset,
            out TextStudioTypewriterSettings settings)
        {
            if (source == TextStudioTypewriterSource.Custom)
            {
                if (customPreset == null)
                {
                    settings = default;
                    return false;
                }

                settings = TextStudioTypewriterSettings.NormalizeAuthoringSettings(customPreset.Settings);
                return true;
            }

            if (source == TextStudioTypewriterSource.BuiltIn)
            {
                TextStudioBuiltInTypewriterPreset? preset = BuiltInTypewriterPresets.ForId(builtInPresetId);
                settings = preset.HasValue
                    ? preset.Value.Settings
                    : BuiltInTypewriterPresets.Default.Settings;
                return true;
            }

            settings = default;
            return false;
        }
    }
}
