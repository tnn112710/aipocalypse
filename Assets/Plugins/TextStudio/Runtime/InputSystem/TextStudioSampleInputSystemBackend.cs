using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace TextStudio.InputSystem
{
#if UNITY_6000_7_OR_NEWER
    // Backend leases restore the previously installed input provider before clearing their shared ownership.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioSampleInputSystemBackend
    {
        static int s_owners;
        static ITextStudioInputProvider s_previous;
        static InputSystemTextStudioInputProvider s_provider;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            ReleaseProvider();
            TextStudioSampleInput.RegisterBackend(0, Install);
        }

#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        static void ReleaseProvider()
        {
            if (s_provider != null && ReferenceEquals(TextStudioRuntimeServices.InputProvider, s_provider))
                TextStudioRuntimeServices.InputProvider = s_previous;
            s_owners = 0;
            s_previous = null;
            s_provider = null;
        }

        static ITextStudioSampleInputBackend Install(GameObject owner) => new Lease(owner);

        sealed class Lease : ITextStudioSampleInputBackend
        {
            InputSystemUIInputModule _module;
            bool _active;
            internal Lease(GameObject owner)
            {
                if (owner.GetComponent<EventSystem>() != null && owner.GetComponent<BaseInputModule>() == null)
                {
                    _module = owner.AddComponent<InputSystemUIInputModule>();
                    _module.AssignDefaultActions();
                    _module.enabled = false;
                }
            }
            public void Activate()
            {
                if (_active) return;
                _active = true;
                if (_module != null) _module.enabled = true;
                if (s_owners++ == 0)
                {
                    s_previous = TextStudioRuntimeServices.InputProvider;
                    s_provider = new InputSystemTextStudioInputProvider();
                    TextStudioRuntimeServices.InputProvider = s_provider;
                }
            }
            public void Deactivate()
            {
                if (!_active) return;
                _active = false;
                if (_module != null) _module.enabled = false;
                if (s_owners == 0 || --s_owners != 0) return;
                if (ReferenceEquals(TextStudioRuntimeServices.InputProvider, s_provider)) TextStudioRuntimeServices.InputProvider = s_previous;
                s_previous = null; s_provider = null;
            }
            public void Dispose()
            {
                Deactivate();
                if (_module != null) { UnityEngine.Object.Destroy(_module); _module = null; }
            }
        }
    }
}
