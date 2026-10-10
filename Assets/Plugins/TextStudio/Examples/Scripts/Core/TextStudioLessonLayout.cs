using System;
using UnityEngine;
using UnityEngine.UI;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>Reflows the same saved lesson objects when the viewing area becomes narrow.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    [UnityEngine.AddComponentMenu("")]
    public sealed class TextStudioLessonLayout : MonoBehaviour
    {
        [Serializable]
        public struct Placement
        {
            public RectTransform Target;
            public Rect Wide;
            public Rect Narrow;
        }

        [SerializeField] Camera _camera;
        [SerializeField] CanvasScaler[] _scalers = Array.Empty<CanvasScaler>();
        [SerializeField] Placement[] _placements = Array.Empty<Placement>();
        bool _applying;
        bool? _narrow;

        public void Configure(Camera camera, CanvasScaler[] scalers, Placement[] placements)
        {
            _camera = camera; _scalers = scalers; _placements = placements;
            _narrow = null;
            ApplyNow();
        }

        public void SetCamera(Camera camera)
        {
            _camera = camera;
            _narrow = null;
            ApplyNow();
        }

        void OnEnable() { _narrow = null; ApplyNow(); }
        void OnRectTransformDimensionsChange() => ApplyNow();

        public void ApplyNow()
        {
            if (_camera == null || _applying) return;
            float aspect = _camera.targetTexture != null
                ? (float)_camera.targetTexture.width / _camera.targetTexture.height
                : _camera.aspect;
            bool narrow = aspect < 1.2f;
            if (_narrow == narrow) return;
            _applying = true;
            try
            {
                _narrow = narrow;
                foreach (CanvasScaler scaler in _scalers)
                    if (scaler != null) scaler.referenceResolution = narrow ? new Vector2(900, 1200) : new Vector2(1600, 900);
                foreach (Placement placement in _placements)
                {
                    if (placement.Target == null) continue;
                    Rect bounds = narrow ? placement.Narrow : placement.Wide;
                    placement.Target.anchorMin = bounds.min;
                    placement.Target.anchorMax = bounds.max;
                    placement.Target.offsetMin = placement.Target.offsetMax = Vector2.zero;
                }
            }
            finally { _applying = false; }
        }
    }
}
