using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SunfishCreations.IK
{
    /// <summary>
    /// Exposes four world-space IK target transforms (hands and feet) for the IK solver.
    /// </summary>
    public class IKBridgeProvider : MonoBehaviour, IIKBridge
    {
        [Header("General")]
        [Tooltip("When false, the vehicle IK system is completely disabled for this bridge.")]
        [SerializeField] private bool active = true;

        [Header("IK Target Transforms")]
        [Tooltip("World-space IK target for the left hand.")]
        [SerializeField] private Transform leftHandTarget;
        [Tooltip("World-space IK target for the right hand.")]
        [SerializeField] private Transform rightHandTarget;
        [Tooltip("World-space IK target for the left foot.")]
        [SerializeField] private Transform leftFootTarget;
        [Tooltip("World-space IK target for the right foot.")]
        [SerializeField] private Transform rightFootTarget;

        [Header("Rotation Offsets")]
        [SerializeField] private Vector3 leftHandRotationOffset;
        [SerializeField] private Vector3 rightHandRotationOffset;

        [Header("Elbow Pole Hints (character-local offsets from shoulder)")]
        [Tooltip("Local-space offset from the left shoulder for the elbow pole direction.")]
        [SerializeField] private Vector3 leftElbowPoleLocal = new Vector3(-0.17f, -0.2f, -0.5f);

        [Tooltip("Local-space offset from the right shoulder for the elbow pole direction.")]
        [SerializeField] private Vector3 rightElbowPoleLocal = new Vector3(0.05f, -0.2f, -0.5f);

        [Header("Knee Pole Hints (character-local offsets from upper leg)")]
        [Tooltip("Local-space offset from the left upper leg for the knee pole direction.")]
        [SerializeField] private Vector3 leftKneePoleLocal = new Vector3(-0.03f, 0f, 0.5f);

        [Tooltip("Local-space offset from the right upper leg for the knee pole direction.")]
        [SerializeField] private Vector3 rightKneePoleLocal = new Vector3(0f, 0f, 0.5f);

        [Header("IK Lerp")]
        [Tooltip("Duration in seconds to lerp IK weights on activation/deactivation. 0 for instant.")]
        [SerializeField] private float ikLerpDuration = 0f;

        public event System.Action<bool> OnActiveChanged;

        public bool IsActive => active;

        public void SetActive(bool value)
        {
            if (active == value)
                return;

            active = value;
            OnActiveChanged?.Invoke(value);
        }

        public bool IKDebugVisible =>
            IsRendererEnabled(leftHandTarget) ||
            IsRendererEnabled(rightHandTarget) ||
            IsRendererEnabled(leftFootTarget) ||
            IsRendererEnabled(rightFootTarget);

        public Vector3 LeftHandPosition => leftHandTarget.position;
        public Quaternion LeftHandRotation => leftHandTarget.rotation * Quaternion.Euler(leftHandRotationOffset);
        public Vector3 RightHandPosition => rightHandTarget.position;
        public Quaternion RightHandRotation => rightHandTarget.rotation * Quaternion.Euler(rightHandRotationOffset);
        public Vector3 LeftFootPosition => leftFootTarget.position;
        public Vector3 RightFootPosition => rightFootTarget.position;

        public Vector3 LeftElbowPoleLocal => leftElbowPoleLocal;
        public Vector3 RightElbowPoleLocal => rightElbowPoleLocal;
        public Vector3 LeftKneePoleLocal => leftKneePoleLocal;
        public Vector3 RightKneePoleLocal => rightKneePoleLocal;

        public float IKLerpDuration => ikLerpDuration;

        public Vector3 LeftHandRotationOffsetEuler => leftHandRotationOffset;
        public Vector3 RightHandRotationOffsetEuler => rightHandRotationOffset;

        public void SetIKDebugVisible(bool visible)
        {
            SetRendererEnabled(leftHandTarget, visible);
            SetRendererEnabled(rightHandTarget, visible);
            SetRendererEnabled(leftFootTarget, visible);
            SetRendererEnabled(rightFootTarget, visible);
        }

        private static void SetRendererEnabled(Transform target, bool enabled)
        {
            if (target == null) return;
            var renderer = target.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.enabled = enabled;
        }

        private static bool IsRendererEnabled(Transform target)
        {
            if (target == null) return false;
            var renderer = target.GetComponent<MeshRenderer>();
            return renderer != null && renderer.enabled;
        }
    }

#if UNITY_EDITOR
    [InitializeOnLoad]
    public static class IKBridgeProviderPlayModeHandler
    {
        static IKBridgeProviderPlayModeHandler()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
                return;

            EditorApplication.delayCall += ApplyAllSavedOffsets;
        }

        private static void ApplyAllSavedOffsets()
        {
            IKBridgeProvider[] allBridges = Object.FindObjectsByType<IKBridgeProvider>(FindObjectsSortMode.None);
            foreach (IKBridgeProvider bridge in allBridges)
                IKBridgeProviderEditor.ApplySavedOffsets(bridge);
        }
    }

    [CustomEditor(typeof(IKBridgeProvider))]
    public class IKBridgeProviderEditor : UnityEditor.Editor
    {
        private const string KeyPrefix     = "IKBridgeOffsets_";
        private const string HasDataSuffix = "_hasData";

        private const string LHRotXSuffix = "_lhrx";
        private const string LHRotYSuffix = "_lhry";
        private const string LHRotZSuffix = "_lhrz";

        private const string RHRotXSuffix = "_rhrx";
        private const string RHRotYSuffix = "_rhry";
        private const string RHRotZSuffix = "_rhrz";

        private const string LEPXSuffix = "_lepx";
        private const string LEPYSuffix = "_lepy";
        private const string LEPZSuffix = "_lepz";

        private const string REPXSuffix = "_repx";
        private const string REPYSuffix = "_repy";
        private const string REPZSuffix = "_repz";

        private const string LKPXSuffix = "_lkpx";
        private const string LKPYSuffix = "_lkpy";
        private const string LKPZSuffix = "_lkpz";

        private const string RKPXSuffix = "_rkpx";
        private const string RKPYSuffix = "_rkpy";
        private const string RKPZSuffix = "_rkpz";

        private IKBridgeProvider bridge;

        private void OnEnable()
        {
            bridge = (IKBridgeProvider)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            bool newActive = EditorGUILayout.Toggle(
                new GUIContent("Active", "When false, the vehicle IK system is completely disabled for this bridge."),
                bridge.IsActive);

            if (newActive != bridge.IsActive)
            {
                Undo.RecordObject(bridge, "Toggle IK Bridge Active");
                bridge.SetActive(newActive);
                EditorUtility.SetDirty(bridge);
            }

            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "active");
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);

            bool currentlyVisible = bridge.IKDebugVisible;
            string label = currentlyVisible ? "IK Debug: ON" : "IK Debug: OFF";

            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = currentlyVisible ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.9f, 0.4f, 0.4f);

            if (GUILayout.Button(label, GUILayout.Height(28f)))
            {
                Undo.RecordObject(bridge, "Toggle IK Debug View");
                bridge.SetIKDebugVisible(!currentlyVisible);
                EditorUtility.SetDirty(bridge);
            }

            GUI.backgroundColor = originalColor;

            EditorGUILayout.Space(8f);

            string activeKey = GetActiveKey(bridge);

            if (Application.isPlaying)
            {
                if (GUILayout.Button("Save Current Offsets", GUILayout.Height(32f)))
                    SaveOffsets(bridge);
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                GUILayout.Button("Save Current Offsets  (Play Mode only)", GUILayout.Height(32f));
                EditorGUI.EndDisabledGroup();
            }

            if (activeKey != null)
            {
                EditorGUILayout.Space(4f);

                EditorGUILayout.HelpBox(
                    $"Saved offsets:\n" +
                    $"LH Rot Offset : {ReadVector3(activeKey, LHRotXSuffix, LHRotYSuffix, LHRotZSuffix)}\n" +
                    $"RH Rot Offset : {ReadVector3(activeKey, RHRotXSuffix, RHRotYSuffix, RHRotZSuffix)}\n" +
                    $"L Elbow Pole  : {ReadVector3(activeKey, LEPXSuffix, LEPYSuffix, LEPZSuffix)}\n" +
                    $"R Elbow Pole  : {ReadVector3(activeKey, REPXSuffix, REPYSuffix, REPZSuffix)}\n" +
                    $"L Knee Pole   : {ReadVector3(activeKey, LKPXSuffix, LKPYSuffix, LKPZSuffix)}\n" +
                    $"R Knee Pole   : {ReadVector3(activeKey, RKPXSuffix, RKPYSuffix, RKPZSuffix)}",
                    MessageType.Info);

                if (GUILayout.Button("Clear Saved Offsets"))
                    ClearAllStoredData(bridge);
            }
        }

        private static void SaveOffsets(IKBridgeProvider target)
        {
            string gidKey  = GetGidKey(target);
            string pathKey = GetPathKey(target);

            StoreOffsetValues(gidKey ?? pathKey, target);
            if (gidKey != null && gidKey != pathKey)
                StoreOffsetValues(pathKey, target);

            Debug.Log($"[IKBridgeProvider] '{target.name}' — offsets saved. Will be applied on Play Mode exit.");
        }

        private static void StoreOffsetValues(string id, IKBridgeProvider target)
        {
            SessionState.SetBool(id + HasDataSuffix, true);

            WriteVector3(id, target.LeftHandRotationOffsetEuler, LHRotXSuffix, LHRotYSuffix, LHRotZSuffix);
            WriteVector3(id, target.RightHandRotationOffsetEuler, RHRotXSuffix, RHRotYSuffix, RHRotZSuffix);
            WriteVector3(id, target.LeftElbowPoleLocal,          LEPXSuffix,   LEPYSuffix,   LEPZSuffix);
            WriteVector3(id, target.RightElbowPoleLocal,         REPXSuffix,   REPYSuffix,   REPZSuffix);
            WriteVector3(id, target.LeftKneePoleLocal,           LKPXSuffix,   LKPYSuffix,   LKPZSuffix);
            WriteVector3(id, target.RightKneePoleLocal,          RKPXSuffix,   RKPYSuffix,   RKPZSuffix);
        }

        public static void ApplySavedOffsets(IKBridgeProvider target)
        {
            if (target == null)
                return;

            string activeKey = GetActiveKey(target);
            if (activeKey == null)
                return;

            SerializedObject so = new SerializedObject(target);

            so.FindProperty("leftHandRotationOffset").vector3Value  = ReadVector3(activeKey, LHRotXSuffix, LHRotYSuffix, LHRotZSuffix);
            so.FindProperty("rightHandRotationOffset").vector3Value = ReadVector3(activeKey, RHRotXSuffix, RHRotYSuffix, RHRotZSuffix);
            so.FindProperty("leftElbowPoleLocal").vector3Value      = ReadVector3(activeKey, LEPXSuffix,   LEPYSuffix,   LEPZSuffix);
            so.FindProperty("rightElbowPoleLocal").vector3Value     = ReadVector3(activeKey, REPXSuffix,   REPYSuffix,   REPZSuffix);
            so.FindProperty("leftKneePoleLocal").vector3Value       = ReadVector3(activeKey, LKPXSuffix,   LKPYSuffix,   LKPZSuffix);
            so.FindProperty("rightKneePoleLocal").vector3Value      = ReadVector3(activeKey, RKPXSuffix,   RKPYSuffix,   RKPZSuffix);

            so.ApplyModifiedProperties();
            ClearAllStoredData(target);

            Debug.Log($"[IKBridgeProvider] '{target.name}' — offsets applied after Play Mode exit.");
        }

        private const string InvalidGid = "GlobalObjectId_V1-0-00000000000000000000000000000000-0-0";

        private static string GetGidKey(IKBridgeProvider target)
        {
            GlobalObjectId gid = GlobalObjectId.GetGlobalObjectIdSlow(target.gameObject);
            string gidString = gid.ToString();
            if (gidString.Equals(InvalidGid, System.StringComparison.Ordinal))
                return null;
            return KeyPrefix + gidString;
        }

        private static string GetPathKey(IKBridgeProvider target)
        {
            string scenePath     = target.gameObject.scene.path;
            string hierarchyPath = GetHierarchyPath(target.transform);
            return KeyPrefix + scenePath + "|" + hierarchyPath;
        }

        private static string GetActiveKey(IKBridgeProvider target)
        {
            string gidKey = GetGidKey(target);
            if (gidKey != null && SessionState.GetBool(gidKey + HasDataSuffix, false))
                return gidKey;

            string pathKey = GetPathKey(target);
            if (SessionState.GetBool(pathKey + HasDataSuffix, false))
                return pathKey;

            return null;
        }

        private static string GetHierarchyPath(Transform t)
        {
            if (t.parent == null)
                return t.name;
            return GetHierarchyPath(t.parent) + "/" + t.name;
        }

        private static void WriteVector3(string id, Vector3 v, string xSuf, string ySuf, string zSuf)
        {
            SessionState.SetFloat(id + xSuf, v.x);
            SessionState.SetFloat(id + ySuf, v.y);
            SessionState.SetFloat(id + zSuf, v.z);
        }

        private static Vector3 ReadVector3(string id, string xSuf, string ySuf, string zSuf) =>
            new Vector3(
                SessionState.GetFloat(id + xSuf, 0f),
                SessionState.GetFloat(id + ySuf, 0f),
                SessionState.GetFloat(id + zSuf, 0f));

        private static void ClearAllStoredData(IKBridgeProvider target)
        {
            string gidKey  = GetGidKey(target);
            string pathKey = GetPathKey(target);
            if (gidKey != null) SessionState.EraseBool(gidKey + HasDataSuffix);
            if (pathKey != gidKey) SessionState.EraseBool(pathKey + HasDataSuffix);
        }
    }
#endif
}
