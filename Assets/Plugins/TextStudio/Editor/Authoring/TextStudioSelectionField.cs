using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    /// <summary>Shared exclusive-range control for selector authoring surfaces.</summary>
#if UNITY_6000_7_OR_NEWER
    // Immutable selection choices stay available throughout the editor session.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class TextStudioSelectionField
    {
        static readonly List<string> Modes = new List<string> { "All", "None", "Range", "Tag", "Advanced" };

        internal static VisualElement Create(SerializedProperty selector, string label,
            TextStudioSelectorCapabilities capabilities = TextStudioSelectorCapabilities.LayoutStable)
        {
            VisualElement root = new VisualElement();
            if (selector == null) return root;
            DropdownField mode = new DropdownField(label, Modes, 0);
            mode.AddToClassList("unity-base-field__aligned");
            TextField range = new TextField("Range");
            range.AddToClassList("unity-base-field__aligned");
            range.tooltip = "A half-open interval of logical text units, for example 0..5 or ^5..^0.";
            TextField tag = new TextField("Tag");
            tag.AddToClassList("unity-base-field__aligned");
            HelpBox diagnostic = new HelpBox(string.Empty, HelpBoxMessageType.Error);
            PropertyField advanced = new PropertyField(selector, "Selector Tree");
            root.Add(mode);
            root.Add(range);
            root.Add(tag);
            root.Add(diagnostic);
            root.Add(advanced);

            void Set(TextStudioApplicationSelector value)
            {
                selector.serializedObject.UpdateIfRequiredOrScript();
                selector.boxedValue = value;
                selector.serializedObject.ApplyModifiedProperties();
                diagnostic.style.display = DisplayStyle.None;
                Refresh();
            }

            void Refresh()
            {
                selector.serializedObject.UpdateIfRequiredOrScript();
                TextStudioApplicationSelector value = (TextStudioApplicationSelector)selector.boxedValue;
                if (!TextStudioSelectorCompiler.TryCompile(in value, capabilities,
                        out _, out string error))
                {
                    diagnostic.text = error;
                    diagnostic.style.display = DisplayStyle.Flex;
                }
                else diagnostic.style.display = DisplayStyle.None;
                int index = 4;
                if (value.IsTrivialAll) index = 0;
                else if (value.NodeCount == 1 && value.TryGetNode(value.Root, out TextStudioApplicationSelectorNode node))
                {
                    index = node.Kind switch
                    {
                        TextStudioSelectorNodeKind.None => 1,
                        TextStudioSelectorNodeKind.Range => 2,
                        TextStudioSelectorNodeKind.Tag => 3,
                        _ => 4
                    };
                    if (index == 2)
                        range.SetValueWithoutNotify(node.UsesTextRange ? node.TextRange.ToString() :
                            node.Start + ".." + ((long)node.Start + node.Count));
                    if (index == 3) tag.SetValueWithoutNotify(node.Key);
                }
                mode.SetValueWithoutNotify(Modes[index]);
                range.style.display = index == 2 ? DisplayStyle.Flex : DisplayStyle.None;
                tag.style.display = index == 3 ? DisplayStyle.Flex : DisplayStyle.None;
                advanced.style.display = index == 4 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            mode.RegisterValueChangedCallback(evt =>
            {
                int index = Modes.IndexOf(evt.newValue);
                if (index == 4)
                {
                    range.style.display = DisplayStyle.None;
                    tag.style.display = DisplayStyle.None;
                    advanced.style.display = DisplayStyle.Flex;
                    return;
                }
                TextStudioApplicationSelectorNode node = index switch
                {
                    1 => TextStudioApplicationSelectorNode.None(),
                    2 => TextStudioApplicationSelectorNode.Range(new TextStudioTextRange(0, 1)),
                    3 => TextStudioApplicationSelectorNode.Tag("tag"),
                    _ => TextStudioApplicationSelectorNode.All()
                };
                Set(TextStudioApplicationSelector.Create(new[] { node }, 0));
            });
            range.RegisterValueChangedCallback(evt =>
            {
                if (!TextStudioTextRange.TryParse(evt.newValue, out TextStudioTextRange parsed, out string error))
                {
                    diagnostic.text = error;
                    diagnostic.style.display = DisplayStyle.Flex;
                    return;
                }
                Set(TextStudioApplicationSelector.Create(new[] { TextStudioApplicationSelectorNode.Range(parsed) }, 0));
            });
            tag.RegisterValueChangedCallback(evt =>
            {
                if (string.IsNullOrWhiteSpace(evt.newValue))
                {
                    diagnostic.text = "Enter a tag name.";
                    diagnostic.style.display = DisplayStyle.Flex;
                    return;
                }
                Set(TextStudioApplicationSelector.Create(new[] { TextStudioApplicationSelectorNode.Tag(evt.newValue) }, 0));
            });
            diagnostic.style.display = DisplayStyle.None;
            Refresh();
            return root;
        }

        internal static TextField CreateRangeNode(SerializedProperty node)
        {
            TextStudioApplicationSelectorNode value = (TextStudioApplicationSelectorNode)node.boxedValue;
            string initial = value.UsesTextRange ? value.TextRange.ToString() :
                value.Start + ".." + ((long)value.Start + value.Count);
            TextField field = new TextField("Range") { value = initial, name = "ts-selector-range-" + node.propertyPath };
            field.AddToClassList("unity-base-field__aligned");
            field.AddToClassList("ts-appearance-responsive-field");
            field.tooltip = "The end is exclusive. ^0 means the end of the current phrase.";
            field.RegisterValueChangedCallback(evt =>
            {
                if (!TextStudioTextRange.TryParse(evt.newValue, out TextStudioTextRange parsed, out string error))
                {
                    field.tooltip = error;
                    return;
                }
                node.serializedObject.UpdateIfRequiredOrScript();
                node.FindPropertyRelative("_textRange").boxedValue = parsed;
                node.FindPropertyRelative("_useTextRange").boolValue = true;
                node.serializedObject.ApplyModifiedProperties();
                field.tooltip = "The end is exclusive. ^0 means the end of the current phrase.";
            });
            return field;
        }
    }
}
