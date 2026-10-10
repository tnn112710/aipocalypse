using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal sealed partial class TextStudio2DStyleEditor
    {
        void AddDecorationChildren(VisualElement parent, SerializedProperty decoration)
        {
            SerializedProperty topology = decoration.FindPropertyRelative("_topology");
            SerializedProperty kind = decoration.FindPropertyRelative("_kind");
            SerializedProperty line = decoration.FindPropertyRelative("_line");
            Dictionary<string, VisualElement> fields = new Dictionary<string, VisualElement>();
            SerializedProperty child = decoration.Copy();
            SerializedProperty end = child.GetEndProperty();
            bool enter = true;
            while (child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end))
            {
                enter = false;
                if (child.depth != decoration.depth + 1 || child.name == "_surfaceTreatments") continue;
                VisualElement field = child.name == "_customPoints"
                    ? new TextStudio2DParameterArrayEditor(child.Copy(), true)
                    : CreateField(child.Copy(), Nicify(child.name), null);
                fields.Add(child.name, field);
                parent.Add(field);
            }
            VisualElement linePadding = new VisualElement();
            linePadding.Add(CreateField(decoration.FindPropertyRelative("_padding.x"), "Left Padding", null));
            linePadding.Add(CreateField(decoration.FindPropertyRelative("_padding.z"), "Right Padding", null));
            parent.Add(linePadding);
            VisualElement lineFields = new VisualElement();
            lineFields.AddToClassList("ts-subsection");
            lineFields.Add(TextStudioInspectorElements.CreateMutedText(
                "Line rhythm follows geometric distance. Match phase continues through wrapping. Pixel-sized lines stay fixed to the text layout and use the actual camera output resolution."));
            Dictionary<string, VisualElement> lineControls = new Dictionary<string, VisualElement>();
            SerializedProperty lineChild = line.Copy();
            SerializedProperty lineEnd = lineChild.GetEndProperty();
            bool enterLine = true;
            while (lineChild.NextVisible(enterLine) && !SerializedProperty.EqualContents(lineChild, lineEnd))
            {
                enterLine = false;
                if (lineChild.depth != line.depth + 1) continue;
                PropertyField field = CreateField(lineChild.Copy(), Nicify(lineChild.name), null);
                lineControls.Add(lineChild.name, field);
                lineFields.Add(field);
            }
            parent.Add(lineFields);
            Label geometryNote = TextStudioInspectorElements.CreateMutedText(
                "Connected ranges join overlapping line bands, then round their complete boundary. Hard breaks and visual gaps remain separate unless a connection is allowed. The surface stays fixed to the text layout during character animation.");
            parent.Add(geometryNote);
            parent.Add(CreateSurfaceTreatmentEditor(decoration.FindPropertyRelative("_surfaceTreatments")));
            void Refresh()
            {
                bool isLine = kind.intValue <= (int)TextStudio2DDecorationKind.Overline;
                bool connected = !isLine && topology.intValue == (int)TextStudio2DDecorationTopology.ConnectedRange;
                bool glyph = !isLine && decoration.FindPropertyRelative("_shape").intValue == (int)TextStudio2DDecorationShape.GlyphSilhouette;
                foreach (KeyValuePair<string, VisualElement> entry in fields)
                {
                    entry.Value.style.display = DecorationFieldApplies(decoration, entry.Key)
                        ? DisplayStyle.Flex : DisplayStyle.None;
                }
                linePadding.style.display = isLine ? DisplayStyle.Flex : DisplayStyle.None;
                lineFields.style.display = isLine ? DisplayStyle.Flex : DisplayStyle.None;
                geometryNote.text = glyph
                    ? "Glyph Silhouette unions prepared, posed letter contours before painting. Padding expands the boundary; Bridge Radius closes nearby gaps. Add an Appearance Surface Group to include other text objects, and keep this surface behind their lettering."
                    : "Connected ranges join overlapping line bands, then round their complete boundary. Hard breaks and visual gaps remain separate unless a connection is allowed. The surface stays fixed to the text layout during character animation.";
                geometryNote.style.display = connected || glyph ? DisplayStyle.Flex : DisplayStyle.None;
                if (isLine)
                {
                    TextStudio2DLinePattern pattern = (TextStudio2DLinePattern)line.FindPropertyRelative("_pattern").intValue;
                    foreach (KeyValuePair<string, VisualElement> entry in lineControls)
                    {
                        bool visible = entry.Key switch {
                            "_dashLength" or "_gap" => pattern == TextStudio2DLinePattern.Dashed,
                            "_dotSpacing" => pattern == TextStudio2DLinePattern.Dotted,
                            "_waveAmplitude" or "_wavelength" => pattern == TextStudio2DLinePattern.Wavy,
                            "_doubleSeparation" => pattern == TextStudio2DLinePattern.Double,
                            "_phase" or "_phaseRestart" => pattern == TextStudio2DLinePattern.Dashed || pattern == TextStudio2DLinePattern.Dotted || pattern == TextStudio2DLinePattern.Wavy,
                            "_cap" => pattern != TextStudio2DLinePattern.Dotted,
                            "_inkClearance" => line.FindPropertyRelative("_skipInk").intValue != (int)TextStudio2DSkipInk.Off,
                            _ => true
                        };
                        entry.Value.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                    }
                }
            }
            parent.TrackSerializedObjectValue(decoration.serializedObject, _ => Refresh());
            Refresh();
        }
        // Match the line geometry builder and analytic shape branches in the decoration shader.
        internal static bool DecorationFieldApplies(SerializedProperty decoration, string field)
        {
            TextStudio2DDecorationKind kind = (TextStudio2DDecorationKind)decoration.FindPropertyRelative("_kind").intValue;
            bool line = kind <= TextStudio2DDecorationKind.Overline;
            bool connected = !line && decoration.FindPropertyRelative("_topology").intValue == (int)TextStudio2DDecorationTopology.ConnectedRange;
            TextStudio2DDecorationShape shape = (TextStudio2DDecorationShape)decoration.FindPropertyRelative("_shape").intValue;
            bool glyph = !line && shape == TextStudio2DDecorationShape.GlyphSilhouette;
            bool plate = !line && !connected && shape == TextStudio2DDecorationShape.Plate;
            bool rectangle = !line && !connected && shape == TextStudio2DDecorationShape.RoundedRectangle;
            int left = decoration.FindPropertyRelative("_leftEnd").intValue;
            int right = decoration.FindPropertyRelative("_rightEnd").intValue;
            return field switch
            {
                "_line" or "_polygonSides" => false,
                "_thickness" => kind == TextStudio2DDecorationKind.Border || line && decoration.FindPropertyRelative("_line._thicknessMode").intValue == (int)TextStudio2DMetricMode.Custom,
                "_glyphPadding" or "_glyphBridgeRadius" or "_glyphFillHoles" or "_backplateVisibility" => glyph,
                "_padding" or "_plateHeight" or "_plateWidth" or "_topology" => !line && !glyph,
                "_offset" => !glyph,
                "_verticalAnchor" => !line && !glyph && decoration.FindPropertyRelative("_plateHeight").floatValue > .00001f,
                "_connectAcrossLineBreaks" => connected && !glyph,
                "_shape" => !line,
                "_leftEnd" or "_rightEnd" => plate,
                "_capSize" => plate && (left >= (int)TextStudio2DDecorationEndStyle.Chamfer || right >= (int)TextStudio2DDecorationEndStyle.Chamfer),
                "_cornerRadius" => !glyph && (connected || plate && (left != (int)TextStudio2DDecorationEndStyle.Square || right != (int)TextStudio2DDecorationEndStyle.Square) || rectangle && decoration.FindPropertyRelative("_cornerRadii").vector4Value.sqrMagnitude <= .0000000001f),
                "_cornerRadii" => rectangle,
                "_squirclePower" => !line && !connected && shape == TextStudio2DDecorationShape.Squircle,
                "_starInset" => !line && !connected && shape == TextStudio2DDecorationShape.Star,
                "_customPoints" => !line && !connected && shape == TextStudio2DDecorationShape.CustomPolygon,
                _ => true
            };
        }
    }
}
