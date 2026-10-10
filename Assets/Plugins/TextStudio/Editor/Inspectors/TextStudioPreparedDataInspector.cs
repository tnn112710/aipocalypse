using System;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    /// <summary>Shared presentation for generated font and image appearance assets.</summary>
    internal static class TextStudioPreparedDataInspector
    {
        internal static VisualElement Root(string name)
        {
            StyleSheet style = CommonUITK.LoadStyleSheetFromAnchor("TextStudioPreparedDataInspector", "/Inspectors/TextStudioPreparedDataInspector.cs", "/Inspectors/TextStudioPreparedDataInspector.uss");
            return TextStudioInspectorElements.CreateRoot(name, style, "ts-prepared-data");
        }

        internal static VisualElement Summary(string title, bool valid, string error, string benefit)
        {
            VisualElement section = TextStudioInspectorElements.CreateSection(title, "prepared-summary");
            section.AddToClassList("ts-prepared-summary");
            section.EnableInClassList("ts-prepared-summary--attention", !valid);
            Label status = TextStudioInspectorElements.CreateStatus(valid ? "✓ Current" : "! Needs updating", valid ? CommonInspectorStatusType.Success : CommonInspectorStatusType.Warning);
            status.tooltip = valid ? "Source and saved data are valid. Text-specific coverage is checked in Runtime Preparation." : "Open Runtime Preparation to review the affected text and refresh its data.";
            section.Add(status);
            section.Add(Copy(benefit));
            if (!valid) section.Add(TextStudioInspectorElements.CreateHelpBox(error, HelpBoxMessageType.Warning));
            section.Add(TextStudioInspectorElements.CreateButton("Open Runtime Preparation", "See what is prepared in your project and prepare missing or outdated data.", TextStudioRuntimePreparationWindow.Open));
            return section;
        }

        internal static Label Copy(string text)
        {
            Label label = TextStudioInspectorElements.CreateMutedText(text);
            label.tooltip = text; label.AddToClassList("ts-prepared-copy"); return label;
        }

        internal static VisualElement Metrics(params (string label, string value, string tooltip)[] values)
        {
            VisualElement row = CommonUITK.CreateContainer("ts-prepared-metrics");
            foreach ((string label, string value, string tooltip) metric in values)
            {
                VisualElement tile = CommonUITK.CreateContainer("ts-prepared-metric");
                tile.tooltip = metric.tooltip;
                Label number = CommonUITK.CreateLabel(metric.value, "ts-prepared-metric-value"); number.tooltip = metric.tooltip; tile.Add(number);
                Label caption = Copy(metric.label); tile.Add(caption); row.Add(tile);
            }
            return row;
        }

        internal static VisualElement Source(string label, Object source)
        {
            VisualElement row = CommonUITK.CreateContainer("ts-prepared-source");
            ObjectField field = new ObjectField(label) { objectType = typeof(Object), value = source, allowSceneObjects = false, tooltip = "The source used to generate this data. Change preparation settings on your text object." };
            field.AddToClassList(BaseField<Object>.alignedFieldUssClassName);
            field.SetEnabled(false); row.Add(field);
            if (source != null) row.Add(TextStudioInspectorElements.CreateButton("Show source", "Select the original source asset in the Project window.", () => { Selection.activeObject = source; EditorGUIUtility.PingObject(source); }));
            return row;
        }

        internal static Foldout Details(string title, string key)
        {
            Foldout foldout = TextStudioInspectorElements.CreateFoldoutSection(title, key, false);
            foldout.tooltip = "Optional source and preparation details. These values are generated, not edited here.";
            return foldout;
        }

        internal static void Storage(VisualElement root, Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            bool shared = path.StartsWith(TextStudioAppearancePreparationProvider.GeneratedFolder + "/", StringComparison.Ordinal);
            Foldout location = Details("Storage", "prepared-storage");
            location.Add(Copy(shared ? "Stored in the shared project preparation folder." : "Stored outside the shared project folder. Supplied assets, including benchmark data, keep their original location and references."));
            location.Add(Copy(path));
            location.Add(Copy(path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0 ? "This asset is in Resources, so Unity includes it in player builds even without a direct reference. This does not mean it is always loaded into memory." : "This asset is included when referenced by build content or explicitly included by your content delivery system."));
            location.Add(Copy("New font and image preparation is saved to " + TextStudioAppearancePreparationProvider.GeneratedFolder + ". Existing assigned assets are updated in place."));
            root.Add(location);
        }
    }
}
