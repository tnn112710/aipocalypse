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
        float _wristShare = 0.8f;
        [SerializeField, Range(0f, 1f), Tooltip("How far the shoulder gives way towards where a straight wrist would hang the arm from. 0 = the shoulder point never moves")]
        float _shoulderGive = 0.7f;
        [SerializeField, Min(0f), Tooltip("The farthest the shoulder gives way (m)")]
        float _maxGive = 0.15f;
        [SerializeField, Tooltip("The most it gives way upwards and forwards in the viewer's frame (m): more brings the upper arm into view")]
        Vector2 _giveIntoView = new(0.02f, 0.02f);
        [SerializeField, Tooltip("The most the wrist share lifts the elbow upwards and forwards from where the elbow point puts it (m)")]
        Vector2 _elbowIntoView = new(0.08f, 0.08f);
        [SerializeField, Min(0f), Tooltip("The most the wrist share swings the elbow in towards the middle of the body (m): more folds the arm across the chest")]
        float _elbowInward = 0.06f;
        [SerializeField, HideInInspector] Quaternion _handRest = Quaternion.identity;
        [SerializeField, HideInInspector] Quaternion _twistRest = Quaternion.identity;

        public Transform Hand => _hand;

        // Puts the hand bone at this pose. The elbow bends towards `elbow`; a hand out of reach pulls the shoulder along.
        public void Solve(Vector3 shoulder, Vector3 elbow, Vector3 handPosition, Quaternion handRotation) =>
            Solve(shoulder, elbow, handPosition, handRotation, Quaternion.identity);

        // `grip` is the pose of the point the hand holds by (HandRig's grip point). The hand may turn on it by up to `maxTurn`
        // degrees (x round the held part, y flat on the palm), rolling round a line `turnRadius` below the grip point.
        public void Solve(Vector3 shoulder, Vector3 elbow, Vector3 handPosition, Quaternion handRotation, Quaternion frame,
            Vector3 gripPosition, Quaternion grip, float turnRadius, Vector2 maxTurn)
        {
            if (maxTurn != Vector2.zero && _upper != null && _lower != null && _hand != null)
                TurnToArm(shoulder, elbow, gripPosition, grip, turnRadius, maxTurn, ref handPosition, ref handRotation);
            Solve(shoulder, elbow, handPosition, handRotation, frame);
        }

        // `frame` is the viewer's rotation: it tells which way is up and forward for the shoulder's give.
        public void Solve(Vector3 shoulder, Vector3 elbow, Vector3 handPosition, Quaternion handRotation, Quaternion frame)
        {
            if (_upper == null || _lower == null || _hand == null)
                return;

            float upperLength = Vector3.Distance(_upper.position, _lower.position);
            float lowerLength = Vector3.Distance(_lower.position, _hand.position);
            // Where the forearm comes from when the wrist is straight.
            Vector3 forearm = handRotation * Quaternion.Inverse(_handRest) * _hand.localPosition.normalized;
            if (_shoulderGive > 0f)
            {
                // The nearest shoulder that a straight wrist's elbow could hang from.
                Vector3 straightElbow = handPosition - forearm * lowerLength;
                Vector3 fromElbow = shoulder - straightElbow;
                if (fromElbow.sqrMagnitude > 1e-8f)
                {
                    Vector3 give = Vector3.ClampMagnitude((straightElbow + fromElbow.normalized * upperLength - shoulder) * _shoulderGive, _maxGive);
                    give = Quaternion.Inverse(frame) * give;
                    give.y = Mathf.Min(give.y, _giveIntoView.x);
                    give.z = Mathf.Min(give.z, _giveIntoView.y);
                    shoulder += frame * give;
                }
            }
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
                Vector3 relaxed = Vector3.ProjectOnPlane(handPosition - forearm * lowerLength - shoulder, direction) / height;
                Vector3 leaning = Vector3.Lerp(side, Vector3.ClampMagnitude(relaxed, 1f), _wristShare);
                if (leaning.sqrMagnitude > 1e-6f)
                {
                    // As far towards it as keeps the elbow from rising, coming forward into view or folding across the body
                    // (inward is away from the side the elbow point hangs out to).
                    Vector3 hinted = side, wanted = leaning.normalized;
                    Quaternion toFrame = Quaternion.Inverse(frame);
                    float inward = (toFrame * hinted).x > 0f ? -1f : 1f;
                    bool Shows(Vector3 swung)
                    {
                        Vector3 moved = toFrame * ((swung - hinted) * height);
                        return moved.y > _elbowIntoView.x || moved.z > _elbowIntoView.y || moved.x * inward > _elbowInward;
                    }
                    side = wanted;
                    if (Shows(wanted))
                    {
                        float low = 0f, high = 1f;
                        for (int i = 0; i < 6; i++)
                        {
                            float middle = (low + high) * 0.5f;
                            if (Shows(Vector3.Slerp(hinted, wanted, middle)))
                                high = middle;
                            else
                                low = middle;
                        }
                        side = Vector3.Slerp(hinted, wanted, low);
                    }
                }
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

        // Turns the hand round its grip point so a straight wrist's forearm points more towards where the plain two-bone
        // elbow (shoulder and elbow point only) puts it: a hold authored for another arm still meets this one without
        // folding the wrist. Flat on the palm (about the hold's palm normal), then round the held part (about its axis):
        // the palm never tilts off the part. Each within maxTurn.
        void TurnToArm(Vector3 shoulder, Vector3 elbow, Vector3 gripPosition, Quaternion grip, float turnRadius, Vector2 maxTurn,
            ref Vector3 handPosition, ref Quaternion handRotation)
        {
            float upperLength = Vector3.Distance(_upper.position, _lower.position);
            float lowerLength = Vector3.Distance(_lower.position, _hand.position);
            Vector3 palm = grip * Vector3.up, part = grip * Vector3.forward;
            Vector3 middle = gripPosition - palm * turnRadius;
            Vector3 heldOffset = handPosition - gripPosition;
            Quaternion heldRotation = handRotation;
            float yaw = 0f, roll = 0f;
            for (int i = 0; i < 3; i++)
            {
                Vector3 reach = handPosition - shoulder;
                if (reach.sqrMagnitude < 1e-8f)
                    return;
                float distance = Mathf.Clamp(reach.magnitude, Mathf.Abs(upperLength - lowerLength) + 1e-4f, upperLength + lowerLength);
                Vector3 direction = reach.normalized;
                float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
                float height = Mathf.Sqrt(Mathf.Max(upperLength * upperLength - along * along, 0f));
                Vector3 side = Vector3.ProjectOnPlane(elbow - shoulder, direction);
                side = side.sqrMagnitude > 1e-8f ? side.normalized : Vector3.ProjectOnPlane(Vector3.down, direction).normalized;
                Vector3 natural = handPosition - (shoulder + direction * along + side * height);
                Vector3 straight = handRotation * Quaternion.Inverse(_handRest) * _hand.localPosition.normalized;

                // The turn that would line them up, split into its parts about the palm normal and the part's axis.
                Quaternion.FromToRotation(straight, natural).ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f)
                    angle -= 360f;
                Quaternion turned = Quaternion.AngleAxis(roll, part);
                yaw = Mathf.Clamp(yaw + Vector3.Dot(axis, turned * palm) * angle, -maxTurn.y, maxTurn.y);
                roll = Mathf.Clamp(roll + Vector3.Dot(axis, part) * angle, -maxTurn.x, maxTurn.x);

                Quaternion rolled = Quaternion.AngleAxis(roll, part), yawed = Quaternion.AngleAxis(yaw, palm);
                handPosition = middle + rolled * (gripPosition + yawed * heldOffset - middle);
                handRotation = rolled * yawed * heldRotation;
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
