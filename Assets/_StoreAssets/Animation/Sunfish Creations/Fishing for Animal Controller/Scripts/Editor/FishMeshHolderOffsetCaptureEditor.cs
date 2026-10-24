#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SunfishCreations
{
    [CustomEditor(typeof(FishMeshHolderOffsetCapture))]
    public class FishMeshHolderOffsetCaptureEditor : Editor
    {
        private string captureStartMessage;
        private bool editorWasPausedBeforeCapture;
        private bool editorPausedByCapture;

        private void OnDisable()
        {
            if (!editorPausedByCapture)
            {
                return;
            }

            FishMeshHolderOffsetCapture capture = target as FishMeshHolderOffsetCapture;
            if (capture != null && capture.CaptureStarted)
            {
                capture.FinishCapture(true);
            }

            ResumeEditorAfterCapture();
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            FishMeshHolderOffsetCapture capture = (FishMeshHolderOffsetCapture)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Fish Holder Offset Capture", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode and present this fish, then click Begin Capture.", MessageType.Info);
                return;
            }

            if (!capture.CaptureStarted)
            {
                EditorGUILayout.HelpBox("Present this fish, then click Begin Capture. The Editor will pause while you adjust the mesh.", MessageType.Info);

                if (GUILayout.Button("Begin Capture"))
                {
                    if (capture.TryBeginCapture(out captureStartMessage))
                    {
                        PauseEditorForCapture();
                    }
                    else
                    {
                        Debug.LogWarning(captureStartMessage, capture);
                    }
                }

                if (!string.IsNullOrEmpty(captureStartMessage))
                {
                    EditorGUILayout.HelpBox(captureStartMessage, MessageType.Error);
                }

                return;
            }

            captureStartMessage = null;
            EditorGUILayout.HelpBox("The Editor is paused. Adjust the mesh, then click Save Position to Fish Data to apply and resume, or Cancel and Restore to discard and resume.", MessageType.Info);

            if (!capture.TryGetCaptureDelta(out Vector3 holderLocalDelta, out string captureMessage))
            {
                EditorGUILayout.HelpBox(captureMessage, MessageType.Error);
                if (GUILayout.Button("Cancel Capture"))
                {
                    capture.FinishCapture(true);
                    ResumeEditorAfterCapture();
                }
                return;
            }

            FishData fishData = capture.CapturedFishData;
            EditorGUILayout.ObjectField("Fish Data", fishData, typeof(FishData), false);
            EditorGUILayout.Vector3Field("Additional Holder Offset", holderLocalDelta);
            EditorGUILayout.Vector3Field("Resulting Fish Data Offset", fishData.holderOffset + holderLocalDelta);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Position to Fish Data"))
            {
                if (SaveCapturedOffset(capture, fishData))
                {
                    ResumeEditorAfterCapture();
                }
            }

            if (GUILayout.Button("Cancel and Restore"))
            {
                capture.FinishCapture(true);
                ResumeEditorAfterCapture();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void PauseEditorForCapture()
        {
            editorWasPausedBeforeCapture = EditorApplication.isPaused;
            editorPausedByCapture = !editorWasPausedBeforeCapture;
            if (editorPausedByCapture)
            {
                EditorApplication.isPaused = true;
            }

            Repaint();
        }

        private void ResumeEditorAfterCapture()
        {
            if (editorPausedByCapture && !editorWasPausedBeforeCapture && EditorApplication.isPaused)
            {
                EditorApplication.isPaused = false;
            }

            editorPausedByCapture = false;
            editorWasPausedBeforeCapture = false;
            Repaint();
        }

        private static bool SaveCapturedOffset(
            FishMeshHolderOffsetCapture capture,
            FishData fishData)
        {
            if (fishData == null || !AssetDatabase.Contains(fishData))
            {
                EditorUtility.DisplayDialog(
                    "Cannot Save Fish Data",
                    "The active FishData is not a project asset, so its offset cannot be saved persistently.",
                    "OK");
                return false;
            }

            if (!capture.TryGetCaptureDelta(out Vector3 holderLocalDelta, out string message))
            {
                EditorUtility.DisplayDialog("Cannot Save Offset", message, "OK");
                return false;
            }

            FishJoint fishJoint = FindActiveFishJoint(capture.transform);
            if (fishJoint == null)
            {
                EditorUtility.DisplayDialog(
                    "Cannot Update Fish Joint",
                    "The active FishJoint for this fish could not be found. Cancel and start capture again.",
                    "OK");
                return false;
            }

            Undo.RecordObject(fishData, "Save Fish Holder Offset");
            fishData.holderOffset += holderLocalDelta;
            EditorUtility.SetDirty(fishData);
            AssetDatabase.SaveAssetIfDirty(fishData);

            capture.FinishCapture(true);
            fishJoint.RefreshFishDataHolderOffset();
            fishJoint.SnapFishToHolderOffset();
            Debug.Log($"Saved holder offset for '{fishData.name}': {fishData.holderOffset}.", fishData);
            return true;
        }

        private static FishJoint FindActiveFishJoint(Transform fishMesh)
        {
            FishJoint[] fishJoints = Object.FindObjectsOfType<FishJoint>();
            foreach (FishJoint fishJoint in fishJoints)
            {
                if (fishJoint == null || !fishJoint.HasActiveFish || !fishJoint.IsAttachedToHolder ||
                    fishJoint.ActiveFishData == null || fishJoint.joint == null || fishJoint.joint.connectedBody == null)
                {
                    continue;
                }

                Transform fishRoot = fishJoint.joint.connectedBody.transform;
                if (fishMesh == fishRoot || fishMesh.IsChildOf(fishRoot))
                {
                    return fishJoint;
                }
            }

            return null;
        }
    }
}
#endif
