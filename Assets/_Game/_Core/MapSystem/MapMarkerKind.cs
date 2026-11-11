using UnityEngine;

namespace MB13.Aipocalypse.Core.MapSystem
{
    // How one kind of map marker looks on the minimap (Home, Objective…), and whether it stays on the rim when out of range.
    [CreateAssetMenu(menuName = "Aipocalypse/Map/Marker Kind", fileName = "MapMarkerKind")]
    public sealed class MapMarkerKind : ScriptableObject
    {
        [Tooltip("Shape behind the icon. Empty = none")] public Sprite Background;
        public Color BackgroundColor = new(0.9f, 0.84f, 0.29f, 1f);
        [Tooltip("Symbol on top of the background. Empty = none")] public Sprite Icon;
        public Color IconColor = new(0.05f, 0.05f, 0.05f, 1f);
        [Min(1f), Tooltip("Pixels at the 1080p reference")] public float Size = 30f;
        [Tooltip("Out of range: sits on the rim as a direction pointer instead of hiding")] public bool PinToEdge = true;
    }
}
