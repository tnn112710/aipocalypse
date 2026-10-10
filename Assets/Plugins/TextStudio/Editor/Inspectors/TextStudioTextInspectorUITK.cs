using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using TextStudio.BuiltIns;
using TMPro;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    internal partial class TextStudioTextEditor : ITextStudioAuthoringSurfaceProvider
    {
        const string UnityAlignedFieldClass = "unity-base-field__aligned";
        const string DefaultEffectModeTooltip =
            "None: no default effects are added. Fallback: defaults apply only where no explicit effect tags are present. Constant: defaults apply to the full text, even where explicit effects are present.";
#if UNITY_6000_7_OR_NEWER
        // Segmented-control classes are immutable inspector metadata; mutable inspector state still auto-resets.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        static readonly CommonUITK.SegmentedControlClasses PreviewModeSegmentedClasses =
            new CommonUITK.SegmentedControlClasses
            {
                RootClass = "ts-preview-mode-segmented",
                ButtonClass = "ts-preview-mode-segment-button",
                ActiveButtonClass = "ts-preview-mode-segment-active",
                FirstButtonClass = "ts-preview-mode-segment-first",
                LastButtonClass = "ts-preview-mode-segment-last"
            };
        static string s_markerRoute = TextStudioBuiltInActionIds.Event;
        static string s_markerParameters = string.Empty;
        const int ContentTabIndex = 0;
        const int TypewriterTabIndex = 1;
        const int PlacementTabIndex = 2;
        const int AppearanceTabIndex = 3;
        const int EventsTabIndex = 4;
        const int DebugTabIndex = 5;

        VisualElement _uitkTabBody;
        CommonTabbedPane _uitkTabs;
        TextField _uitkSourceField;
        TextStudioSourceHighlightElement _uitkSourceHighlight;
        TextStudioInlineTagHelpElement _uitkInlineTagHelp;
        VisualElement _uitkDiagnostics;
        VisualElement _profileReactionRecipeRoot;
        SerializedObject _profileReactionObject;
        Label _uitkTagSummary;
        VisualElement _uitkRoot;
        bool _uitkApplyingStylePreset;
        bool _isEditingAssignedProfile;
        bool _uitkActiveTabRebuildPending;
        TextStudioText _uitkPendingActiveTabHub;
        TextStudioAuthoringSurface _uitkSurface;
        TextStudioText _uitkSurfaceHub;
#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        static readonly GUIContent[] s_AuthoringSurfaceTabs =
        {
            new GUIContent("Content", "Show Content settings."),
            new GUIContent("Typewriter", "Show Typewriter settings."),
            new GUIContent("Placement", "Show Placement settings."),
            new GUIContent("Appearance", "Assign and inspect the persistent 2D appearance stack."),
            new GUIContent("Events", "Show Events settings."),
            new GUIContent("Debug", "Show Debug settings.")
        };

        public override VisualElement CreateInspectorGUI()
        {
            ReleaseUitkBindings();
            TextStudioText hub = (TextStudioText)target;
            hub.Backends.Refresh();
            CacheSerializedProperties();
            serializedObject.Update();

            _uitkSurfaceHub = hub;
            _uitkSurface = new TextStudioAuthoringSurface(this);
            return _uitkSurface.Build();
        }

        void OnDisable()
        {
            ReleaseUitkBindings();
        }

        void OnUitkRootDetached(DetachFromPanelEvent evt)
        {
            VisualElement detachedRoot = evt.currentTarget as VisualElement;
            TryUnbind(detachedRoot);
            if (ReferenceEquals(_uitkRoot, detachedRoot))
                ClearUitkReferences();
        }

        void ReleaseUitkBindings()
        {
            TryUnbind(_uitkTabBody);
            TryUnbind(_uitkRoot);
            ClearUitkReferences();
        }

        void ClearUitkReferences()
        {
            ReleaseInlineWritingEditor();
            _inlineAppearanceEditor?.Dispose();
            _inlineAppearanceEditor = null;
            _uitkInlineTagHelp?.DetachSourceField();
            _uitkTabBody = null;
            _uitkTabs = null;
            _uitkSourceField = null;
            _uitkSourceHighlight = null;
            _uitkInlineTagHelp = null;
            _uitkDiagnostics = null;
            _profileReactionRecipeRoot = null;
            _profileReactionObject = null;
            _uitkTagSummary = null;
            _uitkRoot = null;
            _uitkActiveTabRebuildPending = false;
            _uitkPendingActiveTabHub = null;
            _uitkSurface = null;
            _uitkSurfaceHub = null;
        }

        static void TryUnbind(VisualElement element)
        {
            if (element == null)
                return;

            try
            {
                element.Unbind();
            }
            catch (NullReferenceException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        internal static StyleSheet LoadTextStudioInspectorStyleSheet()
        {
            return CommonUITK.LoadStyleSheetFromAnchor(
                "TextStudioTextInspectorUITK",
                "/Editor/Inspectors/TextStudioTextInspectorUITK.cs",
                "/Editor/Inspectors/TextStudioTextInspector.uss");
        }


        VisualElement BuildUitkAuthoringModeControl(
            TextStudioText hub,
            VisualElement liveBody)
        {
            Action sync = null;
            GUIContent[] options =
            {
                new GUIContent("Raw", "Edit canonical Text Studio markup. TMP shows the authored tags."),
                new GUIContent("Live", "Compile the markup, drive TMP output, and use playback controls.")
            };
            int selectedIndex = hub.AuthoringMode == TextStudioAuthoringMode.Raw ? 0 : 1;
            VisualElement control = CommonUITK.CreateSegmentedControl(
                options,
                selectedIndex,
                index =>
                {
                    ApplyAuthoringMode(
                        hub,
                        index == 0 ? TextStudioAuthoringMode.Raw : TextStudioAuthoringMode.Preview);
                    sync?.Invoke();
                },
                PreviewModeSegmentedClasses);
            control.name = "ts-authoring-mode-control";

            int buttonIndex = 0;
            foreach (VisualElement child in control.Children())
            {
                if (!(child is Button button))
                    continue;

                button.name = buttonIndex == 0
                    ? "ts-authoring-raw-button"
                    : "ts-authoring-preview-button";
                buttonIndex++;
            }

            sync = () =>
            {
                bool rawActive = hub.AuthoringMode == TextStudioAuthoringMode.Raw;
                CommonUITK.RefreshSegmentedControl(
                    control,
                    rawActive ? 0 : 1,
                    "ts-preview-mode-segment-active");
                liveBody.style.display = rawActive ? DisplayStyle.None : DisplayStyle.Flex;
            };

            sync();

            TextStudioInspectorElements.SchedulePeriodicRefresh(_uitkRoot, () =>
            {
                if (!CanRunScheduledInspectorUpdate(control, hub))
                    return;

                sync();
            }, 120);

            return control;
        }


        internal static bool[] CalculateVisibleSettingsTabs(float availableWidth, int selectedIndex)
        {
            return TextStudioAuthoringSurface.CalculateVisibleTabs(
                s_AuthoringSurfaceTabs,
                availableWidth,
                selectedIndex);
        }

        void BuildUitkActiveTab(TextStudioText hub)
        {
            if (_uitkSurface != null)
            {
                _uitkSurfaceHub = hub;
                _uitkSurface.RebuildSelectedTab();
            }
        }

        VisualElement BuildUitkTabContent(TextStudioText hub, int tabIndex)
        {
            return tabIndex switch
            {
                ContentTabIndex => BuildUitkContentTab(hub),
                TypewriterTabIndex => BuildUitkTypewriterTab(hub),
                PlacementTabIndex => BuildUitkPlacementTab(),
                AppearanceTabIndex => BuildUitkAppearanceTab(hub),
                EventsTabIndex => BuildUitkEventsTab(hub),
                DebugTabIndex => BuildUitkDebugTab(hub),
                _ => null
            };
        }

        void RequestUitkActiveTabRebuild(TextStudioText hub)
        {
            if (_uitkTabBody == null || hub == null)
                return;

            _uitkPendingActiveTabHub = hub;
            if (_uitkActiveTabRebuildPending)
                return;

            _uitkActiveTabRebuildPending = true;
            VisualElement scheduledBody = _uitkTabBody;
            scheduledBody.schedule.Execute(() =>
            {
                _uitkActiveTabRebuildPending = false;
                TextStudioText pendingHub = _uitkPendingActiveTabHub;
                _uitkPendingActiveTabHub = null;
                if (!CanRunScheduledInspectorUpdate(scheduledBody, pendingHub))
                    return;

                BuildUitkActiveTab(pendingHub);
            });
        }

        static bool UsesProfileBackedTab(TextStudioText hub, int tab)
        {
            if (hub == null)
                return false;

            return tab == ContentTabIndex
                ? hub.UsesBehaviorProfileDefaultEffects
                : tab == TypewriterTabIndex && hub.UsesBehaviorProfileTypewriter;
        }
        string ITextStudioAuthoringSurfaceProvider.RootName => "ts-text-inspector";
        IReadOnlyList<GUIContent> ITextStudioAuthoringSurfaceProvider.Tabs => s_AuthoringSurfaceTabs;
        int ITextStudioAuthoringSurfaceProvider.SelectedTab
        {
            get => s_activeTab;
            set => s_activeTab = value;
        }

        VisualElement ITextStudioAuthoringSurfaceProvider.BuildPreview()
        {
            return BuildUitkPreviewPanel(_uitkSurfaceHub);
        }

        VisualElement ITextStudioAuthoringSurfaceProvider.BuildTab(int tabIndex)
        {
            return BuildUitkTabContent(_uitkSurfaceHub, tabIndex);
        }

        bool ITextStudioAuthoringSurfaceProvider.IsTabApplicable(int tabIndex)
        {
            return IsUitkTabApplicable(tabIndex);
        }

        void ITextStudioAuthoringSurfaceProvider.OnSurfaceCreated(VisualElement root)
        {
            _uitkRoot = root;
        }

        void ITextStudioAuthoringSurfaceProvider.OnTabsCreated(CommonTabbedPane tabs)
        {
            _uitkTabs = tabs;
            _uitkTabBody = tabs?.Body;
        }

        void ITextStudioAuthoringSurfaceProvider.BeforeTabRebuild(VisualElement body)
        {
            serializedObject.UpdateIfRequiredOrScript();
            body?.Unbind();
            ReleaseInlineWritingEditor();
            _uitkInlineTagHelp?.DetachSourceField();
            _uitkSourceField = null;
            _uitkSourceHighlight = null;
            _uitkInlineTagHelp = null;
            _uitkDiagnostics = null;
            _profileReactionRecipeRoot = null;
            _profileReactionObject = null;
            _uitkTagSummary = null;
        }

        void ITextStudioAuthoringSurfaceProvider.AfterTabRebuild(VisualElement body, int tabIndex)
        {
            if (body != null && !UsesProfileBackedTab(_uitkSurfaceHub, tabIndex))
                body.Bind(serializedObject);
            if (_profileReactionRecipeRoot != null && _profileReactionObject != null)
                BindProfileOwnedSubtree(_profileReactionRecipeRoot, _profileReactionObject);
            if (_inlineWritingRoot != null && _inlineWritingEditor != null)
                _inlineWritingRoot.Bind(_inlineWritingEditor.serializedObject);
        }

        void ITextStudioAuthoringSurfaceProvider.OnSurfaceDetached(VisualElement root)
        {
            TryUnbind(root);
            if (ReferenceEquals(_uitkRoot, root))
                ClearUitkReferences();
        }
    }
}
