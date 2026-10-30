using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ArmoryCoreEditor
{
    /// <summary>
    /// Graphical controls of the ArmoryCore inspectors: scales with named reference marks, choice and toggle tiles,
    /// bars and drawing primitives for small diagrams. Every scale value stays one click away from exact entry: click the
    /// value to type it, or switch on «123» to show all numbers.
    /// </summary>
    internal static class ArmoryCoreGraph
    {
        public static readonly Color Accent = new Color(0.95f, 0.43f, 0.12f, 1f);
        public static readonly Color Good = new Color(0.36f, 0.80f, 0.46f, 1f);
        public static readonly Color Warn = new Color(1f, 0.73f, 0.27f, 1f);
        public static readonly Color Bad = new Color(1f, 0.40f, 0.34f, 1f);
        public static readonly Color Info = new Color(0.32f, 0.62f, 1f, 1f);
        public static readonly Color Muted = new Color(0.55f, 0.57f, 0.60f, 1f);

        public readonly struct Mark
        {
            public readonly float Value;
            public readonly string Name;

            public Mark(float value, string name)
            {
                Value = value;
                Name = name;
            }
        }

        public static Mark M(float value, string name) => new Mark(value, name);

        /// <summary>Range and presentation of a scale. Values are shown multiplied by <see cref="Display"/>.</summary>
        public sealed class Spec
        {
            public readonly float Min;
            public readonly float Max;
            public readonly float Display;
            public readonly bool Log;
            public readonly string Unit;
            public readonly string Format;
            public string Low;
            public string High;
            public Color LowColor = Muted;
            public Color HighColor = Accent;
            public Mark[] Marks = Array.Empty<Mark>();
            /// <summary>Limits of a typed value; dragging stays inside Min..Max.</summary>
            public float Floor = float.NegativeInfinity;
            public float Ceiling = float.PositiveInfinity;

            public Spec(float min, float max, string unit = "", float display = 1f, string format = "0.##", bool log = false)
            {
                Min = min;
                Max = max;
                Unit = unit;
                Display = display;
                Format = format;
                Log = log && min > 0f;
            }

            public Spec Captions(string low, string high)
            {
                Low = low;
                High = high;
                return this;
            }

            public Spec Colors(Color low, Color high)
            {
                LowColor = low;
                HighColor = high;
                return this;
            }

            public Spec With(params Mark[] marks)
            {
                Marks = marks;
                return this;
            }

            public Spec Limits(float floor, float ceiling = float.PositiveInfinity)
            {
                Floor = floor;
                Ceiling = ceiling;
                return this;
            }

            public float ToT(float value) => Log
                ? Mathf.InverseLerp(Mathf.Log(Min), Mathf.Log(Max), Mathf.Log(Mathf.Max(value, Min * 0.001f)))
                : Mathf.InverseLerp(Min, Max, value);

            public float FromT(float t) => Log ? Mathf.Exp(Mathf.Lerp(Mathf.Log(Min), Mathf.Log(Max), t)) : Mathf.Lerp(Min, Max, t);
            public string Text(float value) => (value * Display).ToString(Format) + (string.IsNullOrEmpty(Unit) ? "" : " " + Unit);
            public Color ColorAt(float t) => Color.Lerp(LowColor, HighColor, t);
        }

        /// <summary>A choice or toggle tile: title, optional one-line explanation and a drawn icon or glyph.</summary>
        public sealed class Tile
        {
            public string Title;
            public string Text;
            public string Tooltip;
            public string Glyph;
            public Color Color = Accent;
            public Action<Rect, Color> Icon;
            /// <summary>Width of the drawn icon in heights: 2 gives a side view of a whole weapon room to read.</summary>
            public float IconAspect = 1f;
            /// <summary>Asset shown in a card while the mouse rests on the tile, such as the prefab of an attachment.</summary>
            public UnityEngine.Object Preview;

            public Tile(string title, string text = null, string tooltip = null)
            {
                Title = title;
                Text = text;
                Tooltip = tooltip;
            }
        }

        /// <summary>The tile with a preview under the mouse in this repaint, and where it is; read by <see cref="ArmoryCoreHoverPreview"/>.</summary>
        public static Tile Hovered;
        public static Rect HoveredRect;

        private const string NumbersKey = "ArmoryCore.UI.ShowNumbers";
        private const string ExactControl = "ArmoryCoreGraph.Exact";
        private static readonly int ScaleHint = "ArmoryCoreGraph.Scale".GetHashCode();
        private static readonly Dictionary<long, Texture2D> Gradients = new Dictionary<long, Texture2D>();
        private static int editingId;
        private static bool focusPending;
        private static bool? showNumbers;
        private static Texture2D circle;
        private static GUIStyle label;
        private static GUIStyle valueStyle;
        private static GUIStyle valueField;
        private static GUIStyle caption;
        private static GUIStyle captionLeft;
        private static GUIStyle captionRight;
        private static GUIStyle tileTitle;
        private static GUIStyle tileText;
        private static GUIStyle glyph;
        private static GUIStyle hint;
        private static GUIStyle barValue;

        /// <summary>Shows a typed number next to every scale (expert mode, remembered per user).</summary>
        public static bool ShowNumbers
        {
            get => showNumbers ??= EditorPrefs.GetBool(NumbersKey, false);
            set
            {
                showNumbers = value;
                EditorPrefs.SetBool(NumbersKey, value);
            }
        }

        public static void NumbersToggle()
        {
            var next = GUILayout.Toggle(
                ShowNumbers,
                new GUIContent("123", "Show exact numbers next to the scales. Without it, click the value of a scale to type it."),
                EditorStyles.miniButton,
                GUILayout.Width(36f));
            if (next != ShowNumbers)
                ShowNumbers = next;
        }

        private static void Init()
        {
            if (label != null)
                return;
            label = new GUIStyle(EditorStyles.label) { clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft };
            valueStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
            valueField = new GUIStyle(EditorStyles.miniTextField) { alignment = TextAnchor.MiddleRight };
            caption = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter, clipping = TextClipping.Overflow };
            caption.normal.textColor = Muted;
            captionLeft = new GUIStyle(caption) { alignment = TextAnchor.UpperLeft };
            captionRight = new GUIStyle(caption) { alignment = TextAnchor.UpperRight };
            hint = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { richText = true };
            hint.normal.textColor = Pro ? new Color(0.68f, 0.70f, 0.73f) : new Color(0.28f, 0.29f, 0.31f);
            tileTitle = new GUIStyle(EditorStyles.boldLabel) { clipping = TextClipping.Clip };
            tileText = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { clipping = TextClipping.Clip };
            tileText.normal.textColor = hint.normal.textColor;
            glyph = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            barValue = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, clipping = TextClipping.Clip };
        }

        // ---------- Colors and drawing primitives ----------

        private static bool Pro => EditorGUIUtility.isProSkin;
        private static bool Painting => Event.current.type == EventType.Repaint;
        public static Color PanelColor => Pro ? new Color(0.17f, 0.17f, 0.18f) : new Color(0.76f, 0.76f, 0.78f);
        public static Color TrackColor => Pro ? new Color(0.12f, 0.12f, 0.13f) : new Color(0.6f, 0.6f, 0.62f);
        public static Color GridColor => Pro ? new Color(1f, 1f, 1f, 0.06f) : new Color(0f, 0f, 0f, 0.08f);
        public static Color Ink => Pro ? new Color(0.86f, 0.87f, 0.89f) : new Color(0.12f, 0.12f, 0.14f);

        public static Color Shade(Color color, float factor) => new Color(color.r * factor, color.g * factor, color.b * factor, color.a);
        private static Color Tint(Color color) => GUI.enabled ? color : new Color(color.r, color.g, color.b, color.a * 0.4f);

        public static void Hint(string text)
        {
            Init();
            GUILayout.Label(text, hint);
        }

        /// <summary>One-line colored message.</summary>
        public static void Line(string text, Color color)
        {
            Init();
            var previous = hint.normal.textColor;
            hint.normal.textColor = color;
            GUILayout.Label(text, hint);
            hint.normal.textColor = previous;
        }

        public static void FillRect(Rect rect, Color color)
        {
            if (Painting)
                EditorGUI.DrawRect(rect, Tint(color));
        }

        public static void Frame(Rect rect, Color color, float width = 1f)
        {
            FillRect(new Rect(rect.x, rect.y, rect.width, width), color);
            FillRect(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            FillRect(new Rect(rect.x, rect.y, width, rect.height), color);
            FillRect(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }

        public static void Panel(Rect rect)
        {
            FillRect(rect, PanelColor);
            Frame(rect, Pro ? new Color(0f, 0f, 0f, 0.35f) : new Color(0f, 0f, 0f, 0.15f));
        }

        public static void Disc(Vector2 center, float radius, Color color)
        {
            if (!Painting || radius <= 0f)
                return;
            GUI.DrawTexture(
                new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f),
                Circle, ScaleMode.StretchToFill, true, 0f, Tint(color), 0f, 0f);
        }

        public static void Line(Vector2 a, Vector2 b, Color color, float width = 2f)
        {
            if (!Painting)
                return;
            var delta = b - a;
            var length = delta.magnitude;
            if (length < 0.01f)
                return;
            var previous = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, a);
            GUI.DrawTexture(
                new Rect(a.x, a.y - width * 0.5f, length, width),
                EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, true, 0f, Tint(color), 0f, 0f);
            GUI.matrix = previous;
        }

        public static void Polyline(IList<Vector2> points, Color color, float width = 2f)
        {
            for (var i = 1; i < points.Count; i++)
                Line(points[i - 1], points[i], color, width);
            if (width < 2f)
                return;
            for (var i = 1; i < points.Count - 1; i++)
                Disc(points[i], width * 0.5f, color);
        }

        /// <summary>Antialiased fill of a convex polygon in GUI coordinates, clipped like any other GUI drawing.</summary>
        public static void Fill(Color color, params Vector2[] polygon)
        {
            if (!Painting || polygon.Length < 3)
                return;
            var points = new Vector3[polygon.Length];
            for (var i = 0; i < polygon.Length; i++)
                points[i] = polygon[i];
            var previous = Handles.color;
            Handles.color = Tint(color);
            Handles.DrawAAConvexPolygon(points);
            Handles.color = previous;
        }

        /// <summary>Rectangle through <see cref="Fill(Color, Vector2[])"/>, so it matches the polygons it touches.</summary>
        public static void Fill(Color color, Rect rect)
        {
            Fill(color, new Vector2(rect.x, rect.y), new Vector2(rect.xMax, rect.y), new Vector2(rect.xMax, rect.yMax), new Vector2(rect.x, rect.yMax));
        }

        private static Texture2D Circle
        {
            get
            {
                if (circle)
                    return circle;
                const int size = 64;
                circle = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                var pixels = new Color[size * size];
                var r = size * 0.5f;
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                        pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r))));
                }
                circle.SetPixels(pixels);
                circle.Apply();
                return circle;
            }
        }

        /// <summary>Cached horizontal gradient texture from <paramref name="low"/> to <paramref name="high"/>.</summary>
        private static Texture2D Gradient(Color low, Color high)
        {
            Color32 a = low;
            Color32 b = high;
            var key = (long)(a.r | a.g << 8 | a.b << 16) << 24 | (uint)(b.r | b.g << 8 | b.b << 16);
            if (Gradients.TryGetValue(key, out var texture) && texture)
                return texture;
            texture = new Texture2D(64, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[64];
            for (var i = 0; i < 64; i++)
            {
                pixels[i] = Color.Lerp(low, high, i / 63f);
                pixels[i].a = 1f;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            Gradients[key] = texture;
            return texture;
        }

        private static void Caption(Rect rect, string text, GUIStyle style, Color? color = null)
        {
            Init();
            if (!color.HasValue)
            {
                GUI.Label(rect, text, style);
                return;
            }
            var previous = style.normal.textColor;
            style.normal.textColor = color.Value;
            GUI.Label(rect, text, style);
            style.normal.textColor = previous;
        }

        /// <summary>Colored bar with a label and a right-aligned value.</summary>
        public static void Bar(string title, float value01, Color color, string value = null, string tooltip = null)
        {
            Init();
            var rect = GUILayoutUtility.GetRect(60f, 18f, GUILayout.ExpandWidth(true));
            FillRect(rect, TrackColor);
            FillRect(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value01), rect.height), color * new Color(1f, 1f, 1f, 0.75f));
            var inner = new Rect(rect.x + 6f, rect.y, rect.width - 12f, rect.height);
            GUI.Label(inner, new GUIContent(title, tooltip), EditorStyles.miniBoldLabel);
            if (!string.IsNullOrEmpty(value))
                GUI.Label(inner, value, barValue);
            GUILayout.Space(2f);
        }

        // ---------- Scale: a slider with gradient, reference marks and click-to-type value ----------

        /// <summary>Slider over <paramref name="spec"/> with named marks (click a name to jump to it). Click the value to type it exactly.</summary>
        public static float Scale(GUIContent title, float current, Spec spec)
        {
            Init();
            var captions = spec.Marks.Length > 0 || spec.Low != null || spec.High != null;
            var rect = GUILayoutUtility.GetRect(140f, captions ? 46f : 34f, GUILayout.ExpandWidth(true));
            rect = EditorGUI.IndentedRect(rect);
            var id = GUIUtility.GetControlID(ScaleHint, FocusType.Passive, rect);
            var e = Event.current;
            const float bubbleWidth = 92f;
            var bubble = new Rect(rect.xMax - bubbleWidth, rect.y + 1f, bubbleWidth, 16f);
            var track = new Rect(rect.x + 8f, rect.y + 24f, rect.width - 16f, 5f);
            var hit = new Rect(rect.x, rect.y + 18f, rect.width, 15f);
            var t = spec.ToT(current);

            GUI.Label(new Rect(rect.x, rect.y, rect.width - bubbleWidth - 4f, 18f), title, label);

            if (ShowNumbers || editingId == id)
            {
                var unitWidth = string.IsNullOrEmpty(spec.Unit) ? 0f : Mathf.Min(40f, EditorStyles.miniLabel.CalcSize(new GUIContent(spec.Unit)).x + 2f);
                var field = new Rect(bubble.x, bubble.y, bubble.width - unitWidth - 2f, bubble.height);
                if (editingId == id)
                    GUI.SetNextControlName(ExactControl);
                var indent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
                EditorGUI.BeginChangeCheck();
                var typed = EditorGUI.DelayedFloatField(field, current * spec.Display, valueField);
                if (EditorGUI.EndChangeCheck())
                {
                    current = Mathf.Clamp(typed / spec.Display, spec.Floor, spec.Ceiling);
                    t = spec.ToT(current);
                    GUI.changed = true;
                    if (editingId == id)
                        editingId = 0;
                }
                EditorGUI.indentLevel = indent;
                if (unitWidth > 0f)
                    GUI.Label(new Rect(field.xMax + 2f, bubble.y, unitWidth, bubble.height), spec.Unit, EditorStyles.miniLabel);
                if (editingId == id)
                {
                    if (focusPending)
                    {
                        EditorGUI.FocusTextInControl(ExactControl);
                        focusPending = false;
                    }
                    else if (e.type == EventType.Repaint && GUI.GetNameOfFocusedControl() != ExactControl)
                    {
                        editingId = 0;
                    }
                }
            }
            else
            {
                var tone = spec.ColorAt(t);
                FillRect(bubble, new Color(tone.r, tone.g, tone.b, 0.28f));
                GUI.Label(bubble, new GUIContent(spec.Text(current), "Click to type an exact value"), valueStyle);
                if (GUI.enabled)
                {
                    EditorGUIUtility.AddCursorRect(bubble, MouseCursor.Text);
                    if (e.type == EventType.MouseDown && e.button == 0 && bubble.Contains(e.mousePosition))
                    {
                        editingId = id;
                        focusPending = true;
                        e.Use();
                    }
                }
            }

            if (Painting)
            {
                FillRect(track, TrackColor);
                if (t > 0f)
                {
                    var previous = GUI.color;
                    GUI.color = Tint(Color.white);
                    GUI.DrawTextureWithTexCoords(
                        new Rect(track.x, track.y, track.width * t, track.height),
                        Gradient(spec.LowColor, spec.HighColor),
                        new Rect(0f, 0f, t, 1f));
                    GUI.color = previous;
                }
                foreach (var mark in spec.Marks)
                {
                    FillRect(
                        new Rect(track.x + track.width * spec.ToT(mark.Value) - 0.5f, track.y - 3f, 1f, track.height + 6f),
                        new Color(Ink.r, Ink.g, Ink.b, 0.45f));
                }
                var knob = new Vector2(track.x + track.width * t, track.center.y);
                Disc(knob, 7f, spec.ColorAt(t));
                Disc(knob, 3.5f, Pro ? new Color(0.92f, 0.92f, 0.94f) : Color.white);
            }

            if (captions)
                current = DrawCaptions(rect, track, spec, current);

            if (!GUI.enabled)
                return current;
            EditorGUIUtility.AddCursorRect(hit, MouseCursor.SlideArrow);
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (e.button != 0 || !hit.Contains(e.mousePosition))
                        break;
                    GUIUtility.hotControl = id;
                    editingId = 0;
                    GUI.FocusControl(null);
                    current = Pick(spec, track, e.mousePosition.x);
                    GUI.changed = true;
                    e.Use();
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != id)
                        break;
                    current = Pick(spec, track, e.mousePosition.x);
                    GUI.changed = true;
                    e.Use();
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;
            }
            return current;
        }

        private static float DrawCaptions(Rect rect, Rect track, Spec spec, float current)
        {
            var e = Event.current;
            var y = track.yMax + 4f;
            var lowEdge = rect.x;
            var highEdge = rect.xMax;
            if (spec.Low != null)
            {
                var width = caption.CalcSize(new GUIContent(spec.Low)).x;
                Caption(new Rect(rect.x, y, width, 13f), spec.Low, captionLeft);
                lowEdge = rect.x + width + 4f;
            }
            if (spec.High != null)
            {
                var width = caption.CalcSize(new GUIContent(spec.High)).x;
                Caption(new Rect(rect.xMax - width, y, width, 13f), spec.High, captionRight);
                highEdge = rect.xMax - width - 4f;
            }
            var right = lowEdge;
            foreach (var mark in spec.Marks)
            {
                var x = track.x + track.width * spec.ToT(mark.Value);
                var width = caption.CalcSize(new GUIContent(mark.Name)).x;
                var area = new Rect(x - width * 0.5f, y, width, 13f);
                if (area.x < right || area.xMax > highEdge)
                    continue;
                right = area.xMax + 4f;
                var active = Mathf.Abs(spec.ToT(current) - spec.ToT(mark.Value)) < 0.004f;
                Caption(area, mark.Name, caption, active ? Ink : (Color?)null);
                if (!GUI.enabled)
                    continue;
                EditorGUIUtility.AddCursorRect(area, MouseCursor.Link);
                if (e.type == EventType.MouseDown && e.button == 0 && area.Contains(e.mousePosition))
                {
                    current = mark.Value;
                    editingId = 0;
                    GUI.changed = true;
                    e.Use();
                }
            }
            return current;
        }

        private static float Pick(Spec spec, Rect track, float mouseX)
        {
            foreach (var mark in spec.Marks)
            {
                if (Mathf.Abs(track.x + track.width * spec.ToT(mark.Value) - mouseX) < 5f)
                    return mark.Value;
            }
            return Nice(spec.FromT(Mathf.Clamp01((mouseX - track.x) / track.width)), spec.Display);
        }

        /// <summary>Rounds to three significant digits of the displayed value so dragged numbers stay readable.</summary>
        public static float Nice(float stored, float display = 1f)
        {
            var shown = stored * display;
            if (Mathf.Abs(shown) < 1e-9f)
                return 0f;
            var step = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(Mathf.Abs(shown))) - 2f);
            return Mathf.Round(shown / step) * step / display;
        }

        public static bool Scale(SerializedObject data, string name, string title, string tooltip, Spec spec)
        {
            var property = data.FindProperty(name);
            if (property == null)
                return false;
            var integer = property.propertyType == SerializedPropertyType.Integer;
            EditorGUI.BeginChangeCheck();
            var next = Scale(
                new GUIContent(title, tooltip ?? property.tooltip),
                integer ? property.intValue : property.floatValue,
                spec);
            if (!EditorGUI.EndChangeCheck())
                return false;
            if (integer)
                property.intValue = Mathf.RoundToInt(next);
            else
                property.floatValue = next;
            return true;
        }

        // ---------- Tiles ----------

        private static bool DrawTile(Rect rect, Tile tile, bool on, bool toggle)
        {
            Init();
            var e = Event.current;
            var r = new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width - 3f, rect.height - 3f);
            var hover = GUI.enabled && r.Contains(e.mousePosition);
            if (tile.Preview && e.type == EventType.Repaint && r.Contains(e.mousePosition))
            {
                Hovered = tile;
                HoveredRect = r;
            }
            FillRect(r, on ? Color.Lerp(PanelColor, tile.Color, 0.3f) : hover ? Color.Lerp(PanelColor, tile.Color, 0.1f) : PanelColor);
            if (on)
                Frame(r, tile.Color, 2f);
            else
                Frame(r, Pro ? new Color(1f, 1f, 1f, 0.07f) : new Color(0f, 0f, 0f, 0.12f));
            var iconColor = on ? tile.Color : Muted;
            var iconSize = Mathf.Min(r.height - 10f, 30f);
            var icon = tile.Icon != null || tile.Glyph != null;
            var iconRect = new Rect(r.x + 6f, r.y + (r.height - iconSize) * 0.5f, tile.Icon != null ? iconSize * tile.IconAspect : iconSize, iconSize);
            if (tile.Icon != null)
                tile.Icon(iconRect, iconColor);
            else if (tile.Glyph != null)
                Caption(iconRect, tile.Glyph, glyph, Tint(iconColor));
            var textX = icon ? iconRect.xMax + 6f : r.x + 8f;
            var textWidth = r.xMax - textX - (toggle ? 16f : 4f);
            var text = !string.IsNullOrEmpty(tile.Text) && r.height >= 36f;
            var titleRect = new Rect(textX, text ? r.y + 3f : r.y + (r.height - 18f) * 0.5f, textWidth, 18f);
            GUI.Label(titleRect, tile.Title, tileTitle);
            if (text)
                GUI.Label(new Rect(textX, titleRect.yMax - 3f, textWidth, r.yMax - titleRect.yMax + 1f), tile.Text, tileText);
            if (toggle)
            {
                var lamp = new Vector2(r.xMax - 9f, r.y + 9f);
                Disc(lamp, 5f, on ? tile.Color : TrackColor);
                if (!on)
                    Disc(lamp, 3f, PanelColor);
            }
            if (!string.IsNullOrEmpty(tile.Tooltip) && !tile.Preview)
                GUI.Label(r, new GUIContent("", tile.Tooltip));
            if (!GUI.enabled || e.type != EventType.MouseDown || e.button != 0 || !r.Contains(e.mousePosition))
                return false;
            e.Use();
            return true;
        }

        /// <summary>Row(s) of tiles; returns the selected index.</summary>
        public static int Choice(int current, IList<Tile> tiles, int columns = 0, float height = 44f)
        {
            columns = columns <= 0 ? tiles.Count : columns;
            var chosen = current;
            for (var start = 0; start < tiles.Count; start += columns)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (var index = start; index < start + columns; index++)
                    {
                        var rect = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(40f, height, GUILayout.ExpandWidth(true)));
                        if (index < tiles.Count && DrawTile(rect, tiles[index], index == current, false))
                            chosen = index;
                    }
                }
            }
            if (chosen != current)
                GUI.changed = true;
            return chosen;
        }

        /// <summary>Tile that switches a flag. Several calls inside a horizontal scope share the row.</summary>
        public static bool ToggleTile(bool on, Tile tile, float height = 40f)
        {
            var rect = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(40f, height, GUILayout.ExpandWidth(true)));
            if (!DrawTile(rect, tile, on, true))
                return on;
            GUI.changed = true;
            return !on;
        }

        public static bool ToggleTile(SerializedProperty property, Tile tile, float height = 40f)
        {
            if (property == null)
                return false;
            var next = ToggleTile(property.boolValue, tile, height);
            if (next == property.boolValue)
                return false;
            property.boolValue = next;
            return true;
        }

        /// <summary>Enum as tiles: tile <c>i</c> stands for enum index <c>i</c>.</summary>
        public static bool Choice(SerializedProperty property, IList<Tile> tiles, int columns = 0, float height = 44f)
        {
            if (property == null)
                return false;
            var next = Choice(property.enumValueIndex, tiles, columns, height);
            if (next == property.enumValueIndex)
                return false;
            property.enumValueIndex = next;
            return true;
        }
    }
}
