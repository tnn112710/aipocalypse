using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.HandSystem
{
    // Root of a first-person hand prefab: its grip point and how its finger bones bend. Works with any skinned hand: fill the
    // joints (Auto-find), place GripPoint, then capture the Open, Closed, Spread and Fist shapes from bones bent by hand.
    [DisallowMultipleComponent]
    public sealed class HandRig : MonoBehaviour
    {
        [Serializable]
        sealed class FingerChain
        {
            [Tooltip("Base to tip, without end/leaf bones")] public Transform[] Joints = Array.Empty<Transform>();
            [HideInInspector] public Quaternion[] Open = Array.Empty<Quaternion>();
            [HideInInspector] public Quaternion[] Closed = Array.Empty<Quaternion>();
            [HideInInspector] public Quaternion[] Spread = Array.Empty<Quaternion>();
            [HideInInspector] public Quaternion[] Fist = Array.Empty<Quaternion>();

            public bool IsCalibrated => Joints.Length > 0 && Open.Length == Joints.Length && Closed.Length == Joints.Length &&
                                        Spread.Length == Joints.Length;
        }

        [SerializeField] HandSide _side;
        [SerializeField, Tooltip("Lined up with HandGrips. Sits in the palm: Z along the held object (little finger → index side), Y out of the back of the hand")]
        Transform _gripPoint;
        [SerializeField, ListDrawerSettings(ShowFoldout = false), Tooltip("Thumb, Index, Middle, Ring, Little")]
        FingerChain[] _fingers = NewChains();
        [SerializeField, Tooltip("Hidden while the hand holds nothing")] Renderer[] _renderers = Array.Empty<Renderer>();
        [SerializeField, Min(0f), Tooltip("Finger half-thickness: how close fingers get to an item when grips are fitted (Bake fit)")]
        float _fingerRadius = 0.009f;
        [SerializeField, Min(0f), Tooltip("Bone to palm skin: how close the palm gets to an item when grips are fitted")]
        float _palmThickness = 0.014f;
        [SerializeField, Tooltip("Optional: the arm this hand is the end of. With one, the root stays put and the arm reaches for the grip")]
        ArmRig _arm;

        bool _visible = true;

        public HandSide Side => _side;
        public Transform GripPoint => _gripPoint != null ? _gripPoint : transform;

        public bool HasArm => _arm != null;

        // Puts the grip point at this pose: the arm reaches for it from the shoulder, bending towards the elbow point.
        // A hand without an arm just moves there.
        public void Place(Vector3 position, Quaternion rotation, Vector3 shoulder, Vector3 elbow)
        {
            if (_arm == null)
            {
                Place(position, rotation);
                return;
            }
            Transform grip = GripPoint;
            Transform hand = _arm.Hand;
            Quaternion inverseHand = Quaternion.Inverse(hand.rotation);
            Quaternion handRotation = rotation * Quaternion.Inverse(inverseHand * grip.rotation);
            _arm.Solve(shoulder, elbow, position - handRotation * (inverseHand * (grip.position - hand.position)), handRotation);
        }

        // Moves the whole hand so its grip point sits at this pose.
        public void Place(Vector3 position, Quaternion rotation)
        {
            Transform grip = GripPoint;
            Quaternion inverseRoot = Quaternion.Inverse(transform.rotation);
            Quaternion gripRotation = inverseRoot * grip.rotation;
            Vector3 gripPosition = inverseRoot * (grip.position - transform.position);

            Quaternion rootRotation = rotation * Quaternion.Inverse(gripRotation);
            transform.SetPositionAndRotation(position - rootRotation * gripPosition, rootRotation);
        }

        public void Apply(in HandShape shape)
        {
            for (int f = 0; f < _fingers.Length && f < HandShape.FingerCount; f++)
                ApplyFinger(f, shape[f]);
        }

        public void ApplyFinger(int finger, FingerShape shape)
        {
            FingerChain chain = finger < _fingers.Length ? _fingers[finger] : null;
            if (chain == null || !chain.IsCalibrated)
                return;
            for (int j = 0; j < chain.Joints.Length; j++)
            {
                Transform joint = chain.Joints[j];
                if (joint == null)
                    continue;
                Quaternion open = chain.Open[j];
                float curl = Mathf.Clamp(shape.JointCurl(j), 0f, FingerShape.MaxCurl);
                float fist = Mathf.InverseLerp(1f, FingerShape.MaxCurl, curl);
                Quaternion curled = curl <= 1f
                    ? Quaternion.Slerp(open, chain.Closed[j], curl)
                    : Quaternion.Slerp(chain.Closed[j], chain.Fist.Length == chain.Joints.Length ? chain.Fist[j] : chain.Closed[j], fist);
                Quaternion spread = Quaternion.Inverse(open) * chain.Spread[j];
                joint.localRotation = curled * Quaternion.SlerpUnclamped(Quaternion.identity, spread, shape.Spread * (1f - fist));
            }
        }

        // The hand's long axis pointing back to where the forearm would be (middle knuckle → wrist), in grip-point space.
        public Vector3 ForearmInGrip
        {
            get
            {
                Transform[] middle = _fingers.Length > (int)Finger.Middle ? _fingers[(int)Finger.Middle].Joints : Array.Empty<Transform>();
                if (middle.Length == 0 || middle[0] == null || middle[0].parent == null)
                    return Vector3.back;
                Vector3 knuckle = (middle.Length > 1 && middle[1] != null ? middle[1] : middle[0]).position;
                Vector3 wrist = middle[0].parent.position;
                Transform grip = GripPoint;
                return Quaternion.Inverse(grip.rotation) * (wrist - knuckle).normalized;
            }
        }

        public float FingerRadius => _fingerRadius;

        // Points along a finger's phalanges (metacarpal skipped) and its tip, for contact tests.
        public void GetFingerSamples(int finger, System.Collections.Generic.List<Vector3> samples)
        {
            samples.Clear();
            Transform[] joints = finger < _fingers.Length ? _fingers[finger].Joints : Array.Empty<Transform>();
            for (int j = 1; j < joints.Length; j++)
            {
                Vector3 from = joints[j].position;
                Vector3 to = j + 1 < joints.Length ? joints[j + 1].position : Tip(joints[j], joints[j - 1]);
                samples.Add(from);
                samples.Add((from + to) * 0.5f);
                if (j + 1 == joints.Length)
                    samples.Add(to);
            }
        }

        // Points on the palm skin (knuckles and mid palm, pushed towards the palm), for contact tests.
        public void GetPalmSamples(Vector3 palmNormal, System.Collections.Generic.List<Vector3> samples)
        {
            samples.Clear();
            for (int f = (int)Finger.Index; f < _fingers.Length; f++)
            {
                Transform[] joints = _fingers[f].Joints;
                if (joints.Length < 2)
                    continue;
                samples.Add(joints[1].position + palmNormal * _palmThickness);
                samples.Add(Vector3.Lerp(joints[0].position, joints[1].position, 0.6f) + palmNormal * _palmThickness);
            }
        }

        // A leaf bone's end, or the joint extended by its parent segment.
        static Vector3 Tip(Transform last, Transform before)
        {
            if (last.childCount > 0)
                return last.GetChild(0).position;
            return last.position + (last.position - before.position) * 0.8f;
        }

        public void SetVisible(bool visible)
        {
            if (_visible == visible)
                return;
            _visible = visible;
            foreach (Renderer renderer in _renderers)
            {
                if (renderer != null)
                    renderer.enabled = visible;
            }
        }

        static FingerChain[] NewChains()
        {
            var chains = new FingerChain[HandShape.FingerCount];
            for (int i = 0; i < chains.Length; i++)
                chains[i] = new FingerChain();
            return chains;
        }

#if UNITY_EDITOR
        [FoldoutGroup("Setup"), ShowInInspector, NonSerialized, OnValueChanged(nameof(PreviewShape), true), Tooltip("Try weights on this rig (edit mode)")]
        HandShape _preview;

        // Finger chains under the bone that has the most finger-named children; joints are the non-leaf bones of each chain.
        [FoldoutGroup("Setup"), Button("Auto-find finger bones")]
        void AutoFind()
        {
            Transform hand = null;
            int best = 0;
            foreach (Transform candidate in GetComponentsInChildren<Transform>(true))
            {
                int count = 0;
                foreach (Transform child in candidate)
                {
                    if (Classify(child.name) >= 0)
                        count++;
                }
                if (count > best)
                {
                    best = count;
                    hand = candidate;
                }
            }
            if (hand == null || best < 3)
            {
                Debug.LogWarning($"{name}: no bone with finger-named children (thumb, index, middle, ring, pinky/little).", this);
                return;
            }

            UnityEditor.Undo.RecordObject(this, "Auto-find finger bones");
            _fingers = NewChains();
            foreach (Transform root in hand)
            {
                int finger = Classify(root.name);
                if (finger < 0)
                    continue;
                var joints = new System.Collections.Generic.List<Transform>();
                for (Transform joint = root; joint != null && joint.childCount > 0; joint = joint.GetChild(0))
                    joints.Add(joint);
                _fingers[finger].Joints = joints.ToArray();
            }
            if (_renderers.Length == 0)
                _renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Capture(Shape.Open);
            Capture(Shape.Closed);
            Capture(Shape.Spread);
            Debug.Log($"{name}: found fingers under {hand.name}. Open, Closed and Spread are all the current bones: bend them and capture each.", this);
        }

        // Middle is tested last: rigs often name inner joints "MiddleIndex" etc.
        static int Classify(string boneName)
        {
            string lower = boneName.ToLowerInvariant();
            if (lower.Contains("thumb")) return (int)Finger.Thumb;
            if (lower.Contains("index")) return (int)Finger.Index;
            if (lower.Contains("ring")) return (int)Finger.Ring;
            if (lower.Contains("pink") || lower.Contains("little")) return (int)Finger.Little;
            if (lower.Contains("middle")) return (int)Finger.Middle;
            return -1;
        }

        enum Shape { Open, Closed, Spread, Fist }

        [ButtonGroup("Setup/Capture"), Button("Capture Open")] void CaptureOpen() => Capture(Shape.Open);
        [ButtonGroup("Setup/Capture"), Button("Capture Closed")] void CaptureClosed() => Capture(Shape.Closed);
        [ButtonGroup("Setup/Capture"), Button("Capture Spread")] void CaptureSpread() => Capture(Shape.Spread);
        [ButtonGroup("Setup/Capture"), Button("Capture Fist")] void CaptureFist() => Capture(Shape.Fist);

        [ButtonGroup("Setup/Show"), Button("Show Open")] void ShowOpen() => ShowShape(default);
        [ButtonGroup("Setup/Show"), Button("Show Closed")] void ShowClosed() => ShowShape(Uniform(1f, 0f));
        [ButtonGroup("Setup/Show"), Button("Show Spread")] void ShowSpread() => ShowShape(Uniform(0f, 1f));
        [ButtonGroup("Setup/Show"), Button("Show Fist")] void ShowFist() => ShowShape(Uniform(FingerShape.MaxCurl, 0f));

        static HandShape Uniform(float curl, float spread) => new()
        {
            Thumb = new FingerShape(curl, spread),
            Index = new FingerShape(curl, spread),
            Middle = new FingerShape(curl, spread),
            Ring = new FingerShape(curl, spread),
            Little = new FingerShape(curl, spread),
        };

        void Capture(Shape shape)
        {
            UnityEditor.Undo.RecordObject(this, $"Capture {shape}");
            foreach (FingerChain chain in _fingers)
            {
                var rotations = new Quaternion[chain.Joints.Length];
                for (int j = 0; j < rotations.Length; j++)
                    rotations[j] = chain.Joints[j] != null ? chain.Joints[j].localRotation : Quaternion.identity;
                switch (shape)
                {
                    case Shape.Open: chain.Open = rotations; break;
                    case Shape.Closed: chain.Closed = rotations; break;
                    case Shape.Fist: chain.Fist = rotations; break;
                    default: chain.Spread = rotations; break;
                }
            }
            UnityEditor.EditorUtility.SetDirty(this);
        }

        void PreviewShape() => ShowShape(_preview);

        void ShowShape(HandShape shape)
        {
            foreach (FingerChain chain in _fingers)
            {
                foreach (Transform joint in chain.Joints)
                {
                    if (joint != null)
                        UnityEditor.Undo.RecordObject(joint, "Show hand shape");
                }
            }
            Apply(shape);
        }

        void OnDrawGizmosSelected()
        {
            Transform grip = GripPoint;
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(grip.position, grip.position + grip.forward * 0.05f);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(grip.position, grip.position + grip.up * 0.03f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(grip.position, 0.008f);
        }
#endif
    }
}
