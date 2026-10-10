using System.Collections.Generic;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    [CustomEditor(typeof(TextStudioSurfaceGroup)), CanEditMultipleObjects]
    internal sealed class TextStudioSurfaceGroupEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-surface-group-inspector");
            List<int> indices = new List<int>();
            CommonReorderableListView<int> sources = new CommonReorderableListView<int>(indices,
                () => new ObjectField { objectType = typeof(TextStudioText), allowSceneObjects = true,
                    tooltip = "Additional lettering included in this backing. The owner's text is already included. Empty, duplicate and inactive entries are ignored." },
                (element, item, index) =>
                {
                    ObjectField field = (ObjectField)element;
                    field.Unbind();
                    field.BindProperty(serializedObject.FindProperty("_sources").GetArrayElementAtIndex(index));
                }, 24);
            sources.SetReorderable(false);
            sources.ListView.showBorder = false;
            sources.ListView.style.height = 50;
            sources.SetAddHandler((list, button) =>
            {
                serializedObject.Update();
                SerializedProperty array = serializedObject.FindProperty("_sources");
                if (array.arraySize >= 16) return;
                array.InsertArrayElementAtIndex(array.arraySize);
                array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = null;
                serializedObject.ApplyModifiedProperties();
            });
            sources.SetRemoveHandler(list =>
            {
                serializedObject.Update();
                SerializedProperty array = serializedObject.FindProperty("_sources");
                int index = list.SelectedIndex >= 0 ? list.SelectedIndex : array.arraySize - 1;
                if (index < 0 || index >= array.arraySize) return;
                array.GetArrayElementAtIndex(index).objectReferenceValue = null;
                array.DeleteArrayElementAtIndex(index);
                serializedObject.ApplyModifiedProperties();
            });
            root.Add(TextStudioInspectorElements.CreateSection("Shared Backing", "ts-surface-group-sources",
                TextStudioInspectorElements.CreateMutedText("Join these letter shapes into this text's Glyph Silhouette backplate. Each text keeps its own appearance."),
                new Label("Additional Text"), sources,
                TextStudioInspectorElements.CreateMutedText("Set Backplate Visibility on the Glyph Silhouette layer to follow each text's typewriter or keep the complete backing visible. World-space backing draws behind the listed lettering.")));
            HelpBox warning = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            root.Add(warning);
            void Refresh()
            {
                int count = serializedObject.FindProperty("_sources").arraySize;
                if (indices.Count != count)
                {
                    indices.Clear();
                    for (int index = 0; index < count; index++) indices.Add(index);
                    sources.ListView.style.height = count * 24 + 26;
                    sources.Refresh(Mathf.Min(sources.SelectedIndex, count - 1));
                }
                sources.ListView.allowAdd = count < 16;
                string message = null;
                foreach (Object selected in targets)
                {
                    TextStudioSurfaceGroup group = (TextStudioSurfaceGroup)selected;
                    if (group.SourceCount > 16) { message = "Use at most 16 additional text objects per surface group."; break; }
                    for (int index = 0; index < group.SourceCount; index++)
                    {
                        TextStudioText source = group.GetSource(index);
                        if (source == null || source.gameObject == group.gameObject) continue;
                        if (source.GetComponent<TMPro.TMP_Text>() == null)
                            message = "Additional text must use TextMeshPro with prepared font contours.";
                        else if (Mathf.Abs(Vector3.Dot(group.transform.forward, source.transform.forward)) < .9999f)
                            message = "Keep the text planes parallel so their letter shapes can share one backing.";
                    }
                }
                warning.text = message ?? string.Empty;
                warning.style.display = message == null ? DisplayStyle.None : DisplayStyle.Flex;
            }
            root.TrackSerializedObjectValue(serializedObject, _ => Refresh());
            Refresh();
            return root;
        }
    }
}
