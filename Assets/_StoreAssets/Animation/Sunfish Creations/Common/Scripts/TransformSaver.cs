using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

namespace SunfishCreations.Common
{


public class TransformSaver : MonoBehaviour
{
    [Tooltip("Auto-generated identity snapshot used to match saved transforms across Play Mode, even if this object gets reparented at runtime. Captured once in Edit Mode.")]
    [HideInInspector]
    [SerializeField] private string stableKey = "";

    /// <summary>The frozen identity key used for SessionState lookups.</summary>
    public string StableKey => stableKey;
}

#if UNITY_EDITOR
/// <summary>Static listener that applies all saved transforms on Play Mode exit.</summary>
[InitializeOnLoad]
public static class TransformSaverPlayModeHandler
{
    private const int DelayFrames      = 3;
    private const int MaxRetryAttempts = 5;

    private static int _pendingFrames;
    private static int _retryAttempts;

    static TransformSaverPlayModeHandler()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            // Freeze each TransformSaver's identity key before Play starts, while everything is
            // still in its original Edit Mode hierarchy. Some objects get reparented at runtime,
            // which can shift their live GlobalObjectId.
            EnsureAllStableKeysGenerated();
            return;
        }

        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        // Defer a few editor updates so Unity finishes restoring the pre-play-mode scene snapshot.
        _pendingFrames  = DelayFrames;
        _retryAttempts  = 0;
        EditorApplication.update -= DelayedApplyAllSavedTransforms;
        EditorApplication.update += DelayedApplyAllSavedTransforms;
    }

    private static void EnsureAllStableKeysGenerated()
    {
        TransformSaver[] allSavers = Object.FindObjectsByType<TransformSaver>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (TransformSaver saver in allSavers)
            TransformSaverEditor.EnsureStableKey(saver);
    }

    private static void DelayedApplyAllSavedTransforms()
    {
        if (_pendingFrames > 0)
        {
            _pendingFrames--;
            return;
        }

        // Include inactive GameObjects — FindObjectsByType excludes them by default in Unity 6.
        TransformSaver[] allSavers = Object.FindObjectsByType<TransformSaver>(FindObjectsInactive.Include, FindObjectsSortMode.None);        int appliedCount  = 0;
        int pendingCount  = 0;

        foreach (TransformSaver saver in allSavers)
        {
            // Skip savers that have no saved data.
            if (!TransformSaverEditor.HasSavedData(saver))
                continue;

            if (TransformSaverEditor.ApplySavedTransform(saver))
                appliedCount++;
            else
                pendingCount++;
        }

        if (appliedCount > 0)
            Debug.Log($"[TransformSaver] Applied saved transforms to {appliedCount} object(s) after Play Mode exit.");

        // If some savers had saved data but the apply didn't take effect, retry.
        if (pendingCount > 0 && _retryAttempts < MaxRetryAttempts)
        {
            _retryAttempts++;
            _pendingFrames = 1;
            return;
        }

        EditorApplication.update -= DelayedApplyAllSavedTransforms;

        if (pendingCount > 0)
            Debug.LogWarning($"[TransformSaver] {pendingCount} object(s) still have unapplied saved transforms after {_retryAttempts} retries. The saved data is preserved — select the object and use the inspector to verify.");

        // Reveal the last-used TransformSaver in the Inspector for convenience.
        TransformSaverEditor.SelectLastUsedSaverIfAny();
    }
}

[CustomEditor(typeof(TransformSaver))]
public class TransformSaverEditor : UnityEditor.Editor
{
    // SessionState keys — scoped per component instance via a GlobalObjectId-based key
    // (frozen once in Edit Mode as TransformSaver.stableKey to survive runtime reparenting).
    private const string KeyPrefix     = "TransformSaver_";
    private const string HasDataSuffix = "_hasData";
    private const string LPosXSuffix   = "_lpx";
    private const string LPosYSuffix   = "_lpy";
    private const string LPosZSuffix   = "_lpz";
    private const string LRotXSuffix   = "_lrx";
    private const string LRotYSuffix   = "_lry";
    private const string LRotZSuffix   = "_lrz";
    private const string LRotWSuffix   = "_lrw";
    private const string LScaleXSuffix = "_lsx";
    private const string LScaleYSuffix = "_lsy";
    private const string LScaleZSuffix = "_lsz";

    private TransformSaver transformSaver;

    private void OnEnable()
    {
        transformSaver = (TransformSaver)target;

        // Safety net for objects added mid-session.
        EnsureStableKey(transformSaver);
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        EditorGUILayout.Space(8f);

        string sessionKey = GetSessionKey(transformSaver);

        if (Application.isPlaying)
        {
            if (GUILayout.Button("Save Current Transform", GUILayout.Height(32f)))
                SaveTransform(transformSaver);
        }
        else
        {
            EditorGUI.BeginDisabledGroup(true);
            GUILayout.Button("Save Current Transform  (Play Mode only)", GUILayout.Height(32f));
            EditorGUI.EndDisabledGroup();
        }

        if (sessionKey != null && SessionState.GetBool(sessionKey + HasDataSuffix, false))
        {
            EditorGUILayout.Space(4f);

            Vector3    savedPos   = ReadLocalPosition(sessionKey);
            Quaternion savedRot   = ReadLocalRotation(sessionKey);
            Vector3    savedScale = ReadLocalScale(sessionKey);

            EditorGUILayout.HelpBox(
                $"Saved (local space)\n" +
                $"Position : {savedPos}\n" +
                $"Rotation : {savedRot.eulerAngles}\n" +
                $"Scale    : {savedScale}",
                MessageType.Info);

            if (GUILayout.Button("Clear Saved Transform"))
                ClearStoredData(transformSaver);

            if (GUILayout.Button("Copy Saved Transform"))
                CopySavedTransformToClipboard(savedPos, savedRot, savedScale, transformSaver.name);
        }
    }

    // --- Key generation ---

    private static string GetSessionKey(TransformSaver saver)
    {
        if (!string.IsNullOrEmpty(saver.StableKey))
            return saver.StableKey;

        return GetGidKey(saver) ?? GetPathKey(saver);
    }

    /// <summary>
    /// Captures and freezes this saver's identity key if it doesn't already have one.
    /// Must run in Edit Mode, before any runtime reparenting can occur.
    /// </summary>
    public static void EnsureStableKey(TransformSaver saver)
    {
        if (saver == null || Application.isPlaying)
            return;

        if (!string.IsNullOrEmpty(saver.StableKey))
            return;

        string liveKey = GetGidKey(saver) ?? GetPathKey(saver);
        if (string.IsNullOrEmpty(liveKey))
            return;

        SerializedObject so = new SerializedObject(saver);
        so.FindProperty("stableKey").stringValue = liveKey;
        so.ApplyModifiedProperties();
    }

    // --- Save / Apply / Clear ---

    private static void SaveTransform(TransformSaver saver)
    {
        Transform t = saver.transform;
        string key = GetSessionKey(saver);

        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning($"[TransformSaver] '{saver.name}' — no valid key could be generated. Transform not saved.");
            return;
        }

        StoreTransformValues(key, t);
        RecordLastUsedSaver(saver);
        Debug.Log($"[TransformSaver] '{saver.name}' — local transform saved under key: {key}");
    }

    // --- Last-used tracking (UI convenience only) ---

    private const string LastUsedGidKey = "TransformSaver_LastUsedGid";

    private static void RecordLastUsedSaver(TransformSaver saver)
    {
        GlobalObjectId gid = GlobalObjectId.GetGlobalObjectIdSlow(saver.gameObject);
        string gidString = gid.ToString();
        if (!gidString.Equals(InvalidGid, System.StringComparison.Ordinal))
            SessionState.SetString(LastUsedGidKey, gidString);
    }

    /// <summary>
    /// Selects and pings the last-used TransformSaver's GameObject in the Editor.
    /// </summary>
    public static void SelectLastUsedSaverIfAny()
    {
        string gidString = SessionState.GetString(LastUsedGidKey, "");
        if (string.IsNullOrEmpty(gidString))
            return;

        if (!GlobalObjectId.TryParse(gidString, out GlobalObjectId gid))
            return;

        Object obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
        GameObject target = obj as GameObject ?? (obj as Component)?.gameObject;
        if (target == null)
            return;

        Selection.activeGameObject = target;
        EditorGUIUtility.PingObject(target);
    }

    /// <summary>
    /// Copies the saved local-space transform to Unity's internal component clipboard.
    /// </summary>
    private static void CopySavedTransformToClipboard(Vector3 localPos, Quaternion localRot, Vector3 localScale, string saverName)
    {
        // Create a temporary GameObject, apply the saved values, and copy its Transform.
        GameObject tempGo = new GameObject("TransformSaver_Clipboard_Temp");
        try
        {
            tempGo.hideFlags = HideFlags.HideAndDontSave;
            Transform tempT = tempGo.transform;
            tempT.localPosition = localPos;
            tempT.localRotation = localRot;
            tempT.localScale    = localScale;

            if (ComponentUtility.CopyComponent(tempT))
                Debug.Log($"[TransformSaver] '{saverName}' — saved transform copied to clipboard. Right-click any Transform → Paste Component Values.");
            else
                Debug.LogWarning($"[TransformSaver] '{saverName}' — CopyComponent returned false. Could not copy transform to clipboard.");
        }
        finally
        {
            Object.DestroyImmediate(tempGo);
        }
    }

    private static void StoreTransformValues(string id, Transform t)
    {
        SessionState.SetBool(id + HasDataSuffix, true);

        SessionState.SetFloat(id + LPosXSuffix, t.localPosition.x);
        SessionState.SetFloat(id + LPosYSuffix, t.localPosition.y);
        SessionState.SetFloat(id + LPosZSuffix, t.localPosition.z);

        SessionState.SetFloat(id + LRotXSuffix, t.localRotation.x);
        SessionState.SetFloat(id + LRotYSuffix, t.localRotation.y);
        SessionState.SetFloat(id + LRotZSuffix, t.localRotation.z);
        SessionState.SetFloat(id + LRotWSuffix, t.localRotation.w);

        SessionState.SetFloat(id + LScaleXSuffix, t.localScale.x);
        SessionState.SetFloat(id + LScaleYSuffix, t.localScale.y);
        SessionState.SetFloat(id + LScaleZSuffix, t.localScale.z);
    }

    /// <summary>Returns true if this saver has saved transform data.</summary>
    public static bool HasSavedData(TransformSaver saver)
    {
        if (saver == null)
            return false;

        string key = GetSessionKey(saver);
        return key != null && SessionState.GetBool(key + HasDataSuffix, false);
    }

    /// <summary>
    /// Applies stored local-space values via SerializedObject (undoable and scene-dirty).
    /// Returns false if the apply did not take effect.
    /// </summary>
    public static bool ApplySavedTransform(TransformSaver saver)
    {
        if (saver == null)
            return false;

        string key = GetSessionKey(saver);
        if (key == null || !SessionState.GetBool(key + HasDataSuffix, false))
            return false;

        Vector3    savedPos   = ReadLocalPosition(key);
        Quaternion savedRot   = ReadLocalRotation(key);
        Vector3    savedScale = ReadLocalScale(key);

        SerializedObject so = new SerializedObject(saver.transform);
        so.FindProperty("m_LocalPosition").vector3Value    = savedPos;
        so.FindProperty("m_LocalRotation").quaternionValue = savedRot;
        so.FindProperty("m_LocalScale").vector3Value       = savedScale;

        so.ApplyModifiedProperties();

        // Verify the values actually took effect — during early play-mode-exit frames
        // the scene may not be fully restored.
        Transform t = saver.transform;
        bool positionMatches = t.localPosition == savedPos;
        bool rotationMatches = t.localRotation == savedRot;
        bool scaleMatches    = t.localScale    == savedScale;

        if (positionMatches && rotationMatches && scaleMatches)
        {
            Debug.Log($"[TransformSaver] '{saver.name}' — local transform applied after Play Mode exit (key: {key}). Saved data preserved.");
            return true;
        }

        Debug.LogWarning($"[TransformSaver] '{saver.name}' — ApplyModifiedProperties did not take effect (scene may not be fully restored). Will retry. Saved data preserved.");
        return false;
    }

    /// <summary>Clears stored SessionState data for this saver.</summary>
    private static void ClearStoredData(TransformSaver saver)    {
        string key = GetSessionKey(saver);
        if (key != null)
            SessionState.EraseBool(key + HasDataSuffix);
    }

    // --- Key helpers ---

    private const string InvalidGid = "GlobalObjectId_V1-0-00000000000000000000000000000000-0-0";

    /// <summary>GlobalObjectId-based key. Returns null if GID is invalid.</summary>
    private static string GetGidKey(TransformSaver saver)
    {
        GlobalObjectId gid = GlobalObjectId.GetGlobalObjectIdSlow(saver.gameObject);
        string gidString = gid.ToString();
        if (gidString.Equals(InvalidGid, System.StringComparison.Ordinal))
            return null;
        return KeyPrefix + gidString;
    }

    /// <summary>Scene-path + hierarchy-path key. Fallback when GID is unavailable.</summary>
    private static string GetPathKey(TransformSaver saver)
    {
        string scenePath     = saver.gameObject.scene.path;
        string hierarchyPath = GetHierarchyPath(saver.transform);
        return KeyPrefix + scenePath + "|" + hierarchyPath;
    }

    // --- Path helpers ---

    private static string GetHierarchyPath(Transform t)
    {
        if (t.parent == null)
            return t.name;
        return GetHierarchyPath(t.parent) + "/" + t.name;
    }

    // --- Read helpers ---

    private static Vector3 ReadLocalPosition(string id) =>
        new Vector3(
            SessionState.GetFloat(id + LPosXSuffix, 0f),
            SessionState.GetFloat(id + LPosYSuffix, 0f),
            SessionState.GetFloat(id + LPosZSuffix, 0f));

    private static Quaternion ReadLocalRotation(string id) =>
        new Quaternion(
            SessionState.GetFloat(id + LRotXSuffix, 0f),
            SessionState.GetFloat(id + LRotYSuffix, 0f),
            SessionState.GetFloat(id + LRotZSuffix, 0f),
            SessionState.GetFloat(id + LRotWSuffix, 1f));

    private static Vector3 ReadLocalScale(string id) =>
        new Vector3(
            SessionState.GetFloat(id + LScaleXSuffix, 1f),
            SessionState.GetFloat(id + LScaleYSuffix, 1f),
            SessionState.GetFloat(id + LScaleZSuffix, 1f));
}
#endif
}
