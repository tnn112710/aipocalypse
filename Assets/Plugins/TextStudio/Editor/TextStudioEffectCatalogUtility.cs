using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TextStudio.Editor
{
    internal static class TextStudioEffectCatalogUtility
    {
        internal static bool Contains(TextStudioEffectDefinitionAsset effect)
        {
            if (effect == null)
                return false;

            TextStudioProjectSettings settings = FindSettings();
            if (settings == null || settings.effectCatalog == null)
                return false;

            for (int i = 0; i < settings.effectCatalog.Count; i++)
            {
                if (settings.effectCatalog[i] == effect)
                    return true;
            }

            return false;
        }

        internal static void Add(TextStudioEffectDefinitionAsset effect)
        {
            if (effect == null || string.IsNullOrWhiteSpace(effect.Id))
                return;

            if (!EditorUtility.IsPersistent(effect))
            {
                Debug.LogWarning("[TextStudio] Save the effect as an asset before adding it to the effect catalog.");
                return;
            }

            TextStudioProjectSettings settings = FindSettings();
            if (settings == null)
                settings = CreateSettingsNear(effect);
            if (settings == null)
                return;

            Undo.RecordObject(settings, "Add Text Studio Effect To Catalog");
            Prune(settings, effect, true);
            settings.effectCatalog.Add(effect);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Reload(settings);
        }

        internal static void Remove(TextStudioEffectDefinitionAsset effect)
        {
            if (effect == null)
                return;

            TextStudioProjectSettings settings = FindSettings();
            if (settings == null || settings.effectCatalog == null)
                return;

            Undo.RecordObject(settings, "Remove Text Studio Effect From Catalog");
            Prune(settings, effect, false);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Reload(settings);
        }

        internal static TextStudioProjectSettings FindSettings()
        {
            return TextStudioProjectSettingsEditorUtility.FindActiveOrFirst();
        }

        internal static TextStudioProjectSettings CreateSettingsNear(TextStudioEffectDefinitionAsset effect)
        {
            return TextStudioProjectSettingsEditorUtility.GetOrCreateCanonical();
        }

        static void Prune(
            TextStudioProjectSettings settings,
            TextStudioEffectDefinitionAsset effect,
            bool removeSameId)
        {
            if (settings.effectCatalog == null)
                settings.effectCatalog = new List<TextStudioEffectDefinitionAsset>();

            string id = effect != null ? effect.Id : string.Empty;
            for (int i = settings.effectCatalog.Count - 1; i >= 0; i--)
            {
                TextStudioEffectDefinitionAsset entry = settings.effectCatalog[i];
                bool remove = entry == null || entry == effect;
                if (!remove && removeSameId && !string.IsNullOrWhiteSpace(id))
                    remove = string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase);

                if (remove)
                    settings.effectCatalog.RemoveAt(i);
            }
        }

        static void Reload(TextStudioProjectSettings settings)
        {
            TextStudioProjectSettingsEditorUtility.ActivateAndRebuildRegistry(settings);
        }
    }
}
