using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TextStudio
{
    internal interface ITextStudioSampleInputBackend : IDisposable
    {
        void Activate();
        void Deactivate();
    }

    /// <summary>Owns native input backends for a bundled sample without serialized optional-package references.</summary>
    [AddComponentMenu(""), DisallowMultipleComponent, DefaultExecutionOrder(-1500)]
#if UNITY_6000_7_OR_NEWER
    // Backend factories are repopulated before scene load after the baseline registry reset.
    [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
#endif
    internal sealed partial class TextStudioSampleInput : MonoBehaviour
    {
        static readonly SortedDictionary<int, Func<GameObject, ITextStudioSampleInputBackend>> s_backends = new SortedDictionary<int, Func<GameObject, ITextStudioSampleInputBackend>>();
        readonly List<ITextStudioSampleInputBackend> _leases = new List<ITextStudioSampleInputBackend>();
        BaseInputModule _legacyModule;

        internal static void RegisterBackend(int order, Func<GameObject, ITextStudioSampleInputBackend> install) => s_backends[order] = install;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetBackends() => s_backends.Clear();

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            try
            {
                if (_leases.Count == 0)
                    foreach (Func<GameObject, ITextStudioSampleInputBackend> install in s_backends.Values)
                    {
                        ITextStudioSampleInputBackend lease = install(gameObject);
                        if (lease != null) _leases.Add(lease);
                    }
                foreach (ITextStudioSampleInputBackend lease in _leases) lease.Activate();
#if ENABLE_LEGACY_INPUT_MANAGER
                if (GetComponent<BaseInputModule>() == null && GetComponent<EventSystem>() != null)
                    _legacyModule = gameObject.AddComponent<StandaloneInputModule>();
                if (_legacyModule != null) _legacyModule.enabled = true;
#endif
            }
            catch { Release(); throw; }
        }

        void OnDisable()
        {
            for (int i = _leases.Count - 1; i >= 0; i--) _leases[i].Deactivate();
            if (_legacyModule != null) _legacyModule.enabled = false;
        }
        void OnDestroy() => Release();
        void OnApplicationQuit() => Release();

        void Release()
        {
            for (int i = _leases.Count - 1; i >= 0; i--) _leases[i].Dispose();
            _leases.Clear();
            if (_legacyModule != null) { _legacyModule.enabled = false; Destroy(_legacyModule); _legacyModule = null; }
        }
    }
}
