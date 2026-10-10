using System;
using System.Collections.Generic;
using UnityEditor;

namespace TextStudio.Editor
{
    internal sealed partial class TextStudio2DStyleEditor
    {
        internal static List<int> CollectSubtree(SerializedProperty layers, int index)
        {
            List<int> indices = new List<int>();
            if (index < 0 || index >= layers.arraySize) return indices;
            HashSet<string> identities = new HashSet<string>(StringComparer.Ordinal) { GetItemId(layers.GetArrayElementAtIndex(index).FindPropertyRelative("_itemId")) };
            indices.Add(index);
            bool changed;
            do
            {
                changed = false;
                for (int child = 0; child < layers.arraySize; child++)
                {
                    if (indices.Contains(child)) continue;
                    SerializedProperty layer = layers.GetArrayElementAtIndex(child);
                    if (!identities.Contains(GetItemId(layer.FindPropertyRelative("_parentId")))) continue;
                    indices.Add(child);
                    identities.Add(GetItemId(layer.FindPropertyRelative("_itemId")));
                    changed = true;
                }
            } while (changed);
            indices.Sort();
            return indices;
        }

        void DuplicateSubtree(SerializedProperty layers, int index)
        {
            List<int> subtree = CollectSubtree(layers, index);
            if (subtree.Count == 0) return;
            List<TextStudio2DLayer> copies = new List<TextStudio2DLayer>();
            Dictionary<string, string> remap = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (int child in subtree)
            {
                SerializedProperty property = layers.GetArrayElementAtIndex(child);
                copies.Add(((TextStudio2DLayer)property.boxedValue).Clone());
                VisitIdentityValues(property, value =>
                {
                    if (value.propertyPath.Contains("._itemId._value") && !string.IsNullOrEmpty(value.stringValue))
                        remap[value.stringValue] = TextStudioStackItemId.CreateUnique("layer").Value;
                });
            }
            int insertion = subtree[subtree.Count - 1] + 1;
            for (int i = 0; i < copies.Count; i++)
            {
                layers.InsertArrayElementAtIndex(insertion + i);
                SerializedProperty copy = layers.GetArrayElementAtIndex(insertion + i);
                copy.boxedValue = copies[i].WithState(copies[i].IsEnabled);
                VisitIdentityValues(copy, value => { if (remap.TryGetValue(value.stringValue, out string replacement)) value.stringValue = replacement; });
                TextStudio2DLayer layer = (TextStudio2DLayer)copy.boxedValue;
                if (layer.AppearanceActivation.Mode == TextStudioAppearanceActivationMode.Tagged)
                    copy.boxedValue = layer.WithAppearanceActivation(layer.AppearanceActivation.WithId(UniqueTagId(layers, layer.AppearanceActivation.Id + "-copy", insertion + i)));
                if (subtree[i] == index) _expandedLayerId = GetItemId(copy.FindPropertyRelative("_itemId"));
            }
            SerializedProperty exposures = FindProperty("_exposures");
            if (exposures == null) return;
            int count = exposures.arraySize;
            for (int i = 0; i < count; i++)
            {
                SerializedProperty exposure = exposures.GetArrayElementAtIndex(i);
                if (!remap.ContainsKey(GetItemId(exposure.FindPropertyRelative("_layerId")))) continue;
                object copy = exposure.boxedValue;
                int target = exposures.arraySize++;
                SerializedProperty duplicate = exposures.GetArrayElementAtIndex(target);
                duplicate.boxedValue = copy;
                VisitIdentityValues(duplicate, value => { if (remap.TryGetValue(value.stringValue, out string replacement)) value.stringValue = replacement; });
                SerializedProperty name = duplicate.FindPropertyRelative("_name");
                name.stringValue = UniqueExposureName(exposures, name.stringValue + "Copy", target);
            }
        }

        static void VisitIdentityValues(SerializedProperty property, Action<SerializedProperty> visit)
        {
            SerializedProperty child = property.Copy();
            SerializedProperty end = child.GetEndProperty();
            while (child.Next(true) && !SerializedProperty.EqualContents(child, end))
                if (child.propertyType == SerializedPropertyType.String && child.name == "_value") visit(child);
        }

        static void MoveSubtree(SerializedProperty layers, int index, int offset)
        {
            List<int> subtree = CollectSubtree(layers, index);
            if (subtree.Count == 0 || offset == 0) return;
            string parentId = GetItemId(layers.GetArrayElementAtIndex(index).FindPropertyRelative("_parentId"));
            int sibling = index + Math.Sign(offset);
            while (sibling >= 0 && sibling < layers.arraySize && (subtree.Contains(sibling) || GetItemId(layers.GetArrayElementAtIndex(sibling).FindPropertyRelative("_parentId")) != parentId)) sibling += Math.Sign(offset);
            if (sibling < 0 || sibling >= layers.arraySize) return;
            List<int> adjacent = CollectSubtree(layers, sibling);
            int boundary = offset < 0 ? adjacent[0] : adjacent[adjacent.Count - 1] + 1;
            List<TextStudio2DLayer> ordered = new List<TextStudio2DLayer>();
            List<TextStudio2DLayer> moved = new List<TextStudio2DLayer>();
            foreach (int child in subtree) moved.Add(((TextStudio2DLayer)layers.GetArrayElementAtIndex(child).boxedValue).Clone());
            for (int i = 0; i <= layers.arraySize; i++)
            {
                if (i == boundary) ordered.AddRange(moved);
                if (i < layers.arraySize && !subtree.Contains(i)) ordered.Add((TextStudio2DLayer)layers.GetArrayElementAtIndex(i).boxedValue);
            }
            for (int i = 0; i < ordered.Count; i++) layers.GetArrayElementAtIndex(i).boxedValue = ordered[i];
        }

        static void RemoveSubtree(SerializedProperty layers, int index)
        {
            List<int> subtree = CollectSubtree(layers, index);
            foreach (int child in subtree) ClearReferences(layers, GetItemId(layers.GetArrayElementAtIndex(child).FindPropertyRelative("_itemId")), child);
            for (int i = subtree.Count - 1; i >= 0; i--) layers.DeleteArrayElementAtIndex(subtree[i]);
        }

        internal static string UniqueTagId(SerializedProperty layers, string proposed, int except = -1)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < layers.arraySize; i++)
                if (i != except) ids.Add(((TextStudio2DLayer)layers.GetArrayElementAtIndex(i).boxedValue).AppearanceActivation.Id);
            string candidate = proposed;
            for (int suffix = 2; ids.Contains(candidate); suffix++) candidate = proposed + "-" + suffix;
            return candidate;
        }

        static string UniqueExposureName(SerializedProperty exposures, string proposed, int except = -1)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ref", "mode", "tint", "opacity" };
            for (int i = 0; i < exposures.arraySize; i++) if (i != except) names.Add(exposures.GetArrayElementAtIndex(i).FindPropertyRelative("_name").stringValue);
            string candidate = proposed;
            for (int suffix = 2; names.Contains(candidate); suffix++) candidate = proposed + suffix;
            return candidate;
        }
    }
}
