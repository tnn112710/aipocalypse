using UnityEngine;

namespace MB13.Aipocalypse.Core.HandSystem
{
    // The arm behind a first-person hand: a two-bone solve from a shoulder point to the hand pose HandRig asks for.
    // No Animator or RigBuilder: HandRig.Place calls Solve after the item's final pose, so the arm is never a frame late.
    [DisallowMultipleComponent]
    public sealed class ArmRig : MonoBehaviour
    {
        [SerializeField, Tooltip("Optional: the bone above the upper arm (clavicle). It moves so the upper arm starts at the shoulder point, carrying the shoulder's skin")]
        Transform _carrier;
        [SerializeField] Transform _upper;
        [SerializeField] Transform _lower;
        [SerializeField] Transform _hand;
        [SerializeField, Tooltip("Optional: takes part of the wrist's turn round the forearm so the sleeve doesn't pinch")]
        Transform _lowerTwist;
        [SerializeField, Range(0f, 1f)] float _twistShare = 0.5f;
        [SerializeField, Tooltip("Elbow hinge in the upper arm's space, signed so a positive turn bends the arm")]
        Vector3 _upperHinge = Vector3.forward;
        [SerializeField, Tooltip("The same hinge in the forearm's space")] Vector3 _lowerHinge = Vector3.forward;
        [SerializeField, Range(0.8f, 1f), Tooltip("Longest reach as a share of the arm's length; past it the shoulder follows the hand")]
        float _maxReach = 0.97f;
        [SerializeField, Range(0f, 1f), Tooltip("How far the elbow swings from the elbow point to behind the hand, where the wrist is straight. 0 = always towards the elbow point")]
        float _wristShare = 0.4f;
        [SerializeField, HideInInspector] Quaternion _handRest = Quaternion.identity;
        [SerializeField, HideInInspector] Quaternion _twistRest = Quaternion.identity;

        public Transform Hand => _hand;

        // Puts the hand bone at this pose. The elbow bends towards `elbow`; a hand out of reach pulls the shoulder along.
        public void Solve(Vector3 shoulder, Vector3 elbow, Vector3 handPosition, Quaternion handRotation)
        {
            if (_upper == null || _lower == null || _hand == null)
                return;

            float upperLength = Vector3.Distance(_upper.position, _lower.position);
            float lowerLength = Vector3.Distance(_lower.position, _hand.position);
            Vector3 reach = handPosition - shoulder;
            float distance = reach.magnitude;
            if (distance < 1e-5f)
                return;
            Vector3 direction = reach / distance;
            float longest = (upperLength + lowerLength) * _maxReach;
            if (distance > longest)
            {
                shoulder = handPosition - direction * longest;
                distance = longest;
            }
            distance = Mathf.Max(distance, Mathf.Abs(upperLength - lowerLength) + 1e-4f);

            // Elbow: on the circle both bones can reach, on the side the hint is.
            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(upperLength * upperLength - along * along, 0f));
            Vector3 side = Vector3.ProjectOnPlane(elbow - shoulder, direction);
            side = side.sqrMagnitude > 1e-8f ? side.normalized : Vector3.ProjectOnPlane(Vector3.down, direction).normalized;
            if (_wristShare > 0f && height > 1e-4f)
            {
                // The elbow a straight wrist asks for; it counts less the nearer it sits to the shoulder-hand line.
                Vector3 forearm = handRotation * Quaternion.Inverse(_handRest) * _hand.localPosition.normalized;
                Vector3 relaxed = Vector3.ProjectOnPlane(handPosition - forearm * lowerLength - shoulder, direction) / height;
                Vector3 leaning = Vector3.Lerp(side, Vector3.ClampMagnitude(relaxed, 1f), _wristShare);
                if (leaning.sqrMagnitude > 1e-6f)
                    side = leaning.normalized;
            }
            Vector3 elbowPosition = shoulder + direction * along + side * height;
            Vector3 hinge = Vector3.Cross(side, direction);

            if (_carrier != null)
                _carrier.position += shoulder - _upper.position;
            else
                _upper.position = shoulder;
            _upper.rotation = Aim(_lower.localPosition, _upperHinge, elbowPosition - shoulder, hinge);
            _lower.rotation = Aim(_hand.localPosition, _lowerHinge, handPosition - elbowPosition, hinge);
            _hand.rotation = handRotation;

            if (_lowerTwist != null)
            {
                Quaternion turned = _hand.localRotation * Quaternion.Inverse(_handRest);
                _lowerTwist.localRotation = Quaternion.Slerp(Quaternion.identity, Twist(turned, _hand.localPosition.normalized), _twistShare) * _twistRest;
            }
        }

        // The rotation that points a bone's own `along` axis down `direction` with its hinge on `hinge`.
        static Quaternion Aim(Vector3 along, Vector3 localHinge, Vector3 direction, Vector3 hinge)
        {
            Vector3 localUp = Vector3.ProjectOnPlane(localHinge, along);
            return Quaternion.LookRotation(direction, hinge) * Quaternion.Inverse(Quaternion.LookRotation(along, localUp));
        }

        // The part of a rotation that turns round `axis`.
        static Quaternion Twist(Quaternion rotation, Vector3 axis)
        {
            Vector3 projected = Vector3.Project(new Vector3(rotation.x, rotation.y, rotation.z), axis);
            var twist = new Quaternion(projected.x, projected.y, projected.z, rotation.w);
            float magnitude = Mathf.Sqrt(Quaternion.Dot(twist, twist));
            if (magnitude < 1e-6f)
                return Quaternion.identity;
            twist = new Quaternion(twist.x / magnitude, twist.y / magnitude, twist.z / magnitude, twist.w / magnitude);
            return twist.w < 0f ? new Quaternion(-twist.x, -twist.y, -twist.z, -twist.w) : twist;
        }

#if UNITY_EDITOR
        // Builder hook: the bones, with the hinge read off the rest pose (arms are modelled slightly bent).
        public void Setup(Transform carrier, Transform upper, Transform lower, Transform hand, Transform lowerTwist, Vector3 bendToward)
        {
            _carrier = carrier;
            _upper = upper;
            _lower = lower;
            _hand = hand;
            _lowerTwist = lowerTwist;
            Vector3 upperAxis = (lower.position - upper.position).normalized;
            Vector3 side = Vector3.ProjectOnPlane(bendToward, upperAxis).normalized;
            Vector3 hinge = Vector3.Cross(side, upperAxis);
            _upperHinge = upper.InverseTransformDirection(hinge);
            _lowerHinge = lower.InverseTransformDirection(hinge);
            _handRest = hand.localRotation;
            _twistRest = lowerTwist != null ? lowerTwist.localRotation : Quaternion.identity;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
