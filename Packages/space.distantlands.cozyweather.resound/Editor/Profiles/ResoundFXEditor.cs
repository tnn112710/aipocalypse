using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(ResoundFX))]
    [CanEditMultipleObjects]
    public class ResoundFXEditor : FXProfileEditor
    {
        public override VisualElement RenderInWindow()
        {
            VisualElement container = new VisualElement();
            
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/space.distantlands.cozyweather.core/Editor/UI/Globals.uss");
            if (styleSheet != null) container.styleSheets.Add(styleSheet);

            Label trackLabel = new Label("Track");
            trackLabel.AddToClassList("h2");
            container.Add(trackLabel);

            PropertyField track = new PropertyField(serializedObject.FindProperty("track"));
            container.Add(track);

            PropertyField priority = new PropertyField(serializedObject.FindProperty("priority"));
            container.Add(priority);

            Label transitionsLabel = new Label("Transitions");
            transitionsLabel.AddToClassList("h2");
            container.Add(transitionsLabel);

            PropertyField transitionOnStart = new PropertyField(serializedObject.FindProperty("transitionOnStart"));
            transitionOnStart.label = "When FX Starts";
            container.Add(transitionOnStart);

            PropertyField transitionOnEnd = new PropertyField(serializedObject.FindProperty("transitionOnEnd"));
            transitionOnEnd.label = "When FX Ends";
            container.Add(transitionOnEnd);

            return container;
        }
    }
}
