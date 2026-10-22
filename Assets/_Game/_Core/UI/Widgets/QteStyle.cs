using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace Aipocalypse.Core.UI
{
    [CreateAssetMenu(menuName = "Aipocalypse/UI/QTE Style", fileName = "QteStyle")]
    public sealed class QteStyle : ScriptableObject
    {
        [BoxGroup("Sprites"), Tooltip("Track, fill and zone of a radial QTE")] public Sprite Ring;
        [BoxGroup("Sprites"), Tooltip("Track, fill and zone of a bar QTE")] public Sprite Bar;

        [BoxGroup("Colors")] public Color Track = new(0.25f, 0.29f, 0.28f, 1f);
        [BoxGroup("Colors"), Tooltip("The fill while an attempt is available")] public Color Fill = new(0.98f, 0.78f, 0.47f, 1f);
        [BoxGroup("Colors")] public Color GreenZone = new(0.3f, 0.95f, 0.48f, 1f);
        [BoxGroup("Colors"), FormerlySerializedAs("Miss"), Tooltip("The fill while no attempt is left (after a miss)")]
        public Color NoAttempt = new(1f, 0.35f, 0.22f, 1f);
        [BoxGroup("Colors"), FormerlySerializedAs("Text"), Tooltip("The moving marker")] public Color Marker = new(0.96f, 0.96f, 0.91f, 1f);
        [BoxGroup("Colors"), Range(0f, 1f), Tooltip("How strongly a result flash tints the track, as a share of the other parts' tint")]
        public float TrackFlashStrength = 0.65f;

        [BoxGroup("Key"), Tooltip("Cap behind the confirm key label, below the QTE")] public Sprite KeyCap;
        [BoxGroup("Key"), FormerlySerializedAs("Font")] public TMP_FontAsset KeyFont;
        [BoxGroup("Key"), FormerlySerializedAs("FontSize"), Min(1f)] public float KeyFontSize = 24f;
        [BoxGroup("Key")] public Color KeyLabelColor = Color.black;
        [BoxGroup("Key"), Min(0f), Tooltip("Pixels between the QTE's bottom edge and the key cap's centre")] public float KeyCapGap = 22f;
    }
}
