//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomPropertyDrawer(typeof(WeightedRandomChance))]
    public class WRCField : PropertyDrawer
    {
        private Slider BaseValue => root.Q<Slider>();
        private Label Label => root.Q<Label>();
        private VisualElement CurvesContainer => root.Q<VisualElement>("curves-container");
        private VisualElement ToggleCurves => root.Q<VisualElement>("toggle-curves");
        bool open = false;
        VisualElement root;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            root = new VisualElement();

            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Components/UXML/weighted-random-chance.uxml"
            );
            asset.CloneTree(root);

            BaseValue.BindProperty(property.FindPropertyRelative("baseChance"));
            Label.text = property.displayName;
            ToggleCurves.Add(new Image() { image = open ? (Texture2D)Resources.Load("COZY Icons/Close WRC") : (Texture2D)Resources.Load("COZY Icons/WRC") });
            CurvesContainer.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            ToggleCurves.RegisterCallback<ClickEvent>((ClickEvent evt) =>
            {
                open = !open;
                CurvesContainer.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                ToggleCurves.Q<Image>().image = open ? (Texture2D)Resources.Load("COZY Icons/Close WRC") : (Texture2D)Resources.Load("COZY Icons/WRC");

            });

            ListView effectorsList = new ListView
            {
                showAddRemoveFooter = true,
                showBorder = true,
                showBoundCollectionSize = false,
                reorderable = true,
                reorderMode = ListViewReorderMode.Animated,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight
            };
            effectorsList.BindProperty(property.FindPropertyRelative("chanceEffectors"));
            CurvesContainer.Add(effectorsList);

            return root;

        }
    }

    public class WeightedChance : PropertyDrawer
    {
        
    }
}