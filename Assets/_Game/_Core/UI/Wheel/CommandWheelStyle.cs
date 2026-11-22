using UnityEngine;

namespace MB13.Aipocalypse.Core.UI
{
    // The command wheel's look (Data/UI/CommandWheelStyle.asset). Layout stays on the HUD prefab.
    [CreateAssetMenu(fileName = "CommandWheelStyle", menuName = "Aipocalypse/UI/Command Wheel Style")]
    public sealed class CommandWheelStyle : ScriptableObject
    {
        [Tooltip("Each slice's backing")] public Sprite Slice;
        [Tooltip("The disc behind the wheel")] public Sprite Backdrop;
        public Color Normal = new(0.12f, 0.14f, 0.16f, 0.85f);
        public Color Highlighted = new(0.95f, 0.72f, 0.25f, 0.95f);
        public Color Disabled = new(0.12f, 0.14f, 0.16f, 0.4f);
        [Tooltip("Label colour of the slice that is the current setting")] public Color Current = new(0.55f, 0.9f, 0.55f, 1f);
        public Color Label = Color.white;
        public Color DisabledLabel = new(1f, 1f, 1f, 0.35f);
        [Min(0f), Tooltip("Slice centres' distance from the middle (px at 1080p)")] public float Radius = 170f;
    }
}
