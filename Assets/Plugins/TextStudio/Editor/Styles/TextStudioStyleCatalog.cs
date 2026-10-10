using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    internal sealed class TextStudioStyleCatalogEntry
    {
        internal string Id;
        internal string Title;
        internal string Description;
        internal string Category;
        internal string Keywords;
        internal string SampleText;
        internal string PackageName;
        internal string PreviewFileName;
        internal Object Asset;
        internal bool BuiltIn;
        internal object Data;
        internal string[] Capabilities = Array.Empty<string>();
    }

    /// <summary>The browser owns presentation and preview lifetime; each renderer owns its assets and application.</summary>
    [Serializable]
#if UNITY_6000_7_OR_NEWER
    // Catalog subscribers remain connected throughout the editor session.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal abstract class TextStudioStyleCatalogProvider
    {
        internal static event Action Changed;
        protected static void NotifyChanged() => Changed?.Invoke();
        [SerializeField] protected Object[] Owners;
        internal abstract string Key { get; }
        internal abstract string Title { get; }
        internal virtual string ApplyLabel => "Apply Style";
        internal virtual string AppliedMessage => "Style applied. Changes to the asset update every text using it.";
        internal abstract Object CurrentAsset { get; }

        protected TextStudioStyleCatalogProvider(Object[] owners)
        {
            Owners = owners != null ? (Object[])owners.Clone() : Array.Empty<Object>();
        }

        internal abstract List<TextStudioStyleCatalogEntry> GetEntries();
        internal abstract bool CanApply(TextStudioStyleCatalogEntry entry, out string reason);
        internal abstract void Apply(TextStudioStyleCatalogEntry entry);
        internal abstract TextStudioStylePreviewSession CreatePreview(TextStudioStyleCatalogEntry entry, string text, bool useTarget, int width, int height);
        internal virtual bool CanUnfreeze => false;
        internal virtual void Unfreeze() { }
        internal virtual string InitialText => string.Empty;

        protected T FirstOwner<T>() where T : Object
        {
            foreach (Object owner in Owners)
                if (owner != null && owner is T typed) return typed;
            return null;
        }

        internal static string AssetId(Object asset) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));

        internal static void MarkChanged(Object owner)
        {
            EditorUtility.SetDirty(owner);
            PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
            if (owner is Component component && component.gameObject.scene.IsValid() && !EditorSceneManager.IsPreviewScene(component.gameObject.scene) && !Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
        }
    }

    [Serializable]
    internal sealed class TextStudio2DStyleCatalogProvider : TextStudioStyleCatalogProvider
    {
        static TextStudio2DStyleCatalogProvider() => TextStudio2DStyles.Changed += NotifyChanged;
        internal TextStudio2DStyleCatalogProvider(Object[] owners) : base(owners) { }
        internal override string Key => "2D";
        internal override string Title => "2D Styles";
        TextStudioAppearanceBackendSummary BackendSummary => TextStudioAppearanceBackendSupport.EvaluateEditorTargets(Owners);
        internal override string ApplyLabel => BackendSummary.HasUnsupportedTargets ? "Assign Style" : base.ApplyLabel;
        internal override string AppliedMessage
        {
            get
            {
                string notice = TextStudioAppearanceBackendSupport.GetAppearanceAssignmentNotice(BackendSummary);
                return string.IsNullOrEmpty(notice) ? base.AppliedMessage : "Style assigned. " + notice;
            }
        }
        internal override Object CurrentAsset => FirstOwner<TextStudioText>() is TextStudioText hub && hub.AppearanceSource == TextStudioAppearanceSource.Style ? hub.AppearanceStyle : null;
        internal override string InitialText => FirstOwner<TextStudioText>() is TextStudioText hub ? hub.Markup : string.Empty;

        internal override List<TextStudioStyleCatalogEntry> GetEntries()
        {
            List<TextStudioStyleCatalogEntry> entries = new List<TextStudioStyleCatalogEntry>();
            HashSet<Object> builtIns = new HashSet<Object>();
            foreach (TextStudioVisualDesignEntry entry in TextStudioVisualDesignCatalog.Appearance)
            {
                TextStudio2DStyleAsset asset = TextStudioVisualDesignCatalog.LoadAsset<TextStudio2DStyleAsset>(entry);
                if (asset != null) builtIns.Add(asset);
                entries.Add(new TextStudioStyleCatalogEntry
                {
                    Id = entry.Id, Asset = asset, BuiltIn = true, Title = entry.Title, Description = entry.Description,
                    Category = ObjectNames.NicifyVariableName(entry.Collection.ToString()), Keywords = string.Join(" ", entry.SearchKeywords),
                    SampleText = entry.SampleText, PackageName = "com.wetzold.text-studio", PreviewFileName = entry.PreviewFileName
                });
            }
            HashSet<Object> included = new HashSet<Object>(builtIns);
            foreach (TextStudio2DStyleAsset asset in TextStudioAppearanceCatalogUtility.RegisteredAssets)
            {
                if (asset == null || !included.Add(asset)) continue;
                entries.Add(new TextStudioStyleCatalogEntry
                {
                    Id = AssetId(asset), Asset = asset, Title = asset.name, Description = "Reusable appearance registered as " + asset.Id + ".",
                    Category = "Custom", Keywords = asset.Id, SampleText = "Text Studio"
                });
            }
            return entries;
        }

        internal override bool CanApply(TextStudioStyleCatalogEntry entry, out string reason)
        {
            reason = entry == null ? "Choose a style to apply."
                : entry.Asset == null ? "This style asset is missing. Restore the package or choose another style." : string.Empty;
            if (reason.Length > 0) return false;
            TextStudioAppearanceBackendSummary summary = BackendSummary;
            if (summary.TargetCount > 0)
            {
                reason = TextStudioAppearanceBackendSupport.GetAppearanceAssignmentNotice(summary);
                return true;
            }
            reason = "Select a Text Studio text object to apply this style.";
            return false;
        }

        internal override void Apply(TextStudioStyleCatalogEntry entry)
        {
            if (!CanApply(entry, out _)) return;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply 2D Style");
            foreach (Object owner in Owners)
            {
                if (owner == null || owner is not TextStudioText hub) continue;
                Undo.RecordObject(hub, "Apply 2D Style");
                if (entry.BuiltIn) TextStudioVisualDesignCatalog.ConfigureAppearanceResources(hub, entry.Id);
                TextStudio2DStyleSelection.Assign(hub, (TextStudio2DStyleAsset)entry.Asset, TextStudioStyleSelectionMode.Catalog);
                MarkChanged(hub);
            }
            Undo.CollapseUndoOperations(group);
        }

        internal override TextStudioStylePreviewSession CreatePreview(TextStudioStyleCatalogEntry entry, string text, bool useTarget, int width, int height)
        {
            TextStudioText source = useTarget ? FirstOwner<TextStudioText>() : null;
            TMP_FontAsset font = source != null && source.TMPText != null
                ? source.TMPText.font : TextStudioVisualDesignCatalog.LoadAppearanceDisplayFont(entry.Id);
            return new TextStudioStylePreviewSession(text, font, width, height, false, (tmp, hub) =>
            {
                if (source != null)
                    for (int index = 0; index < source.AppearanceFontCount; index++)
                    {
                        TextStudioFontAppearanceData data = source.GetAppearanceFont(index);
                        if (data != null) hub.SetAppearanceFont(data);
                    }
                if (entry.BuiltIn) TextStudioVisualDesignCatalog.ConfigureAppearanceResources(hub, entry.Id);
                hub.SetAppearanceStyle((TextStudio2DStyleAsset)entry.Asset);
            });
        }
    }
}
