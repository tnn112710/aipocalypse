using UnityEngine;
using UnityEditor;
using DistantLands.Cozy.Horizon;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(CozyHorizonModule))]
    public class CozyHorizonModuleEditor : CozyBiomeModuleEditor
    {
        private CozyHorizonModule horizonModule;
        private VisualElement root;

        void OnEnable()
        {
            if (!target)
                return;

            horizonModule = (CozyHorizonModule)target;
        }


        public override VisualElement DisplayUI()
        {
            root = new VisualElement();

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/space.distantlands.cozyweather.core/Editor/UI/Globals.uss");
            if (styleSheet != null) root.styleSheets.Add(styleSheet);

            Tooltip tooltip = new Tooltip("Make sure the Horizon renderer feature is added to your URP renderer!", StatusType.Warning);
            tooltip.style.marginTop = 8;
            root.Add(tooltip);

            VisualElement profileContainer = new VisualElement();
            profileContainer.AddToClassList("section-bg");
            root.Add(profileContainer);

            PropertyField profile = new PropertyField();
            profile.BindProperty(serializedObject.FindProperty("profile"));
            profileContainer.Add(profile);

            return root;
        }
    }
}
