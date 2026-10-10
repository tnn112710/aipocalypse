using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal static class TextStudioGradientSerializedPropertyUtility
    {
        const float MatchTolerance = 0.001f;

        internal static TextStudio2DPaintKind GetPaintKind(SerializedProperty paint)
        {
            SerializedProperty kind = paint?.FindPropertyRelative("_kind");
            return kind != null
                ? (TextStudio2DPaintKind)kind.intValue
                : TextStudio2DPaintKind.Solid;
        }

        internal static void SetPaintKind(
            SerializedProperty paint,
            TextStudio2DPaintKind kind,
            string undoName)
        {
            if (paint == null)
                return;

            RecordTargets(paint.serializedObject, undoName);
            paint.FindPropertyRelative("_kind").intValue = (int)kind;
            Apply(paint.serializedObject);
        }

        internal static void ApplySpatialRecipe(
            SerializedProperty paint,
            TextStudioGradientRecipe recipe,
            string undoName = "Apply Gradient Recipe")
        {
            if (paint == null || recipe == null || !TextStudioGradientCatalog.IsGradient(recipe.Kind))
                return;

            SerializedObject owner = paint.serializedObject;
            RecordTargets(owner, undoName);
            paint.FindPropertyRelative("_kind").intValue = (int)recipe.Kind;
            paint.FindPropertyRelative("_angle").floatValue = recipe.Angle;
            paint.FindPropertyRelative("_center").vector2Value = recipe.Center;
            paint.FindPropertyRelative("_scale").vector2Value = recipe.Scale;
            paint.FindPropertyRelative("_offset").vector2Value = recipe.Offset;

            if (recipe.Kind == TextStudio2DPaintKind.FourCornerGradient)
            {
                WriteFourCorner(
                    paint.FindPropertyRelative("_fourCornerGradient"),
                    recipe.FourCorner);
            }
            else
            {
                WriteStops(
                    paint.FindPropertyRelative("_gradientStops"),
                    recipe.CopyStops());
            }
            Apply(owner);
        }

        internal static void ApplyRamp(
            SerializedProperty gradientProperty,
            TextStudioGradientRecipe recipe,
            string undoName = "Apply Gradient Recipe")
        {
            if (gradientProperty == null || recipe == null || recipe.StopCount < 2)
                return;

            SerializedObject owner = gradientProperty.serializedObject;
            RecordTargets(owner, undoName);
            gradientProperty.gradientValue = recipe.CreateUnityGradient();
            Apply(owner);
        }

        internal static void WriteGradientStops(
            SerializedProperty paint,
            Gradient gradient,
            string undoName = "Edit Custom Gradient")
        {
            if (paint == null || gradient == null)
                return;

            List<float> times = new List<float>(16);
            GradientColorKey[] colorKeys = gradient.colorKeys;
            GradientAlphaKey[] alphaKeys = gradient.alphaKeys;
            for (int i = 0; i < colorKeys.Length; i++)
                AddUniqueTime(times, colorKeys[i].time);
            for (int i = 0; i < alphaKeys.Length; i++)
                AddUniqueTime(times, alphaKeys[i].time);
            times.Sort();
            if (times.Count == 0)
            {
                times.Add(0f);
                times.Add(1f);
            }
            else if (times.Count == 1)
            {
                times.Add(Mathf.Approximately(times[0], 0f) ? 1f : 0f);
                times.Sort();
            }

            int count = Mathf.Min(times.Count, 16);
            TextStudio2DGradientStop[] stops = new TextStudio2DGradientStop[count];
            for (int i = 0; i < count; i++)
            {
                float time = times[i];
                stops[i] = new TextStudio2DGradientStop(time, gradient.Evaluate(time));
            }

            SerializedObject owner = paint.serializedObject;
            RecordTargets(owner, undoName);
            WriteStops(paint.FindPropertyRelative("_gradientStops"), stops);
            Apply(owner);
        }

        internal static void WriteFourCornerColor(
            SerializedProperty paint,
            string cornerName,
            Color color,
            string undoName = "Edit Four Corner Gradient")
        {
            SerializedProperty corner = paint?
                .FindPropertyRelative("_fourCornerGradient")?
                .FindPropertyRelative(cornerName);
            if (corner == null)
                return;

            SerializedObject owner = paint.serializedObject;
            RecordTargets(owner, undoName);
            corner.colorValue = color;
            Apply(owner);
        }

        internal static Gradient CreateGradientFromStops(SerializedProperty paint)
        {
            SerializedProperty stops = paint?.FindPropertyRelative("_gradientStops");
            if (stops == null || !stops.isArray || stops.arraySize < 2)
                return TextStudioGradientCatalog
                    .GetDefault(TextStudio2DPaintKind.LinearGradient)
                    .CreateUnityGradient();

            int keyCount = Mathf.Min(stops.arraySize, 8);
            GradientColorKey[] colors = new GradientColorKey[keyCount];
            GradientAlphaKey[] alphas = new GradientAlphaKey[keyCount];
            for (int i = 0; i < keyCount; i++)
            {
                int sourceIndex = keyCount == stops.arraySize
                    ? i
                    : Mathf.RoundToInt(i * (stops.arraySize - 1f) / (keyCount - 1f));
                SerializedProperty stop = stops.GetArrayElementAtIndex(sourceIndex);
                float position = Mathf.Clamp01(stop.FindPropertyRelative("_position").floatValue);
                Color color = stop.FindPropertyRelative("_color").colorValue;
                colors[i] = new GradientColorKey(color, position);
                alphas[i] = new GradientAlphaKey(color.a, position);
            }

            Gradient gradient = new Gradient();
            gradient.SetKeys(colors, alphas);
            gradient.mode = GradientMode.Blend;
            return gradient;
        }

        internal static TextStudioGradientRecipe FindSpatialMatch(SerializedProperty paint)
        {
            if (paint == null)
                return null;

            TextStudio2DPaintKind kind = GetPaintKind(paint);
            IReadOnlyList<TextStudioGradientRecipe> recipes = TextStudioGradientCatalog.ForKind(kind);
            for (int i = 0; i < recipes.Count; i++)
            {
                TextStudioGradientRecipe recipe = recipes[i];
                if (MatchesSpatial(paint, recipe))
                    return recipe;
            }
            return null;
        }

        internal static TextStudioGradientRecipe FindRampMatch(Gradient gradient)
        {
            if (gradient == null)
                return null;

            IReadOnlyList<TextStudioGradientRecipe> ramps = TextStudioGradientCatalog.Ramps;
            for (int i = 0; i < ramps.Count; i++)
            {
                if (MatchesGradient(gradient, ramps[i]))
                    return ramps[i];
            }
            return null;
        }

        internal static bool HasValidPayload(SerializedProperty paint, TextStudio2DPaintKind kind)
        {
            if (paint == null)
                return false;
            if (TextStudioGradientCatalog.IsStopGradient(kind))
            {
                SerializedProperty stops = paint.FindPropertyRelative("_gradientStops");
                return stops != null && stops.isArray && stops.arraySize >= 2;
            }
            if (kind == TextStudio2DPaintKind.FourCornerGradient)
            {
                SerializedProperty corners = paint.FindPropertyRelative("_fourCornerGradient");
                if (corners == null)
                    return false;
                Color sum = ReadCorner(corners, "_topLeft") +
                            ReadCorner(corners, "_topRight") +
                            ReadCorner(corners, "_bottomLeft") +
                            ReadCorner(corners, "_bottomRight");
                return sum.maxColorComponent > 0.0001f || sum.a > 0.0001f;
            }
            return true;
        }

        internal static Func<float, float, Color> CreateSpatialEvaluator(SerializedProperty paint)
        {
            if (paint == null)
                return (_, _) => Color.clear;

            TextStudio2DPaintKind kind = GetPaintKind(paint);
            float angle = paint.FindPropertyRelative("_angle").floatValue;
            Vector2 center = paint.FindPropertyRelative("_center").vector2Value;
            Vector2 scale = paint.FindPropertyRelative("_scale").vector2Value;
            Vector2 offset = paint.FindPropertyRelative("_offset").vector2Value;
            TextStudio2DGradientSpread spread = (TextStudio2DGradientSpread)paint
                .FindPropertyRelative("_spread").enumValueIndex;
            Color tint = paint.FindPropertyRelative("_color").colorValue;
            TextStudio2DGradientStop[] stops = ReadStops(paint.FindPropertyRelative("_gradientStops"));
            SerializedProperty corners = paint.FindPropertyRelative("_fourCornerGradient");
            TextStudio2DFourCornerGradient fourCorner = new TextStudio2DFourCornerGradient(
                ReadCorner(corners, "_topLeft"),
                ReadCorner(corners, "_topRight"),
                ReadCorner(corners, "_bottomLeft"),
                ReadCorner(corners, "_bottomRight"));

            return (u, v) =>
            {
                Vector2 safeScale = new Vector2(
                    Mathf.Abs(scale.x) > 0.00001f ? scale.x : 0.00001f,
                    Mathf.Abs(scale.y) > 0.00001f ? scale.y : 0.00001f);
                Vector2 delta = new Vector2(
                    (u - center.x - offset.x) / safeScale.x,
                    (v - center.y - offset.y) / safeScale.y);
                Color result;
                if (kind == TextStudio2DPaintKind.FourCornerGradient)
                {
                    result = fourCorner.Evaluate(
                        Mathf.Clamp01(delta.x + 0.5f),
                        Mathf.Clamp01(delta.y + 0.5f));
                }
                else
                {
                    float coordinate;
                    if (kind == TextStudio2DPaintKind.LinearGradient)
                    {
                        float radians = angle * Mathf.Deg2Rad;
                        Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                        coordinate = ApplySpread(
                            Vector2.Dot(delta, direction) + 0.5f,
                            spread);
                    }
                    else if (kind == TextStudio2DPaintKind.RadialGradient)
                    {
                        coordinate = ApplySpread(delta.magnitude * 2f, spread);
                    }
                    else if (kind == TextStudio2DPaintKind.DistanceGradient)
                    {
                        coordinate = ApplySpread(u, spread);
                    }
                    else
                    {
                        float turn = Mathf.Atan2(delta.y, delta.x) / (Mathf.PI * 2f);
                        coordinate = Mathf.Repeat(turn - angle / 360f + 1f, 1f);
                    }
                    result = EvaluateStops(stops, coordinate);
                }
                return result * tint;
            };
        }

        static float ApplySpread(float coordinate, TextStudio2DGradientSpread spread)
        {
            return spread switch
            {
                TextStudio2DGradientSpread.Repeat => Mathf.Repeat(coordinate, 1f),
                TextStudio2DGradientSpread.Mirror => Mathf.PingPong(coordinate, 1f),
                _ => Mathf.Clamp01(coordinate)
            };
        }

        static bool MatchesSpatial(SerializedProperty paint, TextStudioGradientRecipe recipe)
        {
            if (!Near(paint.FindPropertyRelative("_angle").floatValue, recipe.Angle) ||
                !Near(paint.FindPropertyRelative("_center").vector2Value, recipe.Center) ||
                !Near(paint.FindPropertyRelative("_scale").vector2Value, recipe.Scale) ||
                !Near(paint.FindPropertyRelative("_offset").vector2Value, recipe.Offset))
            {
                return false;
            }

            if (recipe.Kind == TextStudio2DPaintKind.FourCornerGradient)
            {
                SerializedProperty corners = paint.FindPropertyRelative("_fourCornerGradient");
                return Near(ReadCorner(corners, "_topLeft"), recipe.FourCorner.TopLeft) &&
                       Near(ReadCorner(corners, "_topRight"), recipe.FourCorner.TopRight) &&
                       Near(ReadCorner(corners, "_bottomLeft"), recipe.FourCorner.BottomLeft) &&
                       Near(ReadCorner(corners, "_bottomRight"), recipe.FourCorner.BottomRight);
            }

            SerializedProperty stops = paint.FindPropertyRelative("_gradientStops");
            if (stops == null || stops.arraySize != recipe.StopCount)
                return false;
            for (int i = 0; i < recipe.StopCount; i++)
            {
                SerializedProperty stop = stops.GetArrayElementAtIndex(i);
                TextStudio2DGradientStop expected = recipe.GetStop(i);
                if (!Near(stop.FindPropertyRelative("_position").floatValue, expected.Position) ||
                    !Near(stop.FindPropertyRelative("_color").colorValue, expected.Color))
                {
                    return false;
                }
            }
            return true;
        }

        static bool MatchesGradient(Gradient gradient, TextStudioGradientRecipe recipe)
        {
            if (recipe == null || recipe.StopCount < 2)
                return false;
            for (int i = 0; i <= 20; i++)
            {
                float time = i / 20f;
                if (!Near(gradient.Evaluate(time), recipe.EvaluateStops(time), 0.008f))
                    return false;
            }
            return true;
        }

        static void WriteStops(SerializedProperty property, TextStudio2DGradientStop[] stops)
        {
            if (property == null || !property.isArray)
                return;
            property.arraySize = stops?.Length ?? 0;
            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty stop = property.GetArrayElementAtIndex(i);
                stop.FindPropertyRelative("_position").floatValue = stops[i].Position;
                stop.FindPropertyRelative("_color").colorValue = stops[i].Color;
            }
        }

        static TextStudio2DGradientStop[] ReadStops(SerializedProperty property)
        {
            if (property == null || !property.isArray)
                return Array.Empty<TextStudio2DGradientStop>();
            TextStudio2DGradientStop[] stops = new TextStudio2DGradientStop[property.arraySize];
            for (int i = 0; i < stops.Length; i++)
            {
                SerializedProperty stop = property.GetArrayElementAtIndex(i);
                stops[i] = new TextStudio2DGradientStop(
                    stop.FindPropertyRelative("_position").floatValue,
                    stop.FindPropertyRelative("_color").colorValue);
            }
            Array.Sort(stops, (left, right) => left.Position.CompareTo(right.Position));
            return stops;
        }

        static Color EvaluateStops(TextStudio2DGradientStop[] stops, float coordinate)
        {
            if (stops == null || stops.Length == 0)
                return Color.white;
            coordinate = Mathf.Clamp01(coordinate);
            TextStudio2DGradientStop previous = stops[0];
            if (stops.Length == 1 || coordinate <= previous.Position)
                return previous.Color;
            for (int i = 1; i < stops.Length; i++)
            {
                TextStudio2DGradientStop current = stops[i];
                if (coordinate <= current.Position)
                {
                    float amount = Mathf.InverseLerp(previous.Position, current.Position, coordinate);
                    return Color.Lerp(previous.Color, current.Color, amount);
                }
                previous = current;
            }
            return previous.Color;
        }

        static void WriteFourCorner(
            SerializedProperty property,
            TextStudio2DFourCornerGradient gradient)
        {
            if (property == null)
                return;
            property.FindPropertyRelative("_topLeft").colorValue = gradient.TopLeft;
            property.FindPropertyRelative("_topRight").colorValue = gradient.TopRight;
            property.FindPropertyRelative("_bottomLeft").colorValue = gradient.BottomLeft;
            property.FindPropertyRelative("_bottomRight").colorValue = gradient.BottomRight;
        }

        static Color ReadCorner(SerializedProperty property, string name)
        {
            return property?.FindPropertyRelative(name)?.colorValue ?? Color.clear;
        }

        static void AddUniqueTime(List<float> times, float value)
        {
            value = Mathf.Clamp01(value);
            for (int i = 0; i < times.Count; i++)
            {
                if (Near(times[i], value))
                    return;
            }
            times.Add(value);
        }

        static void RecordTargets(SerializedObject owner, string undoName)
        {
            owner.UpdateIfRequiredOrScript();
            Undo.RecordObjects(owner.targetObjects, undoName);
        }

        static void Apply(SerializedObject owner)
        {
            owner.ApplyModifiedPropertiesWithoutUndo();
            UnityEngine.Object[] targets = owner.targetObjects;
            for (int i = 0; i < targets.Length; i++)
                EditorUtility.SetDirty(targets[i]);
        }

        static bool Near(float left, float right, float tolerance = MatchTolerance)
        {
            return Mathf.Abs(left - right) <= tolerance;
        }

        static bool Near(Vector2 left, Vector2 right)
        {
            return Near(left.x, right.x) && Near(left.y, right.y);
        }

        static bool Near(Color left, Color right, float tolerance = MatchTolerance)
        {
            return Near(left.r, right.r, tolerance) &&
                   Near(left.g, right.g, tolerance) &&
                   Near(left.b, right.b, tolerance) &&
                   Near(left.a, right.a, tolerance);
        }
    }

    internal sealed class TextStudioGradientPreviewElement : VisualElement
    {
        const int PreviewWidth = 192;
        const int PreviewHeight = 54;

        readonly Image _image;
        Texture2D _texture;

        internal TextStudioGradientPreviewElement()
        {
            AddToClassList("ts-gradient-preview");
            _image = new Image
            {
                scaleMode = ScaleMode.StretchToFill,
                pickingMode = PickingMode.Ignore
            };
            _image.AddToClassList("ts-gradient-preview__image");
            Add(_image);
            RegisterCallback<DetachFromPanelEvent>(_ => DisposeTexture());
        }

        internal void ShowRecipe(TextStudioGradientRecipe recipe)
        {
            Render(recipe != null ? recipe.Evaluate : (_, _) => Color.clear);
        }

        internal void ShowSpatialPaint(SerializedProperty paint)
        {
            Render(TextStudioGradientSerializedPropertyUtility.CreateSpatialEvaluator(paint));
        }

        internal void ShowGradient(Gradient gradient)
        {
            Gradient snapshot = CopyGradient(gradient);
            Render((u, _) => snapshot != null ? snapshot.Evaluate(u) : Color.clear);
        }

        void Render(Func<float, float, Color> evaluator)
        {
            EnsureTexture();
            Color32[] pixels = new Color32[PreviewWidth * PreviewHeight];
            for (int y = 0; y < PreviewHeight; y++)
            {
                float v = PreviewHeight > 1 ? y / (PreviewHeight - 1f) : 0f;
                for (int x = 0; x < PreviewWidth; x++)
                {
                    float u = PreviewWidth > 1 ? x / (PreviewWidth - 1f) : 0f;
                    Color source = evaluator(u, v);
                    bool lightCell = ((x / 8) + (y / 8)) % 2 == 0;
                    Color checker = lightCell
                        ? new Color(0.42f, 0.42f, 0.42f, 1f)
                        : new Color(0.28f, 0.28f, 0.28f, 1f);
                    Color composite = Color.Lerp(checker, source, Mathf.Clamp01(source.a));
                    composite.a = 1f;
                    pixels[y * PreviewWidth + x] = composite;
                }
            }
            _texture.SetPixels32(pixels);
            _texture.Apply(false, false);
            _image.image = _texture;
        }

        void EnsureTexture()
        {
            if (_texture != null)
                return;
            _texture = new Texture2D(PreviewWidth, PreviewHeight, TextureFormat.RGBA32, false)
            {
                name = "Text Studio Gradient Preview",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        void DisposeTexture()
        {
            if (_texture == null)
                return;
            UnityEngine.Object.DestroyImmediate(_texture);
            _texture = null;
            _image.image = null;
        }

        static Gradient CopyGradient(Gradient source)
        {
            if (source == null)
                return null;
            Gradient copy = new Gradient
            {
                mode = source.mode
            };
            copy.SetKeys(source.colorKeys, source.alphaKeys);
            return copy;
        }
    }

#if UNITY_6000_7_OR_NEWER
    // Immutable paint choices stay available throughout the editor session.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed partial class TextStudio2DPaintAuthoringElement : VisualElement
    {
        static readonly List<string> PaintChoices = new List<string>
        {
            "Solid",
            "Linear Gradient",
            "Radial Gradient",
            "Conic Gradient",
            "Four Corner Gradient",
            "Distance Gradient",
            "Texture",
            "Pattern",
            "Procedural Noise"
        };

        static readonly TextStudio2DPaintKind[] PaintKinds =
        {
            TextStudio2DPaintKind.Solid,
            TextStudio2DPaintKind.LinearGradient,
            TextStudio2DPaintKind.RadialGradient,
            TextStudio2DPaintKind.ConicGradient,
            TextStudio2DPaintKind.FourCornerGradient,
            TextStudio2DPaintKind.DistanceGradient,
            TextStudio2DPaintKind.Texture,
            TextStudio2DPaintKind.Pattern,
            TextStudio2DPaintKind.ProceduralNoise
        };

        readonly SerializedProperty _paint;
        readonly DropdownField _kindField;
        readonly Dictionary<string, PropertyField> _fields = new Dictionary<string, PropertyField>();
        readonly VisualElement _gradientRow;
        readonly Button _browse;
        readonly GradientField _customGradient;
        readonly VisualElement _fourCornerEditor;
        readonly Foldout _advanced;
        VisualElement _fourCornerPresetSlot;
        bool _refreshing;

        internal TextStudio2DPaintAuthoringElement(SerializedProperty paint, bool allowReference = true)
        {
            _paint = paint;
            AddToClassList("ts-2d-style-paint");
            AddToClassList("ts-gradient-authoring");
            if (_paint == null)
                return;

            if (allowReference)
                AddReferenceControls();

            _kindField = new DropdownField("Paint", PaintChoices, PaintIndex(CurrentKind()))
            {
                name = "ts-2d-paint-kind",
                tooltip = "Choose a solid, spatial gradient, texture, pattern, or procedural-noise source."
            };
            ConfigureField(_kindField);
            _kindField.RegisterValueChangedCallback(OnKindChanged);
            Add(_kindField);

            AddProperty("_mapping", "Paint Mapping", "Choose the coordinate space used to place this paint.");
            AddProperty("_rangeMapping", "Range Frame", "Restart the paint at each logical match, or use one frame for the entire selection.");
            AddProperty("_spread", "Gradient Spread", "Choose whether a scalar gradient clamps, repeats, or mirrors outside its normalized domain.");
            AddProperty("_color", "Tint", "Multiply the selected paint by this color and opacity.");

            _gradientRow = new VisualElement
            {
                name = "ts-gradient-inline-row"
            };
            _gradientRow.AddToClassList("ts-inline-row");
            _browse = TextStudioInspectorElements.CreateButton(
                "Presets...",
                "Open the 18 recipes for the currently selected gradient type.",
                OpenBrowser);
            _browse.name = "ts-gradient-browse-recipes";

            _customGradient = new GradientField("Gradient")
            {
                name = "ts-gradient-custom-stops",
                tooltip = "Fine-tune color and alpha stops. Changes create a custom gradient without changing the recipe library."
            };
            ConfigureField(_customGradient);
            _customGradient.RegisterValueChangedCallback(evt =>
            {
                if (_refreshing)
                    return;
                TextStudioGradientSerializedPropertyUtility.WriteGradientStops(_paint, evt.newValue);
                Refresh();
            });
            _gradientRow.Add(_customGradient);
            _gradientRow.Add(_browse);
            Add(_gradientRow);

            _fourCornerEditor = CreateFourCornerEditor();
            Add(_fourCornerEditor);

            AddProperty("_angle", "Gradient Angle", "Rotate only the linear or conic paint gradient. This does not change layer geometry.");
            AddProperty("_center", "Paint Center", "Place the paint center inside its normalized mapping domain.");
            AddProperty("_scale", "Paint Scale", "Scale the gradient or texture in its mapping space.");
            AddProperty("_offset", "Paint Offset", "Move only the gradient or texture in its mapping space. This does not move layer geometry.");
            AddProperty("_texture", "Texture", "Texture used by this paint.");
            AddProperty("_textureFit", "Fit", "Stretch, contain, cover, or tile the source image while preserving its aspect when requested.");
            AddProperty("_distanceDomain", "Distance Domain", "Choose which layer-specific signed-distance measurement drives the ramp.");
            AddProperty("_distanceRange", "Distance Range", "Map these authored distance values to the first and last ramp positions.");
            AddProperty("_noiseScale", "Noise Scale", "Frequency of the procedural noise.");
            AddProperty("_noiseDetail", "Noise Detail", "Fine detail mixed into the procedural noise.");
            AddProperty("_noiseSeed", "Noise Seed", "Stable seed used to vary the procedural noise.");

            _advanced = new Foldout
            {
                name = "ts-2d-paint-advanced",
                text = "Advanced",
                value = false,
                tooltip = "Optional texture cache settings. Automatic identity is sufficient for normal paint authoring."
            };
            AddProperty("_resourceId", "Resource ID", "Optional texture cache key. Leave blank to generate one automatically. Paints sharing this key must reference the same texture.");
            _advanced.Add(_fields["_resourceId"]);
            Add(_advanced);

            TrackPaintChanges();
            Refresh();
        }

        void AddProperty(string path, string label, string tooltip)
        {
            SerializedProperty property = _paint.FindPropertyRelative(path);
            PropertyField field = new PropertyField(property, label)
            {
                name = "ts-2d-paint-" + path.TrimStart('_').Replace('_', '-'),
                tooltip = string.IsNullOrEmpty(property.tooltip) ? tooltip : property.tooltip
            };
            ConfigureField(field);
            _fields[path] = field;
            Add(field);
        }

        VisualElement CreateFourCornerEditor()
        {
            VisualElement editor = new VisualElement
            {
                name = "ts-four-corner-editor"
            };
            editor.AddToClassList("ts-four-corner-editor");
            VisualElement header = new VisualElement();
            header.AddToClassList("ts-four-corner-header");
            Label help = TextStudioInspectorElements.CreateMutedText(
                "Edit the four corners directly.");
            header.Add(help);
            _fourCornerPresetSlot = new VisualElement
            {
                name = "ts-four-corner-preset-slot"
            };
            _fourCornerPresetSlot.AddToClassList("ts-four-corner-preset-slot");
            header.Add(_fourCornerPresetSlot);
            editor.Add(header);
            VisualElement grid = new VisualElement();
            grid.AddToClassList("ts-four-corner-grid");
            AddCornerField(grid, "_topLeft", "Top Left");
            AddCornerField(grid, "_topRight", "Top Right");
            AddCornerField(grid, "_bottomLeft", "Bottom Left");
            AddCornerField(grid, "_bottomRight", "Bottom Right");
            editor.Add(grid);
            return editor;
        }

        void AddCornerField(VisualElement parent, string path, string label)
        {
            SerializedProperty corner = _paint
                .FindPropertyRelative("_fourCornerGradient")
                .FindPropertyRelative(path);
            ColorField field = new ColorField(label)
            {
                name = "ts-four-corner-" + path.TrimStart('_').Replace('_', '-'),
                hdr = true,
                showAlpha = true,
                value = corner.colorValue
            };
            field.AddToClassList("ts-four-corner-field");
            field.RegisterValueChangedCallback(evt =>
            {
                if (_refreshing)
                    return;
                TextStudioGradientSerializedPropertyUtility.WriteFourCornerColor(_paint, path, evt.newValue);
                Refresh();
            });
            parent.Add(field);
        }

        void TrackPaintChanges()
        {
            string[] paths =
            {
                "_kind", "_spread", "_color", "_gradientStops", "_fourCornerGradient",
                "_angle", "_center", "_scale", "_offset", "_textureFit",
                "_distanceDomain", "_distanceRange", "_mapping", "_rangeMapping", "_source", "_overrides"
            };
            for (int i = 0; i < paths.Length; i++)
            {
                SerializedProperty property = _paint.FindPropertyRelative(paths[i]);
                if (property != null)
                    this.TrackPropertyValue(property, _ => Refresh());
            }
        }

        void OnKindChanged(ChangeEvent<string> evt)
        {
            if (_refreshing)
                return;
            int index = PaintChoices.IndexOf(evt.newValue);
            if (index < 0 || index >= PaintKinds.Length)
                return;

            TextStudio2DPaintKind kind = PaintKinds[index];
            if (kind == TextStudio2DPaintKind.DistanceGradient &&
                !TextStudioGradientSerializedPropertyUtility.HasValidPayload(_paint, kind))
            {
                TextStudioGradientRecipe recipe = TextStudioGradientCatalog
                    .GetDefault(TextStudio2DPaintKind.LinearGradient);
                TextStudioGradientSerializedPropertyUtility.ApplySpatialRecipe(
                    _paint,
                    recipe,
                    "Choose Distance Gradient Paint");
                TextStudioGradientSerializedPropertyUtility.SetPaintKind(
                    _paint,
                    TextStudio2DPaintKind.DistanceGradient,
                    "Choose Distance Gradient Paint");
            }
            else if (TextStudioGradientCatalog.IsGradient(kind) &&
                !TextStudioGradientSerializedPropertyUtility.HasValidPayload(_paint, kind))
            {
                TextStudioGradientSerializedPropertyUtility.ApplySpatialRecipe(
                    _paint,
                    TextStudioGradientCatalog.GetDefault(kind),
                    "Choose Gradient Paint");
            }
            else
            {
                TextStudioGradientSerializedPropertyUtility.SetPaintKind(
                    _paint,
                    kind,
                    "Choose Paint Type");
            }
            Refresh();
        }

        void OpenBrowser()
        {
            TextStudio2DPaintKind kind = CurrentKind();
            if (!TextStudioGradientCatalog.IsGradient(kind))
                return;
            TextStudioGradientBrowserWindow.Open(kind, recipe =>
            {
                TextStudioGradientSerializedPropertyUtility.ApplySpatialRecipe(_paint, recipe);
                Refresh();
            });
        }

        void Refresh()
        {
            if (_paint == null || _refreshing)
                return;
            _refreshing = true;
            _paint.serializedObject.UpdateIfRequiredOrScript();
            TextStudio2DPaintKind kind = CurrentKind();
            _kindField.SetValueWithoutNotify(PaintChoices[PaintIndex(kind)]);
            bool gradient = TextStudioGradientCatalog.IsGradient(kind);
            bool stopGradient = TextStudioGradientCatalog.IsStopGradient(kind);
            bool fourCorner = kind == TextStudio2DPaintKind.FourCornerGradient;
            bool distance = kind == TextStudio2DPaintKind.DistanceGradient;
            bool texture = kind == TextStudio2DPaintKind.Texture || kind == TextStudio2DPaintKind.Pattern;
            bool noise = kind == TextStudio2DPaintKind.ProceduralNoise;

            SetVisible(
                _fields["_mapping"],
                kind != TextStudio2DPaintKind.Solid && !distance);
            SetVisible(_fields["_rangeMapping"], kind != TextStudio2DPaintKind.Solid && !distance &&
                (TextStudio2DMappingSpace)_paint.FindPropertyRelative("_mapping").intValue == TextStudio2DMappingSpace.Range);
            SetVisible(
                _fields["_spread"],
                kind == TextStudio2DPaintKind.LinearGradient ||
                kind == TextStudio2DPaintKind.RadialGradient ||
                distance);
            SetVisible(_fields["_color"], true);
            _fields["_color"].label = kind == TextStudio2DPaintKind.Solid ? "Color" : "Tint";
            SetVisible(_gradientRow, stopGradient);
            SetVisible(_fourCornerEditor, fourCorner);
            SetVisible(_fields["_angle"], kind == TextStudio2DPaintKind.LinearGradient || kind == TextStudio2DPaintKind.ConicGradient);
            SetVisible(_fields["_center"], kind == TextStudio2DPaintKind.RadialGradient || kind == TextStudio2DPaintKind.ConicGradient || fourCorner);
            SetVisible(_fields["_scale"], gradient && !distance || texture);
            SetVisible(_fields["_offset"], gradient && !distance || texture);
            SetVisible(_fields["_texture"], texture);
            SetVisible(_fields["_resourceId"], texture);
            SetVisible(_advanced, texture);
            SetVisible(_fields["_textureFit"], texture);
            SetVisible(_fields["_distanceDomain"], distance);
            SetVisible(_fields["_distanceRange"], distance);
            SetVisible(_fields["_noiseScale"], noise);
            SetVisible(_fields["_noiseDetail"], noise);
            SetVisible(_fields["_noiseSeed"], noise);

            if (gradient && !distance)
            {
                VisualElement browseParent = fourCorner
                    ? _fourCornerPresetSlot
                    : _gradientRow;
                if (_browse.parent != browseParent)
                {
                    _browse.RemoveFromHierarchy();
                    browseParent.Add(_browse);
                }
                _browse.tooltip =
                    "Browse the 18 " + TextStudioGradientCatalog.FamilyLabel(kind) +
                    " recipes. Applying one keeps the gradient editable.";
            }
            else if (_browse.parent != null)
            {
                _browse.RemoveFromHierarchy();
            }
            if (stopGradient)
                _customGradient.SetValueWithoutNotify(TextStudioGradientSerializedPropertyUtility.CreateGradientFromStops(_paint));
            RefreshCornerFields();
            RefreshReferenceControls();
            _refreshing = false;
        }

        void RefreshCornerFields()
        {
            SerializedProperty corners = _paint.FindPropertyRelative("_fourCornerGradient");
            string[] paths = { "_topLeft", "_topRight", "_bottomLeft", "_bottomRight" };
            for (int i = 0; i < paths.Length; i++)
            {
                ColorField field = _fourCornerEditor.Q<ColorField>(
                    "ts-four-corner-" + paths[i].TrimStart('_').Replace('_', '-'));
                field?.SetValueWithoutNotify(corners.FindPropertyRelative(paths[i]).colorValue);
            }
        }

        TextStudio2DPaintKind CurrentKind()
        {
            TextStudio2DPaintAsset source = _paint.FindPropertyRelative("_source").objectReferenceValue as TextStudio2DPaintAsset;
            if (source != null)
                return source.Value.Kind;
            return TextStudioGradientSerializedPropertyUtility.GetPaintKind(_paint);
        }

        static int PaintIndex(TextStudio2DPaintKind kind)
        {
            for (int i = 0; i < PaintKinds.Length; i++)
            {
                if (PaintKinds[i] == kind)
                    return i;
            }
            return 0;
        }

        static void ConfigureField(VisualElement field)
        {
            field.AddToClassList("ts-property-field");
            field.AddToClassList("unity-base-field__aligned");
        }

        static void SetVisible(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

#if UNITY_6000_7_OR_NEWER
    // Immutable gradient choices stay available throughout the editor session.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed class TextStudioComposedGradientAuthoringElement : VisualElement
    {
        static readonly List<string> SetChoices = new List<string>
        {
            "Linear Palettes",
            "Radial Palettes",
            "Conic Palettes"
        };

        readonly SerializedProperty _gradientProperty;
        readonly Action _onChanged;
        readonly DropdownField _setField;
        readonly PropertyField _customField;
        readonly Button _browse;
        TextStudio2DPaintKind _selectedKind;

        internal TextStudioComposedGradientAuthoringElement(
            SerializedProperty gradientProperty,
            Action onChanged)
        {
            _gradientProperty = gradientProperty;
            _onChanged = onChanged;
            name = "ts-composed-gradient-authoring";
            AddToClassList("ts-gradient-authoring");
            AddToClassList("ts-composed-gradient-authoring");

            TextStudioGradientRecipe initial = TextStudioGradientSerializedPropertyUtility.FindRampMatch(
                _gradientProperty.gradientValue);
            _selectedKind = initial?.Kind ?? TextStudio2DPaintKind.LinearGradient;

            _setField = new DropdownField("Preset Set", SetChoices, SetIndex(_selectedKind))
            {
                name = "ts-composed-gradient-preset-set",
                tooltip = "Choose which family of 18 color recipes to browse. Applying one does not add spatial mapping to this playback gradient."
            };
            _setField.AddToClassList("ts-property-field");
            _setField.AddToClassList("ts-composed-layer-aligned-field");
            _setField.RegisterValueChangedCallback(evt =>
            {
                _selectedKind = KindForSet(SetChoices.IndexOf(evt.newValue));
                Refresh();
            });
            Add(_setField);

            VisualElement gradientRow = new VisualElement
            {
                name = "ts-composed-gradient-inline-row"
            };
            gradientRow.AddToClassList("ts-inline-row");
            _customField = new PropertyField(_gradientProperty, "Color over playback")
            {
                name = "ts-composed-layer-color-gradient",
                tooltip = "Fine-tune the applied recipe as an ordinary animated Unity gradient."
            };
            _customField.AddToClassList("ts-property-field");
            _customField.AddToClassList("ts-composed-layer-aligned-field");
            gradientRow.Add(_customField);
            _browse = TextStudioInspectorElements.CreateButton(
                "Presets...",
                "Browse only the 18 recipes in the selected palette set.",
                OpenBrowser);
            _browse.name = "ts-composed-gradient-browse-recipes";
            gradientRow.Add(_browse);
            Add(gradientRow);
            this.TrackPropertyValue(_gradientProperty, _ =>
            {
                Refresh();
                _onChanged?.Invoke();
            });
            Refresh();
        }

        void OpenBrowser()
        {
            TextStudioGradientBrowserWindow.Open(_selectedKind, recipe =>
            {
                TextStudioGradientSerializedPropertyUtility.ApplyRamp(_gradientProperty, recipe);
                Refresh();
                _onChanged?.Invoke();
            });
        }

        void Refresh()
        {
            if (_gradientProperty == null)
                return;
            _gradientProperty.serializedObject.UpdateIfRequiredOrScript();
            _browse.tooltip =
                "Browse the 18 " + TextStudioGradientCatalog.FamilyLabel(_selectedKind) +
                " palette recipes. Applying one keeps the playback gradient editable.";
        }

        static int SetIndex(TextStudio2DPaintKind kind)
        {
            return kind == TextStudio2DPaintKind.RadialGradient
                ? 1
                : kind == TextStudio2DPaintKind.ConicGradient ? 2 : 0;
        }

        static TextStudio2DPaintKind KindForSet(int index)
        {
            return index == 1
                ? TextStudio2DPaintKind.RadialGradient
                : index == 2
                    ? TextStudio2DPaintKind.ConicGradient
                    : TextStudio2DPaintKind.LinearGradient;
        }
    }
}
