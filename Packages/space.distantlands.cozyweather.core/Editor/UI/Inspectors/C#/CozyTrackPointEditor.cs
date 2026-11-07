//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(CozyTrackPoint))]
    public class CozyTrackPointEditor : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();

            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/space.distantlands.cozyweather.core/Editor/UI/Globals.uss"));

            string message = "COZY's FX will follow this transform";
            StatusType tooltipType = StatusType.Info;
            if (CozyTrackPoint.Instance != target)
            {
                if (CozyTrackPoint.Instance)
                {
                    message = $"Multiple track points detected. COZY will follow {CozyTrackPoint.Instance.name}.";
                    tooltipType = StatusType.Warning;
                }
                else
                {
                    message = $"No track point detected.";
                    tooltipType = StatusType.Warning;
                }
            }

            Tooltip tooltip = new Tooltip(message, tooltipType);
            root.Add(tooltip);

            return root;
        }
    }
}