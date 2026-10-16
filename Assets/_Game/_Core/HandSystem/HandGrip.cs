using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.HandSystem
{
    public enum GripAim
    {
        // Turned round the held part so the wrist points at the implied elbow (HandSet): no hand-tuned roll needed.
        TowardElbow,
        // Exactly this transform's rotation.
        AsAuthored,
    }

    // A place on an item where a hand holds it, with the finger pose there. Parent it under the part it holds
    // (mag, bolt, forend) so the hand follows that part.
    // Blue (Z) runs along the held part from the little finger to the index; green (Y) is out of the back of the hand
    // (with TowardElbow only Z matters, plus Roll). Bake fit wraps the fingers and palm onto the item's mesh.
    public sealed class HandGrip : MonoBehaviour
    {
        [SerializeField] HandSide _side = HandSide.Right;
        [SerializeField, Tooltip("Finger pose here; none = open hand. Bake fit adjusts the curls to the item")] HandPose _pose;
        [SerializeField, Tooltip("Use a shape keyed by weapon animation clips instead of the fitted pose")] bool _animateShape;
        [SerializeField] HandShape _animatedShape;
        [SerializeField] GripAim _aim = GripAim.TowardElbow;
        [SerializeField, Range(-180f, 180f), Tooltip("Extra turn round the held part (Z), after aiming")] float _roll;
        [SerializeField, Tooltip("Bake fit moves the palm onto the item's surface. Off for pinches (bolt, shell)")] bool _fitPalm = true;
        [SerializeField, Tooltip("Bake fit leaves these fingers at the pose's curl (trigger finger off the trigger…)")] FingerMask _keepFingers;

        [FoldoutGroup("Fitted"), SerializeField, ReadOnly] bool _fitted;
        [FoldoutGroup("Fitted"), SerializeField, ReadOnly] HandShape _fittedShape;
        [FoldoutGroup("Fitted"), SerializeField, ReadOnly, Tooltip("Palm moved along the back-of-hand axis (m)")] float _palmOffset;

        public HandSide Side => _side;
        public HandShape PoseShape => _pose != null ? _pose.Shape : default;
        public HandShape Shape => _animateShape ? _animatedShape : _fitted ? _fittedShape : PoseShape;
        public bool FitsPalm => _fitPalm;
        public FingerMask KeepFingers => _keepFingers;

        public void SetAnimatedShape(HandShape shape) => _animatedShape = shape;

        // Where the rig's grip point goes, given the elbow this hand's forearm leads to.
        public void Resolve(HandRig rig, Vector3 elbow, out Vector3 position, out Quaternion rotation) =>
            Resolve(rig, elbow, _palmOffset, out position, out rotation);

        public void Resolve(HandRig rig, Vector3 elbow, float palmOffset, out Vector3 position, out Quaternion rotation)
        {
            Transform t = transform;
            Vector3 axis = t.forward;
            rotation = t.rotation;
            if (_aim == GripAim.TowardElbow && rig != null)
            {
                Vector3 forearm = Vector3.ProjectOnPlane(rotation * rig.ForearmInGrip, axis);
                Vector3 toElbow = Vector3.ProjectOnPlane(elbow - t.position, axis);
                if (forearm.sqrMagnitude > 1e-8f && toElbow.sqrMagnitude > 1e-8f)
                    rotation = Quaternion.AngleAxis(Vector3.SignedAngle(forearm, toElbow, axis), axis) * rotation;
            }
            rotation = Quaternion.AngleAxis(_roll, axis) * rotation;
            position = t.position + rotation * Vector3.up * palmOffset;
        }

        public void SetFit(HandShape shape, float palmOffset)
        {
            _fitted = true;
            _fittedShape = shape;
            _palmOffset = palmOffset;
        }

        public void ClearFit()
        {
            _fitted = false;
            _fittedShape = default;
            _palmOffset = 0f;
        }

#if UNITY_EDITOR
        [ShowInInspector, LabelText("Preview hand"), PropertyOrder(-1)]
        bool Previewing
        {
            get => HandPreview.IsShowing(this);
            set
            {
                HandPreview.Hide(this);
                if (value)
                    HandPreview.Show(this, _side, () => this);
            }
        }

        [ButtonGroup("Fit"), Button("Bake fit")]
        void BakeFit()
        {
            UnityEditor.Undo.RecordObject(this, "Bake hand fit");
            HandFit.Bake(this);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        [ButtonGroup("Fit"), Button("Clear fit")]
        void ClearFitButton()
        {
            UnityEditor.Undo.RecordObject(this, "Clear hand fit");
            ClearFit();
            UnityEditor.EditorUtility.SetDirty(this);
        }

        // A changed side needs the other hand; objects can't be destroyed inside OnValidate.
        void OnValidate() => UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && Previewing)
                Previewing = true;
        };

        void OnDrawGizmos()
        {
            Gizmos.color = _side == HandSide.Left ? new Color(0.3f, 0.7f, 1f) : new Color(1f, 0.6f, 0.2f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.025f, 0.04f, 0.08f));
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * 0.06f);
        }
#endif
    }

    [System.Flags]
    public enum FingerMask
    {
        None = 0,
        Thumb = 1 << (int)Finger.Thumb,
        Index = 1 << (int)Finger.Index,
        Middle = 1 << (int)Finger.Middle,
        Ring = 1 << (int)Finger.Ring,
        Little = 1 << (int)Finger.Little,
    }
}
