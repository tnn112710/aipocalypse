using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    /// <summary>Shared registration and identity controls; each renderer owns its typed settings asset.</summary>
    internal abstract class TextStudioStyleCatalogEditor<TStyle> where TStyle : ScriptableObject
    {
        protected abstract ScriptableObject GetSettings(bool create);
        protected abstract List<TStyle> GetEntries(ScriptableObject settings);
        protected abstract string GetId(TStyle style);
        protected abstract TextStudioAppearanceApplyMode GetMode(TStyle style);
        protected abstract void SetIdentity(TStyle style, string id, TextStudioAppearanceApplyMode mode);
        protected abstract void Activate(ScriptableObject settings);

        internal IReadOnlyList<TStyle> RegisteredAssets
        {
            get
            {
                ScriptableObject settings = GetSettings(false);
                return settings != null ? GetEntries(settings) : Array.Empty<TStyle>();
            }
        }

        internal bool Contains(TStyle asset)
        {
            IReadOnlyList<TStyle> entries = RegisteredAssets;
            for (int i = 0; i < entries.Count; i++) if (entries[i] == asset) return true;
            return false;
        }

        internal string SuggestAvailableId(string name, TStyle asset = null)
        {
            HashSet<string> used = new HashSet<string>(TextStudioIgnoreCaseComparer.Instance);
            foreach (TStyle entry in RegisteredAssets)
                if (entry != null && entry != asset) used.Add(GetId(entry));
            string seed = TextStudioAppearanceCatalogUtility.SuggestId(name);
            string candidate = seed;
            for (int suffix = 2; used.Contains(candidate); suffix++) candidate = seed + "-" + suffix;
            return candidate;
        }

        internal string RegistrationError(TStyle asset, string id)
        {
            if (asset == null || !EditorUtility.IsPersistent(asset)) return "Save this style as an asset before registering it.";
            if (!TextStudioAppearanceTagSyntax.IsValidId(id)) return "Assign a Tag ID starting with a letter or underscore.";
            IReadOnlyList<TStyle> entries = RegisteredAssets;
            for (int i = 0; i < entries.Count; i++)
            {
                TStyle existing = entries[i];
                if (existing != null && existing != asset && string.Equals(GetId(existing), id, StringComparison.OrdinalIgnoreCase))
                    return "Tag ID '" + id + "' is already used by '" + existing.name + "'. Choose another ID.";
            }
            return null;
        }

        internal bool TryRegister(TStyle asset, string id, out string error)
        {
            error = RegistrationError(asset, id);
            if (error != null) return false;
            ScriptableObject settings;
            try
            {
                settings = GetSettings(true);
            }
            catch (InvalidOperationException exception)
            {
                error = exception.Message;
                return false;
            }
            if (settings == null) { error = "Create the project style catalog first."; return false; }
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Register Style in Catalog");
            Undo.RecordObjects(new UnityEngine.Object[] { asset, settings }, "Register Style in Catalog");
            SetIdentity(asset, id, GetMode(asset));
            List<TStyle> entries = GetEntries(settings);
            if (!entries.Contains(asset)) entries.Add(asset);
            EditorUtility.SetDirty(asset);
            EditorUtility.SetDirty(settings);
            Activate(settings);
            AssetDatabase.SaveAssetIfDirty(asset);
            AssetDatabase.SaveAssetIfDirty(settings);
            Undo.CollapseUndoOperations(group);
            return true;
        }

        internal void Remove(TStyle asset)
        {
            ScriptableObject settings = GetSettings(false);
            if (settings == null || !GetEntries(settings).Contains(asset)) return;
            Undo.RecordObject(settings, "Remove Style From Catalog");
            GetEntries(settings).RemoveAll(entry => entry == asset);
            EditorUtility.SetDirty(settings);
            Activate(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
        }

        internal VisualElement CreateControls(TStyle asset)
        {
            VisualElement section = TextStudioInspectorElements.CreateSection("Style Catalog", "ts-appearance-tag-identity");
            TextField id = new TextField("Tag ID") { value = GetId(asset), isDelayed = true, tooltip = "Stable markup ID, independent of the asset filename. Register the style to use it throughout the project." };
            id.AddToClassList("unity-base-field__aligned");
            EnumField mode = new EnumField("Default Mode", GetMode(asset)) { tooltip = "Replace substitutes the enclosing style. Overlay adds this complete style over it. Each tag can override this mode." };
            mode.AddToClassList("unity-base-field__aligned");
            Label status = TextStudioInspectorElements.CreateMutedText(string.Empty);
            Button register = null, remove = null, suggest = null, copy = null;
            register = TextStudioInspectorElements.CreateButton("Register in Catalog", "Make this Tag ID available throughout the project. Registered custom styles also appear in the style catalog.", () =>
            {
                if (!TryRegister(asset, GetId(asset), out string error)) { status.text = error; return; }
                Refresh();
            });
            remove = TextStudioInspectorElements.CreateButton("Remove from Catalog", "Remove global tag lookup and custom catalog membership. Existing object assignments remain linked to this asset.", () => { Remove(asset); Refresh(); });
            suggest = TextStudioInspectorElements.CreateButton("Use Suggested ID", "Assign an available ID based on this asset name. Renaming the asset later will keep the ID.", () =>
            {
                Commit(SuggestAvailableId(asset.name, asset), GetMode(asset));
            });
            copy = TextStudioInspectorElements.CreateButton("Copy Tag", "Copy a matched opening and closing tag for this registered style.", () => EditorGUIUtility.systemCopyBuffer = TextStudioAppearanceAuthoringMetadata.CreateSnippet(GetId(asset)));
            void Refresh()
            {
                if (asset == null) return;
                string value = GetId(asset);
                id.SetValueWithoutNotify(value);
                mode.SetValueWithoutNotify(GetMode(asset));
                bool registered = Contains(asset);
                string error = RegistrationError(asset, value);
                string openingTag = TextStudioAppearanceTagSyntax.GetPreferredOpeningTag(value);
                register.SetEnabled(error == null && !registered);
                register.style.display = registered ? DisplayStyle.None : DisplayStyle.Flex;
                remove.style.display = registered ? DisplayStyle.Flex : DisplayStyle.None;
                suggest.style.display = string.IsNullOrEmpty(value) ? DisplayStyle.Flex : DisplayStyle.None;
                copy.SetEnabled(registered && error == null && !string.IsNullOrEmpty(openingTag));
                status.text = string.IsNullOrEmpty(value) ? "Assign a Tag ID to register this style for catalog browsing and markup." : error ?? (registered
                    ? string.IsNullOrEmpty(openingTag) ? "Registered, but both tag spellings are occupied. Rename the conflicting tag registration before inserting this style." : "Available in the project catalog using " + openingTag + "."
                    : "Ready to register. This ID is independent of the asset filename.");
            }
            void Commit(string value, TextStudioAppearanceApplyMode applyMode)
            {
                bool registered = Contains(asset);
                string error = registered ? RegistrationError(asset, value) : !string.IsNullOrEmpty(value) && !TextStudioAppearanceTagSyntax.IsValidId(value) ? "Invalid Tag ID. Start with a letter or underscore." : null;
                if (error != null) { status.text = error; return; }
                Undo.RecordObject(asset, "Change Style Tag Identity");
                SetIdentity(asset, value, applyMode);
                EditorUtility.SetDirty(asset);
                ScriptableObject settings = GetSettings(false);
                if (settings != null) Activate(settings);
                Refresh();
            }
            id.RegisterValueChangedCallback(evt => Commit(evt.newValue.Trim(), GetMode(asset)));
            mode.RegisterValueChangedCallback(evt => Commit(GetId(asset), (TextStudioAppearanceApplyMode)evt.newValue));
            section.Add(id);
            section.Add(mode);
            section.Add(status);
            section.Add(TextStudioInspectorElements.CreateButtonRow(register, remove, suggest, copy));
            section.RegisterCallback<AttachToPanelEvent>(_ => { Undo.undoRedoPerformed += Refresh; EditorApplication.projectChanged += Refresh; });
            section.RegisterCallback<DetachFromPanelEvent>(_ => { Undo.undoRedoPerformed -= Refresh; EditorApplication.projectChanged -= Refresh; });
            Refresh();
            return section;
        }
    }
}
