using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    [CustomEditor(typeof(TextStudioWritingStyleAsset))]
    internal sealed class TextStudioWritingStyleAssetEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            return BuildInspector(true);
        }

        internal VisualElement BuildInspector(bool includePreview)
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot(
                "ts-writing-style-inspector");
            Label guidance = TextStudioInspectorElements.CreateMutedText("");
            root.Add(TextStudioInspectorElements.CreateSection(
                "Writing", "ts-writing-style-treatment",
                Config("_mode", "Effect", "Choose a local letter wipe, outline trace, or pen drawing technique. Typewriter controls timing."),
                guidance,
                TextStudioInspectorElements.CreateMutedText("Typewriter controls letter order, timing, playback, and progress.")));

            if (includePreview)
                root.Add(new TextStudioWritingPreviewElement((TextStudioWritingStyleAsset)target));

            PropertyField strokeGap = Config("_strokeGap", "Pen-Lift Pause", "Pause between strokes within a letter, as a fraction of that letter's writing duration. Only used by drawing paths.");
            VisualElement timing = TextStudioInspectorElements.CreateSection(
                "Drawing", "ts-writing-style-timing",
                Config("_direction", "Drawing Direction", "Direction of the reveal edge within each letter. Typewriter controls the order of letters."),
                strokeGap,
                Config("_reverse", "Reverse Drawing", "Draw each path backwards. This changes the direction within a letter, independently of the letter order."),
                Config("_reducedMotionBehavior", "Reduced Motion", "When reduced motion is requested: Preserve keeps this animation, Reduce simplifies it, and Disable immediately shows the finished result."));
            root.Add(timing);

            PropertyField miter = Config("_miterLimit", "Corner Limit", "Maximum sharp-corner extension relative to stroke width. Lower values shorten spikes. Only used with Miter joins.");
            VisualElement stroke = TextStudioInspectorElements.CreateSection(
                "Stroke Appearance", "ts-writing-style-stroke",
                TextStudioInspectorElements.CreateMutedText("These settings style the pen stroke. The text's existing appearance provides the final filled letters."),
                Config("_brush", "Brush", "Choose the stroke texture: a clean solid edge, ink, pressure-style calligraphy, marker, or grainy chalk."),
                Config("_color", "Stroke Color", "Color of the drawn stroke; this does not replace the text's final fill color."),
                Config("_width", "Stroke Width", "Width of the drawn stroke in the writing path's coordinate space. Larger values make a broader pen mark."),
                Config("_cap", "Stroke Ends", "Round gives rounded pen ends, Butt ends at the path endpoint, and Square extends the ends by half the width."),
                Config("_join", "Stroke Corners", "Round smooths turns, Bevel clips corners, and Miter preserves sharp corners up to Corner Limit."), miter);
            root.Add(stroke);
            root.Add(TextStudioInspectorElements.CreateSection(
                "Reveal Edge", "ts-writing-style-edge",
                Config("_edgeSoftness", "Softness", "Softens the moving coverage edge or drawn stroke edge.")));
            VisualElement fill = TextStudioInspectorElements.CreateSection(
                "Fill After Drawing", "ts-writing-style-fill",
                TextStudioInspectorElements.CreateMutedText("Fade in the original letter as its stroke finishes. Values are fractions of each letter's writing duration."),
                Config("_fillStart", "Fill Starts", "0 begins filling immediately; 1 waits until the letter's writing duration ends."),
                Config("_fillDuration", "Fill Fade Duration", "How much of the letter's writing duration is used to fade in the fill. The fade ends no later than the letter's completion."));
            root.Add(fill);
            PropertyField offset = Config("_threeDSurfaceOffset", "Surface Offset", "Distance in local 3D text units between the added stroke and the front surface. Increase slightly if the surfaces flicker.");
            PropertyField depth = Config("_threeDReliefDepth", "Relief Depth", "Depth of the raised stroke in local 3D text units.");
            PropertyField segments = Config("_threeDRadialSegments", "Tube Smoothness", "Number of sides around a volumetric stroke. More sides look rounder but add geometry.");
            Foldout threeDSettings = TextStudioInspectorElements.CreateFoldoutSection(
                "3D Text", "ts-writing-style-3d", false,
                TextStudioInspectorElements.CreateMutedText("Only affects extruded 3D text using this style."),
                Config("_threeDMode", "Presentation", "Reveal the existing surfaces, add a raised ribbon, or draw a volumetric tube. Added strokes require drawing paths."),
                offset, depth, segments);
            root.Add(threeDSettings);

            VisualElement preparation = TextStudioInspectorElements.CreateFoldoutSection("Custom Drawings", "ts-writing-style-preparation", false);
            Label preparationHelp = TextStudioInspectorElements.CreateMutedText("");
            preparation.Add(preparationHelp);
            VisualElement sources = new VisualElement {name = "ts-writing-style-sources"};
            sources.Add(Field("_phraseAsset", "Phrase Drawing", "Optional drawing for one exact phrase and layout. Use this for signatures or connected lettering."));
            sources.Add(Field("_fontAsset", "Letter Drawings", "Reusable drawing paths for individual letters in a matching font. Used when no matching phrase drawing is available."));
            preparation.Add(sources);
            Foldout advanced = new Foldout {text = "Advanced Preparation", value = false};
            advanced.Add(Field("_flatteningTolerance", "Curve Detail", "Maximum curve approximation error in path coordinates. Smaller values produce smoother curves with more points; changes re-prepare assigned drawings automatically."));
            preparation.Add(advanced);
            VisualElement compiled = new Foldout {text = "Diagnostics", value = false, name = "ts-writing-style-compiled"};
            Label status = AddSummary(compiled, "Status");
            Label promise = AddSummary(compiled, "Requested Effect");
            Label brush = AddSummary(compiled, "Brush");
            Label threeD = AddSummary(compiled, "3D Presentation");
            Label phrase = AddSummary(compiled, "Phrase Drawing");
            Label glyphs = AddSummary(compiled, "Letter Drawings");
            Label prepared = AddSummary(compiled, "Prepared Paths");
            VisualElement diagnostics = new VisualElement {name = "ts-writing-style-diagnostics"};
            preparation.Add(diagnostics);
            preparation.Add(compiled);
            root.Add(preparation);

            Action updateVisibility = () =>
            {
                TextStudioWritingConfiguration configuration = ((TextStudioWritingStyleAsset)target).Configuration;
                TextStudioWritingDataLevel level = TextStudioWritingModeUtility.RequestedDataLevel(configuration.Mode);
                bool paths = level >= TextStudioWritingDataLevel.PreparedOutline;
                bool active = configuration.Mode != TextStudioWritingMode.None;
                bool assigned = serializedObject.FindProperty("_phraseAsset").objectReferenceValue != null || serializedObject.FindProperty("_fontAsset").objectReferenceValue != null;
                guidance.text = level switch
                {
                    TextStudioWritingDataLevel.AuthoredCenterline => "Estimates pen strokes from the font. Typewriter controls when each letter starts; custom drawings can provide exact handwriting.",
                    TextStudioWritingDataLevel.PreparedOutline => "Draws around each letter’s outside edge and holes, then fills it. Outlines are taken from the font automatically.",
                    TextStudioWritingDataLevel.InkCoverage => "Moves a reveal edge within each letter. Typewriter controls the sequence of letters.",
                    TextStudioWritingDataLevel.OrdinaryTypewriter => "Shows each scheduled letter or group without drawing a pen stroke.",
                    _ => "Writing is disabled. The text keeps its normal appearance."
                };
                preparationHelp.text = paths
                    ? "Optional: override automatic font paths with your own letter strokes or an exact phrase drawing. Changes are prepared automatically."
                    : "No drawing preparation is needed for this effect.";
                timing.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
                stroke.style.display = paths ? DisplayStyle.Flex : DisplayStyle.None;
                strokeGap.style.display = paths ? DisplayStyle.Flex : DisplayStyle.None;
                fill.style.display = paths ? DisplayStyle.Flex : DisplayStyle.None;
                root.Q("ts-writing-style-edge").style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
                threeDSettings.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
                miter.style.display = configuration.Join == TextStudioWritingStrokeJoin.Miter ? DisplayStyle.Flex : DisplayStyle.None;
                bool raised = paths && configuration.ThreeDMode != TextStudioWriting3DMode.SurfaceReveal;
                offset.style.display = raised ? DisplayStyle.Flex : DisplayStyle.None;
                depth.style.display = raised ? DisplayStyle.Flex : DisplayStyle.None;
                segments.style.display = raised && configuration.ThreeDMode == TextStudioWriting3DMode.VolumetricTube ? DisplayStyle.Flex : DisplayStyle.None;
                sources.style.display = paths || assigned ? DisplayStyle.Flex : DisplayStyle.None;
                advanced.style.display = paths || assigned ? DisplayStyle.Flex : DisplayStyle.None;
                preparation.style.display = paths || assigned ? DisplayStyle.Flex : DisplayStyle.None;
            };
            updateVisibility();
            root.TrackSerializedObjectValue(serializedObject, _ => updateVisibility());

            ulong renderedDependencyRevision = ulong.MaxValue;
            Action refresh = () =>
            {
                TextStudioWritingStyleAsset style =
                    target as TextStudioWritingStyleAsset;
                ulong dependencyRevision =
                    TextStudioWritingCompiler.ComputeDependencyRevision(style);
                if (dependencyRevision == renderedDependencyRevision)
                    return;
                CompiledTextStudioWritingStyle plan =
                    TextStudioWritingCompiler.Compile(style);
                renderedDependencyRevision = dependencyRevision;
                updateVisibility();
                if (plan.DiagnosticCount > 0) preparation.style.display = DisplayStyle.Flex;
                RefreshSummary(
                    plan,
                    status,
                    promise,
                    brush,
                    threeD,
                    phrase,
                    glyphs,
                    prepared,
                    diagnostics);
            };
            refresh();
            TextStudioInspectorElements.SchedulePeriodicRefresh(
                root,
                refresh,
                250);
            return root;
        }

        PropertyField Config(string name, string label, string tooltip)
        {
            return Field("_configuration." + name, label, tooltip);
        }

        PropertyField Field(
            string path,
            string label,
            string tooltip = null)
        {
            return TextStudioInspectorElements.CreatePropertyField(
                serializedObject,
                path,
                label,
                null,
                tooltip);
        }

        static Label AddSummary(VisualElement parent, string title)
        {
            VisualElement row = TextStudioInspectorElements.CreateKeyValueRow(
                title,
                "-");
            Label value = row.Q<Label>(className: "ts-key-value-value");
            parent.Add(row);
            return value;
        }

        static void RefreshSummary(
            CompiledTextStudioWritingStyle plan,
            Label status,
            Label promise,
            Label brush,
            Label threeD,
            Label phrase,
            Label glyphs,
            Label prepared,
            VisualElement diagnostics)
        {
            status.text = plan.IsValid ? "Ready" : "Needs attention";
            promise.text = FormatLevel(
                TextStudioWritingModeUtility.RequestedDataLevel(
                    plan.Configuration.Mode));
            brush.text = ObjectNames.NicifyVariableName(
                plan.Configuration.Brush.ToString());
            threeD.text = ObjectNames.NicifyVariableName(
                plan.Configuration.ThreeDMode.ToString());
            int phrasePoints = CountPoints(plan.Phrase?.Centerline) +
                               CountPoints(plan.Phrase?.Outline);
            phrase.text = plan.Phrase == null
                ? "Not assigned"
                : phrasePoints > 0
                    ? phrasePoints + " prepared points"
                    : "Assigned, no usable path";
            glyphs.text = (plan.Font?.GlyphCount ?? 0).ToString();
            int pointCount = phrasePoints;
            int strokeCount = CountStrokes(plan.Phrase?.Centerline) +
                              CountStrokes(plan.Phrase?.Outline);
            if (plan.Font != null)
            {
                for (int i = 0; i < plan.Font.GlyphCount; i++)
                {
                    TextStudioCompiledWritingGlyph glyph = plan.Font.GetGlyph(i);
                    pointCount += CountPoints(glyph.Centerline) +
                                  CountPoints(glyph.Outline);
                    strokeCount += CountStrokes(glyph.Centerline) +
                                   CountStrokes(glyph.Outline);
                }
            }
            prepared.text = strokeCount + " strokes, " +
                            pointCount + " points";

            diagnostics.Clear();
            if (plan.DiagnosticCount == 0)
            {
                return;
            }
            const int limit = 6;
            int count = Mathf.Min(plan.DiagnosticCount, limit);
            for (int i = 0; i < count; i++)
            {
                TextStudioStackDiagnostic diagnostic = plan.GetDiagnostic(i);
                diagnostics.Add(TextStudioInspectorElements.CreateHelpBox(
                    diagnostic.Message,
                    ToHelpType(diagnostic.Severity)));
            }
            if (plan.DiagnosticCount > limit)
            {
                diagnostics.Add(TextStudioInspectorElements.CreateMutedText(
                    (plan.DiagnosticCount - limit) +
                    " more diagnostics are available."));
            }
        }

        static string FormatLevel(TextStudioWritingDataLevel level)
        {
            return level switch
            {
                TextStudioWritingDataLevel.AuthoredCenterline => "True Handwriting",
                TextStudioWritingDataLevel.PreparedOutline => "Outline Trace",
                TextStudioWritingDataLevel.InkCoverage => "Moves a reveal edge within each letter. Typewriter controls the sequence of letters.",
                TextStudioWritingDataLevel.OrdinaryTypewriter => "Ordinary Typewriter",
                _ => "Disabled"
            };
        }

        static HelpBoxMessageType ToHelpType(
            TextStudioStackDiagnosticSeverity severity)
        {
            return severity switch
            {
                TextStudioStackDiagnosticSeverity.Error => HelpBoxMessageType.Error,
                TextStudioStackDiagnosticSeverity.Warning => HelpBoxMessageType.Warning,
                _ => HelpBoxMessageType.Info
            };
        }

        static int CountPoints(TextStudioPreparedWritingPath path)
        {
            return path?.PointCount ?? 0;
        }

        static int CountStrokes(TextStudioPreparedWritingPath path)
        {
            return path?.StrokeCount ?? 0;
        }
    }

    [CustomEditor(typeof(WritingFontAsset))]
    internal sealed class WritingFontAssetEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot(
                "ts-writing-font-inspector");
            root.Add(TextStudioInspectorElements.CreateHelpBox(
                "Each row is keyed by exact runtime font identity, glyph index, and optional variation fingerprint. Centerlines promise authored stroke order. Outline rows promise contour tracing only."));
            root.Add(TextStudioInspectorElements.CreateSection(
                "Design Space",
                "ts-writing-font-design-space",
                TextStudioInspectorElements.CreatePropertyField(
                    serializedObject,
                    "_unitsPerEm",
                    "Units Per Em")));
            root.Add(TextStudioInspectorElements.CreateSection(
                "Glyph Variations",
                "ts-writing-font-glyphs",
                TextStudioInspectorElements.CreatePropertyField(
                    serializedObject,
                    "_glyphs",
                    "Paths")));
            root.Add(TextStudioInspectorElements.CreateMutedText(
                "Curves are adaptively prepared by each writing style only when this asset revision changes. Runtime playback never subdivides Béziers."));
            return root;
        }
    }

    [CustomEditor(typeof(WritingPhraseAsset))]
    internal sealed class WritingPhraseAssetEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot(
                "ts-writing-phrase-inspector");
            root.Add(TextStudioInspectorElements.CreateHelpBox(
                "A phrase path is accepted only for the exact resolved text and shaping fingerprint. This protects ligatures, fallback fonts, bidirectional order, clusters, and layout from a visually plausible but incorrect match."));
            root.Add(TextStudioInspectorElements.CreateSection(
                "Shaped Phrase",
                "ts-writing-phrase-identity",
                TextStudioInspectorElements.CreatePropertyField(
                    serializedObject,
                    "_resolvedText",
                    "Resolved Text"),
                TextStudioInspectorElements.CreatePropertyField(
                    serializedObject,
                    "_shapingFingerprint",
                    "Shaping Fingerprint"),
                TextStudioInspectorElements.CreatePropertyField(
                    serializedObject,
                    "_designBounds",
                    "Design Bounds"),
                TextStudioInspectorElements.CreateButton(
                    "Capture Selected Text",
                    "Capture exact shaped text, glyph variation, clustering, and layout from the selected TextStudioText component while preserving authored paths.",
                    CaptureSelectedText,
                    true)));
            root.Add(TextStudioInspectorElements.CreateSection(
                "Authored Centerlines",
                "ts-writing-phrase-centerlines",
                TextStudioInspectorElements.CreatePropertyField(
                    serializedObject,
                    "_centerlineStrokes",
                    "Pen-Down Strokes")));
            root.Add(TextStudioInspectorElements.CreateSection(
                "Prepared Outlines",
                "ts-writing-phrase-outlines",
                TextStudioInspectorElements.CreatePropertyField(
                    serializedObject,
                    "_outlineStrokes",
                    "Contour Strokes")));
            return root;
        }

        void CaptureSelectedText()
        {
            TextStudioText hub = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<TextStudioText>()
                : null;
            if (hub == null)
            {
                EditorUtility.DisplayDialog(
                    "Capture Writing Phrase",
                    "Select a GameObject with a TextStudioText component first.",
                    "OK");
                return;
            }

            hub.ForceMeshRefresh(0f);
            if (!hub.TryCaptureWritingPhraseIdentity(
                    out string resolvedText,
                    out ulong shapingFingerprint,
                    out Rect designBounds))
            {
                EditorUtility.DisplayDialog(
                    "Capture Writing Phrase",
                    "The selected text has no prepared visual glyph layout to capture.",
                    "OK");
                return;
            }

            WritingPhraseAsset phrase = (WritingPhraseAsset)target;
            TextStudioWritingStroke[] centerline = ReadStrokes(phrase, true);
            TextStudioWritingStroke[] outline = ReadStrokes(phrase, false);
            Undo.RecordObject(phrase, "Capture Text Studio Writing Phrase");
            phrase.SetPhrase(
                resolvedText,
                shapingFingerprint,
                designBounds,
                centerline,
                outline);
            EditorUtility.SetDirty(phrase);
            serializedObject.UpdateIfRequiredOrScript();
        }

        static TextStudioWritingStroke[] ReadStrokes(
            WritingPhraseAsset phrase,
            bool centerline)
        {
            int count = centerline
                ? phrase.CenterlineStrokeCount
                : phrase.OutlineStrokeCount;
            TextStudioWritingStroke[] strokes =
                new TextStudioWritingStroke[count];
            for (int i = 0; i < count; i++)
            {
                strokes[i] = centerline
                    ? phrase.GetCenterlineStroke(i)
                    : phrase.GetOutlineStroke(i);
            }
            return strokes;
        }
    }
}
