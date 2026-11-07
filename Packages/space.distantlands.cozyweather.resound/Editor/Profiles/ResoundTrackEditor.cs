
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(ResoundTrack))]
    public class ResoundTrackEditor : Editor
    {
        ResoundTrack profile;
        VisualElement root;

        void OnEnable()
        {
            profile = (ResoundTrack)target;
        }

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/space.distantlands.cozyweather.core/Editor/UI/Globals.uss");
            if (styleSheet != null) root.styleSheets.Add(styleSheet);

            Label playbackLabel = new Label("Playback");
            playbackLabel.AddToClassList("h2");
            root.Add(playbackLabel);

            VisualElement playbackContainer = new VisualElement();
            playbackContainer.AddToClassList("section-bg");
            root.Add(playbackContainer);

            playbackContainer.Add(CreatePropertyField("clip"));
            playbackContainer.Add(CreatePropertyField("volume"));
            playbackContainer.Add(CreatePropertyField("chance"));

            PropertyField loopMode = CreatePropertyField("loopMode");
            playbackContainer.Add(loopMode);
            
            VisualElement minMaxTimeContainer = new VisualElement();
            playbackContainer.Add(minMaxTimeContainer);

            loopMode.RegisterValueChangeCallback(evt =>
            {
                minMaxTimeContainer.enabledSelf = serializedObject.FindProperty("loopMode").enumValueIndex == (int)ResoundTrack.LoopMethod.Loop;
            });

            minMaxTimeContainer.Add(CreatePropertyField("minTime"));
            minMaxTimeContainer.Add(CreatePropertyField("maxTime"));

            return root;
        }

        PropertyField CreatePropertyField(string propertyName)
        {
            PropertyField propertyField = new PropertyField();
            propertyField.BindProperty(serializedObject.FindProperty(propertyName));
            return propertyField;
        }
    }
}