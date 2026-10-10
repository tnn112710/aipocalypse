using System;
using System.Collections.Generic;
using System.Linq;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal sealed partial class TextStudioRuntimePreparationWindow
    {
        const int RowLimit = 30;
        const string ScopeTooltip = "Whole project: open scenes including unsaved edits, plus enabled build scenes, Resources and preloaded assets with their dependencies. Addressables and other custom content are prepared by your content build.";

        VisualElement _hero, _consent, _actions, _scopeRow, _cardList;
        Ring _ring;
        Label _headline, _heroDetail, _consentText, _scopeText, _detailsSummary, _detailsEmpty;
        Button _primary, _cancel, _scopeAction;
        Toggle _buildToggle;
        Foldout _details;
        ToolbarSearchField _search;
        Action _scopeCommand;
        string _query = string.Empty;
        bool _focusAttention;
        readonly Dictionary<string, GroupCard> _cards = new Dictionary<string, GroupCard>(StringComparer.Ordinal);

        void BuildOverview(VisualElement parent)
        {
            _hero = CommonUITK.CreateContainer("ts-preparation-hero");
            parent.Add(_hero);
            VisualElement status = CommonUITK.CreateContainer("ts-preparation-hero-status");
            _ring = new Ring();
            status.Add(_ring);
            VisualElement copy = CommonUITK.CreateContainer("ts-preparation-hero-copy");
            _headline = CommonUITK.CreateLabel(string.Empty, "ts-preparation-headline");
            _headline.name = "preparation-headline";
            _heroDetail = Copy(string.Empty, "ts-preparation-hero-detail");
            copy.Add(_headline);
            copy.Add(_heroDetail);
            status.Add(copy);
            _hero.Add(status);

            _consent = CommonUITK.CreateContainer("ts-preparation-consent");
            _consent.Add(CommonUITK.CreateLabel("Allow font outlines in this project?", "ts-preparation-consent-title"));
            _consentText = Copy(string.Empty);
            _consent.Add(_consentText);
            VisualElement consentActions = CommonUITK.CreateContainer("ts-preparation-consent-actions");
            consentActions.Add(Button("Allow and prepare", "Save outlines of the fonts your text uses with this project and include them in builds, then prepare everything.", () => ChooseEmbedding(true), true, "preparation-allow-fonts"));
            consentActions.Add(Button("Use existing data only", "Never save new font outlines. Text that needs them stays unprepared, and builds that require them stop with an explanation. You can change this in Settings.", () => ChooseEmbedding(false), false, "preparation-deny-fonts"));
            _consent.Add(consentActions);
            _hero.Add(_consent);

            _actions = CommonUITK.CreateContainer("ts-preparation-actions");
            _primary = Button("Prepare", string.Empty, PrimaryAction, true, "preparation-primary");
            _primary.AddToClassList("ts-preparation-primary");
            _cancel = Button("Cancel", "Stop the remaining work. Everything prepared so far is kept.", CancelOperation, false, "preparation-cancel");
            _cancel.AddToClassList("ts-preparation-cancel");
            _actions.Add(_primary);
            _actions.Add(_cancel);
            _hero.Add(_actions);

            _buildToggle = new Toggle
            {
                text = "Prepare automatically before each build", name = "preparation-build-automation",
                tooltip = "Before each player build, Text Studio prepares what the build content needs. Required data that cannot be prepared stops the build with an explanation."
            };
            _buildToggle.AddToClassList("ts-preparation-build-toggle");
            _buildToggle.RegisterValueChangedCallback(evt =>
            {
                TextStudioRuntimePreparationSettings.instance.automaticInBuilds = evt.newValue;
                SaveSettings();
            });
            _hero.Add(_buildToggle);

            _scopeRow = CommonUITK.CreateContainer("ts-preparation-scope-row");
            _scopeText = Copy(string.Empty, "ts-preparation-scope-text");
            _scopeAction = LinkButton(string.Empty, string.Empty, () => _scopeCommand?.Invoke());
            _scopeAction.name = "preparation-scope-action";
            _scopeRow.Add(_scopeText);
            _scopeRow.Add(_scopeAction);
            _hero.Add(_scopeRow);

            _details = new Foldout { text = "Details", value = _detailsOpen, name = "preparation-details", tooltip = "What can be prepared, what each kind of data does, and where prepared data is stored." };
            _details.AddToClassList("ts-preparation-details");
            _detailsSummary = CommonUITK.CreateLabel(string.Empty, "ts-preparation-details-summary");
            _details.Q<Toggle>()?.Q(className: Toggle.inputUssClassName)?.Add(_detailsSummary);
            _details.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != _details) return;
                _detailsOpen = evt.newValue;
                _renderedSignature = 0;
                Render();
            });
            _search = new ToolbarSearchField { name = "preparation-search", tooltip = "Find text by name, scene, file or kind of data. Preparation always includes everything." };
            _search.AddToClassList("ts-preparation-search");
            SetPlaceholder(_search, "Search text, scenes and files");
            _search.RegisterValueChangedCallback(evt =>
            {
                _query = evt.newValue ?? string.Empty;
                _renderedSignature = 0;
                Render();
            });
            _details.Add(_search);
            _detailsEmpty = Copy(string.Empty, "ts-preparation-details-empty");
            _details.Add(_detailsEmpty);
            _cardList = CommonUITK.CreateContainer("ts-preparation-cards");
            _details.Add(_cardList);
            parent.Add(_details);
        }

        void PrimaryAction()
        {
            if (_summary.CanPrepare > 0)
            {
                PrepareAll();
                return;
            }
            if (_summary.Manual == 0) return;
            _detailsOpen = true;
            _focusAttention = true;
            _renderedSignature = 0;
            Render();
        }

        void RequestCheck()
        {
            if (TextStudioRuntimePreparation.IsBusy) return;
            SessionState.EraseBool(DeclinedKey);
            StartCheck(null);
            Tick();
        }

        void RenderHero(bool busy)
        {
            TextStudioPreparationSummary summary = _summary;
            bool autoBuild = TextStudioRuntimePreparationSettings.instance.automaticInBuilds;
            bool own = _operation != null, preparing = own && _operationPrepares, checking = own && !_operationPrepares;
            bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
            bool complete = summary.Complete;
            int failed = CountFailures();
            bool consent = !busy && !playing && NeedsFontChoice();
            string headline, detail;
            if (preparing)
            {
                headline = "Preparing… " + summary.Prepared + " of " + summary.Preparable;
                detail = CurrentWork();
            }
            else if (summary.Preparable == 0)
            {
                headline = checking ? "Checking your project…" : "Nothing to prepare";
                detail = checking ? "Looking for text in your open scenes and build content." : summary.RuntimeOnly > 0 ? RuntimeOnlyDetail(summary.RuntimeOnly) :
                    "3D text, shape effects such as outlines, and inline images appear here once your " + (_openScenesOnly ? "open scenes" : "project") + " uses them.";
            }
            else if (complete)
            {
                headline = summary.RuntimeOnly == 0 ? "Everything is prepared" : "All preparable text is prepared";
                detail = (summary.Prepared == 1 ? "1 item uses" : summary.Prepared + " items use") + " ready-made data in Play Mode and builds." +
                    (summary.RuntimeOnly > 0 ? " " + RuntimeOnlyDetail(summary.RuntimeOnly) : string.Empty);
            }
            else
            {
                headline = summary.Prepared + " of " + summary.Preparable + " prepared";
                detail = PendingDetail(summary, autoBuild, failed);
            }
            if (busy && !own) detail = "Updating recently edited text. " + detail;
            if (playing && !preparing) detail = "Preparation runs in Edit Mode. Exit Play Mode to prepare.";
            SetText(_headline, headline);
            SetText(_heroDetail, detail);

            bool attention = failed > 0 || summary.Required > 0 && !autoBuild || summary.Manual > 0 && summary.CanPrepare == 0;
            string tone = complete && failed == 0 ? "ready" : summary.Preparable == 0 ? "neutral" : attention ? "attention" : "info";
            foreach (string name in new[] { "ready", "neutral", "attention", "info" })
                _hero.EnableInClassList("ts-preparation-hero--" + name, tone == name);
            _ring.Set(summary.Progress, summary.Preparable == 0 || complete ? "✓" : Mathf.FloorToInt(summary.Progress * 100f) + "%");
            _ring.tooltip = summary.Preparable == 0 ? "Nothing in this scope needs preparation." : summary.Prepared + " of " + summary.Preparable + " items prepared.";

            SetVisible(_consent, consent);
            if (consent)
            {
                int fonts = _items.Count(item => item.RequiresFontEmbedding && TextStudioPreparationView.Pending(item));
                SetText(_consentText, Items(fonts, "needs", "need") + " outlines from your fonts saved with the project and included in builds. Only allow this if your font licenses permit embedding.");
            }
            bool offerPrepare = !consent && !playing && !complete && (summary.CanPrepare > 0 || summary.Manual > 0);
            SetVisible(_actions, offerPrepare || own);
            SetVisible(_primary, offerPrepare || preparing);
            _primary.text = preparing ? "Preparing…" : summary.CanPrepare == 0 ? "Review needed changes" : failed == summary.CanPrepare ? "Try again" : "Prepare " + Items(summary.CanPrepare);
            _primary.tooltip = summary.CanPrepare > 0
                ? "Prepare everything that can be prepared, including items hidden by the Details search. Items that need a change from you are listed in Details."
                : "Show the items that need a change preparation cannot make.";
            _primary.EnableInClassList("ts-primary-button", summary.CanPrepare > 0 || preparing);
            _primary.SetEnabled(!preparing && (!busy || checking));
            SetVisible(_cancel, own);
            _cancel.text = checking ? "Stop check" : "Cancel";
            _cancel.tooltip = checking ? "Stop checking build content. Results found so far are kept." : "Stop the remaining work. Everything prepared so far is kept.";
            _buildToggle.SetValueWithoutNotify(autoBuild);
            RenderScope(checking);
        }

        void RenderScope(bool checking)
        {
            BuildContentSnapshot content = BuildContent;
            string text, action = null;
            Action command = null;
            if (_openScenesOnly)
            {
                text = "Checking open scenes only.";
                action = "Change";
                command = () => SelectTab(SettingsTab);
            }
            else if (checking)
            {
                string file = string.IsNullOrEmpty(_operation?.OperationLabel) ? string.Empty : System.IO.Path.GetFileNameWithoutExtension(_operation.OperationLabel);
                text = "Checking build content" + (file.Length > 0 ? ": " + file : "…");
            }
            else if (!content.Checked)
            {
                text = "Open scenes are checked. Build content is not checked yet.";
                action = "Check now";
                command = RequestCheck;
            }
            else if (!content.Complete)
            {
                text = "The build content check did not finish.";
                action = "Check again";
                command = RequestCheck;
            }
            else
            {
                text = "Includes open scenes and build content, checked " + Age(content.AgeSeconds) + ".";
                action = "Check again";
                command = RequestCheck;
            }
            SetText(_scopeText, text, ScopeTooltip);
            _scopeCommand = command;
            SetVisible(_scopeAction, action != null);
            _scopeAction.text = action ?? string.Empty;
            _scopeAction.tooltip = _openScenesOnly ? "Choose what the overview checks." : "Check enabled build scenes, Resources and preloaded assets again. Checking never changes your content.";
            _scopeAction.SetEnabled(!TextStudioRuntimePreparation.IsBusy || _openScenesOnly);
        }

        string PendingDetail(TextStudioPreparationSummary summary, bool autoBuild, int failed)
        {
            List<string> parts = new List<string>();
            int untried = summary.CanPrepare - failed;
            if (failed > 0) parts.Add(Items(failed, "could", "could") + " not be prepared. Details show why; until then, the work happens at runtime.");
            if (summary.Required > 0 && !autoBuild)
                parts.Add(Items(summary.Required, "is", "are") + " required for builds. Prepare now, or turn on automatic preparation below.");
            else if (untried > 0 && summary.Required > 0)
                parts.Add(CanBePrepared(untried, summary.Prepared) + " Builds prepare required data automatically; preparing now saves that time.");
            else if (untried > 0)
                parts.Add(CanBePrepared(untried, summary.Prepared) + " Until then, " + (untried == 1 ? "it is" : "they are") + " generated at runtime when first shown.");
            if (summary.Manual > 0) parts.Add(Items(summary.Manual, "needs", "need") + " a change from you. Details show what to do.");
            return string.Join(" ", parts);
        }

        static string CanBePrepared(int count, int prepared) =>
            count + (prepared > 0 ? " more" : string.Empty) + (count == 1 ? " item can" : " items can") + " be prepared.";

        static string RuntimeOnlyDetail(int count) => Items(count, "is", "are") + " generated at runtime because preparation is not possible yet. Details show why.";

        string CurrentWork()
        {
            TextStudioPreparationItem active = _operation?.Items.FirstOrDefault(item => item.State == TextStudioPreparationState.Updating);
            if (active != null)
            {
                string location = TextStudioPreparationView.Location(active);
                return TextStudioPreparationView.KindName(active) + ": " + active.Target + (location.Length > 0 ? " in " + location : string.Empty);
            }
            string label = _operation?.OperationLabel;
            return string.IsNullOrEmpty(label) ? "Working through your open scenes." : "Working through " + System.IO.Path.GetFileNameWithoutExtension(label) + ".";
        }

        int CountFailures()
        {
            if (_failures.Count == 0) return 0;
            bool denied = EmbeddingDenied;
            int count = 0;
            for (int i = 0; i < _items.Count; i++)
                if (_failures.ContainsKey(_keys[i]) && TextStudioPreparationView.Preparable(_items[i], denied)) count++;
            return count;
        }

        static void SetPlaceholder(ToolbarSearchField search, string text)
        {
#if UNITY_2023_1_OR_NEWER
            TextField field = search.Q<TextField>();
            if (field != null) field.textEdition.placeholder = text;
#endif
        }

        static string Items(int count, string singularVerb = null, string pluralVerb = null) =>
            count + (count == 1 ? " item" : " items") + (singularVerb == null ? string.Empty : " " + (count == 1 ? singularVerb : pluralVerb));

        static string Age(double seconds) => seconds < 60 ? "just now" : seconds < 3600 ? Mathf.FloorToInt((float)(seconds / 60)) + " min ago" : Mathf.FloorToInt((float)(seconds / 3600)) + " h ago";

        void RenderDetails()
        {
            if (_details.value != _detailsOpen) _details.SetValueWithoutNotify(_detailsOpen);
            TextStudioPreparationSummary summary = _summary;
            // Zero counts are left out, so a finished project reads "19 prepared" rather than "0 to prepare · 19 prepared".
            List<string> parts = new List<string>(4);
            if (summary.CanPrepare > 0) parts.Add(summary.CanPrepare + " to prepare");
            if (summary.Prepared > 0) parts.Add(summary.Prepared + " prepared");
            if (summary.Manual > 0) parts.Add(summary.Manual + " to change");
            if (summary.RuntimeOnly > 0) parts.Add(summary.RuntimeOnly + " at runtime");
            string counts = string.Join(" · ", parts);
            SetText(_detailsSummary, counts, counts);
            if (!_detailsOpen) return;
            bool denied = EmbeddingDenied, autoBuild = TextStudioRuntimePreparationSettings.instance.automaticInBuilds;
            SetVisible(_search, _items.Count > 6 || _query.Length > 0);
            List<IGrouping<string, int>> groups = Enumerable.Range(0, _items.Count)
                .GroupBy(index => TextStudioPreparationView.ProductId(_items[index]) + "/" + TextStudioPreparationView.KindId(_items[index]))
                .OrderBy(group => TextStudioPreparationView.ProductId(_items[group.First()]) == "other" ? 1 : 0)
                .ThenBy(group => TextStudioPreparationView.ProductName(_items[group.First()]), StringComparer.Ordinal)
                .ThenBy(group => TextStudioPreparationView.KindName(_items[group.First()]), StringComparer.Ordinal).ToList();
            RevealRequestedRows(groups, denied);
            HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
            int visible = 0;
            for (int i = 0; i < groups.Count; i++)
            {
                IGrouping<string, int> group = groups[i];
                if (!_cards.TryGetValue(group.Key, out GroupCard card))
                {
                    card = new GroupCard(this, group.Key);
                    _cards.Add(group.Key, card);
                }
                used.Add(group.Key);
                if (card.Root.parent != _cardList || _cardList.IndexOf(card.Root) != i)
                {
                    card.Root.RemoveFromHierarchy();
                    _cardList.Insert(i, card.Root);
                }
                if (card.Update(group.Select(index => _keys[index]).ToList(), group.Select(index => _items[index]).ToList(), _query, denied, autoBuild)) visible++;
            }
            foreach (string key in _cards.Keys.Where(key => !used.Contains(key)).ToArray())
            {
                _cards[key].Root.RemoveFromHierarchy();
                _cards.Remove(key);
            }
            string empty = _items.Count == 0 ? "Nothing in this scope needs preparation." : visible == 0 ? "No text matches your search." : string.Empty;
            SetText(_detailsEmpty, empty);
            SetVisible(_detailsEmpty, empty.Length > 0);
        }

        /// <summary>Expands and scrolls to a text opened from its inspector, or to the first item that needs a change.</summary>
        void RevealRequestedRows(List<IGrouping<string, int>> groups, bool denied)
        {
            if (!_focusPending && !_focusAttention) return;
            Func<TextStudioPreparationItem, bool> wanted = _focusAttention
                ? item => TextStudioPreparationView.Pending(item) && (item.RequiresManualAction || TextStudioPreparationView.Blocked(item, denied))
                : (Func<TextStudioPreparationItem, bool>)(item => _target != null && item.SourceText == _target);
            string key = groups.FirstOrDefault(group => group.Any(index => wanted(_items[index])))?.Key;
            if (key == null)
            {
                // The target may not be analyzed yet. Keep waiting for live results, but never for an attention request.
                if (_focusAttention || _live != null && !_liveDirty) _focusPending = _focusAttention = false;
                return;
            }
            if (!_openGroups.Contains(key)) _openGroups.Add(key);
            _focusPending = _focusAttention = false;
            _overviewScroll.schedule.Execute(() =>
            {
                if (!_cards.TryGetValue(key, out GroupCard card)) return;
                VisualElement row = card.FindRow(wanted);
                _overviewScroll.ScrollTo(row ?? card.Root);
                row?.Focus();
            }).ExecuteLater(60);
        }

        string RowReason(string key, TextStudioPreparationItem item, bool denied, bool autoBuild)
        {
            if (item.State == TextStudioPreparationState.Ready) return string.Empty;
            if (item.State == TextStudioPreparationState.Updating) return string.IsNullOrEmpty(item.Message) ? "Preparing…" : item.Message;
            if (TextStudioPreparationView.Blocked(item, denied)) return "Needs a change · Saving font outlines is turned off. Allow it in Settings to prepare this text.";
            if (item.RequiresManualAction) return (item.Required ? "Needs a change · " : "Generated at runtime · ") + item.Message;
            if (_failures.TryGetValue(key, out string failure)) return "Could not be prepared · " + failure;
            if (!string.IsNullOrEmpty(item.Summary)) return item.Summary;
            // The card already explains the shared consequence; rows only repeat what needs attention.
            return item.Required && !autoBuild ? "Required for builds · prepare it before building" : string.Empty;
        }

        /// <summary>One kind of prepared data, such as 3D letter shapes, with its benefit and per-text rows.</summary>
        sealed class GroupCard
        {
            readonly TextStudioRuntimePreparationWindow _owner;
            readonly string _key;
            internal readonly VisualElement Root;
            readonly Label _title, _product, _count, _benefit, _note, _coverageText;
            readonly VisualElement _fill, _rows, _coverage;
            readonly Foldout _list;
            readonly Button _data, _more, _coverageLink;
            readonly Dictionary<string, Row> _rowsByKey = new Dictionary<string, Row>(StringComparer.Ordinal);
            List<string> _dataPaths = new List<string>(), _orderedKeys = new List<string>();
            Dictionary<string, int> _frozenOrder;
            int _limit = RowLimit;

            internal GroupCard(TextStudioRuntimePreparationWindow owner, string key)
            {
                _owner = owner;
                _key = key;
                Root = CommonUITK.CreateContainer("ts-preparation-card");
                VisualElement heading = CommonUITK.CreateContainer("ts-preparation-card-heading");
                VisualElement titles = CommonUITK.CreateContainer("ts-preparation-card-titles");
                _title = CommonUITK.CreateLabel(string.Empty, "ts-preparation-card-title");
                _product = CommonUITK.CreateLabel(string.Empty, "ts-preparation-card-product");
                titles.Add(_title);
                titles.Add(_product);
                heading.Add(titles);
                _count = CommonUITK.CreateLabel(string.Empty, "ts-preparation-card-count");
                heading.Add(_count);
                Root.Add(heading);
                VisualElement bar = CommonUITK.CreateContainer("ts-preparation-bar");
                _fill = CommonUITK.CreateContainer("ts-preparation-bar-fill");
                bar.Add(_fill);
                Root.Add(bar);
                _benefit = Copy(string.Empty, "ts-preparation-card-benefit");
                _note = Copy(string.Empty, "ts-preparation-card-note");
                Root.Add(_benefit);
                Root.Add(_note);
                _coverage = CommonUITK.CreateContainer("ts-preparation-card-coverage");
                _coverageText = Copy(string.Empty, "ts-preparation-card-note");
                _coverageLink = LinkButton(string.Empty, "Choose the characters prepared for text that changes while playing, such as scores or names.", owner.ShowRuntimeCharacters);
                _coverageLink.name = "preparation-runtime-characters";
                _coverage.Add(_coverageText);
                _coverage.Add(_coverageLink);
                Root.Add(_coverage);
                _data = LinkButton("Show prepared data", "Select the saved assets that hold this kind of prepared data.", () => RevealAssets(_dataPaths));
                _data.AddToClassList("ts-preparation-card-data");
                Root.Add(_data);
                _list = new Foldout { value = owner._openGroups.Contains(key), tooltip = "Every text object that uses this kind of data. Select a row to select its text." };
                _list.AddToClassList("ts-preparation-card-list");
                _list.RegisterValueChangedCallback(evt =>
                {
                    if (evt.target != _list) return;
                    if (!evt.newValue) owner._openGroups.Remove(_key);
                    else if (!owner._openGroups.Contains(_key)) owner._openGroups.Add(_key);
                    owner._renderedSignature = 0;
                    owner.Render();
                });
                _rows = CommonUITK.CreateContainer("ts-preparation-rows");
                _list.Add(_rows);
                _more = LinkButton(string.Empty, "Show every text object in this group.", () =>
                {
                    _limit = int.MaxValue;
                    owner._renderedSignature = 0;
                    owner.Render();
                });
                _more.AddToClassList("ts-preparation-more");
                _list.Add(_more);
                Root.Add(_list);
            }

            internal bool Update(List<string> keys, List<TextStudioPreparationItem> items, string query, bool denied, bool autoBuild)
            {
                TextStudioPreparationItem first = items[0];
                TextStudioPreparationSummary summary = new TextStudioPreparationSummary(items, denied);
                SetText(_title, TextStudioPreparationView.KindName(first));
                SetText(_product, TextStudioPreparationView.ProductName(first));
                SetText(_count, summary.Preparable == 0 ? "Generated at runtime" : summary.Prepared + " of " + summary.Preparable + " prepared");
                _fill.style.width = Length.Percent(100f * summary.Progress);
                Root.EnableInClassList("ts-preparation-card--ready", summary.Complete);
                Root.EnableInClassList("ts-preparation-card--attention", summary.Manual > 0);
                SetText(_benefit, !string.IsNullOrEmpty(first.Presentation?.Benefit) ? first.Presentation.Benefit : "Prepared by " + first.Provider + ".");
                // Once every item is prepared, whether it was optional no longer matters.
                string note = summary.Complete && summary.RuntimeOnly == 0 || items.All(item => item.RequiresManualAction) ? string.Empty
                    : items.Any(item => item.Required) ? "Required in builds. " + (autoBuild ? "Prepared automatically before each build if you skip it here." : "Prepare it before you build.")
                    : "Optional. If not prepared, it is generated at runtime the first time the text appears.";
                SetText(_note, note);
                SetVisible(_note, note.Length > 0);
                bool characters = first.Presentation != null && first.Presentation.UsesCharacters;
                SetVisible(_coverage, characters);
                if (characters)
                {
                    TextStudioRuntimePreparationSettings settings = TextStudioRuntimePreparationSettings.instance;
                    string added = TextStudioRuntimeCharacters.Describe(settings.runtimeCharacters, settings.additionalText);
                    SetText(_coverageText, added.Length == 0
                        ? "Covers the text shown now and text found in localization and add-ons. Scores, names and other text that changes while playing need their characters added."
                        : "Covers the text shown now, text found in localization and add-ons, and " + added + " for text shown at runtime.");
                    _coverageLink.text = added.Length == 0 ? "Add characters" : "Change characters";
                }
                _dataPaths = items.Where(item => item.State == TextStudioPreparationState.Ready).SelectMany(item => item.DataPaths).Distinct().ToList();
                SetVisible(_data, _dataPaths.Count > 0);

                bool searching = !string.IsNullOrWhiteSpace(query);
                // Rows keep their order while an operation runs, so status changes never move them under the pointer.
                if (_owner._operation == null) _frozenOrder = null;
                else if (_frozenOrder == null)
                {
                    _frozenOrder = new Dictionary<string, int>(StringComparer.Ordinal);
                    for (int i = 0; i < _orderedKeys.Count; i++) _frozenOrder[_orderedKeys[i]] = i;
                }
                IEnumerable<int> matches = Enumerable.Range(0, items.Count).Where(index => TextStudioPreparationView.MatchesQuery(items[index], query));
                List<int> visible = (_frozenOrder != null
                        ? matches.OrderBy(index => _frozenOrder.TryGetValue(keys[index], out int order) ? order : int.MaxValue)
                        : matches.OrderBy(index => _owner.RowOrder(keys[index], items[index], denied)))
                    .ThenBy(index => items[index].Target, StringComparer.CurrentCultureIgnoreCase).ToList();
                _orderedKeys = visible.Select(index => keys[index]).ToList();
                SetVisible(Root, !searching || visible.Count > 0);
                _list.text = (searching ? visible.Count + " of " : string.Empty) + items.Count + (items.Count == 1 ? " text object" : " text objects");
                bool open = searching && visible.Count > 0 || _owner._openGroups.Contains(_key);
                if (_list.value != open) _list.SetValueWithoutNotify(open);
                if (!open) return !searching || visible.Count > 0;
                int count = Math.Min(visible.Count, _limit);
                HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < count; i++)
                {
                    int index = visible[i];
                    if (!_rowsByKey.TryGetValue(keys[index], out Row row))
                    {
                        row = new Row();
                        _rowsByKey.Add(keys[index], row);
                    }
                    used.Add(keys[index]);
                    row.Update(items[index], _owner.RowReason(keys[index], items[index], denied, autoBuild), _owner.RowState(keys[index], items[index], denied),
                        _owner._target != null && items[index].SourceText == _owner._target);
                    if (row.Root.parent != _rows || _rows.IndexOf(row.Root) != i)
                    {
                        row.Root.RemoveFromHierarchy();
                        _rows.Insert(i, row.Root);
                    }
                }
                foreach (string key in _rowsByKey.Keys.Where(key => !used.Contains(key)).ToArray())
                {
                    _rowsByKey[key].Root.RemoveFromHierarchy();
                    _rowsByKey.Remove(key);
                }
                SetVisible(_more, visible.Count > count);
                _more.text = "Show all " + visible.Count;
                return true;
            }

            internal VisualElement FindRow(Func<TextStudioPreparationItem, bool> wanted) =>
                _rows.Children().FirstOrDefault(child => child.userData is TextStudioPreparationItem item && wanted(item));
        }

        int RowOrder(string key, TextStudioPreparationItem item, bool denied)
        {
            string state = RowState(key, item, denied);
            return state == "attention" ? 0 : state == "preparing" ? 1 : state == "required" ? 2 : state == "optional" ? 3 : state == "runtime" ? 4 : 5;
        }

        string RowState(string key, TextStudioPreparationItem item, bool denied)
        {
            if (item.State == TextStudioPreparationState.Ready) return "prepared";
            if (item.State == TextStudioPreparationState.Updating) return "preparing";
            if (TextStudioPreparationView.Blocked(item, denied) || item.RequiresManualAction && item.Required || _failures.ContainsKey(key)) return "attention";
            if (item.RequiresManualAction) return "runtime";
            return item.Required ? "required" : "optional";
        }

        /// <summary>A text object's status. Selecting the row selects the text, or its saved scene or prefab when it is not open.</summary>
        sealed class Row
        {
            static readonly string[] States = { "prepared", "preparing", "attention", "required", "optional", "runtime" };
            internal readonly VisualElement Root;
            readonly Label _dot, _title, _location, _reason;
            readonly Button _data;
            TextStudioPreparationItem _item;

            internal Row()
            {
                Root = CommonUITK.CreateContainer("ts-preparation-row");
                Root.focusable = true;
                Root.AddManipulator(new Clickable(Select));
                Root.RegisterCallback<NavigationSubmitEvent>(evt =>
                {
                    if (evt.target != Root) return;
                    Select();
                    evt.StopPropagation();
                });
                _dot = CommonUITK.CreateLabel(string.Empty, "ts-preparation-dot");
                Root.Add(_dot);
                VisualElement body = CommonUITK.CreateContainer("ts-preparation-row-body");
                VisualElement line = CommonUITK.CreateContainer("ts-preparation-row-line");
                _title = CommonUITK.CreateLabel(string.Empty, "ts-preparation-row-title");
                _location = CommonUITK.CreateLabel(string.Empty, "ts-preparation-row-location");
                line.Add(_title);
                line.Add(_location);
                body.Add(line);
                _reason = CommonUITK.CreateLabel(string.Empty, "ts-preparation-row-reason");
                body.Add(_reason);
                Root.Add(body);
                _data = new Button(() => RevealAssets(_item.DataPaths)) { text = "Data", tooltip = "Select the saved assets that hold this text's prepared data." };
                _data.AddToClassList("ts-preparation-row-data");
                Root.Add(_data);
            }

            internal void Update(TextStudioPreparationItem item, string reason, string state, bool focused)
            {
                _item = item;
                Root.userData = item;
                foreach (string name in States) Root.EnableInClassList("ts-preparation-row--" + name, name == state);
                Root.EnableInClassList("ts-preparation-row--focus", focused);
                SetText(_dot, state == "prepared" ? "✓" : state == "preparing" ? "…" : state == "attention" ? "!" : "•",
                    state == "prepared" ? "Prepared" : state == "preparing" ? "Preparing" : state == "attention" ? "Needs a change" : state == "required" ? "Required for builds" :
                    state == "runtime" ? "Generated at runtime" : "Optional");
                SetText(_title, item.Target);
                string location = TextStudioPreparationView.Location(item);
                SetText(_location, location, string.IsNullOrEmpty(item.SourcePath) ? "Open scene " + location : item.SourcePath);
                SetText(_reason, reason);
                SetVisible(_reason, reason.Length > 0);
                SetVisible(_data, item.State == TextStudioPreparationState.Ready && item.DataPaths.Count > 0);
                Root.tooltip = (item.SourceText != null ? "Select this text." : !string.IsNullOrEmpty(item.SourcePath) ? "Select " + item.SourcePath + "." : string.Empty) +
                    (string.IsNullOrEmpty(item.Message) ? string.Empty : "\n\n" + item.Message);
            }

            void Select()
            {
                if (_item == null) return;
                if (_item.SourceText != null) Reveal(_item.SourceText.gameObject);
                else if (!string.IsNullOrEmpty(_item.SourcePath)) Reveal(AssetDatabase.LoadMainAssetAtPath(_item.SourcePath));
            }
        }

        /// <summary>Circular completion indicator. Colors come from USS custom properties so both editor themes stay legible.</summary>
        sealed class Ring : VisualElement
        {
            static readonly CustomStyleProperty<Color> TrackColor = new CustomStyleProperty<Color>("--ts-ring-track");
            static readonly CustomStyleProperty<Color> FillColor = new CustomStyleProperty<Color>("--ts-ring-fill");
            readonly Label _label;
            Color _track = new Color(1f, 1f, 1f, .1f), _fill = new Color(.52f, .87f, .82f);
            float _value = -1f;

            internal Ring()
            {
                AddToClassList("ts-preparation-ring");
                _label = new Label { pickingMode = PickingMode.Ignore };
                _label.AddToClassList("ts-preparation-ring-label");
                Add(_label);
                generateVisualContent += Draw;
                RegisterCallback<CustomStyleResolvedEvent>(evt =>
                {
                    if (evt.customStyle.TryGetValue(TrackColor, out Color track)) _track = track;
                    if (evt.customStyle.TryGetValue(FillColor, out Color fill)) _fill = fill;
                    MarkDirtyRepaint();
                });
            }

            internal void Set(float value, string text)
            {
                if (_label.text != text) _label.text = text;
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(value, _value)) return;
                _value = value;
                MarkDirtyRepaint();
            }

            void Draw(MeshGenerationContext context)
            {
                const float thickness = 6f;
                Rect rect = contentRect;
                float radius = Mathf.Min(rect.width, rect.height) * .5f - thickness * .5f;
                if (radius <= 0f) return;
                Painter2D painter = context.painter2D;
                painter.lineWidth = thickness;
                painter.lineCap = LineCap.Butt;
                painter.strokeColor = _track;
                painter.BeginPath();
                painter.Arc(rect.center, radius, Angle.Degrees(0f), Angle.Degrees(360f));
                painter.Stroke();
                if (_value <= 0f) return;
                painter.lineCap = LineCap.Round;
                painter.strokeColor = _fill;
                painter.BeginPath();
                painter.Arc(rect.center, radius, Angle.Degrees(-90f), Angle.Degrees(-90f + 360f * _value));
                painter.Stroke();
            }
        }
    }
}
