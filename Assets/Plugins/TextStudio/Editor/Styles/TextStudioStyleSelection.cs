using System;
using UnityEditor;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    internal enum TextStudioStyleSelectionMode
    {
        Unspecified = 0,
        Catalog = 1,
        Custom = 2
    }

    /// <summary>Editor selection memory. The component's existing asset reference remains the rendering authority.</summary>
    internal sealed class TextStudioStyleSelection
    {
        internal const string ModeProperty = "_styleSelectionMode";
        internal const string CatalogProperty = "_catalogStyleSelection";
        internal const string CustomProperty = "_customStyleSelection";

        readonly SerializedObject _owner;
        readonly string _activeProperty;
        readonly Func<Object, bool> _isCatalogAsset;

        internal TextStudioStyleSelection(SerializedObject owner, string activeProperty, Func<Object, bool> isCatalogAsset)
        {
            _owner = owner;
            _activeProperty = activeProperty;
            _isCatalogAsset = isCatalogAsset;
        }

        internal Object ActiveAsset => _owner.FindProperty(_activeProperty).objectReferenceValue;

        internal TextStudioStyleSelectionMode Mode
        {
            get
            {
                TextStudioStyleSelectionMode mode = (TextStudioStyleSelectionMode)_owner.FindProperty(ModeProperty).intValue;
                if ((mode == TextStudioStyleSelectionMode.Catalog || mode == TextStudioStyleSelectionMode.Custom) &&
                    Slot(mode).objectReferenceValue == ActiveAsset)
                    return mode;
                return ActiveAsset != null && _isCatalogAsset(ActiveAsset)
                    ? TextStudioStyleSelectionMode.Catalog : TextStudioStyleSelectionMode.Custom;
            }
        }

        // Callers record the component before a user action, then assign through its existing runtime API.
        internal void Remember()
        {
            RememberCurrent();
            _owner.ApplyModifiedPropertiesWithoutUndo();
        }

        internal Object Select(TextStudioStyleSelectionMode mode)
        {
            RememberCurrent();
            Object asset = Slot(mode).objectReferenceValue;
            _owner.FindProperty(ModeProperty).intValue = (int)mode;
            _owner.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        internal void Assign(TextStudioStyleSelectionMode mode, Object asset)
        {
            RememberCurrent();
            SetAssigned(mode, asset);
        }

        /// <summary>Marks the result of a compound operation whose previous selection was already remembered.</summary>
        internal void SetAssigned(TextStudioStyleSelectionMode mode, Object asset)
        {
            Slot(mode).objectReferenceValue = asset;
            _owner.FindProperty(ModeProperty).intValue = (int)mode;
            _owner.ApplyModifiedPropertiesWithoutUndo();
        }

        void RememberCurrent()
        {
            TextStudioStyleSelectionMode mode = Mode;
            Slot(mode).objectReferenceValue = ActiveAsset;
            _owner.FindProperty(ModeProperty).intValue = (int)mode;
        }

        SerializedProperty Slot(TextStudioStyleSelectionMode mode) => _owner.FindProperty(
            mode == TextStudioStyleSelectionMode.Catalog ? CatalogProperty : CustomProperty);

        internal static VisualElement CreateCatalogField(string name, out Label styleLabel, Button browseButton)
        {
            styleLabel = new Label
            {
                tooltip = "The live style selected from the catalog. Use Browse to choose a different style."
            };
            styleLabel.style.whiteSpace = WhiteSpace.NoWrap;
            styleLabel.style.overflow = Overflow.Hidden;
            styleLabel.style.textOverflow = TextOverflow.Ellipsis;
            VisualElement row = TextStudioInspectorElements.CreateAlignedControlRow("Style", styleLabel, name, browseButton);
            row.AddToClassList("ts-catalog-style");
            return row;
        }

        internal static Button CreateBrowseButton(Action click, string name)
        {
            Button button = TextStudioInspectorElements.CreateButton("Browse…",
                "Preview built-in and registered styles, then apply a catalog style.", click, true);
            button.name = name;
            return button;
        }

        internal static Button CreateDuplicateButton(Action click, string name)
        {
            Button button = TextStudioInspectorElements.CreateButton("Duplicate Style…",
                "Save an independent style asset and select it as Custom. Referenced resources remain shared.", click);
            button.name = name;
            return button;
        }
    }

    internal static class TextStudio2DStyleSelection
    {
        internal static TextStudioStyleSelection State(TextStudioText text) => new TextStudioStyleSelection(
            new SerializedObject(text), "_appearanceStyle", IsCatalogAsset);

        internal static string Choice(TextStudioText text) => text.AppearanceSource == TextStudioAppearanceSource.Inline
            ? "Inline" : State(text).Mode.ToString();

        internal static bool IsCatalogAsset(Object candidate)
        {
            if (candidate is not TextStudio2DStyleAsset asset) return false;
            foreach (TextStudio2DStyleAsset registered in TextStudioAppearanceCatalogUtility.RegisteredAssets)
                if (registered == asset) return true;
            string path = AssetDatabase.GetAssetPath(asset).Replace('\\', '/');
            foreach (TextStudioVisualDesignEntry entry in TextStudioVisualDesignCatalog.Appearance)
                if (path.EndsWith("/" + entry.AssetRelativePath, StringComparison.OrdinalIgnoreCase) &&
                    TextStudioVisualDesignCatalog.LoadAsset<TextStudio2DStyleAsset>(entry) == asset)
                    return true;
            return false;
        }

        internal static void Select(TextStudioText text, string choice)
        {
            TextStudioStyleSelection state = State(text);
            if (choice == "Inline")
            {
                state.Remember();
                text.AppearanceSource = TextStudioAppearanceSource.Inline;
                return;
            }
            TextStudioStyleSelectionMode mode = choice == "Catalog"
                ? TextStudioStyleSelectionMode.Catalog : TextStudioStyleSelectionMode.Custom;
            text.SetAppearanceStyle((TextStudio2DStyleAsset)state.Select(mode));
        }

        internal static void Assign(TextStudioText text, TextStudio2DStyleAsset asset, TextStudioStyleSelectionMode mode)
        {
            State(text).Assign(mode, asset);
            text.SetAppearanceStyle(asset);
        }
    }
}
