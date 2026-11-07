//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomPropertyDrawer(typeof(WeightedChanceCurve))]
    public class ChanceCurveDrawer : PropertyDrawer
    {
        VisualElement container;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {


            container = new VisualElement();

            StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Globals.uss"
            );
            container.styleSheets.Add(style);
            container.AddToClassList("unity-base-field");

            DropdownField chanceType = new DropdownField();
            chanceType.label = "";
            chanceType.dataSourceType = typeof(ChanceConditionType);
            chanceType.BindProperty(property.FindPropertyRelative("curveType"));
            chanceType.AddToClassList("unity-base-field__label");
            chanceType.style.marginRight = 3;
            container.Add(chanceType);

            VisualElement graphContainer = new VisualElement();
            graphContainer.AddToClassList("unity-base-field__input");
            graphContainer.AddToClassList("unity-property-field__input");
            container.Add(graphContainer);

            graphContainer.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
            graphContainer.style.borderTopLeftRadius = 9;
            graphContainer.style.borderTopRightRadius = 9;
            graphContainer.style.borderBottomLeftRadius = 9;
            graphContainer.style.borderBottomRightRadius = 9;
            graphContainer.style.overflow = Overflow.Hidden;

            graphContainer.RegisterCallback<ClickEvent>(evt =>
            {
                CozyChanceCurveWindow.ShowPropertyEditor(property, () =>
                {
                    graphContainer.MarkDirtyRepaint();
                });
            });

            SerializedProperty propertyCopy = property.Copy();

            graphContainer.generateVisualContent += context =>
            {
                if (propertyCopy == null || !propertyCopy.serializedObject.targetObject)
                    return;

                propertyCopy.serializedObject.Update();

                SerializedProperty keysProperty = propertyCopy.FindPropertyRelative("keys");

                float width = graphContainer.contentRect.width;
                float height = graphContainer.contentRect.height;

                Painter2D painter = context.painter2D;
                painter.strokeColor = Branding.blue;
                painter.BeginPath();

                if (keysProperty == null || !keysProperty.isArray || keysProperty.arraySize == 0)
                {
                    return;
                }

                float v = 0;
                float t = 0;

                for (int i = 0; i < keysProperty.arraySize; i++)
                {
                    var keyProperty = keysProperty.GetArrayElementAtIndex(i);
                    v = 1 - keyProperty.FindPropertyRelative("value").floatValue;
                    t = keyProperty.FindPropertyRelative("time").floatValue;

                    if (i == 0)
                    {
                        painter.MoveTo(new Vector2(0, height * v));
                    }

                    painter.LineTo(new Vector2(width * t, height * v));
                }

                painter.LineTo(new Vector2(width, height * v));



                painter.Stroke();


            };

            return container;

        }
    }
}