using MB13.Aipocalypse.Core.EquipmentSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // The hammer in hand (a RepairToolDefinition's held item), posed between markers like the melee swing. It rests at idle;
    // while PlayerRepair works it sets Cycle, and each knock winds up, strikes and recovers.
    public sealed class RepairTool : HeldItem
    {
        [SerializeField, Tooltip("Moved between the pose markers")] Transform _posePivot;
        [SerializeField] Transform _idlePose;
        [SerializeField] Transform _windUpPose;
        [SerializeField] Transform _strikePose;
        [SerializeField, Range(0.05f, 0.9f), Tooltip("Share of a knock spent winding up")] float _windUpEnd = 0.42f;
        [SerializeField, Range(0.05f, 0.95f), Tooltip("Share of a knock where the hammer lands (the repair happens)")] float _strikeAt = 0.55f;
        [SerializeField, Range(0.05f, 1f), Tooltip("Share of a knock it rests on the wood before recovering")] float _recoverFrom = 0.66f;

        public float StrikeAt => _strikeAt;
        // How far through a knock, in [0, 1); negative = resting at idle.
        public float Cycle { get; set; } = -1f;

        // The knock's arc mustn't be bent by the obstacle pose.
        public override bool KeepsClear => Cycle < 0f;

        public override void Tick(in HeldItemInput input, float deltaTime) => Pose(Cycle);

        protected override void OnShown()
        {
            Cycle = -1f;
            Pose(Cycle);
        }

        void Pose(float cycle)
        {
            if (_posePivot == null || _idlePose == null)
                return;
            KnockPose(cycle, out Vector3 position, out Quaternion rotation);
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
        [ButtonGroup("Preview"), Button("Idle")] void PreviewIdle() => PoseBlender.Preview(_posePivot, _idlePose);
        [ButtonGroup("Preview"), Button("Wind-up")] void PreviewWindUp() => PoseBlender.Preview(_posePivot, _windUpPose);
        [ButtonGroup("Preview"), Button("Strike")] void PreviewStrike() => PoseBlender.Preview(_posePivot, _strikePose);

        // Setup scripts only.
        public void Setup(Transform posePivot, Transform idle, Transform windUp, Transform strike)
        {
            _posePivot = posePivot;
            _idlePose = idle;
            _windUpPose = windUp;
            _strikePose = strike;
        }
#endif
    }
}
