using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.UI
{
    // How the hit marker and kill numbers look. The marker's sprites, the sounds and the damage number prefab stay on
    // HitFeedbackHud in the HUD prefab.
    [CreateAssetMenu(menuName = "Aipocalypse/UI/Hit Feedback Style", fileName = "HitFeedbackStyle")]
    public sealed class HitFeedbackStyle : ScriptableObject
    {
        [BoxGroup("Marker")] public Color HitColor = Color.white;
        [BoxGroup("Marker")] public Color KillColor = new(1f, 0.25f, 0.2f, 1f);
        [BoxGroup("Marker"), Min(0.01f), Tooltip("Seconds to fade (real time)")] public float HitDuration = 0.18f;
        [BoxGroup("Marker"), Min(0.01f)] public float KillDuration = 0.4f;
        [BoxGroup("Marker"), Min(1f), Tooltip("Pop size at the start of a hit marker")] public float HitPop = 1.3f;
        [BoxGroup("Marker"), Min(1f)] public float KillPop = 1.8f;
        [BoxGroup("Marker"), Range(0.05f, 1f), Tooltip("Share of the fade the marker takes to shrink back from its pop")] public float PopShare = 0.4f;
        [BoxGroup("Damage numbers"), Min(0f), Tooltip("Metres above the hit point a damage number starts, so it rises clear of the target")]
        public float NumberLift = 0.5f;

    }
}
