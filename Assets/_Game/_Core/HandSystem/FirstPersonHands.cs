using UnityEngine;

namespace Aipocalypse.Core.HandSystem
{
    // The player's two floating hands. Each frame, after the item in hand has its final pose, puts each hand on the grip
    // the item names (ItemHands), turned so its wrist leads to the implied elbow. When the grip changes the hand travels
    // on an arc that bows towards the camera, opening its fingers on the way. Hidden with no item or no grip.
    // Spawned from a HandSet, on this object's layer (the first-person overlay layer).
    public sealed class FirstPersonHands : MonoBehaviour
    {
        sealed class Hand
        {
            public HandSide Side;
            public HandRig Rig;
            public Vector3 BaseScale;
            public ItemHands Item;
            public HandGrip Grip;
            // Where the move starts, relative to the item, so the hand doesn't lag behind a moving item.
            public Vector3 FromPosition;
            public Quaternion FromRotation;
            public HandShape FromShape;
            public float Blend;
            public float Elapsed;
            public Vector3 Position;
            public Quaternion Rotation;
            public HandShape Shape;
        }

        [SerializeField, Tooltip("Hand prefabs and implied elbows; swap it to change the hand mesh")] HandSet _set;
        [SerializeField, Tooltip("The view the elbows are relative to (camera pitch)")] Transform _view;
        [Header("Moving between grips")]
        [SerializeField, Min(0f), Tooltip("Arc height as a share of the distance travelled")] float _arc = 0.3f;
        [SerializeField, Min(0f), Tooltip("Arc height cap (m)")] float _maxArc = 0.05f;
        [SerializeField, Range(0f, 1f), Tooltip("How far the fingers open half way through a move (0 = they just morph)")]
        float _reachOpen = 0.6f;
        [SerializeField, Min(0f), Tooltip("Moves shorter than this don't arc or open (regrips)")] float _minReach = 0.03f;

        Hand _left;
        Hand _right;

        void Awake()
        {
            if (_view == null)
                _view = transform.parent;
            _left = Spawn(HandSide.Left);
            _right = Spawn(HandSide.Right);
        }

        // item = the ItemHands of the item in hand, or null.
        public void Tick(ItemHands item, float deltaTime)
        {
            Tick(_left, item, deltaTime);
            Tick(_right, item, deltaTime);
        }

        Hand Spawn(HandSide side)
        {
            HandRig prefab = _set != null ? _set.Get(side) : null;
            if (prefab == null)
            {
                Debug.LogError($"{name}: no {side} hand in the hand set.", this);
                return null;
            }
            HandRig rig = Instantiate(prefab, transform, false);
            rig.name = prefab.name;
            SetLayer(rig.transform, gameObject.layer);
            rig.SetVisible(false);
            return new Hand { Side = side, Rig = rig, BaseScale = rig.transform.localScale };
        }

        void Tick(Hand hand, ItemHands item, float deltaTime)
        {
            if (hand == null)
                return;

            float blend = 0f;
            HandGrip grip = item != null && item.isActiveAndEnabled ? item.GetGrip(hand.Side, out blend) : null;
            if (grip == null)
            {
                hand.Rig.SetVisible(false);
                hand.Item = null;
                hand.Grip = null;
                return;
            }

            Transform space = item.transform;
            hand.Rig.transform.localScale = hand.BaseScale * item.HandScale;
            if (hand.Item != item || hand.Grip == null)
            {
                // A new item or a hand coming back: no move, it's already out of view or tied to the switch motion.
                hand.Item = item;
                hand.Grip = grip;
                hand.Blend = 0f;
            }
            else if (grip != hand.Grip)
            {
                hand.FromPosition = space.InverseTransformPoint(hand.Position);
                hand.FromRotation = Quaternion.Inverse(space.rotation) * hand.Rotation;
                hand.FromShape = hand.Shape;
                hand.Grip = grip;
                hand.Blend = blend;
                hand.Elapsed = 0f;
            }

            Vector3 elbow = _view != null ? _view.TransformPoint(_set.Elbow(hand.Side)) : space.position;
            grip.Resolve(hand.Rig, elbow, out Vector3 position, out Quaternion rotation);
            HandShape shape = grip.Shape;
            if (hand.Blend > 0f && hand.Elapsed < hand.Blend)
            {
                hand.Elapsed += deltaTime;
                float t = Mathf.Clamp01(hand.Elapsed / hand.Blend);
                float w = Mathf.SmoothStep(0f, 1f, t);
                float bump = Mathf.Sin(t * Mathf.PI);
                Vector3 from = space.TransformPoint(hand.FromPosition);
                Vector3 travel = position - from;
                float distance = travel.magnitude;

                Vector3 point = Vector3.Lerp(from, position, w);
                rotation = Quaternion.Slerp(space.rotation * hand.FromRotation, rotation, w);
                shape = HandShape.Lerp(hand.FromShape, shape, w);
                if (distance > _minReach)
                {
                    // Bow out, away from the item's long axis, and a little towards the eye: the hand goes round the
                    // item rather than through it, and stays on the visible side.
                    Vector3 eye = _view != null ? _view.position : space.position;
                    ItemAxis(item, out Vector3 axisPoint, out Vector3 axisDirection);
                    Vector3 onAxis = axisPoint + Vector3.Project(point - axisPoint, axisDirection);
                    Vector3 away = point - onAxis;
                    Vector3 bow = Vector3.ProjectOnPlane(away.normalized + (eye - point).normalized * 0.35f, travel).normalized;
                    point += bow * (Mathf.Min(_maxArc, distance * _arc) * bump);
                    shape = HandShape.Lerp(shape, default, _reachOpen * bump);
                }
                position = point;
            }

            hand.Position = position;
            hand.Rotation = rotation;
            hand.Shape = shape;
            hand.Rig.Place(position, rotation);
            hand.Rig.Apply(shape);
            hand.Rig.SetVisible(true);
        }

        // The item's long axis: through its two default grips (along a gun from grip to forend), else its root's forward.
        static void ItemAxis(ItemHands item, out Vector3 point, out Vector3 direction)
        {
            HandGrip right = item.GetDefault(HandSide.Right);
            HandGrip left = item.GetDefault(HandSide.Left);
            point = right != null ? right.transform.position : item.transform.position;
            direction = item.transform.forward;
            if (right != null && left != null)
            {
                Vector3 between = left.transform.position - right.transform.position;
                if (between.sqrMagnitude > 1e-4f)
                    direction = between.normalized;
            }
        }

        static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayer(root.GetChild(i), layer);
        }
    }
}
