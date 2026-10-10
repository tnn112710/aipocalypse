using ImpossibleRobert.Common;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TextStudio
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-32700)]
    [AddComponentMenu("")]
    public sealed class TextStudioSingleEventSystemGuard : MonoBehaviour
    {
        [SerializeField, Min(0)] int _fallbackDelayFrames = 2;

        EventSystem _eventSystem;
        BaseInputModule[] _inputModules;
        int _framesSinceEnable;

        void Awake()
        {
            CacheLocalEventSystem();
        }

        void OnEnable()
        {
            _framesSinceEnable = 0;
            CacheLocalEventSystem();
        }

        void LateUpdate()
        {
            _framesSinceEnable++;
            EnsureSingleEventSystem();
        }

        public void EnsureSingleEventSystem()
        {
            CacheLocalEventSystem();

            EventSystem[] activeSystems = UnityUtils.FindAll<EventSystem>();
            EventSystem keeper = ResolveActiveKeeper(activeSystems);
            if (keeper == null && _eventSystem != null && _framesSinceEnable >= _fallbackDelayFrames)
            {
                SetLocalEventSystemEnabled(true);
                activeSystems = UnityUtils.FindAll<EventSystem>();
                keeper = ResolveActiveKeeper(activeSystems);
            }

            if (keeper == null)
                return;

            for (int i = 0; i < activeSystems.Length; i++)
            {
                EventSystem system = activeSystems[i];
                if (system == null || system == keeper)
                    continue;

                DestroyDuplicateEventSystem(system);
            }
        }

        void CacheLocalEventSystem()
        {
            _eventSystem = _eventSystem != null ? _eventSystem : GetComponent<EventSystem>();
            _inputModules = _inputModules != null && _inputModules.Length > 0
                ? _inputModules
                : GetComponents<BaseInputModule>();
        }

        EventSystem ResolveActiveKeeper(EventSystem[] activeSystems)
        {
            if (_eventSystem != null && _eventSystem.isActiveAndEnabled)
                return _eventSystem;

            if (EventSystem.current != null && EventSystem.current.isActiveAndEnabled)
                return EventSystem.current;

            return activeSystems.Length > 0 ? activeSystems[0] : null;
        }

        void SetLocalEventSystemEnabled(bool enabled)
        {
            if (enabled)
                SetInputModulesEnabled(true);

            if (_eventSystem != null)
                _eventSystem.enabled = enabled;

            if (!enabled)
                SetInputModulesEnabled(false);
        }

        void SetInputModulesEnabled(bool enabled)
        {
            for (int i = 0; i < _inputModules.Length; i++)
            {
                if (_inputModules[i] != null)
                    _inputModules[i].enabled = enabled;
            }
        }

        static void DestroyDuplicateEventSystem(EventSystem system)
        {
            GameObject duplicate = system.gameObject;
            BaseInputModule[] modules = system.GetComponents<BaseInputModule>();
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i] != null)
                    modules[i].enabled = false;
            }

            if (Application.isPlaying)
                Destroy(duplicate);
            else
                DestroyImmediate(duplicate);
        }
    }
}
