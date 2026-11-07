//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEngine.Events;

namespace DistantLands.Cozy
{
    [AddComponentMenu("Distant Lands/Cozy/Weather Event Listener")]
    public class WeatherEventListener : MonoBehaviour
    {
        public EventFX Event => _event;
        [SerializeField]
        private EventFX _event;

        public UnityEvent onStart;
        public UnityEvent onRunning;
        public UnityEvent onEnd;

        void OnEnable()
        {
            _event.OnStart += () => onStart.Invoke();
            _event.OnUpdate += () => onRunning.Invoke();
            _event.OnEnd += () => onEnd.Invoke();
        }
        void OnDisable()
        {
            _event.OnStart -= () => onStart.Invoke();
            _event.OnUpdate -= () => onRunning.Invoke();
            _event.OnEnd -= () => onEnd.Invoke();
        }
    }
}
