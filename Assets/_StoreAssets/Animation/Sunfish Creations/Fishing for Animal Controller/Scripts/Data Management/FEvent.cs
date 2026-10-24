using UnityEngine;
using UnityEngine.Events;
using MalbersAnimations.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SunfishCreations.Events
{
    [HelpURL("https://app.gitbook.com/o/vDVUhMUw0phKmcUzPvrh/s/7extQzUzohv5ZZCgCVG8/secondary-components/event-system")]
    [CreateAssetMenu(fileName = "Fish Hooked Event", menuName = "Malbers Animations/Fishing/Fish Event")]
    public class FEvent : MEvent
    {
        [System.Serializable]
        public class FishDataUnityEvent : UnityEvent<FishData> { }

        [SerializeField, HideInInspector]
        private FishDataUnityEvent onEventRaised = new FishDataUnityEvent();

        public FishDataUnityEvent OnEventRaised => onEventRaised;

        public virtual void Invoke(FishData data)
        {
#if UNITY_EDITOR
            if (debug)
            {
                Debug.Log($"<color=cyan><b>{name}</b></color> → FishData: {data}", this);
            }
#endif
            onEventRaised.Invoke(data);
            base.Invoke();  // Generic event notification
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(FEvent))]
    public class FishEventEditor : MEventEditor
    {
        private FishData fishDataAsset; // Selected FishData ScriptableObject

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI(); // Draw default MEvent inspector

            var fEvent = (FEvent)target;

            // Show FishData invoke row ONLY if Debug is enabled and app is playing
            if (fEvent.debug && Application.isPlaying)
            {
                EditorGUILayout.Space();
                DrawFishDataInvokeRow(fEvent);
            }
        }

        protected virtual void DrawFishDataInvokeRow(FEvent fEvent)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Invoke FishData", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            fishDataAsset = (FishData)EditorGUILayout.ObjectField(fishDataAsset, typeof(FishData), false);

            // Invoke even if null (like Malbers)
            if (GUILayout.Button("Invoke", GUILayout.Width(60)))
            {
                fEvent.Invoke(fishDataAsset);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
    }
#endif
}
