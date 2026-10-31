using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // Drives an enemy model's Animator from how its mover moves: Speed (m/s), LookX/LookY, Lean, and the Fidget trigger with
    // FidgetIndex; EnemyBody adds hits (Hit + HitIndex) and death (Dead + DeathIndex), the brain the fighting stance
    // (Combat) and arrival (Appear), melee tasks swings (Attack + AttackIndex) and celebrating a kill (Celebrate +
    // CelebrateIndex). No Update of its own:
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
        static readonly int AttackId = Animator.StringToHash("Attack");
        static readonly int AttackIndexId = Animator.StringToHash("AttackIndex");
        static readonly int CombatId = Animator.StringToHash("Combat");
        static readonly int AppearId = Animator.StringToHash("Appear");
        static readonly int CelebrateId = Animator.StringToHash("Celebrate");
        static readonly int CelebrateIndexId = Animator.StringToHash("CelebrateIndex");
        static readonly int AttackTagId = Animator.StringToHash("Attack");
        static readonly int CombatStateId = Animator.StringToHash("Combat");
        static readonly int LocomotionStateId = Animator.StringToHash("Locomotion");
        // HitIndex: where the hit came from, + Heavy for a hard push.
        const int HitFront = 0, HitBack = 1, HitLeft = 2, HitRight = 3, Heavy = 4;

        [SerializeField, Tooltip("The model's Animator (on the Model child)")] Animator _animator;
        [SerializeField, Min(0), Tooltip("Fidget states in the controller, picked by FidgetIndex 0..n-1")] int _fidgets;
        [SerializeField, Min(0), Tooltip("Death states in the controller, picked at random by DeathIndex 0..n-1")] int _deaths;
        [SerializeField, Tooltip("Directional death states per side the killing hit came from (front, back, left, right: falls away " +
            "from it), DeathIndex 10 × (side + 1) + n; a side without any uses the random deaths")]
        int[] _sideDeaths = new int[4];
        [SerializeField, Min(0), Tooltip("Attack states in the controller, picked at random by AttackIndex 0..n-1")] int _attacks;
        [SerializeField, Min(0), Tooltip("Celebration (dance) states in the controller, picked at random by CelebrateIndex 0..n-1")]
        int _celebrations;

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
            int from = SideHitFrom(push);
            if (from < 0)
                return;
            _animator.SetInteger(HitIndexId, from + (heavy ? Heavy : 0));
            _animator.SetTrigger(HitId);
        }

        // Fighting stance (combat idle between swings, no fidgets) while engaged.
        public void SetCombat(bool combat)
        {
            if (_motion != null)
                _animator.SetBool(CombatId, combat);
        }

        // Plays the controller's appear clip, if it has one (just spawned; the enemy stands while it plays).
        public void Appear()
        {
            if (_motion == null)
                return;
            _animator.ResetTrigger(FidgetId);
            _animator.SetTrigger(AppearId);
        }

        // The appear time is over: a trigger still pending (the model was culled off screen) must not play it late.
        public void EndAppear()
        {
            if (_motion != null)
                _animator.ResetTrigger(AppearId);
        }

        // A random celebration, repeated while `on` (the enemy stands); none in the controller = it just idles.
        public void Celebrate(bool on)
        {
            if (_motion == null)
                return;
            if (on && _celebrations > 0)
                _animator.SetInteger(CelebrateIndexId, Random.Range(0, _celebrations));
            _animator.SetBool(CelebrateId, on && _celebrations > 0);
        }

        // Staggered: a swing in progress blends back to the stance (no blow lands), a queued one is dropped.
        public void CancelAttack()
        {
            if (_motion == null)
                return;
            _animator.ResetTrigger(AttackId);
            if (_animator.GetCurrentAnimatorStateInfo(0).tagHash == AttackTagId)
                _animator.CrossFadeInFixedTime(_animator.GetBool(CombatId) ? CombatStateId : LocomotionStateId, 0.15f, 0);
        }

        // Plays a random swing (full body; the enemy stands while it plays).
        public void Attack()
        {
            if (_motion == null || _attacks == 0)
                return;
            _animator.ResetTrigger(FidgetId);
            _animator.SetInteger(AttackIndexId, Random.Range(0, _attacks));
            _animator.SetTrigger(AttackId);
        }

        // Plays a death and mutes the layers above the base one (look, lean, hits), even off screen so it's done when seen.
        // Falls away from `push` (the killing hit's direction) when the controller has a death for that side.
        public void Die(Vector3 push)
        {
            if (_motion == null)
                return;
            int side = SideHitFrom(push);
            int sideDeaths = side >= 0 && _sideDeaths != null && side < _sideDeaths.Length ? _sideDeaths[side] : 0;
            _animator.ResetTrigger(HitId);
            _animator.ResetTrigger(FidgetId);
            _animator.ResetTrigger(AttackId);
            _animator.ResetTrigger(AppearId);
            _animator.SetBool(CelebrateId, false);
            _animator.SetInteger(DeathIndexId, sideDeaths > 0 ? 10 * (side + 1) + Random.Range(0, sideDeaths) : _deaths > 0 ? Random.Range(0, _deaths) : 0);
            _animator.SetBool(DeadId, true);
            for (int i = 1; i < _animator.layerCount; i++)
                _animator.SetLayerWeight(i, 0f);
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        // HitFront / HitBack / HitLeft / HitRight from a push in world space; -1 when it has no horizontal direction. Pushed
        // backwards = hit from the front; pushed to the right = hit from the left.
        int SideHitFrom(Vector3 push)
        {
            Vector3 local = transform.InverseTransformDirection(push);
            if (local.x * local.x + local.z * local.z < 0.0001f)
                return -1;
            return Mathf.Abs(local.z) >= Mathf.Abs(local.x) ? (local.z < 0f ? HitFront : HitBack) : (local.x > 0f ? HitLeft : HitRight);
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
            _animator.SetBool(CombatId, false);
            _animator.SetBool(CelebrateId, false);
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
