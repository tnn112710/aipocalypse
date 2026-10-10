using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal sealed partial class TextStudioRuntimePreparationWindow
    {
        TextStudioPreparationReport _saved;
        Label _dataSummary, _dataEmpty;
        Toggle _dataUnreferenced;
        ToolbarSearchField _dataSearch;
        VisualElement _dataList;

        void BuildInventory(VisualElement parent)
        {
            VisualElement header = CommonUITK.CreateContainer("ts-preparation-data-header");
            VisualElement titles = CommonUITK.CreateContainer("ts-preparation-data-titles");
            titles.Add(CommonUITK.CreateLabel("Prepared data", "ts-preparation-section-title"));
            _dataSummary = Copy(string.Empty, "ts-preparation-data-summary");
            titles.Add(_dataSummary);
            header.Add(titles);
            header.Add(Button("Refresh", "Check the saved assets again. Checking never changes or deletes assets.", () => RefreshInventory(true), false, "preparation-data-refresh"));
            parent.Add(header);
            parent.Add(Copy("Text Studio creates and updates these assets for you. Select one to inspect its source, coverage and storage.", "ts-preparation-section-intro"));
            VisualElement filters = CommonUITK.CreateContainer("ts-preparation-data-filters");
            _dataSearch = new ToolbarSearchField { name = "preparation-data-search", tooltip = "Find prepared data by asset name, path or kind." };
            _dataSearch.AddToClassList("ts-preparation-search");
            SetPlaceholder(_dataSearch, "Search prepared data");
            _dataSearch.RegisterValueChangedCallback(_ => RefreshInventory(false));
            filters.Add(_dataSearch);
            _dataUnreferenced = new Toggle { text = "Show assets not used by builds", name = "preparation-data-unreferenced",
                tooltip = "Include assets not found in enabled build scenes, Resources or preloaded assets. Code, automatic binding or custom loading may still use them. Nothing is deleted here." };
            _dataUnreferenced.RegisterValueChangedCallback(_ => RefreshInventory(false));
            filters.Add(_dataUnreferenced);
            parent.Add(filters);
            _dataEmpty = Copy(string.Empty, "ts-preparation-details-empty");
            parent.Add(_dataEmpty);
            _dataList = CommonUITK.CreateContainer("ts-preparation-cards");
            parent.Add(_dataList);
        }

        void RefreshInventory(bool analyze)
        {
            if (_dataList == null) return;
            if (analyze || _saved == null) _saved = TextStudioRuntimePreparation.AnalyzeSavedAssets();
            IReadOnlyList<TextStudioPreparationItem> all = _saved.Items;
            int outdated = all.Count(item => item.State != TextStudioPreparationState.Ready);
            int unreferenced = all.Count(TextStudioPreparationView.NotReferenced);
            long bytes = all.Sum(item => SizeOnDisk(item.Target));
            SetText(_dataSummary, all.Count == 0 ? "No prepared data yet." :
                all.Count + (all.Count == 1 ? " asset · " : " assets · ") + EditorUtility.FormatBytes(bytes) + " on disk" + (outdated > 0 ? " · " + outdated + (outdated == 1 ? " needs updating" : " need updating") : string.Empty));
            _dataUnreferenced.text = "Show assets not used by builds" + (unreferenced > 0 ? " (" + unreferenced + ")" : string.Empty);
            SetVisible(_dataUnreferenced, unreferenced > 0 || _dataUnreferenced.value);
            List<TextStudioPreparationItem> visible = all.Where(item => TextStudioPreparationView.Matches(item, true, _dataUnreferenced.value, string.Empty, string.Empty, 0, _dataSearch.value)).ToList();
            string empty = all.Count == 0 ? "Prepared data appears here after Text Studio prepares your text." :
                visible.Count == 0 ? (string.IsNullOrWhiteSpace(_dataSearch.value) ? "All prepared data is outside your build content. Show assets not used by builds to list it." : "No prepared data matches your search.") : string.Empty;
            SetText(_dataEmpty, empty);
            SetVisible(_dataEmpty, empty.Length > 0);
            _dataList.Clear();
            foreach (IGrouping<string, TextStudioPreparationItem> group in visible.GroupBy(item => TextStudioPreparationView.ProductId(item) + "/" + TextStudioPreparationView.KindId(item))
                         .OrderBy(group => TextStudioPreparationView.ProductName(group.First()), StringComparer.Ordinal).ThenBy(group => TextStudioPreparationView.KindName(group.First()), StringComparer.Ordinal))
            {
                TextStudioPreparationItem first = group.First();
                VisualElement card = CommonUITK.CreateContainer("ts-preparation-card");
                VisualElement heading = CommonUITK.CreateContainer("ts-preparation-card-heading");
                VisualElement titles = CommonUITK.CreateContainer("ts-preparation-card-titles");
                titles.Add(CommonUITK.CreateLabel(TextStudioPreparationView.KindName(first), "ts-preparation-card-title"));
                titles.Add(CommonUITK.CreateLabel(TextStudioPreparationView.ProductName(first), "ts-preparation-card-product"));
                heading.Add(titles);
                heading.Add(CommonUITK.CreateLabel(EditorUtility.FormatBytes(group.Sum(item => SizeOnDisk(item.Target))), "ts-preparation-card-count"));
                card.Add(heading);
                VisualElement rows = CommonUITK.CreateContainer("ts-preparation-rows");
                foreach (TextStudioPreparationItem item in group.OrderBy(item => item.State == TextStudioPreparationState.Ready ? 1 : 0).ThenBy(item => DisplayName(item.Target), StringComparer.CurrentCultureIgnoreCase))
                    rows.Add(CreateAssetRow(item));
                card.Add(rows);
                _dataList.Add(card);
            }
        }

        static VisualElement CreateAssetRow(TextStudioPreparationItem item)
        {
            bool current = item.State == TextStudioPreparationState.Ready;
            VisualElement row = CommonUITK.CreateContainer("ts-preparation-row", current ? "ts-preparation-row--prepared" : "ts-preparation-row--attention");
            row.focusable = true;
            void Select() => Reveal(AssetDatabase.LoadMainAssetAtPath(item.Target));
            row.AddManipulator(new Clickable(Select));
            row.RegisterCallback<NavigationSubmitEvent>(_ => Select());
            row.tooltip = item.Target + "\n\n" + item.Message;
            Label dot = CommonUITK.CreateLabel(current ? "✓" : "!", "ts-preparation-dot");
            dot.tooltip = current ? "Current" : "Needs updating";
            row.Add(dot);
            VisualElement body = CommonUITK.CreateContainer("ts-preparation-row-body");
            VisualElement line = CommonUITK.CreateContainer("ts-preparation-row-line");
            line.Add(CommonUITK.CreateLabel(DisplayName(item.Target), "ts-preparation-row-title"));
            Label size = CommonUITK.CreateLabel(EditorUtility.FormatBytes(SizeOnDisk(item.Target)), "ts-preparation-row-location");
            size.tooltip = "Size of the asset file on disk.";
            line.Add(size);
            body.Add(line);
            string reference = TextStudioPreparationView.NotReferenced(item) ? "Not found in build content" : item.BuildReference == TextStudioPreparationReference.Found ? "Used by build content" : "Build use not checked";
            body.Add(CommonUITK.CreateLabel((current ? "Current" : "Needs updating") + " · " + reference, "ts-preparation-row-reason"));
            row.Add(body);
            return row;
        }

        /// <summary>Generated font data files are named by font identity; show the font instead.</summary>
        static string DisplayName(string path)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset is TextStudioFontAppearanceData font) return font.SourceName;
            return asset != null ? asset.name : Path.GetFileNameWithoutExtension(path);
        }

        static long SizeOnDisk(string path)
        {
            try { return File.Exists(path) ? new FileInfo(path).Length : 0; }
            catch (IOException) { return 0; }
            catch (UnauthorizedAccessException) { return 0; }
        }
    }
}
