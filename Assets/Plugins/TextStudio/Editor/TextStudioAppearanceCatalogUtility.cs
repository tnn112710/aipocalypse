using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TextStudio.Editor
{
#if UNITY_6000_7_OR_NEWER
    // Registered authoring assets belong to the editor session.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class TextStudioAppearanceCatalogUtility
    {
        internal sealed class Catalog : TextStudioStyleCatalogEditor<TextStudio2DStyleAsset>
        {
            protected override ScriptableObject GetSettings(bool create) => create ? TextStudioProjectSettingsEditorUtility.GetOrCreateCanonical() : TextStudioProjectSettingsEditorUtility.FindCanonical();
            protected override List<TextStudio2DStyleAsset> GetEntries(ScriptableObject settings) => ((TextStudioProjectSettings)settings).appearanceStyleCatalog ??= new List<TextStudio2DStyleAsset>();
            protected override string GetId(TextStudio2DStyleAsset style) => style.Id;
            protected override TextStudioAppearanceApplyMode GetMode(TextStudio2DStyleAsset style) => style.DefaultApplyMode;
            protected override void SetIdentity(TextStudio2DStyleAsset style, string id, TextStudioAppearanceApplyMode mode) => style.SetTagIdentity(id, mode);
            protected override void Activate(ScriptableObject settings)
            {
                TextStudioProjectSettingsEditorUtility.ActivateAndRebuildRegistry((TextStudioProjectSettings)settings);
                TextStudioAppearanceAuthoringMetadata.Invalidate();
            }
        }

        internal static readonly Catalog Instance = new Catalog();
        internal static IReadOnlyList<TextStudio2DStyleAsset> RegisteredAssets => Instance.RegisteredAssets;
        internal static bool Contains(TextStudio2DStyleAsset asset) => Instance.Contains(asset);
        internal static bool TryRegister(TextStudio2DStyleAsset asset, string id, out string error) => Instance.TryRegister(asset, id, out error);
        internal static string SuggestAvailableId(string name, TextStudio2DStyleAsset asset = null) => Instance.SuggestAvailableId(name, asset);

        internal static string SuggestId(string name)
        {
            StringBuilder id = new StringBuilder();
            foreach (char c in name ?? string.Empty)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.' || c == ':') id.Append(char.ToLowerInvariant(c));
                else if (id.Length > 0 && id[id.Length - 1] != '-') id.Append('-');
            }
            if (id.Length == 0) id.Append("appearance");
            if (!char.IsLetter(id[0]) && id[0] != '_') id.Insert(0, "style-");
            return id.ToString().TrimEnd('-');
        }

        internal static void Register(TextStudio2DStyleAsset asset, string id)
        {
            if (!TryRegister(asset, id, out string error)) throw new InvalidOperationException(error);
        }

        internal static void Remove(TextStudio2DStyleAsset asset) => Instance.Remove(asset);
    }
}
