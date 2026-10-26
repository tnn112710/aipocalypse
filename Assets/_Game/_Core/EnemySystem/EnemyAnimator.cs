using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // Drives an enemy model's Animator from how its mover moves: Speed (m/s), LookX/LookY, Lean, and the Fidget trigger with
    // FidgetIndex. No Update of its own: EnemyAnimationManager advances every registered enemy each frame. Controllers follow
    // the parameter contract of the aipocalypse-characters skill (CharacterKit.BuildLocomotionController).
    [RequireComponent(typeof(EnemyMover))]
    public sealed class EnemyAnimator : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int LookXId = Animator.StringToHash("LookX");
        static readonly int LookYId = Animator.StringToHash("LookY");
        static readonly int LeanId = Animator.StringToHash("Lean");
        static readonly int FidgetId = Animator.StringToHash("Fidget");
        static readonly int FidgetIndexId = Animator.StringToHash("FidgetIndex");

        [SerializeField, Tooltip("The model's Animator (on the Model child)")] Animator _animator;
        [SerializeField, Min(0), Tooltip("Fidget states in the controller, picked by FidgetIndex 0..n-1")] int _fidgets;

        EnemyMover _mover;
        EnemyAnimationSettings _settings;
        LocomotionAnimation _motion;
        float _lastYaw;

        public Animator Animator => _animator;

        public void Initialize(EnemyAnimationSettings settings)
        {
            _mover = GetComponent<EnemyMover>();
            _settings = settings;
            _motion = _animator != null && settings != null ? new LocomotionAnimation(settings, _fidgets, Random.Range(0, int.MaxValue)) : null;
            _lastYaw = transform.eulerAngles.y;
            if (_motion == null)
                Debug.LogError($"{name}: EnemyAnimator needs its Animator and the definition's Animation settings.", this);
        }

        public void Advance(float deltaTime)
        {
            if (_motion == null || deltaTime <= 0f)
                return;
            Vector3 velocity = _mover.Velocity;
            float yaw = transform.eulerAngles.y;
            float turnRate = Mathf.DeltaAngle(_lastYaw, yaw) / deltaTime;
            _lastYaw = yaw;
            _motion.Advance(deltaTime, new Vector2(velocity.x, velocity.z).magnitude, turnRate);

            _animator.SetFloat(SpeedId, _motion.Speed, _settings.SpeedDampTime, deltaTime);
            _animator.SetFloat(LookXId, _motion.Look.x, _settings.LookDampTime, deltaTime);
            _animator.SetFloat(LookYId, _motion.Look.y, _settings.LookDampTime, deltaTime);
            _animator.SetFloat(LeanId, _motion.Lean, _settings.LeanDampTime, deltaTime);
            int fidget = _motion.TakeFidget();
            if (fidget >= 0)
            {
                _animator.SetInteger(FidgetIndexId, fidget);
                _animator.SetTrigger(FidgetId);
            }
        }
    }
}
