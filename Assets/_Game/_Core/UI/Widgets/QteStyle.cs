using TMPro;
using UnityEngine;

namespace Aipocalypse.Core.UI
{
    [CreateAssetMenu(menuName = "Aipocalypse/UI/QTE Style", fileName = "QteStyle")]
    public sealed class QteStyle : ScriptableObject
    {
        public Sprite Ring;
        public Sprite Bar;
        public Sprite KeyCap;
        public TMP_FontAsset Font;
        public Color Track = new(0.25f, 0.29f, 0.28f, 1f);
        public Color Fill = new(0.98f, 0.78f, 0.47f, 1f);
        public Color GreenZone = new(0.3f, 0.95f, 0.48f, 1f);
        public Color Miss = new(1f, 0.35f, 0.22f, 1f);
        public Color Text = new(0.96f, 0.96f, 0.91f, 1f);
        [Min(1f)] public float FontSize = 24f;
    }
}
