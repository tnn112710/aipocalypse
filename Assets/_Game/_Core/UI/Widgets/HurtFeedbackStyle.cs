using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.UI
{
    // How the screen edge flashes when the active player is hurt. The edges themselves are on HurtFeedbackHud in the HUD prefab;
    // their gradient is made in code.
    [CreateAssetMenu(menuName = "Aipocalypse/UI/Hurt Feedback Style", fileName = "HurtFeedbackStyle")]
    public sealed class HurtFeedbackStyle : ScriptableObject
    {
        [BoxGroup("Look")] public Color FlashColor = new(0.72f, 0.03f, 0.02f, 1f);
        [BoxGroup("Look"), Range(0f, 1f), Tooltip("Alpha of an edge for a full-strength hit from straight on")] public float MaxAlpha = 0.6f;
        [BoxGroup("Look"), Range(0.02f, 0.5f), Tooltip("How far an edge reaches into the screen, as a share of the screen height")]
        public float Thickness = 0.2f;
        [BoxGroup("Strength"), Range(0f, 1f), Tooltip("Strength of the lightest hit (1 = full)")] public float MinStrength = 0.35f;
        [BoxGroup("Strength"), Range(0.01f, 1f), Tooltip("Damage that flashes at full strength, as a share of max health")]
        public float FullStrengthShare = 0.25f;
        [BoxGroup("Timing"), Min(0.01f), Tooltip("Seconds an edge takes to fade out (real time)")] public float FadeOut = 0.7f;
    }
}
