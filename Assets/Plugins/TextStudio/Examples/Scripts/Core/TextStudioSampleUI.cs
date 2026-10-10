using TMPro;
using UnityEngine.UI;

namespace TextStudio.Examples.Tutorials
{
    // Lesson copy and exploratory controls are optional; the authored product output is not.
    internal static class TextStudioSampleUI
    {
        internal static void SetSampleText(this TMP_Text label, string value)
        {
            if (label != null) label.text = value;
        }
        internal static void AppendSampleText(this TMP_Text label, string value)
        {
            if (label != null) label.text += value;
        }
        internal static void SetSampleValue(this Slider slider, float value)
        {
            if (slider != null) slider.SetValueWithoutNotify(value);
        }
        internal static void SetSampleInteractable(this Selectable control, bool value)
        {
            if (control != null) control.interactable = value;
        }
    }
}
