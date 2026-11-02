using Aipocalypse.Core.EquipmentSystem;
using Aipocalypse.Core.HandSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    // The first-person hammer the player repairs with (PlayerRepair): a model with hand grips, posed between markers like the
    // melee swing. Raised from below the view, then each knock winds up, strikes and recovers. A child of the equipment rig at
    // the hand's rest pose (not under the hand, which is lowered while the item is put away); saved hidden, shown while used.
    public sealed class RepairTool : MonoBehaviour
    {
        [SerializeField, Tooltip("Moved between the pose markers")] Transform _posePivot;
        [SerializeField, Tooltip("Below the view: where it rises from")] Transform _hiddenPose;
        [SerializeField] Transform _idlePose;
        [SerializeField] Transform _windUpPose;
        [SerializeField] Transform _strikePose;
        [SerializeField] ItemHands _hands;
        [SerializeField, Range(0.05f, 0.9f), Tooltip("Share of a knock spent winding up")] float _windUpEnd = 0.42f;
        [SerializeField, Range(0.05f, 0.95f), Tooltip("Share of a knock where the hammer lands (the repair happens)")] float _strikeAt = 0.55f;
        [SerializeField, Range(0.05f, 1f), Tooltip("Share of a knock it rests on the wood before recovering")] float _recoverFrom = 0.66f;

        public ItemHands Hands => _hands;
        public float StrikeAt => _strikeAt;

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
        }

        // `raise` 0 = hidden below the view, 1 = up; `cycle` in [0, 1) = how far through a knock, negative = resting at idle.
        public void Pose(float raise, float cycle)
        {
            if (_posePivot == null || _idlePose == null)
                return;
            KnockPose(cycle, out Vector3 position, out Quaternion rotation);
            if (raise < 1f && _hiddenPose != null)
            {
                float t = Mathf.SmoothStep(0f, 1f, raise);
                position = Vector3.Lerp(_hiddenPose.localPosition, position, t);
                rotation = Quaternion.Slerp(_hiddenPose.localRotation, rotation, t);
            }
            _posePivot.SetLocalPositionAndRotation(position, rotation);
        }

        void KnockPose(float cycle, out Vector3 position, out Quaternion rotation)
        {
            if (cycle < 0f || _windUpPose == null || _strikePose == null)
            {
                position = _idlePose.localPosition;
                rotation = _idlePose.localRotation;
                return;
            }
            if (cycle < _windUpEnd)
                Blend(_idlePose, _windUpPose, EaseOut(cycle / _windUpEnd), out position, out rotation);
            else if (cycle < _strikeAt)
                Blend(_windUpPose, _strikePose, EaseIn((cycle - _windUpEnd) / (_strikeAt - _windUpEnd)), out position, out rotation);
            else if (cycle < _recoverFrom)
            {
                position = _strikePose.localPosition;
                rotation = _strikePose.localRotation;
            }
            else
                Blend(_strikePose, _idlePose, Mathf.SmoothStep(0f, 1f, (cycle - _recoverFrom) / (1f - _recoverFrom)), out position, out rotation);
        }

        static void Blend(Transform from, Transform to, float t, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.Lerp(from.localPosition, to.localPosition, t);
            rotation = Quaternion.Slerp(from.localRotation, to.localRotation, t);
        }

        static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
        static float EaseIn(float t) => t * t;

#if UNITY_EDITOR
        [ButtonGroup("Preview"), Button("Hidden")] void PreviewHidden() => PoseBlender.Preview(_posePivot, _hiddenPose);
        [ButtonGroup("Preview"), Button("Idle")] void PreviewIdle() => PoseBlender.Preview(_posePivot, _idlePose);
        [ButtonGroup("Preview"), Button("Wind-up")] void PreviewWindUp() => PoseBlender.Preview(_posePivot, _windUpPose);
        [ButtonGroup("Preview"), Button("Strike")] void PreviewStrike() => PoseBlender.Preview(_posePivot, _strikePose);

        // Setup scripts only.
        public void Setup(Transform posePivot, Transform hidden, Transform idle, Transform windUp, Transform strike, ItemHands hands)
        {
            _posePivot = posePivot;
            _hiddenPose = hidden;
            _idlePose = idle;
            _windUpPose = windUp;
            _strikePose = strike;
            _hands = hands;
        }
#endif
    }
}
