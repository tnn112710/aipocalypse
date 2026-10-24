using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SunfishCreations
{
    [AddComponentMenu("Sunfish Creations/Fishing/Fish Rigidbody Runtime Settings")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class FishRigidbodyRuntimeSettings : MonoBehaviour
    {
        private const float DefaultMass = 1f;
        private const float DefaultDamping = 0f;

        [Header("Runtime Rigidbody Settings")]
        [SerializeField, Min(0.01f), Tooltip("Mass applied when Play Mode starts.")]
        private float mass = DefaultMass;

        [SerializeField, Min(0f), Tooltip("Linear damping applied when Play Mode starts.")]
        private float linearDamping = DefaultDamping;

        [SerializeField, Min(0f), Tooltip("Angular damping applied when Play Mode starts.")]
        private float angularDamping = DefaultDamping;

        private Rigidbody targetRigidbody;

        private void Awake()
        {
            targetRigidbody = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            targetRigidbody.mass = mass;

#if UNITY_6000_0_OR_NEWER
            targetRigidbody.linearDamping = linearDamping;
            targetRigidbody.angularDamping = angularDamping;
#else
            targetRigidbody.drag = linearDamping;
            targetRigidbody.angularDrag = angularDamping;
#endif
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(FishRigidbodyRuntimeSettings))]
    public class FishRigidbodyRuntimeSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Required on fish with a Rigidbody. Unity package imports may not preserve Rigidbody values, so this component reapplies them when Play Mode starts.",
                MessageType.Info);
        }
    }
#endif
}
