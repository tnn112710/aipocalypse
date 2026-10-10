using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio
{
    public sealed partial class TextStudioBinding
    {
        float _textChangeLayoutDuration;
        TextStudioLayoutInterpolation _layoutInterpolation;

        [UxmlAttribute("text-change-layout-duration")]
        public float TextChangeLayoutDuration
        {
            get => _textChangeLayoutDuration;
            set
            {
                _textChangeLayoutDuration = float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;
                if (_textChangeLayoutDuration == 0f) _layoutInterpolation = null;
                ApplyPlacementChange();
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
