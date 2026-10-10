using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    [CustomEditor(typeof(TextStudioSignalDriver))]
    [CanEditMultipleObjects]
    internal sealed class TextStudioSignalDriverEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-signal-driver-inspector");
            root.Add(TextStudioInspectorElements.CreateSection("Effect", "ts-signal-effect",
                Field("_effect", "Effect"), Field("_destination", "Drive"), Field("_intensity", "Maximum Intensity")));
            PropertyField source = Field("_source", "Source");
            VisualElement input = TextStudioInspectorElements.CreateSection("Signal", "ts-signal-input", source);
            PropertyField value = Field("_value", "Normalized Value");
            VisualElement audio = new VisualElement();
            audio.Add(Field("_audioSource", "Audio Source"));
            audio.Add(Field("_audioGain", "Amplitude Gain"));
            VisualElement beat = new VisualElement();
            beat.Add(Field("_beatsPerMinute", "Beats Per Minute"));
            beat.Add(Field("_beatsPerCycle", "Beats Per Cycle"));
            beat.Add(Field("_beatOriginSeconds", "DSP Start Time"));
            input.Add(value);
            input.Add(audio);
            input.Add(beat);
            root.Add(input);
            PropertyField progress = Field("_fixedProgress", "Fixed Effect Progress");
            root.Add(TextStudioInspectorElements.CreateSection("Response", "ts-signal-response",
                Field("_response", "Response Curve"), Field("_attackSeconds", "Attack Time"),
                Field("_releaseSeconds", "Release Time"), progress,
                TextStudioInspectorElements.CreateMutedText("Signals run during Play Mode. The text's motion preference also applies to the driven effect.")));
            void Refresh()
            {
                SerializedProperty choice = serializedObject.FindProperty("_source");
                bool mixed = choice.hasMultipleDifferentValues;
                value.style.display = mixed || choice.enumValueIndex == (int)TextStudioSignalSource.NormalizedValue ? DisplayStyle.Flex : DisplayStyle.None;
                audio.style.display = mixed || choice.enumValueIndex == (int)TextStudioSignalSource.AudioAmplitude ? DisplayStyle.Flex : DisplayStyle.None;
                beat.style.display = mixed || choice.enumValueIndex == (int)TextStudioSignalSource.Beat ? DisplayStyle.Flex : DisplayStyle.None;
                SerializedProperty destination = serializedObject.FindProperty("_destination");
                progress.style.display = destination.hasMultipleDifferentValues || destination.enumValueIndex == (int)TextStudioSignalDestination.EffectIntensity
                    ? DisplayStyle.Flex : DisplayStyle.None;
            }
            root.TrackSerializedObjectValue(serializedObject, _ => Refresh());
            Refresh();
            return root;
        }

        PropertyField Field(string property, string label) => TextStudioInspectorElements.CreatePropertyField(serializedObject, property, label);
    }
}
