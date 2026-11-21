using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.HandSystem
{
    public enum GripAim
    {
        // Turned round the held part so the wrist points at the implied elbow (HandSet): no hand-tuned roll needed.
        TowardElbow,
        // Exactly this transform's rotation.
        AsAuthored,
    }

    // A place on the item where the hand may turn on its grip to meet the arm (ArmRig): round the part, and flat on the palm.
    [Serializable]
    public struct GripTurn
    {
        [Tooltip("The contact (forend hold, bolt handle). The turn fades as the grip moves away from it (HandSet > Turn Fade)")]
        public Transform Contact;
        [Min(0f), Tooltip("From the contact to the middle of the held part (m): the hand rolls round that line, so the fingers stay wrapped. 0 for a pinch")]
        public float Radius;
        [Tooltip("The most the hand turns there (degrees): x round the part (grip Z), y flat on the palm (grip Y)")]
        public Vector2 Max;
    }

    // Where a hand holds an item, with its finger pose; parent it under the held part (mag, bolt) so the hand follows it.
    // Blue (Z) runs little finger → index along the part, green (Y) out of the back of the hand (only Z + Roll with TowardElbow).
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
        [SerializeField, Tooltip("Where the hand may turn to meet its arm: a hold made for other arms then doesn't fold the wrist. Empty = as held")]
        GripTurn[] _turns = Array.Empty<GripTurn>();

        [FoldoutGroup("Fitted"), SerializeField, ReadOnly] bool _fitted;
        [FoldoutGroup("Fitted"), SerializeField, ReadOnly] HandShape _fittedShape;
        [FoldoutGroup("Fitted"), SerializeField, ReadOnly, Tooltip("Palm moved along the back-of-hand axis (m)")] float _palmOffset;

        public HandSide Side => _side;
        public HandShape PoseShape => _pose != null ? _pose.Shape : default;
        public HandShape Shape => _animateShape ? _animatedShape : _fitted ? _fittedShape : PoseShape;
        public bool FitsPalm => _fitPalm;
        public FingerMask KeepFingers => _keepFingers;

        public void SetAnimatedShape(HandShape shape) => _animatedShape = shape;

        // The turn allowed where the grip is now: the nearest contact's, its limits faded by the grip's distance from it.
        public void GetTurn(Vector2 fade, out float radius, out Vector2 max)
        {
            radius = 0f;
            max = Vector2.zero;
            float best = 0f;
            foreach (GripTurn turn in _turns)
            {
                if (turn.Contact == null)
                    continue;
                float weight = 1f - Mathf.InverseLerp(fade.x, fade.y, Vector3.Distance(transform.position, turn.Contact.position));
                if (weight <= best)
                    continue;
                best = weight;
                radius = turn.Radius;
                max = turn.Max * weight;
            }
        }

#if UNITY_EDITOR
        public void SetTurns(GripTurn[] turns)
        {
            _turns = turns;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

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
