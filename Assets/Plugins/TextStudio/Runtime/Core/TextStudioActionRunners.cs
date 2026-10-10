using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    internal interface ITextStudioActionRunner
    {
        bool CanRunActions { get; }
        MonoBehaviour CoroutineHost { get; }
        float DeltaTime { get; }
        bool IsEditModePreview { get; }
        void Run(IEnumerator routine);
        void LogException(System.Exception exception);
    }

    internal sealed class TextStudioMonoBehaviourActionRunner : ITextStudioActionRunner
    {
        readonly MonoBehaviour _host;
        readonly System.Func<float> _deltaTimeProvider;
        List<TrackedRoutine> _routines;

        sealed class TrackedRoutine : IEnumerator, System.IDisposable
        {
            readonly TextStudioMonoBehaviourActionRunner _owner;
            IEnumerator _routine;
            bool _advancing;
            bool _cancelled;

            public Coroutine Coroutine { get; set; }
            public object Current => _routine?.Current;

            public TrackedRoutine(TextStudioMonoBehaviourActionRunner owner, IEnumerator routine)
            {
                _owner = owner;
                _routine = routine;
            }

            public bool MoveNext()
            {
                if (_routine == null || _cancelled) return false;
                bool more = false;
                _advancing = true;
                try { more = _routine.MoveNext(); }
                finally
                {
                    _advancing = false;
                    if (!more || _cancelled) Dispose();
                }
                return more && !_cancelled;
            }

            public void Reset() => throw new System.NotSupportedException();

            public void Cancel()
            {
                _cancelled = true;
                try
                {
                    if (Coroutine != null && _owner._host != null)
                        _owner._host.StopCoroutine(Coroutine);
                }
                finally { Dispose(); }
            }

            public void Dispose()
            {
                // An action may disable its own host while its iterator is executing.
                if (_advancing) { _cancelled = true; return; }
                IEnumerator routine = _routine;
                _routine = null;
                _owner._routines.Remove(this);
                if (routine is System.IDisposable disposable) disposable.Dispose();
            }
        }

        public TextStudioMonoBehaviourActionRunner(MonoBehaviour host, System.Func<float> deltaTimeProvider = null)
        {
            _host = host;
            _deltaTimeProvider = deltaTimeProvider;
        }

        public bool CanRunActions => _host != null && _host.isActiveAndEnabled && Application.isPlaying;
        public MonoBehaviour CoroutineHost => _host;
        public float DeltaTime => _deltaTimeProvider != null
            ? Mathf.Max(0f, _deltaTimeProvider())
            : Time.deltaTime;
        public bool IsEditModePreview => false;

        public void Run(IEnumerator routine)
        {
            if (routine == null || !CanRunActions)
                return;

            TrackedRoutine tracked = new TrackedRoutine(this, routine);
            (_routines ??= new List<TrackedRoutine>(4)).Add(tracked);
            try { tracked.Coroutine = _host.StartCoroutine(tracked); }
            catch { tracked.Dispose(); throw; }
        }

        public void Clear()
        {
            if (_routines == null) return;
            while (_routines.Count > 0)
            {
                TrackedRoutine routine = _routines[_routines.Count - 1];
                _routines.RemoveAt(_routines.Count - 1);
                try { routine.Cancel(); }
                catch (System.Exception exception) { LogException(exception); }
            }
        }

        public void LogException(System.Exception exception)
        {
            Debug.LogException(exception, _host);
        }
    }

    [AddComponentMenu("")]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed partial class TextStudioRuntimeActionHost : MonoBehaviour, ITextStudioActionRunner
    {
        static TextStudioRuntimeActionHost s_instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetRuntimeHostForFastEnterPlayMode()
        {
            ReleaseRuntimeHost(false);
        }

#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnEnteringPlayMode]
        [UnityEngine.OnExitingPlayMode]
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
        static void ReleaseRuntimeHostForUnityLifecycle()
        {
            ReleaseRuntimeHost(true);
        }
#endif

        static void ReleaseRuntimeHost(bool destroyImmediately)
        {
            TextStudioRuntimeActionHost host = s_instance;
            s_instance = null;
            if (host == null)
                return;

            host.StopAllCoroutines();
            GameObject hostObject = host.gameObject;
#if UNITY_EDITOR
            if (destroyImmediately)
            {
                DestroyImmediate(hostObject);
                return;
            }
#endif
            if (Application.isPlaying)
                Destroy(hostObject);
            else
                DestroyImmediate(hostObject);
        }

        public static TextStudioRuntimeActionHost Instance
        {
            get
            {
                if (s_instance != null)
                    return s_instance;

                GameObject go = new GameObject("Text Studio Runtime Action Host");
                go.hideFlags = HideFlags.HideAndDontSave;
                s_instance = go.AddComponent<TextStudioRuntimeActionHost>();
                if (Application.isPlaying)
                    DontDestroyOnLoad(go);
                return s_instance;
            }
        }

        public bool CanRunActions => this != null && isActiveAndEnabled && Application.isPlaying;
        public MonoBehaviour CoroutineHost => this;
        public float DeltaTime => Time.deltaTime;
        public bool IsEditModePreview => false;

        public void Run(IEnumerator routine)
        {
            if (routine == null || !CanRunActions)
                return;

            StartCoroutine(routine);
        }

        public void LogException(System.Exception exception)
        {
            Debug.LogException(exception, this);
        }

        void OnDestroy()
        {
            if (ReferenceEquals(s_instance, this))
                s_instance = null;
        }
    }

#if UNITY_6000_7_OR_NEWER
    // Shared is a stateless code-lifetime adapter; its runtime host resets independently.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed partial class TextStudioLazyRuntimeActionRunner : ITextStudioActionRunner
    {
        public static readonly TextStudioLazyRuntimeActionRunner Shared = new TextStudioLazyRuntimeActionRunner();

        TextStudioLazyRuntimeActionRunner() { }

        public bool CanRunActions => Application.isPlaying && TextStudioRuntimeActionHost.Instance.CanRunActions;
        public MonoBehaviour CoroutineHost => Application.isPlaying ? TextStudioRuntimeActionHost.Instance : null;
        public float DeltaTime => Time.deltaTime;
        public bool IsEditModePreview => false;

        public void Run(IEnumerator routine)
        {
            if (routine == null || !Application.isPlaying)
                return;

            TextStudioRuntimeActionHost.Instance.Run(routine);
        }

        public void LogException(System.Exception exception)
        {
            if (Application.isPlaying)
                TextStudioRuntimeActionHost.Instance.LogException(exception);
            else
                Debug.LogException(exception);
        }
    }

    internal sealed class TextStudioEditModeActionRunner : ITextStudioActionRunner
    {
        readonly List<IEnumerator> _routines = new List<IEnumerator>(4);
        float _deltaTime;

        public bool CanRunActions => !Application.isPlaying;
        public MonoBehaviour CoroutineHost => null;
        public float DeltaTime => _deltaTime;
        public bool IsEditModePreview => true;

        public void Tick(float deltaTime)
        {
            if (Application.isPlaying)
                return;

            _deltaTime = Mathf.Max(0f, deltaTime);
            for (int i = _routines.Count - 1; i >= 0; i--)
            {
                if (!MoveNext(_routines[i]))
                {
                    Dispose(_routines[i]);
                    _routines.RemoveAt(i);
                }
            }
        }

        public void Clear()
        {
            for (int i = _routines.Count - 1; i >= 0; i--)
                Dispose(_routines[i]);

            _routines.Clear();
            _deltaTime = 0f;
        }

        public void Run(IEnumerator routine)
        {
            if (routine == null || !CanRunActions)
                return;

            if (MoveNext(routine))
                _routines.Add(routine);
            else
                Dispose(routine);
        }

        public void LogException(System.Exception exception)
        {
            Debug.LogException(exception);
        }

        bool MoveNext(IEnumerator routine)
        {
            try
            {
                return routine.MoveNext();
            }
            catch (System.Exception exception)
            {
                LogException(exception);
                return false;
            }
        }

        static void Dispose(IEnumerator routine)
        {
            if (routine is System.IDisposable disposable)
                disposable.Dispose();
        }
    }
}
