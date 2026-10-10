using UnityEngine;

namespace TextStudio.InputSystem
{
    [AddComponentMenu("")]
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class TextStudioInputSystemSampleBootstrap : MonoBehaviour
    {
        InputSystemTextStudioInputProvider _provider;
        ITextStudioInputProvider _previousProvider;
        bool _ownsProvider;

        void OnEnable()
        {
            if (!Application.isPlaying || _ownsProvider)
                return;

            _provider = _provider ?? new InputSystemTextStudioInputProvider();
            _previousProvider = TextStudioRuntimeServices.InputProvider;
            TextStudioRuntimeServices.InputProvider = _provider;
            _ownsProvider = true;
        }

        void OnDisable()
        {
            if (!_ownsProvider)
                return;

            if (ReferenceEquals(TextStudioRuntimeServices.InputProvider, _provider))
                TextStudioRuntimeServices.InputProvider = _previousProvider;

            _previousProvider = null;
            _ownsProvider = false;
        }
    }
}
