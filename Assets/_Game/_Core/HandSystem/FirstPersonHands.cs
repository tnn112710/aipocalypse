using UnityEngine;

namespace MB13.Aipocalypse.Core.HandSystem
{
    // The player's two floating hands, spawned from a HandSet on this object's layer. Each frame, after the item's final pose,
    // puts each hand on the grip the item names (ItemHands), wrist toward the implied elbow; hidden without a grip.
    public sealed class FirstPersonHands : MonoBehaviour
    {
        sealed class Hand
        {
            public HandSide Side;
            public HandRig Rig;
            public Vector3 BaseScale;
        }

        [SerializeField, Tooltip("Hand prefabs and implied elbows; swap it to change the hand mesh")] HandSet _set;
        [SerializeField, Tooltip("The view the elbows are relative to (camera pitch)")] Transform _view;

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
        public void Tick(ItemHands item)
        {
            bool shown = item != null && item.isActiveAndEnabled;
            Tick(_left, shown ? item : null);
            Tick(_right, shown ? item : null);
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
            rig.transform.SetLayerRecursively(gameObject.layer);
            rig.SetVisible(false);
            return new Hand { Side = side, Rig = rig, BaseScale = rig.transform.localScale };
        }

        void Tick(Hand hand, ItemHands item)
        {
            if (hand == null)
                return;

            HandGrip grip = item != null ? item.GetGrip(hand.Side) : null;
            if (grip == null)
            {
                hand.Rig.SetVisible(false);
                return;
            }

            hand.Rig.transform.localScale = hand.BaseScale * item.HandScale;
            Vector3 elbow = _view != null ? item.Carry(_view.TransformPoint(_set.Elbow(hand.Side))) : item.transform.position;
            grip.Resolve(hand.Rig, elbow, out Vector3 position, out Quaternion rotation);
            Vector3 shoulder = _view != null ? item.Carry(_view.TransformPoint(_set.Shoulder(hand.Side))) : elbow;
            hand.Rig.Place(position, rotation, shoulder, elbow);
            hand.Rig.Apply(grip.Shape);
            hand.Rig.SetVisible(true);
        }
    }
}
