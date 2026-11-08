using UnityEditor;
using UnityEngine;

namespace Aipocalypse.Editor.RandomSystem
{
    // A stacked bar of weight shares (one coloured segment per entry, nothing in grey), labelled where they fit.
    public static class WeightStripe
    {
        public const float Height = 22f;

        static readonly Color Background = new(0.16f, 0.16f, 0.16f);
        static readonly Color NothingColor = new(0.38f, 0.38f, 0.38f);
        static GUIStyle _label;

        // Golden-angle hues: neighbours never share a colour.
        public static Color ColorOf(int index) => Color.HSVToRGB(index * 0.618034f % 1f, 0.55f, 0.85f);

        public static void Draw(Rect rect, string[] labels, float[] weights, float nothingWeight)
        {
            _label ??= new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip, normal = { textColor = Color.black }
            };
            EditorGUI.DrawRect(rect, Background);
            float total = Mathf.Max(0f, nothingWeight);
            foreach (float weight in weights)
                total += Mathf.Max(0f, weight);
            if (total <= 0f)
            {
                GUI.Label(rect, "no weights", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            float x = rect.x;
            for (int i = 0; i <= weights.Length; i++)
            {
                bool nothing = i == weights.Length;
                float weight = nothing ? nothingWeight : weights[i];
                if (weight <= 0f)
                    continue;
                float share = weight / total;
                var segment = new Rect(x, rect.y, rect.width * share, rect.height);
                x = segment.xMax;
                EditorGUI.DrawRect(new Rect(segment.x, segment.y, Mathf.Max(1f, segment.width - 1f), segment.height),
                    nothing ? NothingColor : ColorOf(i));
                string name = nothing ? "nothing" : labels[i];
                string percent = $"{share * 100f:0.#}%";
                string text = _label.CalcSize(new GUIContent($"{name} {percent}")).x <= segment.width ? $"{name} {percent}"
                    : _label.CalcSize(new GUIContent(percent)).x <= segment.width ? percent : string.Empty;
                GUI.Label(segment, new GUIContent(text, $"{name}: {percent} (weight {weight:0.###})"), _label);
            }
        }
    }
}
