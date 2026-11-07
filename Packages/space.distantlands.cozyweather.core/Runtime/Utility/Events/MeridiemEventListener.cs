//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEngine.Events;

namespace DistantLands.Cozy
{
    [AddComponentMenu("Distant Lands/Cozy/Meridiem Event Listener")]
    public class MeridiemEventListener : MonoBehaviour
    {
        public StaticEvent Event => _event;
        [SerializeField]
        private StaticEvent _event;

        public UnityEvent onStart;
        public UnityEvent onRunning;
        public UnityEvent onEnd;

        void OnEnable()
        {
            _event.OnEventStart += () => onStart.Invoke();
            _event.OnEventRunning += () => onRunning.Invoke();
            _event.OnEventEnd += () => onEnd.Invoke();
        }
        void OnDisable()
        {
            _event.OnEventStart -= () => onStart.Invoke();
            _event.OnEventRunning -= () => onRunning.Invoke();
            _event.OnEventEnd -= () => onEnd.Invoke();
        }
    }
}
