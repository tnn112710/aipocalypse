using UnityEngine;

namespace SunfishCreations
{
    [AddComponentMenu("Sunfish Creations/Fishing/Fish Mesh Holder Offset Capture")]
    [DisallowMultipleComponent]
    public class FishMeshHolderOffsetCapture : MonoBehaviour
    {
        [System.NonSerialized] private bool captureStarted;
        [System.NonSerialized] private Vector3 originalLocalPosition;
        [System.NonSerialized] private FishJoint capturedFishJoint;
        [System.NonSerialized] private FishData capturedFishData;
        [System.NonSerialized] private Transform capturedHolder;
        [System.NonSerialized] private Transform capturedMeshParent;

        public bool CaptureStarted => captureStarted;
        public FishData CapturedFishData => capturedFishData;

        /// <summary>
        /// Starts a holder-offset capture for this mesh on the active fish attached to its holder.
        /// </summary>
        public bool TryBeginCapture(out string message)
        {
            if (!Application.isPlaying)
            {
                message = "Enter Play Mode and present this fish before starting a capture.";
                return false;
            }

            FishJoint fishJoint = FindAttachedFishJoint();
            if (fishJoint == null)
            {
                message = "No active FishJoint was found for this fish. Present it in hand before capturing.";
                return false;
            }

            capturedFishJoint = fishJoint;
            capturedFishData = fishJoint.ActiveFishData;
            capturedHolder = fishJoint.HolderTransform;
            capturedMeshParent = transform.parent;
            originalLocalPosition = transform.localPosition;
            captureStarted = true;
            message = string.Empty;
            return true;
        }

        /// <summary>
        /// Gets the mesh movement since capture began, expressed in Fish Holder local space.
        /// Computed from a single current-frame read of the mesh's local-position change converted
        /// through the Holder's CURRENT orientation only (TransformVector/InverseTransformVector),
        /// so an animated Holder rotating between capture start and now cannot leak into the delta.
        /// </summary>
        public bool TryGetCaptureDelta(out Vector3 holderLocalDelta, out string message)
        {
            holderLocalDelta = Vector3.zero;
            if (!IsCaptureContextValid(out message))
            {
                return false;
            }

            Vector3 localDelta = transform.localPosition - originalLocalPosition;
            Vector3 worldDelta = capturedMeshParent.TransformVector(localDelta);
            holderLocalDelta = capturedHolder.InverseTransformVector(worldDelta);
            return true;
        }

        /// <summary>
        /// Restores the mesh's original local position and clears the active capture state.
        /// </summary>
        public void FinishCapture(bool restoreOriginalPosition)
        {
            if (restoreOriginalPosition && this != null)
            {
                transform.localPosition = originalLocalPosition;
            }

            ClearCaptureState();
        }

        private bool IsCaptureContextValid(out string message)
        {
            if (!captureStarted || capturedFishJoint == null || capturedFishData == null || capturedHolder == null ||
                capturedFishJoint.ActiveFishData != capturedFishData || !capturedFishJoint.HasActiveFish ||
                !capturedFishJoint.IsAttachedToHolder || capturedFishJoint.HolderTransform != capturedHolder)
            {
                message = "The fish or its holder changed during capture. Cancel and start again.";
                return false;
            }

            if (capturedMeshParent == null || transform.parent != capturedMeshParent)
            {
                message = "This mesh was reparented during capture. Cancel and start again.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private FishJoint FindAttachedFishJoint()
        {
            FishJoint[] fishJoints = FindObjectsOfType<FishJoint>();
            foreach (FishJoint fishJoint in fishJoints)
            {
                if (fishJoint == null || !fishJoint.HasActiveFish || !fishJoint.IsAttachedToHolder ||
                    fishJoint.ActiveFishData == null || fishJoint.HolderTransform == null ||
                    fishJoint.joint == null || fishJoint.joint.connectedBody == null)
                {
                    continue;
                }

                Transform fishRoot = fishJoint.joint.connectedBody.transform;
                if (transform == fishRoot || transform.IsChildOf(fishRoot))
                {
                    return fishJoint;
                }
            }

            return null;
        }

        private void ClearCaptureState()
        {
            captureStarted = false;
            originalLocalPosition = Vector3.zero;
            capturedFishJoint = null;
            capturedFishData = null;
            capturedHolder = null;
            capturedMeshParent = null;
        }
    }
}
