using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal sealed partial class TextStudio2DStyleEditor
    {
        readonly Dictionary<string, bool> _disclosures = new Dictionary<string, bool>(StringComparer.Ordinal);
        readonly Dictionary<string, TextStudioApplicationSelector> _renderedSelectors = new Dictionary<string, TextStudioApplicationSelector>(StringComparer.Ordinal);
        Action _pendingRetainedRestore;
        IVisualElementScheduledItem _retainedRestoreTimeout;

        sealed class RetainedState
        {
            internal readonly Dictionary<ScrollView, Vector2> Scroll = new Dictionary<ScrollView, Vector2>();
            internal string FocusKey;
            internal int Cursor;
            internal int Selection;
        }

        RetainedState CaptureRetainedState()
        {
            RetainedState state = new RetainedState();
            if (_cards == null) return state;
            _cards.Query<Foldout>().ForEach(foldout =>
            {
                if (!string.IsNullOrEmpty(foldout.viewDataKey)) _disclosures[foldout.viewDataKey] = foldout.value;
            });
            VisualElement focus = _cards.Query<VisualElement>().Focused().Last();
            TextField text = focus as TextField ?? focus?.GetFirstAncestorOfType<TextField>();
            VisualElement keyed = text ?? focus;
            while (keyed != null && !(keyed.userData is RetainedControlKey)) keyed = keyed.parent;
            state.FocusKey = (keyed?.userData as RetainedControlKey)?.Value;
            if (text != null) { state.Cursor = text.cursorIndex; state.Selection = text.selectIndex; }
            for (VisualElement parent = _cards.parent; parent != null; parent = parent.parent)
                if (parent is ScrollView scroll) state.Scroll[scroll] = scroll.scrollOffset;
            return state;
        }

        void CacheRenderedSelectors()
        {
            _renderedSelectors.Clear();
            if (_serializedObject.isEditingMultipleObjects) return;
            UnityEditor.SerializedProperty layers = FindProperty("_layers");
            if (layers == null || !layers.isArray) return;
            for (int index = 0; index < layers.arraySize; index++)
            {
                UnityEditor.SerializedProperty layer = layers.GetArrayElementAtIndex(index);
                string layerId = GetItemId(layer.FindPropertyRelative("_itemId"));
                if (string.IsNullOrEmpty(layerId)) continue;
                TextStudioApplicationSelector selector = ((TextStudio2DLayer)layer.boxedValue).Selector;
                _renderedSelectors[layerId] = TextStudioApplicationSelector.Create(selector.GetNodesUnsafe(), selector.Root);
            }
        }

        RetainedState CaptureUndoRedoSelectorState()
        {
            RetainedState state = CaptureRetainedState();
            if (_serializedObject.isEditingMultipleObjects) return state;
            _serializedObject.UpdateIfRequiredOrScript();
            UnityEditor.SerializedProperty layers = FindProperty("_layers");
            if (layers == null || !layers.isArray) return state;
            for (int index = 0; index < layers.arraySize; index++)
            {
                UnityEditor.SerializedProperty layer = layers.GetArrayElementAtIndex(index);
                string layerId = GetItemId(layer.FindPropertyRelative("_itemId"));
                if (!_renderedSelectors.TryGetValue(layerId, out TextStudioApplicationSelector previous)) continue;
                TextStudioApplicationSelector current = ((TextStudio2DLayer)layer.boxedValue).Selector;
                if (RootExclusionWraps(current, previous))
                    state = CaptureSelectorRootState(index, true, current, state);
                else if (RootExclusionWraps(previous, current))
                    state = CaptureSelectorRootState(index, false, current, state);
            }
            return state;
        }

        static bool RootExclusionWraps(TextStudioApplicationSelector wrapped, TextStudioApplicationSelector unwrapped)
        {
            if (wrapped.Root == unwrapped.Root || !wrapped.TryGetNode(wrapped.Root, out TextStudioApplicationSelectorNode root) ||
                root.Kind != TextStudioSelectorNodeKind.Not || root.Invert || root.Left != unwrapped.Root) return false;
            HashSet<int> visited = new HashSet<int>();
            Stack<int> pending = new Stack<int>();
            pending.Push(unwrapped.Root);
            while (pending.Count > 0)
            {
                int index = pending.Pop();
                if (!visited.Add(index)) continue;
                if (!wrapped.TryGetNode(index, out TextStudioApplicationSelectorNode next) ||
                    !unwrapped.TryGetNode(index, out TextStudioApplicationSelectorNode previous) || !next.Equals(previous)) return false;
                if (previous.Kind == TextStudioSelectorNodeKind.Not || previous.Kind == TextStudioSelectorNodeKind.And ||
                    previous.Kind == TextStudioSelectorNodeKind.Or || previous.Kind == TextStudioSelectorNodeKind.Within)
                    pending.Push(previous.Left);
                if (previous.Kind == TextStudioSelectorNodeKind.And || previous.Kind == TextStudioSelectorNodeKind.Or ||
                    previous.Kind == TextStudioSelectorNodeKind.Within)
                    pending.Push(previous.Right);
            }
            return true;
        }

        RetainedState CaptureSelectorRootState(int layerIndex, bool wrapping, TextStudioApplicationSelector replacement, RetainedState state = null)
        {
            state ??= CaptureRetainedState();
            string layerId = GetItemId(FindProperty("_layers").GetArrayElementAtIndex(layerIndex).FindPropertyRelative("_itemId"));
            string owner = ExpansionKey + "/" + layerId + "/";
            string selection = owner + "text-studio-2d-style-selector";
            bool simpleAfterUnwrap = !wrapping && !_advancedSelectors.Contains(layerId) && SelectionMode(replacement) != 4;
            string[] sections = { "text-studio-2d-style-selector-node-list-", "text-studio-2d-style-selector-node-", "text-studio-2d-style-selector-scope-", "text-studio-2d-style-selector-child-", "ts-selector-condition-kind-" + layerIndex + "-", "ts-selector-condition-match-" + layerIndex + "-" };

            string RemapControlKey(string key)
            {
                if (string.IsNullOrEmpty(key) || !key.StartsWith(owner, StringComparison.Ordinal)) return key;
                if (wrapping && key.StartsWith(selection + "/control/", StringComparison.Ordinal) &&
                    key.IndexOf("/control/ts-2d-style-selector-mode-", StringComparison.Ordinal) < 0)
                    return owner + "text-studio-2d-style-selector-node-L" + key.Substring(selection.Length);
                string[] parts = key.Split('/');
                for (int index = 0; index < parts.Length; index++)
                {
                    for (int sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
                    {
                        string section = sections[sectionIndex];
                        if (!parts[index].StartsWith(section, StringComparison.Ordinal)) continue;
                        string path = parts[index].Substring(section.Length);
                        if (path == "root") path = string.Empty;
                        bool occurrence = true;
                        for (int character = 0; character < path.Length; character++)
                            if (path[character] != 'L' && path[character] != 'R') occurrence = false;
                        if (!occurrence) continue;
                        if (!wrapping && !path.StartsWith("L", StringComparison.Ordinal)) return null;
                        path = wrapping ? "L" + path : path.Substring(1);
                        parts[index] = section + PathKey(path);
                        break;
                    }
                }
                string remapped = string.Join("/", parts);
                string simpleControl = owner + "text-studio-2d-style-selector-node-root/control/";
                if (simpleAfterUnwrap && remapped.StartsWith(simpleControl, StringComparison.Ordinal))
                    return selection + "/control/" + remapped.Substring(simpleControl.Length);
                return remapped;
            }

            string RemapConditionKey(string key)
            {
                string prefix = layerId + "/";
                if (!key.StartsWith(prefix, StringComparison.Ordinal)) return key;
                string path = key.Substring(prefix.Length);
                if (!wrapping && !path.StartsWith("L", StringComparison.Ordinal)) return null;
                return prefix + (wrapping ? "L" + path : path.Substring(1));
            }

            RemapRetainedKeys(_disclosures, RemapControlKey);
            RemapRetainedKeys(_conditionGroupKinds, RemapConditionKey);
            RemapRetainedKeys(_conditionSelections, RemapConditionKey);
            state.FocusKey = RemapControlKey(state.FocusKey);
            return state;
        }

        static void RemapRetainedKeys<T>(Dictionary<string, T> values, Func<string, string> remap)
        {
            List<KeyValuePair<string, T>> previous = new List<KeyValuePair<string, T>>(values);
            values.Clear();
            foreach (KeyValuePair<string, T> pair in previous)
            {
                string key = remap(pair.Key);
                if (key != null) values[key] = pair.Value;
            }
        }

        sealed class RetainedControlKey
        {
            internal readonly string Value;
            internal RetainedControlKey(string value) { Value = value; }
        }

        void ConfigureRetainedKeys(VisualElement card, string layerId)
        {
            string owner = ExpansionKey + "/" + layerId + "/";
            card.Query<Foldout>().ForEach(foldout =>
            {
                string section = foldout.viewDataKey;
                if (string.IsNullOrEmpty(section)) return;
                Foldout parent = foldout.parent?.GetFirstAncestorOfType<Foldout>();
                string parentKey = parent?.viewDataKey;
                foldout.viewDataKey = section.IndexOf("selector-node-", StringComparison.Ordinal) >= 0 || string.IsNullOrEmpty(parentKey) ? owner + section : parentKey + "/" + section;
                if (_disclosures.TryGetValue(foldout.viewDataKey, out bool expanded)) foldout.SetValueWithoutNotify(expanded);
                foldout.RegisterValueChangedCallback(evt =>
                {
                    if (evt.target != foldout) return;
                    _disclosures[foldout.viewDataKey] = evt.newValue;
                    evt.StopPropagation();
                });
            });
            Dictionary<string, int> controlCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            card.Query<VisualElement>().ForEach(control =>
            {
                string key;
                if (control is PropertyField property)
                {
                    string section = control.GetFirstAncestorOfType<Foldout>()?.viewDataKey;
                    string propertyOwner = section != null && section.IndexOf("selector-node-", StringComparison.Ordinal) >= 0 ? section + "/" : owner;
                    key = propertyOwner + "property/" + RelativeBinding(property.bindingPath);
                }
                else if (control is TextField || control is DropdownField || control is EnumField)
                {
                    string section = control.GetFirstAncestorOfType<Foldout>()?.viewDataKey ?? owner;
                    string label = control is TextField text ? text.label : control is DropdownField dropdown ? dropdown.label : ((EnumField)control).label;
                    key = section + "/control/" + (string.IsNullOrEmpty(control.name) ? label : RelativeBinding(control.name));
                }
                else return;
                controlCounts.TryGetValue(key, out int count);
                controlCounts[key] = count + 1;
                control.userData = new RetainedControlKey(key + "/" + count);
            });
            card.RegisterCallback<GeometryChangedEvent>(HandleRetainedGeometryChanged);
            if (_pendingRetainedRestore != null) card.schedule.Execute(TryRestoreRetainedState);
        }

        static string RelativeBinding(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            int layer = path.IndexOf("_layers.Array.data[", StringComparison.Ordinal);
            int end = layer < 0 ? -1 : path.IndexOf(']', layer);
            return end < 0 ? path : path.Substring(end + 1);
        }

        void RestoreRetainedState(RetainedState state)
        {
            CancelRetainedRestore();
            if (state == null || _cards == null || state.Scroll.Count == 0 && string.IsNullOrEmpty(state.FocusKey)) return;
            _pendingRetainedRestore = () =>
            {
                if (_cards.panel == null || _cards.layout.height <= 0) return;
                bool focusRestored = string.IsNullOrEmpty(state.FocusKey);
                if (!focusRestored)
                {
                    VisualElement target = null;
                    _cards.Query<VisualElement>().ForEach(element =>
                    {
                        if (target == null && element.userData is RetainedControlKey key && key.Value == state.FocusKey)
                            target = element;
                    });
                    if (target != null && target.panel == _cards.panel && target.layout.height > 0)
                    {
                        TextField text = target as TextField ?? target.Q<TextField>();
                        VisualElement focusTarget = text ?? (target.focusable ? target : target.Q<VisualElement>(className: "unity-base-field__input"));
                        if (focusTarget != null)
                        {
                            focusTarget.Focus();
                            if (text != null)
                            {
                                text.cursorIndex = Mathf.Clamp(state.Cursor, 0, text.value?.Length ?? 0);
                                text.selectIndex = Mathf.Clamp(state.Selection, 0, text.value?.Length ?? 0);
                            }
                            VisualElement focused = focusTarget.Query<VisualElement>().Focused().Last();
                            focusRestored = focused != null && (focused == focusTarget || focusTarget.Contains(focused));
                        }
                    }
                }
                bool scrollRestored = true;
                foreach (KeyValuePair<ScrollView, Vector2> pair in state.Scroll)
                {
                    if (pair.Key.panel == null) continue;
                    pair.Key.scrollOffset = pair.Value;
                    if ((pair.Key.scrollOffset - pair.Value).sqrMagnitude > 0.25f) scrollRestored = false;
                }
                if (focusRestored && scrollRestored) CancelRetainedRestore();
            };
            _retainedRestoreTimeout = _cards.schedule.Execute(CancelRetainedRestore).StartingIn(5000);
            _cards.schedule.Execute(TryRestoreRetainedState);
        }

        void TryRestoreRetainedState() => _pendingRetainedRestore?.Invoke();

        void HandleRetainedGeometryChanged(GeometryChangedEvent evt) => TryRestoreRetainedState();

        void CancelRetainedRestore()
        {
            _pendingRetainedRestore = null;
            _retainedRestoreTimeout?.Pause();
            _retainedRestoreTimeout = null;
        }
    }
}
