using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace MB13.Aipocalypse.Core.Debugging
{
    // Everything the dev console overlay looks like. Colours are read on every redraw; font and sizes when the overlay starts.
    // Sizes and placement stay on DebugConsole.prefab.
    [CreateAssetMenu(menuName = "Aipocalypse/Debug/Console Style", fileName = "ConsoleStyle")]
    public sealed class ConsoleStyle : ScriptableObject
    {
        [BoxGroup("Text")] public TMP_FontAsset Font;
        [BoxGroup("Text"), Min(8f)] public float FontSize = 21f;
        [BoxGroup("Text"), Min(0f), Tooltip("Width of every character in em, so columns line up. 0 = the font's own spacing")]
        public float Monospace = 0.56f;

        [BoxGroup("Panel"), Tooltip("UI blends in linear space here, so it needs a high alpha to read as dark")]
        public Color Background = new(0.03f, 0.035f, 0.045f, 0.97f);
        [BoxGroup("Panel")] public Color InputBackground = new(0f, 0f, 0f, 0.6f);
        [BoxGroup("Panel")] public Color Caret = new(0.9f, 0.9f, 0.9f, 0.9f);
        [BoxGroup("Panel"), Min(0.05f), Tooltip("Seconds the caret is shown, then hidden")] public float CaretBlink = 0.5f;

        [BoxGroup("Lines")] public Color Log = new(0.86f, 0.87f, 0.88f, 1f);
        [BoxGroup("Lines")] public Color Warning = new(1f, 0.83f, 0.35f, 1f);
        [BoxGroup("Lines")] public Color Error = new(1f, 0.42f, 0.4f, 1f);
        [BoxGroup("Lines"), Tooltip("Lines typed into the console")] public Color Input = new(0.45f, 0.8f, 1f, 1f);
        [BoxGroup("Lines"), Tooltip("Command results")] public Color Output = new(0.6f, 0.95f, 0.6f, 1f);
        [BoxGroup("Lines"), Tooltip("Repeat counts, the prompt, hints")] public Color Dim = new(0.55f, 0.57f, 0.6f, 1f);

        [BoxGroup("Suggestions"), Min(1)] public int MaxSuggestions = 8;
        [BoxGroup("Suggestions")] public Color Suggestion = new(0.75f, 0.77f, 0.8f, 1f);
        [BoxGroup("Suggestions")] public Color SelectedSuggestion = new(1f, 1f, 1f, 1f);
        [BoxGroup("Suggestions")] public Color SuggestionBackground = new(0.02f, 0.025f, 0.03f, 0.92f);

        [BoxGroup("Screen log"), Min(8f)] public float ScreenFontSize = 20f;
        [BoxGroup("Screen log"), Tooltip("Band behind each message so it reads over a bright sky")]
        public Color ScreenBacking = new(0f, 0f, 0f, 0.8f);

        public Color ColorOf(ConsoleEntryKind kind) => kind switch
        {
            ConsoleEntryKind.Warning => Warning,
            ConsoleEntryKind.Error => Error,
            ConsoleEntryKind.Input => Input,
            ConsoleEntryKind.Output => Output,
            _ => Log,
        };
    }
}
