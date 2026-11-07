//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(CozyVFXInstance))]
    [CanEditMultipleObjects]
    public class CozyVFXInstanceEditor : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();

            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/space.distantlands.cozyweather.core/Editor/UI/Globals.uss"));

            Tooltip tooltip = new Tooltip("Nothing else to configure!", StatusType.Pass);
            root.Add(tooltip);

            return root;
        }
    }
}