using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal sealed partial class TextStudio2DStyleEditor
    {
        void AddStrokeChildren(VisualElement parent, SerializedProperty property)
        {
            SerializedProperty join = property.FindPropertyRelative("_join");
            SerializedProperty pattern = property.FindPropertyRelative("_dashPattern");
            SerializedProperty units = property.FindPropertyRelative("_units");
            parent.Add(CreateField(property.FindPropertyRelative("_width"), "Width", null));
            parent.Add(CreateField(property.FindPropertyRelative("_alignment"), "Alignment", "Place the full width inside (-1), centered (0), or outside (1) the glyph contour."));
            parent.Add(CreateField(join, "Join", "Choose round corners, clipped bevels, or pointed miters. Miter and bevel joins require prepared font contours."));
            VisualElement miter = CreateField(property.FindPropertyRelative("_miterLimit"), "Miter Limit", null);
            parent.Add(miter);
            parent.Add(new TextStudio2DParameterArrayEditor(pattern.Copy(), false));
            VisualElement dashes = new VisualElement();
            dashes.Add(CreateField(property.FindPropertyRelative("_dashOffset"), "Dash Offset", "Shift the pattern along the contour using the selected Units."));
            dashes.Add(CreateField(property.FindPropertyRelative("_dashCap"), "Dash Cap", "Shape of each dash endpoint."));
            dashes.Add(CreateField(property.FindPropertyRelative("_continueAcrossContours"), "Continue Across Contours", null));
            parent.Add(dashes);
            parent.Add(CreateField(property.FindPropertyRelative("_softness"), "Softness", null));
            parent.Add(CreateField(units, "Units", null));
            VisualElement hint = TextStudioInspectorElements.CreateMutedText("Pixel contours follow the output camera. Use separate text instances for views with different scale or perspective. Corner deformation requires Em units.");
            parent.Add(hint);
            void Refresh()
            {
                miter.style.display = join.intValue == (int)TextStudio2DStrokeJoin.Miter ? DisplayStyle.Flex : DisplayStyle.None;
                dashes.style.display = pattern.arraySize > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                hint.style.display = units.intValue == (int)TextStudio2DUnit.Pixels &&
                    (join.intValue != (int)TextStudio2DStrokeJoin.Round || pattern.arraySize > 0) ? DisplayStyle.Flex : DisplayStyle.None;
            }
            parent.TrackPropertyValue(join, _ => Refresh());
            parent.TrackPropertyValue(pattern, _ => Refresh());
            parent.TrackPropertyValue(units, _ => Refresh());
            Refresh();
        }
    }
}
