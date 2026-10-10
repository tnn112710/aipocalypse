using ImpossibleRobert.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal sealed class TextStudioHealthWindow : EditorWindow
    {
        TextStudioHealthStatus _status;

        public static void ShowWindow()
        {
            TextStudioHealthWindow window = GetWindow<TextStudioHealthWindow>();
            window.titleContent = new GUIContent("Text Studio Health");
            window.minSize = new Vector2(540f, 380f);
            window.Refresh();
            window.Show();
        }

        void OnEnable()
        {
            TextStudioIntegrationPackageTracker.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            TextStudioIntegrationPackageTracker.Changed -= Refresh;
        }

        void CreateGUI()
        {
            if (_status == null)
                Refresh();
            else
                BuildUI();
        }

        void BuildUI()
        {
            rootVisualElement.Clear();
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-health-window-root");
            root.style.flexGrow = 1f;
            CommonUITK.ApplyWindowLayout(root);
            rootVisualElement.Add(root);

            VisualElement header = TextStudioInspectorElements.CreateSection(
                "Project Health",
                "ts-health-header");
            header.Add(TextStudioInspectorElements.CreateMutedText(
                _status.ErrorCount + " errors · " + _status.WarningCount + " warnings",
                "ts-health-summary"));
            Button refresh = TextStudioInspectorElements.CreateButton(
                "Refresh",
                "Run Text Studio's project health checks again.",
                Refresh,
                primary: true);
            header.Add(refresh);
            root.Add(header);

            ScrollView items = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "ts-health-results"
            };
            items.style.flexGrow = 1f;
            root.Add(items);

            if (_status.Items.Count == 0)
            {
                CommonEmptyState empty = new CommonEmptyState(new CommonEmptyState.EmptyStateClasses
                {
                    RootClass = "ts-section",
                    TitleClass = "ts-section-title",
                    DetailClass = "ts-muted-text"
                });
                empty.SetContent("Everything looks healthy", "No Text Studio project issues were found.");
                items.Add(empty);
                return;
            }

            for (int i = 0; i < _status.Items.Count; i++)
                items.Add(CreateItem(_status.Items[i]));
        }

        void Refresh()
        {
            _status = TextStudioHealthCheck.Run();
            BuildUI();
        }

        static HelpBox CreateItem(TextStudioHealthItem item)
        {
            HelpBoxMessageType type = item.Severity == TextStudioHealthSeverity.Error ? HelpBoxMessageType.Error :
                item.Severity == TextStudioHealthSeverity.Warning ? HelpBoxMessageType.Warning : HelpBoxMessageType.Info;
            string message = string.IsNullOrEmpty(item.Action)
                ? item.Detail
                : item.Detail + "\nNext: " + item.Action;

            HelpBox helpBox = TextStudioInspectorElements.CreateHelpBox(item.Title + "\n" + message, type);
            if (item.Target == null)
                return helpBox;

            helpBox.tooltip = "Click to ping " + item.Target.name + " in the Editor.";
            helpBox.focusable = true;
            helpBox.AddManipulator(new Clickable(() => PingTarget(item.Target)));
            helpBox.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter && evt.keyCode != KeyCode.Space)
                    return;

                PingTarget(item.Target);
                evt.StopPropagation();
            });
            return helpBox;
        }

        static void PingTarget(Object target)
        {
            if (target != null)
                EditorGUIUtility.PingObject(target);
        }
    }
}
