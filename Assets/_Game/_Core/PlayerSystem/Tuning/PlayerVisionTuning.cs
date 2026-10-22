using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    [CreateAssetMenu(fileName = "PlayerVisionTuning", menuName = "Aipocalypse/Player/Vision Tuning")]
    public sealed class PlayerVisionTuning : ScriptableObject
    {
        [field: SerializeField, Min(0f), Tooltip("Metres around the player that read clearly in the dark, before StatId.VisionRadius")]
        public float Radius { get; private set; } = 4f;
        [field: SerializeField, ColorUsage(false)] public Color Color { get; private set; } = new(0.78f, 0.85f, 1f);
        [field: SerializeField, Min(0f), Tooltip("How bright the light still is on the ground at the radius' edge, at full darkness. The intensity follows from it, so a bigger radius is a brighter light")]
        public float Brightness { get; private set; } = 0.12f;
        [field: SerializeField, Tooltip("Where the light sits without a lantern, from the player's feet (m). Above the head spreads it evenly; a low light blows out the ground at your feet")]
        public Vector3 Position { get; private set; } = new(0f, 3f, 0f);

        [field: Header("Lantern"), SerializeField, Tooltip("Where a worn lantern's light comes from, from the player's feet (m). Not drawn in first person, so it's placed for the look, not at the belt: lower washes out the held item")]
        public Vector3 LanternPosition { get; private set; } = new(0.3f, 3.2f, 0.15f);

        [field: Header("Light"), SerializeField, Min(1.1f), Tooltip("Light range = distance to the radius' edge × this. URP lights fade out towards their range")]
        public float RangeScale { get; private set; } = 1.6f;
        [field: SerializeField, Min(0f), Tooltip("Cap on the intensity, so a very large radius doesn't blow out the hands and nearby ground")]
        public float MaxIntensity { get; private set; } = 30f;
        [field: SerializeField, Min(0f), Tooltip("Seconds to ease radius, colour, brightness and position when a lantern is put on or taken off")]
        public float BlendTime { get; private set; } = 0.35f;
    }
}
