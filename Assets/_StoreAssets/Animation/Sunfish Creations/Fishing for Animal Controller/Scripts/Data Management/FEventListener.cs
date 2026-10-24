using UnityEngine;
using MalbersAnimations.Events;
using MalbersAnimations;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SunfishCreations.Events
{
    [HelpURL("https://app.gitbook.com/o/vDVUhMUw0phKmcUzPvrh/s/7extQzUzohv5ZZCgCVG8/~/changes/29/secondary-components/event-system#feventlistener")]
    [AddComponentMenu("Malbers/Fishing/Fish Event Listener")]
    public class FEventListener : MEventListener
    {
        [SerializeField] private FEvent fishEvent;

        [System.Serializable]
        public class FishDataResponse : UnityEngine.Events.UnityEvent<FishData> { }

        public FishDataResponse fishResponse;

        // Ensure we subscribe/unsubscribe like MEventListener
        public override void OnEnable()
        {
            base.OnEnable();

            if (fishEvent != null)
                fishEvent.OnEventRaised.AddListener(OnFishEventRaised);
        }

        public override void OnDisable()
        {
            base.OnDisable();

            if (fishEvent != null)
                fishEvent.OnEventRaised.RemoveListener(OnFishEventRaised);
        }

        protected virtual void OnFishEventRaised(FishData data)
        {
            fishResponse?.Invoke(data);
        }

        public FEvent GetFishEvent() => fishEvent;
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(FEventListener))]
    public class FEventListenerEditor : MTempEventListenerEditor
    {
        SerializedProperty fishEventProp;
        SerializedProperty fishResponseProp;

        protected override void OnEnable()
        {
            base.OnEnable();
            fishEventProp = serializedObject.FindProperty("fishEvent");
            fishResponseProp = serializedObject.FindProperty("fishResponse");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            base.OnInspectorGUI();

            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Fish Data Event", EditorStyles.boldLabel);

                MalbersEditor.DrawLineHelpBox();

                EditorGUILayout.PropertyField(fishEventProp, new GUIContent("Fish Event", "Fish Event to listen to"));

                // Only show response if there's an event assigned
                if (fishEventProp.objectReferenceValue != null)
                {
                    EditorGUILayout.PropertyField(fishResponseProp, new GUIContent("Fish Response", "Response when Fish Event is raised"));
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}