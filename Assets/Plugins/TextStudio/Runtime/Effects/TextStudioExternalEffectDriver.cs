using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Exposes one effect definition as externally driven progress and intensity. The component owns
    /// no clock or lifecycle, so Animator, Timeline, tweens, gameplay code, and editor scrubbing can
    /// drive the same renderer-neutral effect contract.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TextStudioText))]
    [AddComponentMenu("Text Studio/Animation/External Effect Driver")]
    [HelpURL("https://www.wetzold.com/tools/text-studio")]
    public sealed class TextStudioExternalEffectDriver : MonoBehaviour,
        ITextStudioExternalEffectSource
    {
        [SerializeField, Tooltip("Renderer-neutral effect definition driven by Progress and Intensity.")]
        TextStudioEffectDefinitionAsset _effect;
        [SerializeField, Range(0f, 1f), Tooltip("Normalized effect state. The effect renders this exact sample without advancing its own clock.")]
        float _progress;
        [SerializeField, Range(0f, 1f), Tooltip("Contribution strength blended over the authored Text Studio result.")]
        float _intensity = 1f;
        [SerializeField, Tooltip("Semantic text-unit range with an exclusive end. Use .. for all text.")]
        TextStudioTextRange _range = default;

        TextStudioText _registeredTarget;

        public TextStudioEffectDefinitionAsset Effect
        {
            get => _effect;
            set
            {
                if (_effect == value)
                    return;
                _effect = value;
                ScheduleUpdate();
            }
        }

        public float Progress
        {
            get => _progress;
            set
            {
                float resolved = Normalize01(value);
                if (Mathf.Approximately(_progress, resolved))
                    return;
                _progress = resolved;
                ScheduleUpdate();
            }
        }

        public float Intensity
        {
            get => _intensity;
            set
            {
                float resolved = Normalize01(value);
                if (Mathf.Approximately(_intensity, resolved))
                    return;
                _intensity = resolved;
                ScheduleUpdate();
            }
        }

        public TextStudioTextRange Range
        {
            get => _range;
            set
            {
                if (_range.Equals(value))
                    return;
                _range = value;
                ScheduleUpdate();
            }
        }

        public bool IsActive =>
            isActiveAndEnabled &&
            _effect != null &&
            _intensity > 0f && _range.IsValid;

        void OnEnable()
        {
            RefreshRegistration();
        }

        void OnDisable()
        {
            Unregister();
        }

        void OnValidate()
        {
            _progress = Normalize01(_progress);
            _intensity = Normalize01(_intensity);
            if (isActiveAndEnabled)
                RefreshRegistration();
            ScheduleUpdate();
        }

        /// <summary>Submits the driver's current effect state during an external effect collection pass.</summary>
        public void Submit(in TextStudioExternalEffectSubmissionContext context)
        {
            if (!IsActive)
                return;

            context.Submit(_effect, _progress, _intensity, _range);
        }

        TextStudioText ResolveTarget()
        {
            return GetComponent<TextStudioText>();
        }

        void RefreshRegistration()
        {
            TextStudioText target = ResolveTarget();
            if (_registeredTarget == target)
            {
                target?.ScheduleVisualStateUpdate();
                return;
            }

            Unregister();
            _registeredTarget = target;
            _registeredTarget?.RegisterExternalEffectSource(this);
        }

        void Unregister()
        {
            if (_registeredTarget == null)
                return;
            TextStudioText target = _registeredTarget;
            _registeredTarget = null;
            target.UnregisterExternalEffectSource(this);
            target.ScheduleVisualStateUpdate();
        }

        void ScheduleUpdate()
        {
            ResolveTarget()?.ScheduleVisualStateUpdate();
        }

        static float Normalize01(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Mathf.Clamp01(value);
        }
    }
}
