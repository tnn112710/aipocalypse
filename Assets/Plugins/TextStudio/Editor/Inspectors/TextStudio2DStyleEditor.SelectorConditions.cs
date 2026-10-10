using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal sealed partial class TextStudio2DStyleEditor
    {
        readonly Dictionary<string, TextStudioSelectorNodeKind> _conditionGroupKinds = new Dictionary<string, TextStudioSelectorNodeKind>(StringComparer.Ordinal);
        readonly Dictionary<string, int> _conditionSelections = new Dictionary<string, int>(StringComparer.Ordinal);

        sealed class SelectorCondition
        {
            internal readonly int Node;
            internal readonly string Path;
            internal SelectorCondition(int node, string path) { Node = node; Path = path; }
        }

        Foldout CreateSelectorConditionList(VisualElement parent, SerializedProperty nodes, int nodeIndex, int layerIndex, string path, HashSet<int> ancestors)
        {
            if (path.Length > 64 || nodeIndex < 0 || nodeIndex >= nodes.arraySize || !ancestors.Add(nodeIndex))
            {
                parent.Add(TextStudioInspectorElements.CreateStatus("This selector contains an invalid or cyclic branch.", CommonInspectorStatusType.Error));
                return null;
            }

            TextStudioApplicationSelector selector = CurrentSelector(layerIndex);
            TextStudioApplicationSelectorNode node = (TextStudioApplicationSelectorNode)nodes.GetArrayElementAtIndex(nodeIndex).boxedValue;
            string key = ConditionKey(layerIndex, path);
            TextStudioSelectorNodeKind combination = node.Kind == TextStudioSelectorNodeKind.Or || path.Length == 0 && node.Kind == TextStudioSelectorNodeKind.None && !node.Invert
                ? TextStudioSelectorNodeKind.Or : TextStudioSelectorNodeKind.And;
            if (node.Kind != TextStudioSelectorNodeKind.And && node.Kind != TextStudioSelectorNodeKind.Or && _conditionGroupKinds.TryGetValue(key, out TextStudioSelectorNodeKind retainedKind)) combination = retainedKind;
            _conditionGroupKinds[key] = combination;

            Foldout group = CreateGroupFoldout(GetSelectorNodeTitle(combination), "selector-node-list-" + PathKey(path), true);
            group.AddToClassList("ts-selector-condition-group");
            DropdownField match = new DropdownField("Match", new List<string> { "All Conditions", "Any Condition" }, combination == TextStudioSelectorNodeKind.Or ? 1 : 0)
            {
                name = "ts-selector-condition-match-" + layerIndex + "-" + PathKey(path),
                tooltip = "All requires every listed condition. Any accepts any listed condition. Nested groups retain their own logic."
            };
            match.AddToClassList("unity-base-field__aligned");
            group.Add(match);

            List<SelectorCondition> conditions = new List<SelectorCondition>();
            bool binary = node.Kind == TextStudioSelectorNodeKind.And || node.Kind == TextStudioSelectorNodeKind.Or;
            if (binary)
            {
                HashSet<int> shared = SharedSelectorNodes(selector);
                CollectSelectorConditions(selector, node.Left, path + "L", node.Kind, shared, new HashSet<int>(ancestors), conditions);
                CollectSelectorConditions(selector, node.Right, path + "R", node.Kind, shared, new HashSet<int>(ancestors), conditions);
            }
            else if (path.Length != 0 || node.Invert || !(node.Kind == TextStudioSelectorNodeKind.All && combination == TextStudioSelectorNodeKind.And || node.Kind == TextStudioSelectorNodeKind.None && combination == TextStudioSelectorNodeKind.Or))
                conditions.Add(new SelectorCondition(nodeIndex, path));

            _conditionSelections.TryGetValue(key, out int selectedIndex);
            selectedIndex = Math.Min(Math.Max(0, selectedIndex), conditions.Count - 1);
            ListView view = null;
            CommonReorderableListView<SelectorCondition> list = new CommonReorderableListView<SelectorCondition>(conditions,
                () => new VisualElement(),
                (element, condition, index) =>
                {
                    element.Clear();
                    element.AddToClassList("ts-selector-condition-row");
                    HashSet<int> branchAncestors = new HashSet<int>(ancestors);
                    if (!binary) branchAncestors.Remove(nodeIndex);
                    AddSelectorNodeEditor(element, nodes, condition.Node, layerIndex, condition.Path, branchAncestors);
                    Foldout item = element.Q<Foldout>();
                    if (item == null) return;
                    item.name = "ts-selector-condition-item-" + layerIndex + "-" + PathKey(condition.Path);
                    item.AddToClassList("ts-selector-condition-item");
                    item.SetValueWithoutNotify(index == selectedIndex);
                    item.tooltip = "Expand to edit this condition. Use the list's − button to remove the selected condition.";
                    item.RegisterCallback<PointerDownEvent>(_ => view.SetSelection(index), TrickleDown.TrickleDown);
                    item.RegisterCallback<FocusInEvent>(_ => view.SetSelection(index));
                    SerializedProperty conditionProperty = nodes.GetArrayElementAtIndex(condition.Node);
                    item.text = SelectorConditionSummary((TextStudioApplicationSelectorNode)conditionProperty.boxedValue);
                    item.TrackPropertyValue(conditionProperty, updated => item.text = SelectorConditionSummary((TextStudioApplicationSelectorNode)updated.boxedValue));
                    element.Bind(_serializedObject);
                    ConfigureRetainedKeys(element, GetItemId(FindProperty("_layers").GetArrayElementAtIndex(layerIndex).FindPropertyRelative("_itemId")));
                }, 26f);
            view = list.ListView;
            list.SetReorderable(false);
            view.name = "ts-selector-condition-list-" + layerIndex + "-" + PathKey(path);
            view.AddToClassList("ts-selector-condition-list");
            view.showBorder = false;
            view.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
            view.style.flexGrow = 0;
            view.style.flexShrink = 0;
            view.style.height = StyleKeyword.Auto;
            view.tooltip = "Expand a condition to edit its properties. Use + to add a condition or − to remove the selected condition.";
            ScrollView scroll = view.Q<ScrollView>(className: BaseVerticalCollectionView.listScrollViewUssClassName);
            if (scroll != null)
            {
                scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
                scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                scroll.nestedInteractionKind = ScrollView.NestedInteractionKind.ForwardScrolling;
            }

            void Rewrite(TextStudioSelectorNodeKind kind, TextStudioApplicationSelectorNode? addition, int selectedIndex, string undoName)
            {
                _conditionGroupKinds[key] = kind;
                _conditionSelections[key] = selectedIndex;
                RewriteSelectorConditionGroup(layerIndex, path, kind, conditions, addition, undoName);
            }

            list.SetAddHandler((_, _) => Rewrite(combination, CreateSelectorPredicate(TextStudioSelectorNodeKind.Range), conditions.Count, "Add 2D Layer Selection Condition"));
            list.SetRemoveHandler(_ =>
            {
                int selected = view.selectedIndex >= 0 ? view.selectedIndex : conditions.Count - 1;
                if (selected < 0 || selected >= conditions.Count) return;
                conditions.RemoveAt(selected);
                Rewrite(combination, null, Math.Max(0, Math.Min(selected, conditions.Count - 1)), "Remove 2D Layer Selection Condition");
            });
            match.RegisterValueChangedCallback(evt => Rewrite(evt.newValue == "Any Condition" ? TextStudioSelectorNodeKind.Or : TextStudioSelectorNodeKind.And, null, view.selectedIndex, "Change 2D Layer Selection Logic"));
            view.selectionChanged += _ => _conditionSelections[key] = view.selectedIndex;
            group.Add(list);
            if (conditions.Count == 0) group.Add(TextStudioInspectorElements.CreateMutedText(combination == TextStudioSelectorNodeKind.Or ? "No conditions. Nothing matches this selection." : "No conditions. All text matches this selection."));
            if (binary) group.Add(CreateField(nodes.GetArrayElementAtIndex(nodeIndex).FindPropertyRelative("_invert"), "Invert Group", "Invert the combined result of this group before its parent selection is evaluated."));
            parent.Add(group);
            if (conditions.Count > 0)
            {
                view.SetSelection(selectedIndex);
            }
            return group;
        }

        void AddSelectorNodeEditor(VisualElement parent, SerializedProperty nodes, int nodeIndex, int layerIndex, string path, HashSet<int> ancestors)
        {
            if (path.Length > 64 || nodeIndex < 0 || nodeIndex >= nodes.arraySize || !ancestors.Add(nodeIndex))
            {
                parent.Add(TextStudioInspectorElements.CreateStatus("This selector contains an invalid or cyclic branch.", CommonInspectorStatusType.Error));
                return;
            }
            SerializedProperty property = nodes.GetArrayElementAtIndex(nodeIndex);
            TextStudioApplicationSelectorNode node = (TextStudioApplicationSelectorNode)property.boxedValue;
            Foldout group = CreateGroupFoldout(GetSelectorNodeTitle(node.Kind), "selector-node-" + PathKey(path), true);
            group.AddToClassList("ts-2d-style-selector-node");
            List<TextStudioSelectorNodeKind> kinds = new List<TextStudioSelectorNodeKind>
            {
                TextStudioSelectorNodeKind.Range, TextStudioSelectorNodeKind.Tag, TextStudioSelectorNodeKind.AppearanceTag,
                TextStudioSelectorNodeKind.SourceColor, TextStudioSelectorNodeKind.SemanticState, TextStudioSelectorNodeKind.Coverage,
                TextStudioSelectorNodeKind.RenderProperty, TextStudioSelectorNodeKind.And, TextStudioSelectorNodeKind.Or,
                TextStudioSelectorNodeKind.Not, TextStudioSelectorNodeKind.Within, TextStudioSelectorNodeKind.All, TextStudioSelectorNodeKind.None
            };
            List<string> choices = kinds.ConvertAll(GetSelectorNodeTitle);
            DropdownField kind = new DropdownField("Kind", choices, kinds.IndexOf(node.Kind))
            {
                name = "ts-selector-condition-kind-" + layerIndex + "-" + PathKey(path),
                tooltip = "Choose a predicate or wrap this condition in a logical group. Replacing a group with a predicate replaces its nested conditions; Undo restores them."
            };
            kind.AddToClassList("unity-base-field__aligned");
            kind.RegisterValueChangedCallback(evt => ReplaceSelectorConditionKind(layerIndex, path, kinds[choices.IndexOf(evt.newValue)]));
            group.Add(kind);
            if (SharedSelectorNodes(CurrentSelector(layerIndex)).Contains(nodeIndex))
            {
                group.Add(TextStudioInspectorElements.CreateMutedText("This condition is shared. Value edits affect every use."));
                group.Add(TextStudioInspectorElements.CreateButton("Make Independent", "Copy this occurrence so its values can be edited without changing other uses.", () => MakeSelectorConditionIndependent(layerIndex, path)));
            }
            switch (node.Kind)
            {
                case TextStudioSelectorNodeKind.Range:
                    group.Add(TextStudioSelectionField.CreateRangeNode(property));
                    AddNodeFields(group, property, "_invert");
                    break;
                case TextStudioSelectorNodeKind.Tag:
                case TextStudioSelectorNodeKind.AppearanceTag:
                    group.Add(CreateTypedTagSelector(property));
                    AddNodeFields(group, property, "_invert");
                    break;
                case TextStudioSelectorNodeKind.SourceColor:
                    AddNodeFields(group, property, "_color", "_tolerance", "_invert");
                    break;
                case TextStudioSelectorNodeKind.SemanticState:
                    AddNodeFields(group, property, "_semanticState", "_invert");
                    break;
                case TextStudioSelectorNodeKind.Coverage:
                    AddNodeFields(group, property, "_comparison", "_value");
                    break;
                case TextStudioSelectorNodeKind.RenderProperty:
                    AddNodeFields(group, property, "_key", "_comparison", "_value");
                    break;
                case TextStudioSelectorNodeKind.And:
                case TextStudioSelectorNodeKind.Or:
                    HashSet<int> listAncestors = new HashSet<int>(ancestors);
                    listAncestors.Remove(nodeIndex);
                    Foldout listGroup = CreateSelectorConditionList(parent, nodes, nodeIndex, layerIndex, path, listAncestors);
                    if (listGroup != null)
                    {
                        List<VisualElement> controls = new List<VisualElement>(group.Children());
                        for (int i = 0; i < controls.Count; i++) listGroup.Insert(i, controls[i]);
                    }
                    return;
                case TextStudioSelectorNodeKind.Not:
                    AddSelectorNodeEditor(group, nodes, node.Left, layerIndex, path + "L", new HashSet<int>(ancestors));
                    break;
                case TextStudioSelectorNodeKind.Within:
                    group.Add(TextStudioInspectorElements.CreateMutedText("The child selection is evaluated separately inside each matching scope."));
                    Foldout scope = CreateGroupFoldout("Scope", "selector-scope-" + PathKey(path), true);
                    AddSelectorNodeEditor(scope, nodes, node.Left, layerIndex, path + "L", new HashSet<int>(ancestors));
                    group.Add(scope);
                    Foldout child = CreateGroupFoldout("Child Selection", "selector-child-" + PathKey(path), true);
                    AddSelectorNodeEditor(child, nodes, node.Right, layerIndex, path + "R", new HashSet<int>(ancestors));
                    group.Add(child);
                    break;
                default:
                    group.Add(TextStudioInspectorElements.CreateMutedText(node.Kind == TextStudioSelectorNodeKind.All ? "Every glyph matches this branch." : "No glyph matches this branch."));
                    break;
            }
            parent.Add(group);
        }

        TextStudioApplicationSelector CurrentSelector(int layerIndex)
        {
            _serializedObject.UpdateIfRequiredOrScript();
            return ((TextStudio2DLayer)FindProperty("_layers").GetArrayElementAtIndex(layerIndex).boxedValue).Selector;
        }

        string ConditionKey(int layerIndex, string path)
        {
            return GetItemId(FindProperty("_layers").GetArrayElementAtIndex(layerIndex).FindPropertyRelative("_itemId")) + "/" + path;
        }

        static string PathKey(string path) => path.Length == 0 ? "root" : path;

        static string SelectorConditionSummary(TextStudioApplicationSelectorNode node)
        {
            string title = GetSelectorNodeTitle(node.Kind);
            if (node.Kind == TextStudioSelectorNodeKind.Range) title += "  " + (node.UsesTextRange ? node.TextRange.ToString() : node.Start + ".." + ((long)node.Start + node.Count));
            else if (node.Kind == TextStudioSelectorNodeKind.Tag || node.Kind == TextStudioSelectorNodeKind.AppearanceTag) title += "  " + node.Key;
            else if (node.Kind == TextStudioSelectorNodeKind.Coverage) title += "  " + ObjectNames.NicifyVariableName(node.Comparison.ToString()) + " " + node.Value.ToString("0.###");
            return node.Invert ? title + " (Inverted)" : title;
        }

        static HashSet<int> SharedSelectorNodes(TextStudioApplicationSelector selector)
        {
            Dictionary<int, int> references = new Dictionary<int, int>();
            HashSet<int> visited = new HashSet<int>();
            Stack<int> pending = new Stack<int>();
            pending.Push(selector.Root);
            while (pending.Count > 0)
            {
                int index = pending.Pop();
                references.TryGetValue(index, out int count);
                references[index] = count + 1;
                if (!visited.Add(index) || !selector.TryGetNode(index, out TextStudioApplicationSelectorNode node)) continue;
                if (HasSelectorLeft(node.Kind)) pending.Push(node.Left);
                if (HasSelectorRight(node.Kind)) pending.Push(node.Right);
            }
            HashSet<int> shared = new HashSet<int>();
            foreach (KeyValuePair<int, int> pair in references) if (pair.Value > 1) shared.Add(pair.Key);
            pending = new Stack<int>(shared);
            while (pending.Count > 0)
            {
                if (!selector.TryGetNode(pending.Pop(), out TextStudioApplicationSelectorNode node)) continue;
                if (HasSelectorLeft(node.Kind) && shared.Add(node.Left)) pending.Push(node.Left);
                if (HasSelectorRight(node.Kind) && shared.Add(node.Right)) pending.Push(node.Right);
            }
            return shared;
        }

        static void CollectSelectorConditions(TextStudioApplicationSelector selector, int index, string path, TextStudioSelectorNodeKind combination, HashSet<int> shared, HashSet<int> ancestors, List<SelectorCondition> conditions)
        {
            if (path.Length <= 64 && ancestors.Add(index) && selector.TryGetNode(index, out TextStudioApplicationSelectorNode node) && node.Kind == combination && !node.Invert && !shared.Contains(index))
            {
                CollectSelectorConditions(selector, node.Left, path + "L", combination, shared, new HashSet<int>(ancestors), conditions);
                CollectSelectorConditions(selector, node.Right, path + "R", combination, shared, new HashSet<int>(ancestors), conditions);
            }
            else conditions.Add(new SelectorCondition(index, path));
        }

        void RewriteSelectorConditionGroup(int layerIndex, string path, TextStudioSelectorNodeKind combination, List<SelectorCondition> conditions, TextStudioApplicationSelectorNode? addition, string undoName)
        {
            TextStudioApplicationSelector selector = CurrentSelector(layerIndex);
            if (!TrySelectorPath(selector, path, out int original)) return;
            TextStudioApplicationSelectorNode oldGroup = selector.GetNodesUnsafe()[original];
            List<TextStudioApplicationSelectorNode> program = new List<TextStudioApplicationSelectorNode>(selector.GetNodesUnsafe());
            List<int> operands = conditions.ConvertAll(condition => condition.Node);
            if (addition.HasValue) { operands.Add(program.Count); program.Add(addition.Value); }
            int replacement = CombineSelectorConditions(program, operands, combination, 0, operands.Count);
            if ((oldGroup.Kind == TextStudioSelectorNodeKind.And || oldGroup.Kind == TextStudioSelectorNodeKind.Or) && oldGroup.Invert)
            {
                program.Add(TextStudioApplicationSelectorNode.Not(replacement));
                replacement = program.Count - 1;
            }
            ApplySelectorReplacement(layerIndex, selector, program, path, replacement, undoName);
        }

        static int CombineSelectorConditions(List<TextStudioApplicationSelectorNode> program, List<int> operands, TextStudioSelectorNodeKind kind, int start, int count)
        {
            if (count == 1) return operands[start];
            TextStudioApplicationSelectorNode node;
            if (count == 0) node = kind == TextStudioSelectorNodeKind.Or ? TextStudioApplicationSelectorNode.None() : TextStudioApplicationSelectorNode.All();
            else
            {
                int leftCount = count / 2;
                int left = CombineSelectorConditions(program, operands, kind, start, leftCount);
                int right = CombineSelectorConditions(program, operands, kind, start + leftCount, count - leftCount);
                node = kind == TextStudioSelectorNodeKind.Or ? TextStudioApplicationSelectorNode.Or(left, right) : TextStudioApplicationSelectorNode.And(left, right);
            }
            program.Add(node);
            return program.Count - 1;
        }

        void ReplaceSelectorConditionKind(int layerIndex, string path, TextStudioSelectorNodeKind kind)
        {
            TextStudioApplicationSelector selector = CurrentSelector(layerIndex);
            if (!TrySelectorPath(selector, path, out int original) || selector.GetNodesUnsafe()[original].Kind == kind) return;
            List<TextStudioApplicationSelectorNode> program = new List<TextStudioApplicationSelectorNode>(selector.GetNodesUnsafe());
            TextStudioApplicationSelectorNode replacement;
            if (kind == TextStudioSelectorNodeKind.Not) replacement = TextStudioApplicationSelectorNode.Not(original);
            else if (kind == TextStudioSelectorNodeKind.And || kind == TextStudioSelectorNodeKind.Or || kind == TextStudioSelectorNodeKind.Within)
            {
                int child = program.Count;
                program.Add(CreateSelectorPredicate(TextStudioSelectorNodeKind.Range));
                replacement = kind == TextStudioSelectorNodeKind.And ? TextStudioApplicationSelectorNode.And(original, child) : kind == TextStudioSelectorNodeKind.Or ? TextStudioApplicationSelectorNode.Or(original, child) : TextStudioApplicationSelectorNode.Within(original, child);
            }
            else replacement = kind == TextStudioSelectorNodeKind.All ? TextStudioApplicationSelectorNode.All() : kind == TextStudioSelectorNodeKind.None ? TextStudioApplicationSelectorNode.None() : CreateSelectorPredicate(kind);
            program.Add(replacement);
            ApplySelectorReplacement(layerIndex, selector, program, path, program.Count - 1, "Change 2D Layer Selection Condition");
        }

        void MakeSelectorConditionIndependent(int layerIndex, string path)
        {
            TextStudioApplicationSelector selector = CurrentSelector(layerIndex);
            if (!TrySelectorPath(selector, path, out int original)) return;
            List<TextStudioApplicationSelectorNode> program = new List<TextStudioApplicationSelectorNode>(selector.GetNodesUnsafe());
            Dictionary<int, int> copies = new Dictionary<int, int>();
            Dictionary<int, int> origins = new Dictionary<int, int>();
            int Copy(int index, HashSet<int> ancestors)
            {
                if (ancestors.Count > 64 || !ancestors.Add(index) || !selector.TryGetNode(index, out TextStudioApplicationSelectorNode node)) return -1;
                if (copies.TryGetValue(index, out int existing)) return existing;
                int left = HasSelectorLeft(node.Kind) ? Copy(node.Left, new HashSet<int>(ancestors)) : node.Left;
                int right = HasSelectorRight(node.Kind) ? Copy(node.Right, new HashSet<int>(ancestors)) : node.Right;
                if (HasSelectorLeft(node.Kind) && left < 0 || HasSelectorRight(node.Kind) && right < 0) return -1;
                program.Add(node.WithChildren(left, right));
                copies.Add(index, program.Count - 1);
                origins.Add(program.Count - 1, index);
                return program.Count - 1;
            }
            int copy = Copy(original, new HashSet<int>());
            if (copy >= 0) ApplySelectorReplacement(layerIndex, selector, program, path, copy, "Make 2D Layer Selection Condition Independent", origins);
        }

        void ApplySelectorReplacement(int layerIndex, TextStudioApplicationSelector original, List<TextStudioApplicationSelectorNode> program, string path, int replacement, string undoName, Dictionary<int, int> origins = null)
        {
            origins ??= new Dictionary<int, int>();
            HashSet<int> shared = SharedSelectorNodes(original);
            List<int> parents = new List<int>();
            int current = original.Root;
            for (int i = 0; i < path.Length; i++)
            {
                if (!original.TryGetNode(current, out TextStudioApplicationSelectorNode node)) return;
                parents.Add(current);
                current = path[i] == 'L' ? node.Left : node.Right;
            }
            if (replacement >= original.NodeCount && !origins.ContainsKey(replacement)) origins.Add(replacement, current);
            for (int i = parents.Count - 1; i >= 0; i--)
            {
                TextStudioApplicationSelectorNode parent = program[parents[i]];
                TextStudioApplicationSelectorNode updated = parent.WithChildren(path[i] == 'L' ? replacement : parent.Left, path[i] == 'R' ? replacement : parent.Right);
                if (shared.Contains(parents[i]))
                {
                    program.Add(updated);
                    replacement = program.Count - 1;
                    origins[replacement] = parents[i];
                }
                else
                {
                    program[parents[i]] = updated;
                    replacement = parents[i];
                }
            }
            if (!TryCompactSelector(program, replacement, origins, out TextStudioApplicationSelector compact)) return;
            if (compact.NodeCount > 256)
            {
                _validation.Add(TextStudioInspectorElements.CreateStatus("This selection already uses the maximum 256 conditions and operators. Remove a condition before adding more.", CommonInspectorStatusType.Error));
                return;
            }
            _advancedSelectors.Add(GetItemId(FindProperty("_layers").GetArrayElementAtIndex(layerIndex).FindPropertyRelative("_itemId")));
            SetSelector(layerIndex, compact, undoName);
        }

        static bool TrySelectorPath(TextStudioApplicationSelector selector, string path, out int index)
        {
            index = selector.Root;
            for (int i = 0; i < path.Length; i++)
            {
                if (!selector.TryGetNode(index, out TextStudioApplicationSelectorNode node)) return false;
                index = path[i] == 'L' ? node.Left : node.Right;
            }
            return selector.TryGetNode(index, out _);
        }

        static bool TryCompactSelector(List<TextStudioApplicationSelectorNode> program, int root, Dictionary<int, int> origins, out TextStudioApplicationSelector selector)
        {
            HashSet<int> reachable = new HashSet<int>();
            Dictionary<int, int> occurrenceOrder = new Dictionary<int, int>();
            bool Visit(int index, HashSet<int> ancestors)
            {
                if (ancestors.Count > 64 || index < 0 || index >= program.Count || !ancestors.Add(index)) return false;
                if (!reachable.Add(index)) return true;
                occurrenceOrder.Add(index, occurrenceOrder.Count);
                TextStudioApplicationSelectorNode node = program[index];
                return (!HasSelectorLeft(node.Kind) || Visit(node.Left, new HashSet<int>(ancestors))) && (!HasSelectorRight(node.Kind) || Visit(node.Right, new HashSet<int>(ancestors)));
            }
            selector = default;
            if (!Visit(root, new HashSet<int>())) return false;
            List<int> indices = new List<int>(reachable);
            // Node order breaks ties between equal-start occurrences. Copies retain the rank of the authored node.
            indices.Sort((left, right) =>
            {
                int leftRank = origins.TryGetValue(left, out int leftOrigin) ? leftOrigin : left;
                int rightRank = origins.TryGetValue(right, out int rightOrigin) ? rightOrigin : right;
                int rank = leftRank.CompareTo(rightRank);
                return rank != 0 ? rank : occurrenceOrder[left].CompareTo(occurrenceOrder[right]);
            });
            Dictionary<int, int> remap = new Dictionary<int, int>();
            for (int i = 0; i < indices.Count; i++) remap.Add(indices[i], i);
            TextStudioApplicationSelectorNode[] nodes = new TextStudioApplicationSelectorNode[indices.Count];
            for (int i = 0; i < indices.Count; i++)
            {
                TextStudioApplicationSelectorNode node = program[indices[i]];
                nodes[i] = node.WithChildren(HasSelectorLeft(node.Kind) ? remap[node.Left] : node.Left, HasSelectorRight(node.Kind) ? remap[node.Right] : node.Right);
            }
            selector = TextStudioApplicationSelector.Create(nodes, remap[root]);
            return true;
        }

        static bool HasSelectorLeft(TextStudioSelectorNodeKind kind) => kind == TextStudioSelectorNodeKind.And || kind == TextStudioSelectorNodeKind.Or || kind == TextStudioSelectorNodeKind.Not || kind == TextStudioSelectorNodeKind.Within;
        static bool HasSelectorRight(TextStudioSelectorNodeKind kind) => kind == TextStudioSelectorNodeKind.And || kind == TextStudioSelectorNodeKind.Or || kind == TextStudioSelectorNodeKind.Within;
    }
}
