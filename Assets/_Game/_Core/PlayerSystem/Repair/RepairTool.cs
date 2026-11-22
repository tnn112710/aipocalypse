using MB13.Aipocalypse.Core.EquipmentSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // The hammer in hand (a RepairToolDefinition's held item), posed between markers like the melee swing. It rests at idle
    // (sprint pose while sprinting); while PlayerRepair works it sets Cycle, and each knock winds up, strikes and recovers.
    public sealed class RepairTool : HeldItem
    {
        [SerializeField, Tooltip("Moved between the pose markers")] Transform _posePivot;
        [SerializeField] Transform _idlePose;
        [SerializeField] Transform _windUpPose;
        [SerializeField] Transform _strikePose;
        [SerializeField, Tooltip("Held while sprinting; none = stays at idle")] Transform _sprintPose;
        [SerializeField, Min(0f), Tooltip("How fast it moves between idle and sprint (higher = faster)")] float _poseSpeed = 10f;
        [SerializeField, Range(0.05f, 0.9f), Tooltip("Share of a knock spent winding up")] float _windUpEnd = 0.42f;
        [SerializeField, Range(0.05f, 0.95f), Tooltip("Share of a knock where the hammer lands (the repair happens)")] float _strikeAt = 0.55f;
        [SerializeField, Range(0.05f, 1f), Tooltip("Share of a knock it rests on the wood before recovering")] float _recoverFrom = 0.66f;

        readonly PoseBlender _pose = new();
        // Where the first wind-up starts: the pose the hammer was in when the work began (sprint pose, mid-ease).
        Vector3 _fromPosition;
        Quaternion _fromRotation;
        float _lastCycle = -1f;

        public float StrikeAt => _strikeAt;
        // How far through a knock, in [0, 1); negative = resting.
        public float Cycle { get; set; } = -1f;

        // The knock's arc mustn't be bent by the obstacle pose.
        public override bool KeepsClear => Cycle < 0f;

        // A knock finishes before a sprint starts (PlayerRepair stops the work on sprint).
        public override bool StopsSprint(bool useHeld, bool aimHeld) => Cycle >= 0f;

        public override void Tick(in HeldItemInput input, float deltaTime)
        {
            if (_posePivot == null || _idlePose == null)
                return;
            if (Cycle >= 0f && _windUpPose != null && _strikePose != null)
            {
                if (_lastCycle < 0f)
                {
                    _fromPosition = _pose.Position;
                    _fromRotation = _pose.Rotation;
                }
                else if (Cycle < _lastCycle)
                {
                    _fromPosition = _idlePose.localPosition;
                    _fromRotation = _idlePose.localRotation;
                }
                KnockPose(Cycle, out Vector3 position, out Quaternion rotation);
                _pose.Set(position, rotation);
            }
            else
            {
                bool sprinting = input.IsSprinting && input.IsReady && _sprintPose != null;
                _pose.MoveTowards(sprinting ? _sprintPose : _idlePose, _poseSpeed, deltaTime);
            }
            _lastCycle = Cycle;
            _pose.ApplyTo(_posePivot);
        }

        protected override void OnShown()
        {
            Cycle = -1f;
            _lastCycle = -1f;
            if (_posePivot == null || _idlePose == null)
                return;
            _pose.SnapTo(_idlePose);
            _pose.ApplyTo(_posePivot);
        }

        void KnockPose(float cycle, out Vector3 position, out Quaternion rotation)
        {
            if (cycle < _windUpEnd)
            {
                float t = EaseOut(cycle / _windUpEnd);
                position = Vector3.Lerp(_fromPosition, _windUpPose.localPosition, t);
                rotation = Quaternion.Slerp(_fromRotation, _windUpPose.localRotation, t);
            }
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
        [ButtonGroup("Preview"), Button("Sprint")] void PreviewSprint() => PoseBlender.Preview(_posePivot, _sprintPose);

        // Setup scripts only.
        public void Setup(Transform posePivot, Transform idle, Transform windUp, Transform strike, Transform sprint)
        {
            _posePivot = posePivot;
            _idlePose = idle;
            _windUpPose = windUp;
            _strikePose = strike;
            _sprintPose = sprint;
        }
#endif
    }
}
