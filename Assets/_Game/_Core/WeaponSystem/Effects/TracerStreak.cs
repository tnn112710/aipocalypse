using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // The tracer's look per shot, driven by BulletSystem: a little random length, width and brightness, a brighter glow in
    // the dark, width that grows with distance so far shots keep their size on screen (zoom counted), and a head glow that
    // goes out when the bullet ends.
    [DisallowMultipleComponent]
    public sealed class TracerStreak : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] TrailRenderer[] _trails;
        [SerializeField, Tooltip("Trail whose brightness varies per shot")] TrailRenderer _core;
        [SerializeField, Tooltip("Trail that glows brighter in the dark")] TrailRenderer _glow;
        [SerializeField, Tooltip("Glow riding at the front; cleared when the bullet hits or runs out of range")] ParticleSystem _head;

        [SerializeField, Min(0.1f), Tooltip("Up to this distance from the camera (m) the streak keeps its real size; beyond it, it widens to keep its size on screen")]
        float _fullSizeDistance = 15f;
        [SerializeField, Range(20f, 120f), Tooltip("Vertical field of view Full Size Distance is tuned at; a narrower (zoomed) view widens it less")]
        float _referenceFov = 70f;
        [SerializeField, Min(1f), Tooltip("Most it widens far away")] float _maxScale = 6f;
        [SerializeField, Range(0f, 0.5f), Tooltip("Random ± share of trail length per shot")] float _lengthJitter = 0.2f;
        [SerializeField, Range(0f, 0.5f), Tooltip("Random ± share of width per shot")] float _widthJitter = 0.15f;
        [SerializeField, Range(0f, 0.5f), Tooltip("Random ± share of core brightness per shot")] float _brightnessJitter = 0.25f;
        [SerializeField, Min(1f), Tooltip("Glow brightness multiplier in full darkness (1 = same as by day)")] float _nightGlow = 1.8f;

        float[] _baseTimes;
        float[] _baseWidths;
        Color _coreColor;
        Color _glowColor;
        MaterialPropertyBlock _block;
        float _width = 1f;
        float _referenceTan;

        // Width multiplier for a streak `distance` m from a camera whose tan(half vertical FOV) is `tanHalfFov`: its screen
        // size compared with one at the full-size distance seen through the reference FOV.
        public static float ScreenScale(float distance, float tanHalfFov, float fullSizeDistance, float referenceTan, float maxScale) =>
            Mathf.Clamp(distance * tanHalfFov / (Mathf.Max(0.1f, fullSizeDistance) * referenceTan), 1f, Mathf.Max(1f, maxScale));

        // Right after it spawns (pooled: base values are read once, from the prefab's own). `darkness`: 0 by day, 1 at night.
        public void Begin(float darkness)
        {
            if (_baseTimes == null)
            {
                _baseTimes = new float[_trails.Length];
                _baseWidths = new float[_trails.Length];
                for (int i = 0; i < _trails.Length; i++)
                {
                    _baseTimes[i] = _trails[i].time;
                    _baseWidths[i] = _trails[i].widthMultiplier;
                }
                if (_core != null && _core.sharedMaterial != null)
                    _coreColor = _core.sharedMaterial.GetColor(BaseColorId);
                if (_glow != null && _glow.sharedMaterial != null)
                    _glowColor = _glow.sharedMaterial.GetColor(BaseColorId);
                _block = new MaterialPropertyBlock();
            }
            _referenceTan = Mathf.Tan(_referenceFov * 0.5f * Mathf.Deg2Rad);

            float length = 1f + Random.Range(-_lengthJitter, _lengthJitter);
            _width = 1f + Random.Range(-_widthJitter, _widthJitter);
            for (int i = 0; i < _trails.Length; i++)
                _trails[i].time = _baseTimes[i] * length;
            if (_core != null)
            {
                _block.SetColor(BaseColorId, Brighter(_coreColor, 1f + Random.Range(-_brightnessJitter, _brightnessJitter)));
                _core.SetPropertyBlock(_block);
            }
            if (_glow != null)
            {
                _block.SetColor(BaseColorId, Brighter(_glowColor, Mathf.Lerp(1f, _nightGlow, Mathf.Clamp01(darkness))));
                _glow.SetPropertyBlock(_block);
            }
            ScaleFor(0f, 1f);
        }

        // Additive blending weights colour by alpha: scale only the colour, or the change would count twice.
        static Color Brighter(Color color, float by) => new(color.r * by, color.g * by, color.b * by, color.a);

        public void ScaleFor(float cameraDistance, float tanHalfFov)
        {
            float scale = ScreenScale(cameraDistance, tanHalfFov, _fullSizeDistance, _referenceTan, _maxScale);
            for (int i = 0; i < _trails.Length; i++)
                _trails[i].widthMultiplier = _baseWidths[i] * _width * scale;
            if (_head != null)
                _head.transform.localScale = Vector3.one * (_width * scale);
        }

        // The bullet stopped: the trail fades into the end point, the head doesn't glow there. Stopped, not just cleared:
        // a point-blank hit ends before the head's burst has emitted. PooledEffect.Restart plays it again.
        public void End()
        {
            if (_head != null)
                _head.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
