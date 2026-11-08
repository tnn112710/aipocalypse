using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    [CreateAssetMenu(fileName = "PlayerVisionTuning", menuName = "Aipocalypse/Player/Vision Tuning")]
    public sealed class PlayerVisionTuning : ScriptableObject
    {
        [SerializeField, Min(0f), Tooltip("Metres around the player that read clearly in the dark, before StatId.VisionRadius")]
        float _radius = 4f;
        [SerializeField, ColorUsage(false)] Color _color = new(0.78f, 0.85f, 1f);
        [SerializeField, Min(0f), Tooltip("How bright the light still is on the ground at the radius' edge, at full darkness. The intensity follows from it, so a bigger radius is a brighter light")]
        float _brightness = 0.12f;
        [SerializeField, Tooltip("Where the light sits without a lantern, from the player's feet (m). Above the head spreads it evenly; a low light blows out the ground at your feet")]
        Vector3 _position = new(0f, 3f, 0f);

        [Header("Lantern"), SerializeField, Tooltip("Where a worn lantern's light comes from, from the player's feet (m). Not drawn in first person, so it's placed for the look, not at the belt: lower washes out the held item")]
        Vector3 _lanternPosition = new(0.3f, 3.2f, 0.15f);

        [Header("Light"), SerializeField, Min(1.1f), Tooltip("Light range = distance to the radius' edge × this. URP lights fade out towards their range")]
        float _rangeScale = 1.6f;
        [SerializeField, Min(0f), Tooltip("Cap on the intensity, so a very large radius doesn't blow out the hands and nearby ground")]
        float _maxIntensity = 30f;
        [SerializeField, Min(0f), Tooltip("Seconds to ease radius, colour, brightness and position when a lantern is put on or taken off")]
        float _blendTime = 0.35f;

        public float Radius => _radius;
        public Color Color => _color;
        public float Brightness => _brightness;
        public Vector3 Position => _position;
        public Vector3 LanternPosition => _lanternPosition;
        public float RangeScale => _rangeScale;
        public float MaxIntensity => _maxIntensity;
        public float BlendTime => _blendTime;
    }
}
