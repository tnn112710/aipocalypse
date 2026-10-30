using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ArmoryCoreEditor
{
    /// <summary>
    /// Layout vocabulary of the ArmoryCore setup inspectors and the Weapon Setup window: numbered steps with a summary,
    /// padded card bodies, badges, a compact issue list, the result of the last action and the primary button.
    /// </summary>
    internal static class ArmoryCoreSetupGUI
    {
        public enum StepState
        {
            Locked,
            Current,
            Done,
            Optional
        }

        private const string KeyPrefix = "ArmoryCore.UI.";
        private static GUIStyle body;
        private static GUIStyle headerLabel;
        private static GUIStyle headerSummary;
        private static GUIStyle badge;
        private static GUIStyle stepBadge;
        private static GUIStyle bullet;
        private static GUIStyle emptyTitle;
        private static GUIStyle emptyText;

        private static Color HeaderBackground => EditorGUIUtility.isProSkin ? new Color(0.20f, 0.21f, 0.23f) : new Color(0.80f, 0.81f, 0.83f);
        private static Color BodyBackground => EditorGUIUtility.isProSkin ? new Color(0.235f, 0.235f, 0.24f) : new Color(0.86f, 0.86f, 0.87f);
        private static Color LockedColor => new Color(0.38f, 0.38f, 0.40f);

        private static void Init()
        {
            if (body != null)
                return;
            body = new GUIStyle { padding = new RectOffset(8, 8, 6, 8) };
            headerLabel = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            headerSummary = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, clipping = TextClipping.Clip };
            headerSummary.normal.textColor = ArmoryCoreGraph.Muted;
            badge = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
            badge.normal.textColor = Color.white;
            stepBadge = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
            stepBadge.normal.textColor = Color.white;
            bullet = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { richText = true, padding = new RectOffset(14, 2, 1, 1) };
            emptyTitle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 13, wordWrap = true };
            emptyText = new GUIStyle(EditorStyles.wordWrappedLabel) { alignment = TextAnchor.MiddleCenter, richText = true };
            emptyText.normal.textColor = ArmoryCoreGraph.Muted;
        }

        /// <summary>Foldout state remembered for the editor session and shared by every inspector and window.</summary>
        public static bool Expanded(string key, bool defaultOpen) => SessionState.GetBool(KeyPrefix + key, defaultOpen);

        public static void SetExpanded(string key, bool open) => SessionState.SetBool(KeyPrefix + key, open);

        /// <summary>Numbered step header. Done steps show a check mark; a locked step explains what is missing in its summary.</summary>
        public static bool Step(string key, int number, string title, StepState state, string summary = null, string tooltip = null)
        {
            Init();
            var open = Expanded(key, state == StepState.Current);
            var header = GUILayoutUtility.GetRect(0f, 26f, GUILayout.ExpandWidth(true));
            var color = state == StepState.Done ? ArmoryCoreGraph.Good
                : state == StepState.Current ? ArmoryCoreGraph.Accent
                : state == StepState.Optional ? ArmoryCoreGraph.Muted
                : LockedColor;
            var circle = new Rect(header.x + 22f, header.y + 4f, 18f, 18f);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(header, HeaderBackground);
                EditorGUI.DrawRect(new Rect(header.x, header.y, 3f, header.height), color);
                EditorGUI.DrawRect(circle, color);
            }
            GUI.Label(circle, state == StepState.Done ? "✓" : number.ToString(), stepBadge);
            if (DrawHeaderContent(header, open, new GUIContent(title, tooltip), summary, 26f))
                Toggle(key, open);
            if (!open)
                GUILayout.Space(3f);
            return open;
        }

        // The new state applies from the next layout pass, so the layout of this event stays consistent.
        private static void Toggle(string key, bool open)
        {
            SetExpanded(key, !open);
            if (EditorWindow.mouseOverWindow)
                EditorWindow.mouseOverWindow.Repaint();
        }

        private static bool DrawHeaderContent(Rect header, bool open, GUIContent title, string summary, float indent)
        {
            var arrow = new Rect(header.x + 6f, header.y + (header.height - 16f) * 0.5f, 14f, 16f);
            var summaryWidth = string.IsNullOrEmpty(summary)
                ? 0f
                : Mathf.Min(header.width * 0.5f, headerSummary.CalcSize(new GUIContent(summary)).x + 4f);
            var label = new Rect(header.x + 20f + indent, header.y, header.width - 28f - indent - summaryWidth, header.height);
            var right = new Rect(header.xMax - summaryWidth - 6f, header.y, summaryWidth, header.height);
            if (Event.current.type == EventType.Repaint)
                EditorStyles.foldout.Draw(arrow, false, false, open, false);
            GUI.Label(label, title, headerLabel);
            if (summaryWidth > 0f)
                GUI.Label(right, new GUIContent(summary, summary), headerSummary);
            if (Event.current.type != EventType.MouseDown || Event.current.button != 0 || !header.Contains(Event.current.mousePosition))
                return false;
            Event.current.Use();
            return true;
        }

        /// <summary>Padded body of a step. Dispose to close it.</summary>
        public static CardBodyScope CardBody()
        {
            Init();
            return new CardBodyScope(body, BodyBackground);
        }

        public readonly struct CardBodyScope : IDisposable
        {
            public CardBodyScope(GUIStyle style, Color background)
            {
                var area = EditorGUILayout.BeginVertical(style);
                if (Event.current.type == EventType.Repaint)
                    EditorGUI.DrawRect(area, background);
            }

            public void Dispose()
            {
                EditorGUILayout.EndVertical();
                GUILayout.Space(4f);
            }
        }

        public readonly struct LabelWidthScope : IDisposable
        {
            private readonly float previous;

            public LabelWidthScope(float width)
            {
                previous = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = width;
            }

            public void Dispose() => EditorGUIUtility.labelWidth = previous;
        }

        public static LabelWidthScope LabelWidth(float panelWidth, float fraction = 0.40f, float min = 120f, float max = 200f)
            => new LabelWidthScope(Mathf.Clamp(panelWidth * fraction, min, max));

        public static void Badge(string text, Color color, float width = 0f)
        {
            Init();
            var content = new GUIContent(text);
            var rect = GUILayoutUtility.GetRect(
                content,
                badge,
                width > 0f ? GUILayout.Width(width) : GUILayout.Width(badge.CalcSize(content).x + 10f),
                GUILayout.Height(16f));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, color * new Color(1f, 1f, 1f, 0.85f));
            GUI.Label(rect, content, badge);
        }

        /// <summary>Centered explanation shown instead of an empty form.</summary>
        public static void Empty(string title, string text, float height = 120f)
        {
            Init();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinHeight(height)))
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(title, emptyTitle);
                GUILayout.Space(4f);
                GUILayout.Label(text, emptyText);
                GUILayout.FlexibleSpace();
            }
        }

        /// <summary>Compact issue list: the first problem is always visible, the rest fold out.</summary>
        public static void Issues(string key, IList<string> issues, string okText = null)
        {
            Init();
            if (issues == null || issues.Count == 0)
            {
                if (!string.IsNullOrEmpty(okText))
                    ArmoryCoreGraph.Line("✓ " + okText, ArmoryCoreGraph.Good);
                return;
            }
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(EditorGUIUtility.IconContent("console.warnicon.sml"), GUILayout.Width(18f), GUILayout.Height(18f));
                    GUILayout.Label(issues.Count == 1 ? "To fix" : $"To fix · {issues.Count}", EditorStyles.boldLabel);
                }
                var open = Expanded(key, false);
                var shown = open ? issues.Count : 1;
                for (var i = 0; i < shown; i++)
                    GUILayout.Label("• " + issues[i], bullet);
                if (issues.Count > 1 && GUILayout.Button(open ? "Collapse" : $"Show {issues.Count - 1} more", EditorStyles.miniButton))
                    SetExpanded(key, !open);
            }
        }

        /// <summary>Dismissible result of the last action. Returns the message, or null once it is dismissed.</summary>
        public static string Status(string text, bool error = false)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(EditorGUIUtility.IconContent(error ? "console.erroricon.sml" : "console.infoicon.sml"), GUILayout.Width(18f), GUILayout.Height(18f));
                GUILayout.Label(text, EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button(new GUIContent("×", "Hide the message"), EditorStyles.miniButton, GUILayout.Width(20f)))
                    return null;
            }
            return text;
        }

        public static bool PrimaryButton(string label, bool enabled = true, float height = 30f, string tooltip = null)
        {
            using (new EditorGUI.DisabledScope(!enabled))
            {
                var previous = GUI.backgroundColor;
                if (enabled)
                    GUI.backgroundColor = ArmoryCoreGraph.Accent * 1.25f;
                var pressed = GUILayout.Button(new GUIContent(label, tooltip), GUILayout.Height(height));
                GUI.backgroundColor = previous;
                return pressed;
            }
        }

        public static bool Button(string label, bool enabled = true, string tooltip = null, params GUILayoutOption[] options)
        {
            using (new EditorGUI.DisabledScope(!enabled))
                return GUILayout.Button(new GUIContent(label, tooltip), options);
        }

        public static bool MiniButton(string label, string tooltip = null, float width = 0f)
        {
            return width > 0f
                ? GUILayout.Button(new GUIContent(label, tooltip), EditorStyles.miniButton, GUILayout.Width(width))
                : GUILayout.Button(new GUIContent(label, tooltip), EditorStyles.miniButton);
        }

        /// <summary>Object field with a button that takes the current selection.</summary>
        public static T ObjectWithSelection<T>(string label, T value, bool allowScene, string tooltip = null, Func<GameObject, T> fromSelection = null)
            where T : UnityEngine.Object
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                value = (T)EditorGUILayout.ObjectField(new GUIContent(label, tooltip), value, typeof(T), allowScene);
                var selected = Selection.activeGameObject;
                var candidate = !selected ? null
                    : fromSelection != null ? fromSelection(selected)
                    : typeof(T) == typeof(GameObject) ? selected as T
                    : selected.GetComponentInParent(typeof(T)) as T;
                using (new EditorGUI.DisabledScope(!candidate || candidate == value))
                {
                    if (GUILayout.Button(new GUIContent("From selection", "Take the object selected in the Hierarchy or Scene view"), EditorStyles.miniButton, GUILayout.Width(96f), GUILayout.Height(18f)))
                    {
                        value = candidate;
                        GUI.changed = true;
                    }
                }
            }
            return value;
        }

        /// <summary>Marks a changed object so prefab overrides and the scene are saved.</summary>
        public static void MarkChanged(UnityEngine.Object value)
        {
            if (!value)
                return;
            EditorUtility.SetDirty(value);
            if (PrefabUtility.IsPartOfPrefabInstance(value))
                PrefabUtility.RecordPrefabInstancePropertyModifications(value);
            var gameObject = value is GameObject go ? go : value is Component component ? component.gameObject : null;
            if (gameObject && gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
    }
}
