//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(CozyLightningAttractor))]
    public class CozyLightningAttractorEditor : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();

            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/space.distantlands.cozyweather.core/Editor/UI/Globals.uss"));

            string message = "Lighting will strike this point when it is enabled.";
            StatusType tooltipType = StatusType.Info;

            Tooltip tooltip = new Tooltip(message, tooltipType);
            root.Add(tooltip);

            PropertyField onStrike = new PropertyField();
            onStrike.BindProperty(serializedObject.FindProperty("onStrike"));
            root.Add(onStrike);

            return root;
        }
    }
}