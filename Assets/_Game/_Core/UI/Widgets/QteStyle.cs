using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace MB13.Aipocalypse.Core.UI
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

        [BoxGroup("Success")] public Color SuccessColor = new(0.5f, 1f, 0.64f, 1f);
        [BoxGroup("Success"), Min(0.05f)] public float SuccessDuration = 1f;
        [BoxGroup("Success"), Min(0f), Tooltip("Time to keep the successful widget visible before its final fade.")] public float SuccessHold = 0.32f;
        [BoxGroup("Success"), Range(1f, 1.8f)] public float SuccessScale = 1.5f;
        [BoxGroup("Success"), Range(1f, 3f)] public float BurstScale = 2.6f;
        [BoxGroup("Success"), Range(0.01f, 0.5f), Tooltip("Share of the duration the widget takes to pop up to Success Scale, and again to settle")]
        public float SuccessPopShare = 0.12f;
        [BoxGroup("Success"), Range(1f, 1.8f), Tooltip("Scale it settles at after the pop, while held")] public float SuccessSettleScale = 1.08f;
        [BoxGroup("Success"), Range(0.5f, 1f), Tooltip("Scale it shrinks to while fading out")] public float SuccessEndScale = 0.9f;
        [BoxGroup("Success"), Range(0.1f, 1f), Tooltip("Share of the duration at which the fade-out ends")] public float SuccessFadeEndShare = 0.76f;
        [BoxGroup("Success"), Range(0.1f, 1f), Tooltip("Share of the duration the burst ring grows and fades over; also caps Success Hold")]
        public float BurstShare = 0.65f;

        [BoxGroup("Miss")] public Color MissColor = new(1f, 0.2f, 0.12f, 1f);
        [BoxGroup("Miss"), Min(0.05f)] public float MissDuration = 0.4f;
        [BoxGroup("Miss"), Min(0f)] public float MissShakePixels = 22f;
        [BoxGroup("Miss"), Min(1), Tooltip("Shake oscillations")] public int MissShakeVibrato = 10;
        [BoxGroup("Miss"), Range(0f, 1f)] public float MissShakeElasticity = 0.7f;
        [BoxGroup("Miss"), Range(0f, 0.5f), Tooltip("Widget squash on a miss")] public float MissSquash = 0.18f;
        [BoxGroup("Miss"), Range(0.05f, 1f), Tooltip("Share of the miss duration the squash lasts")] public float MissSquashShare = 0.6f;
        [BoxGroup("Miss"), Min(1)] public int MissSquashVibrato = 4;
        [BoxGroup("Miss"), Range(0f, 1f)] public float MissSquashElasticity = 0.5f;

        [BoxGroup("Ended otherwise"), Min(0.05f), Tooltip("Fade when it ends without a success (timed out, the gun cleared by itself, put away)")]
        public float EndFadeDuration = 0.18f;
    }
}
