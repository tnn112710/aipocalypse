using UnityEngine;

namespace TextStudio
{
    public sealed partial class TextStudioText
    {
        [SerializeField, Min(0f), Tooltip("Move retained glyphs smoothly to their new layout positions when text changes. Zero disables interpolation.")]
        float _textChangeLayoutDuration;
        TextStudioLayoutInterpolation _layoutInterpolation;

        public float TextChangeLayoutDuration
        {
            get => _textChangeLayoutDuration;
            set
            {
                _textChangeLayoutDuration = float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;
                if (_textChangeLayoutDuration == 0f) _layoutInterpolation = null;
                _runtime.SetDirty(DirtyFlags.Layout | DirtyFlags.Placement | DirtyFlags.Visual);
                ScheduleMeshRefresh();
            }
        }

        void InterpolateLayout(int count)
        {
            if (_textChangeLayoutDuration <= 0f) return;
            _layoutInterpolation ??= new TextStudioLayoutInterpolation();
            _layoutInterpolation.Apply(_placementEngine.Placements, count, _textChangeLayoutDuration,
                TextStudioMotionSettings.Resolve(MotionPolicy));
        }
    }
}
