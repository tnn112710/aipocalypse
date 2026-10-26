using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // Drives an enemy model's Animator from how its mover moves: Speed (m/s), LookX/LookY, Lean, and the Fidget trigger with
    // FidgetIndex; EnemyBody adds hits (Hit + HitIndex) and death (Dead + DeathIndex). No Update of its own:
    // EnemyAnimationManager advances every registered enemy each frame. Controllers follow the parameter contract of the
    // aipocalypse-characters skill (CharacterKit.BuildLocomotionController).
    [RequireComponent(typeof(EnemyMover))]
    public sealed class EnemyAnimator : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int LookXId = Animator.StringToHash("LookX");
        static readonly int LookYId = Animator.StringToHash("LookY");
        static readonly int LeanId = Animator.StringToHash("Lean");
        static readonly int FidgetId = Animator.StringToHash("Fidget");
        static readonly int FidgetIndexId = Animator.StringToHash("FidgetIndex");
        static readonly int HitId = Animator.StringToHash("Hit");
        static readonly int HitIndexId = Animator.StringToHash("HitIndex");
        static readonly int DeadId = Animator.StringToHash("Dead");
        static readonly int DeathIndexId = Animator.StringToHash("DeathIndex");
        // HitIndex: where the hit came from, + Heavy for a hard push.
        const int HitFront = 0, HitBack = 1, HitLeft = 2, HitRight = 3, Heavy = 4;

        [SerializeField, Tooltip("The model's Animator (on the Model child)")] Animator _animator;
        [SerializeField, Min(0), Tooltip("Fidget states in the controller, picked by FidgetIndex 0..n-1")] int _fidgets;
        [SerializeField, Min(0), Tooltip("Death states in the controller, picked at random by DeathIndex 0..n-1")] int _deaths;

        EnemyMover _mover;
        EnemyAnimationSettings _settings;
        LocomotionAnimation _motion;
        float _lastYaw;
        float[] _layerWeights;
        AnimatorCullingMode _culling;

        public Animator Animator => _animator;

        public void Initialize(EnemyAnimationSettings settings)
        {
            _mover = GetComponent<EnemyMover>();
            _settings = settings;
            _motion = _animator != null && settings != null ? new LocomotionAnimation(settings, _fidgets, Random.Range(0, int.MaxValue)) : null;
            _lastYaw = transform.eulerAngles.y;
            if (_motion == null)
            {
                Debug.LogError($"{name}: EnemyAnimator needs its Animator and the definition's Animation settings.", this);
                return;
            }
            Revive();
        }

        // `push`: the hit's direction of travel (its force, or from the hit point to the body).
        public void Hit(Vector3 push, bool heavy)
        {
            if (_motion == null)
                return;
            Vector3 local = transform.InverseTransformDirection(push);
            if (local.x * local.x + local.z * local.z < 0.0001f)
                return;
            // Pushed backwards = hit from the front; pushed to the right = hit from the left.
            int from = Mathf.Abs(local.z) >= Mathf.Abs(local.x) ? (local.z < 0f ? HitFront : HitBack) : (local.x > 0f ? HitLeft : HitRight);
            _animator.SetInteger(HitIndexId, from + (heavy ? Heavy : 0));
            _animator.SetTrigger(HitId);
        }

        // Plays a death and mutes the layers above the base one (look, lean, hits), even off screen so it's done when seen.
        public void Die()
        {
            if (_motion == null)
                return;
            _animator.ResetTrigger(HitId);
            _animator.ResetTrigger(FidgetId);
            _animator.SetInteger(DeathIndexId, _deaths > 0 ? Random.Range(0, _deaths) : 0);
            _animator.SetBool(DeadId, true);
            for (int i = 1; i < _animator.layerCount; i++)
                _animator.SetLayerWeight(i, 0f);
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        void Revive()
        {
            if (_layerWeights == null)
            {
                _culling = _animator.cullingMode;
                _layerWeights = new float[_animator.layerCount];
                for (int i = 0; i < _layerWeights.Length; i++)
                    _layerWeights[i] = _animator.GetLayerWeight(i);
            }
            _animator.cullingMode = _culling;
            _animator.SetBool(DeadId, false);
            for (int i = 1; i < _layerWeights.Length; i++)
                _animator.SetLayerWeight(i, _layerWeights[i]);
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
