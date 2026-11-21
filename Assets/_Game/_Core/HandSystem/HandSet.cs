using UnityEngine;

namespace MB13.Aipocalypse.Core.HandSystem
{
    // The pair of hand prefabs the player shows, and where the (invisible) arms come from. Swap the asset (or its
    // prefabs) to change hand meshes everywhere; item grips and poses don't change.
    [CreateAssetMenu(menuName = "Aipocalypse/Hands/Hand Set", fileName = "HandSet")]
    public sealed class HandSet : ScriptableObject
    {
        [SerializeField] HandRig _left;
        [SerializeField] HandRig _right;

        [Header("Implied arms (view space: x right, y up, z forward, from the eye)")]
        [SerializeField, Tooltip("Hands on grips turn so their wrist points here, as if a forearm led to this elbow")]
        Vector3 _leftElbow = new(-0.22f, -0.55f, 0.35f);
        [SerializeField] Vector3 _rightElbow = new(0.32f, -0.55f, 0.15f);
        [SerializeField, Tooltip("Hands with an arm (ArmRig): where each arm starts, off screen. A hand out of reach pulls it along")]
        Vector3 _leftShoulder = new(-0.2f, -0.4f, 0.1f);
        [SerializeField] Vector3 _rightShoulder = new(0.3f, -0.4f, -0.05f);
        [SerializeField, Tooltip("The most a shoulder carried with the item's pose (ItemHands.Carry) moves towards the screen centre, up and forward (m). " +
            "A pose may swing the gun far (sprint), the body stays where it is")]
        Vector3 _shoulderCarry = new(0.1f, 0.05f, 0.1f);
        [SerializeField, Tooltip("A grip's turn contacts (HandGrip > Turns) count fully within x of the grip and not at all past y (m)")]
        Vector2 _turnFade = new(0.02f, 0.08f);
        [SerializeField, Tooltip("Edit-mode previews only: where an item's root sits in view space (the player's Hand point)")]
        Vector3 _previewItemPosition = new(0.17f, -0.21f, 0.62f);

        public HandRig Get(HandSide side) => side == HandSide.Left ? _left : _right;
        public Vector3 Elbow(HandSide side) => side == HandSide.Left ? _leftElbow : _rightElbow;
        public Vector3 Shoulder(HandSide side) => side == HandSide.Left ? _leftShoulder : _rightShoulder;
        public Vector3 PreviewItemPosition => _previewItemPosition;
        public Vector2 TurnFade => _turnFade;

        // A carried shoulder (view space) held near the body: no further towards the centre, up or forward than Shoulder Carry.
        public Vector3 LimitShoulder(HandSide side, Vector3 carried)
        {
            Vector3 rest = Shoulder(side);
            float inward = side == HandSide.Left ? 1f : -1f;
            carried.x = rest.x + inward * Mathf.Min((carried.x - rest.x) * inward, _shoulderCarry.x);
            carried.y = Mathf.Min(carried.y, rest.y + _shoulderCarry.y);
            carried.z = Mathf.Min(carried.z, rest.z + _shoulderCarry.z);
            return carried;
        }
    }
}
