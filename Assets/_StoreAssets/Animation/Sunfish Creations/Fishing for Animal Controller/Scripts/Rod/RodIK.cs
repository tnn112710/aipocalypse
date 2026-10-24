using System.Collections;
using System;
using MalbersAnimations.Items;
using SunfishCreations.Animations;
using SunfishCreations.IK;
using UnityEngine;
using SunfishCreations.Common;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SunfishCreations
{
    [HelpURL("https://sunfish-studios.gitbook.io/fishing-for-animal-controller/secondary-components/rod-ik")]
    public class RodIK : MonoBehaviour, IHandIK, IIKBridge
    {
        [Header("Fishing Loop Default Pose")]
        [Space]
        [Tooltip("Default position for the left hand")]
        public Transform leftHandDefaultPos;
        [Tooltip("Default position for the right hand when not gripping the handle")]
        public Transform rightHandDefaultPos;


        [Header("Reeling Pose")]
        [Space]
        [Tooltip("Position for the left hand when operating the reel")]
        public Transform leftHandReelPos;
        [Tooltip("Position for the right hand when gripping the rod handle")]
        public Transform rightHandHandlerPos;

        [Header("Rotation Offsets")]
        [Tooltip("Euler offset applied to the left hand target rotation")]
        [SerializeField] private Vector3 leftHandRotationOffset;

        [Tooltip("Euler offset applied to the right hand target rotation")]
        [SerializeField] private Vector3 rightHandRotationOffset;

        [Header("Elbow Pole Hints (character-local offsets from shoulder)")]
        [Tooltip("Local-space offset from the left shoulder for the elbow pole direction. X = right, Y = up, Z = forward in character space.")]
        [SerializeField] private Vector3 leftElbowPoleLocal = new Vector3(-0.17f, -0.2f, -0.5f);

        [Tooltip("Local-space offset from the right shoulder for the elbow pole direction.")]
        [SerializeField] private Vector3 rightElbowPoleLocal = new Vector3(0.05f, -0.2f, -0.5f);

        [Header("IK Lerp")]
        [Tooltip("Duration in seconds to lerp hand IK weights from zero to full on activation, and back to zero on deactivation. Set to 0 for instant activation/deactivation.")]
        [SerializeField] private float ikLerpDuration = 0f;

        [Header("Debug")]
        [Tooltip("Enable debug features for this component.")]
        [SerializeField] private bool m_debug = true;

        [Tooltip("DEBUG ONLY — When true, freezes time the moment IK activates (Fishing_Loop state enter). " +
                 "The character remains locked until you uncheck this in the inspector, at which point the original time scale is restored. " +
                 "Use this to inspect and adjust IK target positions with the character frozen in the fishing pose.")]

        [HideIf(nameof(m_debug), false)]
        [SerializeField] private bool debugLockOnFishingLoop = false;

        [Tooltip("DEBUG ONLY — When true, keeps the character locked in the reel pose even if the fish gets away. " +
                 "Logs a warning when a fish escape is intercepted. Exits normally when reeling finishes or fishing is cancelled. " +
                 "Use this to inspect and adjust the reel hand target position and rotation.")]
        [HideIf(nameof(m_debug), false)]
        [SerializeField] private bool debugLockOnReeling = false;

        [HideInInspector]
        [SerializeField] private string saveGuid = "";

        private Transform currentLeftHandTarget;
        private Transform currentRightHandTarget;

        private FishingRodWM cachedFishingRod;
        private SuperItem cachedSuperItem;

        private FishingRigCapabilities cachedCapabilities;
        private bool capabilitiesResolved;

        private IKSolver activeIKSolver;
        private bool ikActive;
        private bool isRodHolstered;
        private Coroutine debugLockCoroutine;
        private bool debugReelingLocked;

        public bool IKActive => ikActive;

        public bool DefaultIKDebugVisible =>
            IsRendererEnabled(leftHandDefaultPos) ||
            IsRendererEnabled(rightHandDefaultPos);

        public bool ReelingIKDebugVisible =>
            IsRendererEnabled(leftHandReelPos) ||
            IsRendererEnabled(rightHandHandlerPos);

        public bool IKDebugVisible => DefaultIKDebugVisible || ReelingIKDebugVisible;

        public bool IsDebugLocked => debugLockCoroutine != null;

        public bool IsReelingDebugLocked => debugReelingLocked;

        public string DebugLockLabel =>
            debugLockOnFishingLoop ? "Debug Lock On Fishing Loop"
            : debugReelingLocked ? "Debug Lock On Reeling"
            : null;

        public string SaveGuid => saveGuid;

        public Transform[] GetTargetTransforms() =>
            new[] { rightHandDefaultPos, rightHandHandlerPos, leftHandDefaultPos, leftHandReelPos };

        public void SetDefaultIKDebugVisible(bool visible)
        {
            SetRendererEnabled(leftHandDefaultPos, visible);
            SetRendererEnabled(rightHandDefaultPos, visible);
        }

        public void SetReelingIKDebugVisible(bool visible)
        {
            SetRendererEnabled(leftHandReelPos, visible);
            SetRendererEnabled(rightHandHandlerPos, visible);
        }

        public void ReleaseReelingDebugLock()
        {
            if (!debugReelingLocked)
                return;

            debugReelingLocked = false;
            Debug.Log("[RodIK] Reeling debug lock released.");
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

        private void OnEnable()
        {
            cachedSuperItem = GetComponentInParent<SuperItem>();
            isRodHolstered = cachedSuperItem != null && cachedSuperItem.IsInHolster;

            if (cachedSuperItem != null)
            {
                cachedSuperItem.OnUnequip.AddListener(HandleItemUnequipStarted);
                cachedSuperItem.OnEquip.AddListener(HandleItemEquipped);
                cachedSuperItem.OnHolstered.AddListener(HandleItemHolstered);
                cachedSuperItem.OnUnHolstered.AddListener(HandleItemUnHolstered);
            }
        }

        private void Awake()
        {
            currentLeftHandTarget = leftHandDefaultPos;
            currentRightHandTarget = rightHandDefaultPos;
        }

        public virtual void EnableIK()
        {
            ActivateIK();
        }

        public virtual void DisableIK()
        {
            DeactivateIK();
        }

        public virtual void EnableLeftHandIK()
        {
            if (activeIKSolver != null)
                activeIKSolver.SetLeftHandWeight(1f);
            else
                Debug.LogWarning("[RodIK] Cannot enable left hand IK — solver is not active. Call EnableIK() first.");
        }

        public virtual void DisableLeftHandIK()
        {
            if (activeIKSolver != null)
                activeIKSolver.SetLeftHandWeight(0f);
            else
                Debug.LogWarning("[RodIK] Cannot disable left hand IK — solver is not active. Call EnableIK() first.");
        }

        public virtual void EnableRightHandIK()
        {
            if (activeIKSolver != null)
                activeIKSolver.SetRightHandWeight(1f);
            else
                Debug.LogWarning("[RodIK] Cannot enable right hand IK — solver is not active. Call EnableIK() first.");
        }

        public virtual void DisableRightHandIK()
        {
            if (activeIKSolver != null)
                activeIKSolver.SetRightHandWeight(0f);
            else
                Debug.LogWarning("[RodIK] Cannot disable right hand IK — solver is not active. Call EnableIK() first.");
        }

        void IHandIK.OnToggleHandIK(bool isActive)
        {
            FishingRigCapabilities capabilities = ResolveCapabilities();
            if (capabilities != null && !capabilities.SupportsIK)
                return;

            if (isActive)
                ActivateIK();
            else
                DeactivateIK();
        }

        void IHandIK.SwitchLeftHand(SwitchLeftHand target)
        {
            FishingRigCapabilities capabilities = ResolveCapabilities();
            if (capabilities != null && !capabilities.SupportsIK)
                return;

            switch (target)
            {
                case SwitchLeftHand.SwitchToDefault:
                    currentLeftHandTarget = leftHandDefaultPos;
                    break;
                case SwitchLeftHand.SwitchToReelPose:
                    currentLeftHandTarget = leftHandReelPos;
                    if (m_debug && debugLockOnReeling)
                        debugReelingLocked = true;
                    break;
            }
        }

        void IHandIK.SwitchRightHand(SwitchRightHand target)
        {
            FishingRigCapabilities capabilities = ResolveCapabilities();
            if (capabilities != null && !capabilities.SupportsIK)
                return;

            switch (target)
            {
                case SwitchRightHand.SwitchToDefault:
                    currentRightHandTarget = rightHandDefaultPos;
                    break;
                case SwitchRightHand.SwitchToHandle:
                    currentRightHandTarget = rightHandHandlerPos;
                    break;
            }
        }

        private FishingRigCapabilities ResolveCapabilities()
        {
            if (!capabilitiesResolved)
            {
                Animator animator = GetComponentInParent<Animator>();
                cachedCapabilities = animator != null ? animator.GetComponentInChildren<FishingRigCapabilities>() : null;
                capabilitiesResolved = true;
            }
            return cachedCapabilities;
        }

        private void ActivateIK()
        {
            if (isRodHolstered || ikActive)
                return;

            Animator animator = GetComponentInParent<Animator>();
            if (animator == null)
            {
                Debug.LogWarning("[RodIK] No Animator found on character. IK will not work.");
                return;
            }

            activeIKSolver = animator.GetComponentInChildren<IKSolver>(true);
            if (activeIKSolver == null)
            {
                Debug.LogWarning("[RodIK] No IKSolver found on character. IK will not work.");
                return;
            }

            ikActive = true;
            activeIKSolver.Activate(this);

            if (m_debug && debugLockOnFishingLoop)
                StartDebugLock(() => debugLockOnFishingLoop, "Fishing_Loop", 3f);
        }

        private void StartDebugLock(Func<bool> isStillLocked, string label, float delay)
        {
            if (debugLockCoroutine != null)
                StopCoroutine(debugLockCoroutine);
            debugLockCoroutine = StartCoroutine(DebugLockRoutine(isStillLocked, label, delay));
        }

        private IEnumerator DebugLockRoutine(Func<bool> isStillLocked, string label, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            float originalTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            Debug.Log($"[RodIK] Frozen on {label}. Adjust IK target positions, then uncheck to resume.");

            yield return new WaitUntil(() => !isStillLocked());

            Time.timeScale = originalTimeScale;
            Debug.Log($"[RodIK] Resumed from {label}.");

            debugLockCoroutine = null;
        }

        private void DeactivateIK()
        {
            if (!ikActive)
                return;

            ikActive = false;

            if (activeIKSolver != null)
            {
                activeIKSolver.Deactivate();
                activeIKSolver = null;
            }
        }

        private void HandleItemUnequipStarted(GameObject owner)
        {
            isRodHolstered = true;
            DeactivateIK();
        }

        private void HandleItemEquipped(GameObject owner)
        {
            isRodHolstered = false;
        }

        private void HandleItemHolstered(GameObject owner)
        {
            isRodHolstered = true;
            DeactivateIK();
        }

        private void HandleItemUnHolstered(GameObject owner)
        {
            isRodHolstered = false;
        }

        private void OnDisable()
        {
            if (cachedSuperItem != null)
            {
                cachedSuperItem.OnUnequip.RemoveListener(HandleItemUnequipStarted);
                cachedSuperItem.OnEquip.RemoveListener(HandleItemEquipped);
                cachedSuperItem.OnHolstered.RemoveListener(HandleItemHolstered);
                cachedSuperItem.OnUnHolstered.RemoveListener(HandleItemUnHolstered);
                cachedSuperItem = null;
            }

            if (debugLockCoroutine != null)
            {
                StopCoroutine(debugLockCoroutine);
                debugLockCoroutine = null;
                Time.timeScale = 1f;
            }

            DeactivateIK();
        }

        private FishingRodWM GetFishingRod()
        {
            if (cachedFishingRod == null)
                cachedFishingRod = GetComponentInParent<FishingRodWM>();
            return cachedFishingRod;
        }

        public Vector3 LeftHandPosition => currentLeftHandTarget != null ? currentLeftHandTarget.position : Vector3.zero;

        public Quaternion LeftHandRotation => currentLeftHandTarget != null
            ? currentLeftHandTarget.rotation * Quaternion.Euler(leftHandRotationOffset)
            : Quaternion.identity;

        public Vector3 RightHandPosition => currentRightHandTarget != null ? currentRightHandTarget.position : Vector3.zero;

        public Quaternion RightHandRotation => currentRightHandTarget != null
            ? currentRightHandTarget.rotation * Quaternion.Euler(rightHandRotationOffset)
            : Quaternion.identity;

        // Foot targets — not used for fishing
        public Vector3 LeftFootPosition => Vector3.zero;
        public Vector3 RightFootPosition => Vector3.zero;

        public Vector3 LeftElbowPoleLocal => leftElbowPoleLocal;
        public Vector3 RightElbowPoleLocal => rightElbowPoleLocal;

        // Knee poles — not used for fishing
        public Vector3 LeftKneePoleLocal => Vector3.zero;
        public Vector3 RightKneePoleLocal => Vector3.zero;

        public float IKLerpDuration => ikLerpDuration;
    }
}

#if UNITY_EDITOR
namespace SunfishCreations
{
    /// <summary>Applies saved RodIK target transforms on Play Mode exit.</summary>
    [InitializeOnLoad]
    public static class RodIKPlayModeHandler
    {
        static RodIKPlayModeHandler()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                EnsureAllGuids();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += ApplyAllSavedTargets;
            }
        }

        private static void EnsureAllGuids()
        {
            RodIK[] allRodIKs = UnityEngine.Object.FindObjectsByType<RodIK>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (RodIK rodIK in allRodIKs)
            {
                if (string.IsNullOrEmpty(rodIK.SaveGuid))
                {
                    SerializedObject so = new SerializedObject(rodIK);
                    so.FindProperty("saveGuid").stringValue = System.Guid.NewGuid().ToString("N");
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(rodIK);
                }
            }
        }

        private static void ApplyAllSavedTargets()
        {
            RodIK[] allRodIKs = UnityEngine.Object.FindObjectsByType<RodIK>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (RodIK rodIK in allRodIKs)
                RodIKEditor.ApplySavedTargets(rodIK);
        }
    }

    [CustomEditor(typeof(RodIK))]
    public class RodIKEditor : UnityEditor.Editor
    {
        private RodIK rodIK;
        private bool prevDebugLockOnReeling;

        private const string KeyPrefix     = "RodIK_";
        private const string HasDataSuffix = "_hasData";
        private const string CountSuffix   = "_count";
        private const string ValidSuffix   = "_valid";

        private static readonly string[] TargetNames =
            { "RightHandDefault", "RightHandHandle", "LeftHandDefault", "LeftHandReel" };

        private void OnEnable()
        {
            rodIK = (RodIK)target;
            prevDebugLockOnReeling = serializedObject.FindProperty("debugLockOnReeling").boolValue;

            // Generate a save GUID if this RodIK doesn't have one yet.
            if (!Application.isPlaying && string.IsNullOrEmpty(rodIK.SaveGuid))
            {
                serializedObject.FindProperty("saveGuid").stringValue = System.Guid.NewGuid().ToString("N");
                serializedObject.ApplyModifiedProperties();
            }

            EditorApplication.update += HandleEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= HandleEditorUpdate;
        }

        private void HandleEditorUpdate()
        {
            if (Application.isPlaying && rodIK != null)
                Repaint();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "saveGuid");

            // Detect when debugLockOnReeling is unchecked at runtime.
            var reelingProp = serializedObject.FindProperty("debugLockOnReeling");
            bool currentReeling = reelingProp.boolValue;
            if (Application.isPlaying && prevDebugLockOnReeling && !currentReeling)
                rodIK.ReleaseReelingDebugLock();
            prevDebugLockOnReeling = currentReeling;

            serializedObject.ApplyModifiedProperties();

            // Read-only IK active indicator
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Toggle("IK Active", rodIK.IKActive);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);

            // Two independent IK debug visibility toggles
            Color originalColor = GUI.backgroundColor;

            bool defaultVisible = rodIK.DefaultIKDebugVisible;
            bool reelingVisible = rodIK.ReelingIKDebugVisible;

            GUILayout.BeginHorizontal();

            GUI.backgroundColor = defaultVisible ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.9f, 0.4f, 0.4f);
            if (GUILayout.Button(defaultVisible ? "Default IK: ON" : "Default IK: OFF", GUILayout.Height(28f)))
            {
                Undo.RecordObject(rodIK, "Toggle Default IK Debug View");
                rodIK.SetDefaultIKDebugVisible(!defaultVisible);
                EditorUtility.SetDirty(rodIK);
            }

            GUI.backgroundColor = reelingVisible ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.9f, 0.4f, 0.4f);
            if (GUILayout.Button(reelingVisible ? "Reeling IK: ON" : "Reeling IK: OFF", GUILayout.Height(28f)))
            {
                Undo.RecordObject(rodIK, "Toggle Reeling IK Debug View");
                rodIK.SetReelingIKDebugVisible(!reelingVisible);
                EditorUtility.SetDirty(rodIK);
            }

            GUILayout.EndHorizontal();
            GUI.backgroundColor = originalColor;

            if (Application.isPlaying && rodIK.IsDebugLocked)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox($"Character is frozen. Uncheck '{rodIK.DebugLockLabel}' to resume.", MessageType.Warning);
            }

            if (Application.isPlaying && rodIK.IsReelingDebugLocked)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox("Character held in reel pose. Uncheck 'Debug Lock On Reeling' to release.", MessageType.Warning);
            }

            // Target save/restore
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Target Transforms", EditorStyles.boldLabel);

            string sessionKey = GetSessionKey(rodIK);
            bool hasSavedData = sessionKey != null && SessionState.GetBool(sessionKey + HasDataSuffix, false);

            if (Application.isPlaying)
            {
                if (GUILayout.Button("Save Current Targets", GUILayout.Height(32f)))
                    SaveTargets(rodIK);
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                GUILayout.Button("Save Current Targets  (Play Mode only)", GUILayout.Height(32f));
                EditorGUI.EndDisabledGroup();
            }

            if (hasSavedData)
            {
                EditorGUILayout.Space(4f);

                int savedCount = SessionState.GetInt(sessionKey + CountSuffix, 0);
                for (int i = 0; i < savedCount; i++)
                {
                    if (TargetNames.Length <= i)
                        break;
                    if (!SessionState.GetBool(sessionKey + i + ValidSuffix, false))
                    {
                        EditorGUILayout.LabelField(TargetNames[i], "(was null — skipped)");
                        continue;
                    }
                    Vector3 pos = ReadVector3(sessionKey, i, "_px", "_py", "_pz");
                    Vector3 rot = ReadVector3(sessionKey, i, "_rx", "_ry", "_rz");
                    EditorGUILayout.LabelField(TargetNames[i], $"{pos}  {rot}");
                }

                if (GUILayout.Button("Clear Saved Targets"))
                    ClearSavedData(rodIK);
            }
        }

        private static string GetSessionKey(RodIK rodIK)
        {
            if (string.IsNullOrEmpty(rodIK.SaveGuid))
                return null;
            return KeyPrefix + rodIK.SaveGuid;
        }

        private static void SaveTargets(RodIK rodIK)
        {
            string key = GetSessionKey(rodIK);
            if (key == null)
            {
                EditorUtility.DisplayDialog(
                    "RodIK — Cannot Save Targets",
                    $"'{rodIK.name}' has no save GUID. Enter Play Mode again — the GUID is now auto-generated before Play Mode starts.",
                    "OK");
                Debug.LogWarning($"[RodIK] '{rodIK.name}' — no save GUID. Cannot save targets.");
                return;
            }

            Transform[] targets = rodIK.GetTargetTransforms();
            SessionState.SetInt(key + CountSuffix, targets.Length);

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null)
                {
                    SessionState.SetBool(key + i + ValidSuffix, false);
                    continue;
                }

                SessionState.SetBool(key + i + ValidSuffix, true);
                Transform t = targets[i];
                WriteVector3(key, i, "_px", "_py", "_pz", t.localPosition);
                WriteVector3(key, i, "_rx", "_ry", "_rz", t.localRotation.eulerAngles);
            }

            SessionState.SetBool(key + HasDataSuffix, true);
            Debug.Log($"[RodIK] '{rodIK.name}' — {targets.Length} target transforms saved. Will be applied on Play Mode exit.");
        }

        // Restores the saved pose onto each target once we're back in Edit Mode.
        public static void ApplySavedTargets(RodIK rodIK)
        {
            if (rodIK == null)
                return;

            string key = GetSessionKey(rodIK);
            if (key == null || !SessionState.GetBool(key + HasDataSuffix, false))
                return;

            Transform[] targets = rodIK.GetTargetTransforms();
            int savedCount = SessionState.GetInt(key + CountSuffix, 0);

            for (int i = 0; i < targets.Length && i < savedCount; i++)
            {
                if (targets[i] == null)
                    continue;

                if (!SessionState.GetBool(key + i + ValidSuffix, false))
                    continue;

                SerializedObject so = new SerializedObject(targets[i]);
                so.FindProperty("m_LocalPosition").vector3Value =
                    ReadVector3(key, i, "_px", "_py", "_pz");
                so.FindProperty("m_LocalRotation").quaternionValue =
                    Quaternion.Euler(ReadVector3(key, i, "_rx", "_ry", "_rz"));
                so.ApplyModifiedProperties();
            }

            ClearSavedData(rodIK);
            Debug.Log($"[RodIK] '{rodIK.name}' — target transforms applied after Play Mode exit.");
        }

        private static void ClearSavedData(RodIK rodIK)
        {
            string key = GetSessionKey(rodIK);
            if (key == null)
                return;

            int count = SessionState.GetInt(key + CountSuffix, 0);
            for (int i = 0; i < count; i++)
            {
                EraseVector3(key, i, "_px", "_py", "_pz");
                EraseVector3(key, i, "_rx", "_ry", "_rz");
                SessionState.EraseBool(key + i + ValidSuffix);
            }
            SessionState.EraseInt(key + CountSuffix);
            SessionState.EraseBool(key + HasDataSuffix);
        }

        // Small helpers so we don't repeat the same three SessionState calls everywhere.
        private static void WriteVector3(string key, int index, string xSuf, string ySuf, string zSuf, Vector3 v)
        {
            SessionState.SetFloat(key + index + xSuf, v.x);
            SessionState.SetFloat(key + index + ySuf, v.y);
            SessionState.SetFloat(key + index + zSuf, v.z);
        }

        private static Vector3 ReadVector3(string key, int index, string xSuf, string ySuf, string zSuf) =>
            new Vector3(
                SessionState.GetFloat(key + index + xSuf, 0f),
                SessionState.GetFloat(key + index + ySuf, 0f),
                SessionState.GetFloat(key + index + zSuf, 0f));

        private static void EraseVector3(string key, int index, string xSuf, string ySuf, string zSuf)
        {
            SessionState.EraseFloat(key + index + xSuf);
            SessionState.EraseFloat(key + index + ySuf);
            SessionState.EraseFloat(key + index + zSuf);
        }
    }
}
#endif
