//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomPropertyDrawer(typeof(WeightedRandomChance))]
    public class WeightedRandomChanceDrawer : PropertyDrawer
    {
        VisualElement container;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            container = new VisualElement();

            StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Globals.uss"
            );
            container.styleSheets.Add(style);

            Foldout foldout = new Foldout();

            foldout.TrackPropertyValue(property.FindPropertyRelative("chanceEffectors"), (evt) =>
            {
                if (property.FindPropertyRelative("chanceEffectors").arraySize > 0)
                    foldout.text = $"{preferredLabel} (x{property.FindPropertyRelative("chanceEffectors").arraySize})";
                else
                    foldout.text = preferredLabel;
            });

            if (property.FindPropertyRelative("chanceEffectors").arraySize > 0)
                foldout.text = $"{preferredLabel} (x{property.FindPropertyRelative("chanceEffectors").arraySize})";
            else
                foldout.text = preferredLabel;

            container.Add(foldout);
            PropertyField propertyField = new PropertyField();
            propertyField.style.flexGrow = 1;
            propertyField.BindProperty(property.FindPropertyRelative("baseChance"));
            propertyField.label = "";
            propertyField.style.marginRight = 6;
            propertyField.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                propertyField.Q<TextElement>().style.marginLeft = 1;
            });
            foldout.Q<VisualElement>(className: "unity-base-field__input").Add(propertyField);
            foldout.value = false;

            container.Add(foldout);

            VisualElement weightsContainer = new VisualElement();
            foldout.Add(weightsContainer);

            ListView weightsList = new ListView();
            weightsList.showFoldoutHeader = false;
            weightsList.reorderable = true;
            weightsList.reorderMode = ListViewReorderMode.Animated;
            weightsList.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
            weightsList.showAlternatingRowBackgrounds = AlternatingRowBackground.All;
            weightsList.showAddRemoveFooter = true;
            weightsList.showBoundCollectionSize = false;
            weightsList.showBorder = true;
            weightsList.BindProperty(property.FindPropertyRelative("chanceEffectors"));
            weightsContainer.Add(weightsList);

            return container;

        }
    }

}