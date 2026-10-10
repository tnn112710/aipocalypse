using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal sealed partial class TextStudio2DStyleEditor
    {
        readonly HashSet<string> _advancedSelectors = new HashSet<string>(StringComparer.Ordinal);

        internal static int SelectionMode(TextStudioApplicationSelector selector)
        {
            if (selector.IsTrivialAll) return 0;
            if (selector.NodeCount != 1 || !selector.TryGetNode(selector.Root, out TextStudioApplicationSelectorNode node)) return 4;
            if (node.Invert) return 4;
            return node.Kind switch
            {
                TextStudioSelectorNodeKind.None => 1,
                TextStudioSelectorNodeKind.Range => 2,
                TextStudioSelectorNodeKind.Tag => 3,
                TextStudioSelectorNodeKind.AppearanceTag => 3,
                _ => 4
            };
        }

        VisualElement CreateTypedTagSelector(SerializedProperty node)
        {
            string domain = null;
            string layerPrefix = FindProperty("_layers").propertyPath + ".Array.data[";
            int start = node.propertyPath.IndexOf(layerPrefix, StringComparison.Ordinal);
            if (start >= 0)
            {
                start += layerPrefix.Length;
                int end = node.propertyPath.IndexOf(']', start);
                TextStudio2DLayer[] layers = ReadSettings().CloneAuthoredLayers();
                if (end > start && int.TryParse(node.propertyPath.Substring(start, end - start), out int index) && index < layers.Length)
                {
                    int[] domains = TextStudioAppearanceDomains.Build(layers);
                    if (domains[index] >= 0) domain = layers[domains[index]].AppearanceActivation.Id;
                }
            }
            return new TextStudioAppearanceTagPicker(node, ReadSettings, key =>
            {
                if (Target is not TextStudioText hub) return string.Empty;
                CompiledTextStudioAppearancePlan plan = hub.CompiledAppearancePlan;
                TextStudioCompiledPlan source = hub.CompiledPlan;
                return plan != null && ReferenceEquals(plan.SourceIdentity, source) && plan.SourceVersion == source.SourceVersion
                    ? EmptyAppearanceTagSelectionMessage(plan, domain, key) : string.Empty;
            });
        }

        internal static string EmptyAppearanceTagSelectionMessage(CompiledTextStudioAppearancePlan plan, string ownerId, string selectedId)
        {
            if (plan?.SourceIdentity == null || string.IsNullOrEmpty(selectedId)) return string.Empty;
            bool intersectsDomain = false;
            TextStudioCompiledPlan source = plan.SourceIdentity;
            for (int ownerIndex = 0; ownerIndex < plan.Occurrences.Count; ownerIndex++)
            {
                TextStudioAppearanceOccurrence owner = plan.Occurrences[ownerIndex];
                if (!owner.IsActive || (ownerId == null ? ownerIndex != 0 : !string.Equals(owner.Id, ownerId, StringComparison.OrdinalIgnoreCase))) continue;
                for (int i = 0; i < source.AppearanceSpanCount; i++)
                {
                    TextStudioAppearanceSpan span = source.AppearanceSpans[i];
                    if (!string.Equals(span.Id, selectedId, StringComparison.OrdinalIgnoreCase)) continue;
                    int end = span.Start + span.Length;
                    if (span.Start >= owner.Domain.End || end <= owner.Domain.Start) continue;
                    intersectsDomain = true;
                    foreach (TextStudioAppearanceRange visible in owner.VisibleRanges)
                        if (span.Start < visible.End && end > visible.Start) return string.Empty;
                }
            }
            return intersectsDomain ? "No matching text is currently owned by this style. Inner Replace spans belong to their replacement." : string.Empty;
        }
    }

    internal sealed class TextStudioAppearanceTagPicker : VisualElement
    {
        readonly List<TextStudioTagBrowserItem> _matches = new List<TextStudioTagBrowserItem>();
        readonly ListView _list;
        readonly Label _status;
        readonly TextField _key;
        readonly DropdownField _kind;
        readonly SerializedProperty _node;
        readonly Func<TextStudio2DStyleSettings> _settings;
        readonly Func<string, string> _selectionStatus;
        IReadOnlyList<TextStudioTagBrowserItem> _available = Array.Empty<TextStudioTagBrowserItem>();
        bool _refreshing;
        int _generation = -1;
        ulong _revision;
        int _availableKind = -1;
        readonly ToolbarSearchField _search;

        internal TextStudioAppearanceTagPicker(SerializedProperty node, Func<TextStudio2DStyleSettings> settings, Func<string, string> selectionStatus = null)
        {
            _node = node.Copy(); _settings = settings; _selectionStatus = selectionStatus;
            _kind = new DropdownField("Tag Type", new List<string> { "Appearance style", "Effect tag" }, node.FindPropertyRelative("_kind").intValue == (int)TextStudioSelectorNodeKind.AppearanceTag ? 0 : 1) { tooltip = "Appearance styles select named appearance invocations. Effect tags select existing effect ranges." };
            _kind.AddToClassList("unity-base-field__aligned");
            _kind.AddToClassList("ts-appearance-responsive-field");
            _key = new TextField("Tag ID") { value = node.FindPropertyRelative("_key").stringValue, isDelayed = true, tooltip = "Keep or enter an ID, including an unavailable reference. Use the searchable list to select a known tag." };
            _key.AddToClassList("unity-base-field__aligned");
            _key.AddToClassList("ts-appearance-responsive-field");
            _status = TextStudioInspectorElements.CreateMutedText(string.Empty);
            Foldout choices = new Foldout { text = "Choose a Tag", value = false, viewDataKey = "tag-picker-choices" };
            _search = new ToolbarSearchField { tooltip = "Search appearance styles or effect tags by ID and origin." };
            _list = new ListView(_matches, 36, () => new Label { enableRichText = false }, (element, index) =>
            {
                TextStudioTagBrowserItem item = _matches[index];
                ((Label)element).text = item.Definition.DisplayName + "\n" + item.GroupDisplayName;
                element.tooltip = item.Definition.Description;
            }) { selectionType = SelectionType.Single };
            _list.style.height = 180;
            _list.selectionChanged += selection =>
            {
                if (_refreshing) return;
                foreach (object item in selection)
                {
                    if (item is not TextStudioTagBrowserItem entry) continue;
                    string id = entry.Definition.Id.TrimStart('$');
                    _key.SetValueWithoutNotify(id);
                    Commit();
                    break;
                }
            };
            choices.RegisterValueChangedCallback(evt => { if (evt.target == choices && evt.newValue) Refresh(); });
            _search.RegisterValueChangedCallback(_ => Refresh());
            _kind.RegisterValueChangedCallback(_ => { Commit(); Refresh(); });
            _key.RegisterValueChangedCallback(_ => Commit());
            Add(_kind); Add(_key); Add(_status);
            choices.Add(_search); choices.Add(_list); Add(choices);
            Refresh();
        }

        void Commit()
        {
            _node.serializedObject.UpdateIfRequiredOrScript();
            _node.FindPropertyRelative("_key").stringValue = _key.value;
            _node.FindPropertyRelative("_kind").intValue = _kind.index == 0 ? (int)TextStudioSelectorNodeKind.AppearanceTag : (int)TextStudioSelectorNodeKind.Tag;
            _node.serializedObject.ApplyModifiedProperties();
            RefreshStatus();
        }

        void Refresh()
        {
            _refreshing = true;
            try
            {
                int generation = TextStudioAppearanceAuthoringMetadata.Generation;
                ulong revision = TextStudioAppearanceAuthoringMetadata.RegistryRevision;
                if (_generation != generation || _revision != revision || _availableKind != _kind.index)
                {
                    _available = _kind.index == 0 ? TextStudioAppearanceAuthoringMetadata.ForSettings(_settings()) : TextStudioTagBrowserModel.Build().Items;
                    _generation = generation; _revision = revision; _availableKind = _kind.index;
                }
                _matches.Clear();
                foreach (TextStudioTagBrowserItem item in _available)
                {
                    if (_kind.index != 0 && item.Definition.Kind != TextStudioTagKind.TextStudioEffect) continue;
                    if (!string.IsNullOrEmpty(_search.value) && item.SearchText.IndexOf(_search.value, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    _matches.Add(item);
                }
                _list.Rebuild();
                RefreshStatus();
            }
            finally { _refreshing = false; }
        }

        void RefreshStatus()
        {
            bool found = false;
            foreach (TextStudioTagBrowserItem item in _available) if (string.Equals(item.Definition.Id.TrimStart('$'), _key.value, StringComparison.OrdinalIgnoreCase)) { found = true; break; }
            _status.text = string.IsNullOrEmpty(_key.value) ? "Choose a tag or enter its ID." : found ? "Selection intersects this appearance's active range. A Tag selection does not activate a style." : "This ID is currently unavailable. The authored value is retained.";
            if (_kind.index == 0 && found)
            {
                string contextual = _selectionStatus?.Invoke(_key.value);
                _status.text += string.IsNullOrEmpty(contextual) ? " Inner Replace spans belong to their replacement and are excluded from this style's selection." : " " + contextual;
            }
        }
    }
}
