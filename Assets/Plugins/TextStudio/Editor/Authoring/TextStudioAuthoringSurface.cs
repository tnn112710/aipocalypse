using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal interface ITextStudioAuthoringSurfaceProvider
    {
        string RootName { get; }
        IReadOnlyList<GUIContent> Tabs { get; }
        int SelectedTab { get; set; }
        VisualElement BuildPreview();
        VisualElement BuildTab(int tabIndex);
        bool IsTabApplicable(int tabIndex);
        void OnSurfaceCreated(VisualElement root);
        void OnTabsCreated(CommonTabbedPane tabs);
        void BeforeTabRebuild(VisualElement body);
        void AfterTabRebuild(VisualElement body, int tabIndex);
        void OnSurfaceDetached(VisualElement root);
    }

    internal sealed class TextStudioAuthoringSurface
    {
        const float TabOverflowWidth = 30f;
        const float TabHorizontalInset = 8f;

        readonly ITextStudioAuthoringSurfaceProvider _provider;
        readonly List<Button> _tabButtons = new List<Button>();
        readonly List<int> _hiddenTabs = new List<int>();

        VisualElement _root;
        CommonTabbedPane _tabs;
        Button _overflow;
        float _availableWidth;

        public TextStudioAuthoringSurface(ITextStudioAuthoringSurfaceProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public VisualElement Root => _root;
        public CommonTabbedPane Tabs => _tabs;
        public VisualElement Body => _tabs?.Body;

        public VisualElement Build()
        {
            _root = TextStudioInspectorElements.CreateRoot(_provider.RootName);
            _provider.OnSurfaceCreated(_root);
            _root.RegisterCallback<DetachFromPanelEvent>(OnDetached);

            VisualElement preview = _provider.BuildPreview();
            if (preview != null)
                _root.Add(preview);

            _tabs = CreateTabs();
            _root.Add(_tabs);
            _provider.OnTabsCreated(_tabs);
            RebuildSelectedTab();
            return _root;
        }

        public void RebuildSelectedTab()
        {
            if (_tabs == null)
                return;

            int selected = NormalizeSelectedTab(_provider.SelectedTab);
            _provider.SelectedTab = selected;
            VisualElement body = _tabs.Body;
            _provider.BeforeTabRebuild(body);
            body.Clear();
            VisualElement tab = _provider.BuildTab(selected);
            if (tab != null)
                body.Add(tab);
            _provider.AfterTabRebuild(body, selected);
            RefreshTabs();
        }

        public void RefreshTabs()
        {
            if (_tabs == null)
                return;

            int selected = NormalizeSelectedTab(_provider.SelectedTab);
            _provider.SelectedTab = selected;
            _tabs.SetSelectedIndexWithoutNotify(selected);
            for (int i = 0; i < _provider.Tabs.Count; i++)
                _tabs.SetTabEnabled(i, _provider.IsTabApplicable(i));
            RefreshOverflow();
        }

        CommonTabbedPane CreateTabs()
        {
            CommonTabbedPane pane = new CommonTabbedPane(new CommonTabbedPane.TabbedPaneClasses
            {
                RootClass = "ts-tab-pane",
                HeaderClass = "ts-tab-bar",
                TabStripClass = "ts-tab-strip",
                TabClass = "ts-tab-toggle",
                SelectedTabClass = "ts-tab-active",
                TrailingClass = "ts-tab-overflow-slot",
                BodyClass = "ts-tab-body"
            });

            Action<int> selectTab = index =>
            {
                if (index < 0 || index >= _provider.Tabs.Count || !_provider.IsTabApplicable(index))
                    return;

                _provider.SelectedTab = index;
                RebuildSelectedTab();
            };
            pane.SetTabs(_provider.Tabs, NormalizeSelectedTab(_provider.SelectedTab), selectTab);
            foreach (VisualElement child in pane.TabStrip.Children())
            {
                if (child is Button button)
                    _tabButtons.Add(button);
            }

            _overflow = new Button
            {
                name = "ts-tab-overflow-menu",
                text = "...",
                tooltip = "Show settings tabs that do not fit in the tab strip."
            };
            _overflow.AddToClassList("ts-tab-overflow-button");
            _overflow.clicked += ShowOverflowMenu;
            pane.Trailing.Add(_overflow);
            pane.Trailing.style.display = DisplayStyle.None;
            pane.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                _availableWidth = evt.newRect.width;
                RefreshOverflow();
            });
            return pane;
        }

        void RefreshOverflow()
        {
            if (_tabs == null)
                return;

            _hiddenTabs.Clear();
            bool[] visible = CalculateVisibleTabs(
                _provider.Tabs,
                _availableWidth,
                _provider.SelectedTab);
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                bool show = i < visible.Length && visible[i];
                _tabButtons[i].style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (!show)
                    _hiddenTabs.Add(i);
            }

            _tabs.Trailing.style.display =
                _hiddenTabs.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void ShowOverflowMenu()
        {
            if (_overflow == null || _hiddenTabs.Count == 0)
                return;

            GenericMenu menu = new GenericMenu();
            for (int i = 0; i < _hiddenTabs.Count; i++)
            {
                int tabIndex = _hiddenTabs[i];
                GUIContent tab = _provider.Tabs[tabIndex];
                menu.AddItem(tab, false, () =>
                {
                    _provider.SelectedTab = tabIndex;
                    RebuildSelectedTab();
                });
            }
            CommonUITK.ShowGenericMenu(menu, _overflow);
        }

        int NormalizeSelectedTab(int selected)
        {
            int count = _provider.Tabs.Count;
            if (count == 0)
                return 0;

            selected = Mathf.Clamp(selected, 0, count - 1);
            if (_provider.IsTabApplicable(selected))
                return selected;
            for (int i = 0; i < count; i++)
            {
                if (_provider.IsTabApplicable(i))
                    return i;
            }
            return selected;
        }

        void OnDetached(DetachFromPanelEvent evt)
        {
            VisualElement detached = evt.currentTarget as VisualElement;
            _provider.OnSurfaceDetached(detached);
        }

        internal static bool[] CalculateVisibleTabs(
            IReadOnlyList<GUIContent> tabs,
            float availableWidth,
            int selectedIndex)
        {
            int count = tabs?.Count ?? 0;
            bool[] visible = new bool[count];
            if (count == 0)
                return visible;

            selectedIndex = Mathf.Clamp(selectedIndex, 0, count - 1);
            float allTabsWidth = 0f;
            for (int i = 0; i < count; i++)
                allTabsWidth += GetTabMinimumWidth(tabs[i]?.text);

            float availableForTabs = Mathf.Max(0f, availableWidth - TabHorizontalInset);
            if (allTabsWidth <= availableForTabs)
            {
                for (int i = 0; i < count; i++)
                    visible[i] = true;
                return visible;
            }

            float remaining = Mathf.Max(0f, availableForTabs - TabOverflowWidth);
            visible[selectedIndex] = true;
            remaining -= GetTabMinimumWidth(tabs[selectedIndex]?.text);
            for (int i = 0; i < count; i++)
            {
                if (i == selectedIndex)
                    continue;

                float tabWidth = GetTabMinimumWidth(tabs[i]?.text);
                if (tabWidth > remaining)
                    continue;
                visible[i] = true;
                remaining -= tabWidth;
            }
            return visible;
        }

        static float GetTabMinimumWidth(string label)
        {
            int length = string.IsNullOrEmpty(label) ? 0 : label.Length;
            return Mathf.Max(56f, length * 7f + 18f);
        }
    }
}
