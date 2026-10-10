using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal sealed partial class TextAnimatorMigrationWindow
    {
        enum FindingFilter
        {
            Everything,
            OneClick,
            NeedsYou,
            Done,
            WorksAsIs
        }

        sealed class Finding
        {
            public TextAnimatorMigrationPlanItem Item;
            public string Category;
        }

        sealed class FindingRow
        {
            public Label Title;
            public Label Detail;
            public Label Location;
            public Label Category;
            public Label State;
            public Button Select;
            public Button Open;
            public Finding Finding;
        }

        readonly List<Finding> _allFindings = new List<Finding>();
        readonly List<Finding> _visibleFindings = new List<Finding>();
        TextAnimatorMigrationPlan _findingsPlan;
        VisualElement _findingsView;
        ToolbarSearchField _findingsSearch;
        DropdownField _findingsFilter;
        Label _findingsCount;
        ListView _findingsList;

        Foldout _advanced;
        Toggle _samplesToggle;
        TextField _checkField;
        VisualElement _checkResult;
        VisualElement _expandBlock;
        Button _expandButton;

        void BuildFindings()
        {
            _findingsView = CommonUITK.CreateContainer("ts-migration-findings");
            _findingsView.name = "ts-migration-findings";
            _findingsView.style.flexGrow = 1f;

            VisualElement toolbar = CommonUITK.CreateContainer("ts-migration-findings-toolbar");
            _findingsSearch = new ToolbarSearchField
            {
                name = "ts-migration-findings-search",
                tooltip = "Filter the findings by name, location or description."
            };
            _findingsSearch.RegisterValueChangedCallback(_ => ApplyFindingsFilter());
            toolbar.Add(_findingsSearch);

            _findingsFilter = new DropdownField(
                "Show",
                new List<string> { "Everything", "One-click", "Needs you", "Done", "Works as-is" },
                0)
            {
                name = "ts-migration-findings-filter",
                tooltip = "Show only findings with this status."
            };
            _findingsFilter.RegisterValueChangedCallback(_ => ApplyFindingsFilter());
            toolbar.Add(_findingsFilter);
            _findingsCount = Copy(string.Empty, "ts-migration-findings-count");
            toolbar.Add(_findingsCount);
            _findingsView.Add(toolbar);

            _findingsList = new ListView
            {
                name = "ts-migration-findings-list",
                itemsSource = _visibleFindings,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                selectionType = SelectionType.None,
                makeItem = MakeFindingRow,
                bindItem = BindFindingRow
            };
            _findingsList.AddToClassList("ts-migration-findings-list");
            _findingsView.Add(_findingsList);
            _tabs.Body.Add(_findingsView);
        }

        VisualElement MakeFindingRow()
        {
            VisualElement row = CommonUITK.CreateContainer("ts-migration-item");
            FindingRow parts = new FindingRow();
            VisualElement text = CommonUITK.CreateContainer("ts-migration-item-text");
            parts.Title = CommonUITK.CreateLabel(string.Empty, "ts-migration-item-title");
            parts.Detail = Copy(string.Empty, "ts-migration-item-detail");
            parts.Location = Copy(string.Empty, "ts-migration-item-location");
            text.Add(parts.Title);
            text.Add(parts.Detail);
            text.Add(parts.Location);
            row.Add(text);

            VisualElement side = CommonUITK.CreateContainer("ts-migration-item-side");
            parts.Category = Pill(string.Empty, string.Empty, "The part of the migration this finding belongs to.");
            parts.State = Pill(string.Empty, string.Empty, string.Empty);
            side.Add(parts.Category);
            side.Add(parts.State);
            parts.Select = ItemButton("Select", "Select this in the Hierarchy or Project window.", () =>
            {
                if (parts.Finding != null)
                    SelectItem(parts.Finding.Item);
            });
            parts.Open = ItemButton("Open", "Open this scene or script.", () =>
            {
                if (parts.Finding != null)
                    OpenItem(parts.Finding.Item);
            });
            side.Add(parts.Select);
            side.Add(parts.Open);
            row.Add(side);
            row.userData = parts;
            return row;
        }

        void BindFindingRow(VisualElement element, int index)
        {
            if (!(element.userData is FindingRow parts) || index < 0 || index >= _visibleFindings.Count)
                return;

            Finding finding = _visibleFindings[index];
            TextAnimatorMigrationPlanItem item = finding.Item;
            parts.Finding = finding;
            parts.Title.text = item.Title;
            parts.Detail.text = item.Detail;
            SetDisplay(parts.Detail, !string.IsNullOrEmpty(item.Detail));
            parts.Location.text = item.Location;
            SetDisplay(parts.Location, !string.IsNullOrEmpty(item.Location) && !string.Equals(item.Location, item.Title, StringComparison.Ordinal));
            SetPill(parts.Category, finding.Category, string.Empty, "The part of the migration this finding belongs to.");
            SetPill(parts.State, StateText(item.State), StateTone(item.State), StateTooltip(item.State));
            SetDisplay(parts.Select, item.SelectTarget != null);
            SetDisplay(parts.Open, CanOpen(item));
        }

        void RefreshFindings()
        {
            if (_findingsList == null)
                return;

            if (!ReferenceEquals(_findingsPlan, _plan))
            {
                _findingsPlan = _plan;
                _allFindings.Clear();
                if (_scan != null && !_scan.Canceled)
                {
                    for (int i = 0; i < _plan.Steps.Count; i++)
                        AddFindings(_plan.Steps[i].Items, StepCategory(_plan.Steps[i].Kind));
                    for (int i = 0; i < _plan.Topics.Count; i++)
                        AddFindings(_plan.Topics[i].Items, TopicTitle(_plan.Topics[i].Kind));
                    AddFindings(_plan.WorkingTexts, "Text");
                    AddFindings(_plan.WorkingAssets, "Text Animator asset");
                }
            }

            ApplyFindingsFilter();
        }

        void AddFindings(IReadOnlyList<TextAnimatorMigrationPlanItem> items, string category)
        {
            for (int i = 0; i < items.Count; i++)
                _allFindings.Add(new Finding { Item = items[i], Category = category });
        }

        void ApplyFindingsFilter()
        {
            if (_findingsList == null)
                return;

            FindingFilter filter = (FindingFilter)Mathf.Max(0, _findingsFilter.index);
            string search = _findingsSearch.value ?? string.Empty;
            _visibleFindings.Clear();
            for (int i = 0; i < _allFindings.Count; i++)
            {
                Finding finding = _allFindings[i];
                if (MatchesFilter(finding.Item.State, filter) && MatchesSearch(finding, search))
                    _visibleFindings.Add(finding);
            }

            _findingsCount.text = _scan == null
                ? "Check the project in the Guide tab first."
                : "Showing " + _visibleFindings.Count + " of " + Count(_allFindings.Count, "finding", "findings") + ".";
            _findingsList.RefreshItems();
        }

        static bool MatchesFilter(TextAnimatorMigrationItemState state, FindingFilter filter)
        {
            switch (filter)
            {
                case FindingFilter.OneClick:
                    return state == TextAnimatorMigrationItemState.Ready ||
                           state == TextAnimatorMigrationItemState.OpenScene ||
                           state == TextAnimatorMigrationItemState.Blocked;
                case FindingFilter.NeedsYou:
                    return state == TextAnimatorMigrationItemState.NeedsYou;
                case FindingFilter.Done:
                    return state == TextAnimatorMigrationItemState.Done;
                case FindingFilter.WorksAsIs:
                    return state == TextAnimatorMigrationItemState.WorksAsIs;
                default:
                    return true;
            }
        }

        static bool MatchesSearch(Finding finding, string search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return true;

            string term = search.Trim();
            TextAnimatorMigrationPlanItem item = finding.Item;
            return Contains(item.Title, term) || Contains(item.Detail, term) || Contains(item.Location, term) ||
                   Contains(finding.Category, term);
        }

        static bool Contains(string value, string term)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string StepCategory(TextAnimatorMigrationStepKind kind)
        {
            return kind == TextAnimatorMigrationStepKind.AddTextStudio ? "Add Text Studio" : Capitalize(StepShortName(kind));
        }

        static string Capitalize(string value)
        {
            return string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value.Substring(1);
        }

        void BuildAdvanced()
        {
            _advanced = CommonUITK.CreateFoldout(
                "More tools",
                false,
                null,
                "Optional tools for special cases. The guide above covers a normal migration.",
                "ts-migration-advanced");
            _advanced.name = "ts-migration-more-tools";
            _advanced.viewDataKey = "ts-migration-more-tools";

            _samplesToggle = new Toggle
            {
                name = "ts-migration-include-samples",
                text = "Include Text Animator's own samples",
                tooltip = "Count the scenes, prefabs and assets that Package Manager imported as Text Animator samples. They are left out by default because they are removed together with Text Animator."
            };
            _samplesToggle.SetValueWithoutNotify(_includeSamples);
            _samplesToggle.RegisterValueChangedCallback(evt =>
            {
                _includeSamples = evt.newValue;
                RebuildPlan();
                UpdateView();
            });
            _advanced.Add(_samplesToggle);

            VisualElement check = CommonUITK.CreateContainer("ts-migration-advanced-block");
            check.Add(CommonUITK.CreateLabel("Check one text", "ts-migration-advanced-title"));
            check.Add(Copy("Paste Text Animator markup to see how Text Studio reads it. Nothing in your project changes."));
            _checkField = new TextField
            {
                name = "ts-migration-check-source",
                multiline = true,
                tooltip = "Text Animator markup to check, for example a dialogue line."
            };
            TextStudioInspectorElements.ConfigureCodeTextField(_checkField);
            _checkField.AddToClassList("ts-migration-paste");
            check.Add(_checkField);
            check.Add(Button(
                "Check text",
                "Show which tags Text Studio reads as they are and which ones need attention.",
                CheckPastedText,
                "ts-migration-check-text"));
            _checkResult = CommonUITK.CreateContainer("ts-migration-check-result");
            check.Add(_checkResult);
            _advanced.Add(check);

            _expandBlock = CommonUITK.CreateContainer("ts-migration-advanced-block");
            _expandBlock.Add(CommonUITK.CreateLabel("Write style markup into texts", "ts-migration-advanced-title"));
            _expandBlock.Add(Copy("Optional. Replaces Text Animator style tags such as <style1> with their markup in editable texts. Imported style aliases already keep these tags working, so you only need this to stop depending on the alias asset. Supports Undo."));
            _expandButton = Button(
                "Write style markup...",
                "Rewrite style tags in texts in open scenes and prefabs. You are asked to confirm first.",
                ExpandStyleTags,
                "ts-migration-expand-styles");
            _expandBlock.Add(_expandButton);
            _advanced.Add(_expandBlock);
            _guide.Add(_advanced);
        }

        void UpdateAdvanced(bool editable)
        {
            _samplesToggle.SetValueWithoutNotify(_includeSamples);
            bool hasStyles = false;
            IReadOnlyList<TextAnimatorMigrationArtifactRecord> artifacts = _plan.Inventory.Artifacts;
            for (int i = 0; i < artifacts.Count && !hasStyles; i++)
            {
                hasStyles = artifacts[i].Kind == TextAnimatorMigrationArtifactKind.Style ||
                            artifacts[i].Kind == TextAnimatorMigrationArtifactKind.StyleSheet;
            }

            SetDisplay(_expandBlock, hasStyles && _plan.Inventory.SourceCount > 0);
            _expandButton.SetEnabled(editable);
        }

        void CheckPastedText()
        {
            _checkResult.Clear();
            string markup = _checkField.value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(markup))
            {
                _checkResult.Add(Copy("Paste some markup first."));
                return;
            }

            TextAnimatorMigrationResult result = TextAnimatorMigrationAnalyzer.Analyze(markup);
            List<Label> lines = new List<Label>();
            HashSet<string> reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < result.Issues.Count; i++)
            {
                TextAnimatorMigrationIssue issue = result.Issues[i];
                if (issue.Status != TextAnimatorMigrationStatus.Manual && issue.Status != TextAnimatorMigrationStatus.Unsupported)
                    continue;

                // Opening and closing forms of one tag are reported once.
                IReadOnlyList<TextAnimatorMarkupToken> tokens = TextAnimatorMarkupScanner.Scan(issue.OriginalText);
                string key = tokens.Count > 0 ? tokens[0].Name + "|" + issue.Message : issue.OriginalText;
                if (!reported.Add(key))
                    continue;

                lines.Add(Copy(
                    issue.OriginalText + ": " + issue.Message + (string.IsNullOrEmpty(issue.Action) ? string.Empty : " " + issue.Action),
                    "ts-migration-item-detail"));
            }

            if (lines.Count == 0)
            {
                _checkResult.Add(Copy("✓ Text Studio reads this markup as it is."));
                return;
            }

            _checkResult.Add(Copy(Count(lines.Count, "tag needs", "tags need") + " attention:"));
            for (int i = 0; i < lines.Count; i++)
                _checkResult.Add(lines[i]);
        }

        void ExpandStyleTags()
        {
            if (!CanEdit() || _plan.Inventory.SourceCount == 0)
                return;

            if (!EditorUtility.DisplayDialog(
                    "Write style markup into texts",
                    "Text Studio replaces Text Animator style tags with their markup in every editable text it found. Prefabs are saved; save open scenes yourself. This supports Undo.",
                    "Write markup",
                    "Cancel"))
            {
                return;
            }

            int group = BeginUndoGroup("Write Text Animator Style Markup");
            TextAnimatorMigrationActionResult result;
            try
            {
                result = TextAnimatorMigrationActions.ExpandStyleTags(_plan.Inventory);
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }

            ShowResults(new List<TextAnimatorMigrationActionResult> { result });
        }

        void ExportReport()
        {
            string path = EditorUtility.SaveFilePanel(
                "Export migration report",
                Application.dataPath,
                "Text Animator Migration Report",
                "md");
            if (string.IsNullOrEmpty(path))
                return;

            File.WriteAllText(path, BuildReport());
            _resultMessage = "✓ Report saved to " + path + ".";
            UpdateView();
        }

        internal string BuildReport()
        {
            TextAnimatorMigrationPlan plan = _plan;
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# Text Animator Migration Report");
            builder.AppendLine();
            builder.AppendLine("Checked " + DescribeScope() + " on " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ".");
            builder.AppendLine();
            builder.AppendLine("## Summary");
            builder.AppendLine();
            builder.AppendLine("- Already work: " + Count(plan.WorkingTexts.Count, "text", "texts") + ", " +
                               Count(plan.BuiltInEquivalentCount, "built-in equivalent", "built-in equivalents"));
            builder.AppendLine("- One-click: " + plan.ReadyCount + " ready, " + plan.DoneCount + " done, " +
                               Count(plan.ClosedSceneCount, "closed scene", "closed scenes"));
            builder.AppendLine("- Needs you: " + Count(plan.NeedsYouCount, "item", "items"));
            if (plan.ExcludedSampleCount > 0 && !_includeSamples)
                builder.AppendLine("- Not counted: " + Count(plan.ExcludedSampleCount, "item", "items") + " in Text Animator's own samples");

            builder.AppendLine();
            builder.AppendLine("## One-click steps");
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                TextAnimatorMigrationPlanStep step = plan.Steps[i];
                if (!step.IsApplicable)
                    continue;

                builder.AppendLine();
                builder.AppendLine("### " + StepTitle(step.Kind));
                builder.AppendLine();
                AppendReportItems(builder, step.Items);
            }

            builder.AppendLine();
            builder.AppendLine("## Needs you");
            for (int i = 0; i < plan.Topics.Count; i++)
            {
                TextAnimatorMigrationPlanTopic topic = plan.Topics[i];
                if (!topic.IsApplicable)
                    continue;

                builder.AppendLine();
                builder.AppendLine("### " + TopicTitle(topic.Kind));
                builder.AppendLine();
                builder.AppendLine(TopicGuidance(topic.Kind));
                builder.AppendLine();
                AppendReportItems(builder, topic.Items);
            }

            builder.AppendLine();
            builder.AppendLine("## Already works");
            builder.AppendLine();
            AppendReportItems(builder, plan.WorkingTexts);
            AppendReportItems(builder, plan.WorkingAssets);
            return builder.ToString();
        }

        static void AppendReportItems(StringBuilder builder, IReadOnlyList<TextAnimatorMigrationPlanItem> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                TextAnimatorMigrationPlanItem item = items[i];
                builder.Append("- [").Append(StateText(item.State)).Append("] ").Append(item.Title);
                if (!string.IsNullOrEmpty(item.Location) && !string.Equals(item.Location, item.Title, StringComparison.Ordinal))
                    builder.Append(" (").Append(item.Location).Append(')');
                if (!string.IsNullOrEmpty(item.Detail))
                    builder.Append(": ").Append(item.Detail);
                builder.AppendLine();
            }
        }
    }
}
