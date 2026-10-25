using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Aipocalypse.Core.UI
{
    // Everything the interact prompt looks like. Sizes and placement stay in the InteractPrompt prefab.
    [CreateAssetMenu(menuName = "Aipocalypse/UI/Interact Prompt Style", fileName = "InteractPromptStyle")]
    public sealed class InteractPromptStyle : ScriptableObject
    {
        [BoxGroup("Key")] public Sprite KeySprite;
        [BoxGroup("Key")] public Color KeyColor = new(0.95f, 0.95f, 0.95f, 0.95f);
        [BoxGroup("Key")] public TMP_FontAsset KeyFont;
        [BoxGroup("Key")] public Color KeyLabelColor = new(0.1f, 0.1f, 0.1f, 1f);
        [BoxGroup("Key"), Min(1f)] public float KeyFontSize = 20f;

        [BoxGroup("Text")] public TMP_FontAsset Font;
        [BoxGroup("Text")] public Color VerbColor = new(0.95f, 0.95f, 0.95f, 1f);
        [BoxGroup("Text"), Tooltip("Color of the item / object name")] public Color TargetColor = new(1f, 0.72f, 0.6f, 1f);
        [BoxGroup("Text"), Min(1f)] public float FontSize = 26f;

#if UNITY_EDITOR
        // Edit-time preview only: prompts re-apply their style in Awake.
        void OnValidate() => StyleUtils.RefreshViews<InteractPromptView>(this, prompt => prompt.Style, prompt => prompt.ApplyStyle());
#endif
    }
}
