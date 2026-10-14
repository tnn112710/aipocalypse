using Aipocalypse.Core.Combat;
using Aipocalypse.Core.EquipmentSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Aipocalypse.Core.WeaponSystem
{
    // A melee weapon in hand: runs its MeleeAttack rules from the holder's input and shows them — a procedural swing
    // between pose markers, the hit check from the camera, damage, knockback, impact effect and sounds.
    // Prefab layout: Poses (Idle, WindUp, FollowThrough, Sprint markers) and PosePivot > Model.
    public sealed class HeldMelee : HeldItem
    {
        [SerializeField, Tooltip("Moved to the active pose; parent of the model")] Transform _posePivot;
        [SerializeField, Tooltip("2D source for the weapon's own sounds")] AudioSource _audio;

        [BoxGroup("Poses"), SerializeField, Tooltip("Siblings of the pose pivot; move them to pose the weapon")] Transform _idlePose;
        [BoxGroup("Poses"), SerializeField, Tooltip("Raised, ready to strike")] Transform _windUpPose;
        [BoxGroup("Poses"), SerializeField, Tooltip("Where the swing ends, past the hit")] Transform _followThroughPose;
        [BoxGroup("Poses"), SerializeField] Transform _sprintPose;

        [BoxGroup("Swing motion"), SerializeField, Min(0.01f), Tooltip("Seconds from wind-up to follow-through; the hit lands halfway")]
        float _strikeTime = 0.1f;
        [BoxGroup("Swing motion"), SerializeField, Tooltip("Every other swing in a row comes from the other side (poses mirrored around Idle)")]
        bool _alternateSides = true;
        [BoxGroup("Swing motion"), SerializeField, Min(0f), Tooltip("How fast it moves into and out of the sprint pose")]
        float _poseSpeed = 10f;

        readonly RaycastHit[] _hits = new RaycastHit[16];

        EffectPool _effects;
        IWeaponUser _user;
        MeleeDefinition _definition;
        MeleeAttack _attack;

        Vector3 _posePosition;
        Quaternion _poseRotation;
        Vector3 _swingFromPosition;
        Quaternion _swingFromRotation;
        bool _mirrored;
        // Set by this frame's MeleeAttack.Tick, checked in LateTick once the camera is in its final place.
        bool _pendingStrike;

        public MeleeAttack Attack => _attack;

        [Inject]
        void Construct(EffectPool effects) => _effects = effects;

        protected override void OnBound()
        {
            _user = Owner as IWeaponUser;
            if (Item?.Definition is not MeleeDefinition definition || _user == null)
            {
                Debug.LogError($"{name}: needs a MeleeDefinition item and a holder that is an IWeaponUser.", this);
                return;
            }
            if (_effects == null)
                Debug.LogError($"{name}: not injected; spawn it through the EquipmentRig.", this);

            _definition = definition;
            _attack = new MeleeAttack(_definition.Swing);
            _attack.SwingStarted += OnSwingStarted;
            _attack.Struck += OnStruck;

            if (_effects != null)
                _effects.Prewarm(_definition.ImpactEffect, 4);
        }

        public override void Tick(in HeldItemInput input, float deltaTime)
        {
            if (_attack == null)
                return;

            _attack.Tick(new MeleeInput
            {
                AttackHeld = input.UseHeld,
                AttackPressed = input.UsePressed,
                IsReady = input.IsReady,
                CanAttack = !input.IsSprinting,
            }, deltaTime);

            UpdatePose(input.IsSprinting && input.IsReady, deltaTime);
        }

        public override void LateTick()
        {
            if (!_pendingStrike)
                return;
            _pendingStrike = false;
            Strike();
        }

        protected override void OnShown()
        {
            if (_definition != null)
                Play(_definition.Sounds.Equip);
            if (_idlePose != null)
            {
                _posePosition = _idlePose.localPosition;
                _poseRotation = _idlePose.localRotation;
            }
            ApplyPose();
        }

        protected override void OnHidden()
        {
            _pendingStrike = false;
            _attack?.Interrupt();
        }

        void OnDestroy()
        {
            if (_attack == null)
                return;
            _attack.SwingStarted -= OnSwingStarted;
            _attack.Struck -= OnStruck;
        }

        void OnSwingStarted(int index)
        {
            _swingFromPosition = _posePosition;
            _swingFromRotation = _poseRotation;
            _mirrored = _alternateSides && index % 2 == 1;
            Play(_definition.Sounds.Swing);
        }

        void OnStruck() => _pendingStrike = true;

        void Strike()
        {
            Transform eye = _user.Eye;
            SwingSettings swing = _definition.Swing;
            if (!TryHit(eye.position, eye.forward, swing, out RaycastHit hit))
                return;

            Vector3 force = eye.forward * swing.Knockback;
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null && !target.IsDead)
                target.TakeDamage(new DamageInfo(swing.Damage, DamageType.Melee, hit.point, force));

            Rigidbody body = hit.rigidbody;
            if (body != null && !body.isKinematic)
                body.AddForceAtPosition(force, hit.point, ForceMode.Impulse);

            if (_effects != null)
                _effects.Spawn(_definition.ImpactEffect, hit.point, Quaternion.LookRotation(hit.normal));
            Play(target != null ? _definition.Sounds.HitFlesh : _definition.Sounds.HitSurface);
        }

        // Nearest hit along a short sphere-cast that isn't the holder. Triggers (pickups) are ignored.
        bool TryHit(Vector3 origin, Vector3 direction, SwingSettings swing, out RaycastHit nearest)
        {
            int count = Physics.SphereCastNonAlloc(origin, swing.Radius, direction, _hits, swing.Range, swing.HitLayers, QueryTriggerInteraction.Ignore);
            nearest = default;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.distance >= nearestDistance || hit.collider.transform.IsChildOf(_user.Root))
                    continue;
                nearest = hit;
                nearestDistance = hit.distance;
            }
            if (nearestDistance == float.MaxValue)
                return false;

            // Already touching at the start of the cast: Unity reports no point or normal.
            if (nearest.distance <= 0f)
            {
                nearest.point = nearest.collider.ClosestPoint(origin);
                nearest.normal = -direction;
            }
            return true;
        }

        void Play(AudioClip clip)
        {
            if (clip != null && _audio != null)
                _audio.PlayOneShot(clip, _definition.Sounds.Volume);
        }

        void UpdatePose(bool sprinting, float deltaTime)
        {
            if (_attack.Phase != SwingPhase.None)
            {
                SwingPose(_attack.SwingTime, out _posePosition, out _poseRotation);
            }
            else
            {
                Transform target = sprinting && _sprintPose != null ? _sprintPose : _idlePose;
                float t = _poseSpeed > 0f ? 1f - Mathf.Exp(-_poseSpeed * deltaTime) : 1f;
                if (target != null)
                {
                    _posePosition = Vector3.Lerp(_posePosition, target.localPosition, t);
                    _poseRotation = Quaternion.Slerp(_poseRotation, target.localRotation, t);
                }
            }
            ApplyPose();
        }

        // Start → wind-up (eased out), a fast strike through the hit to the follow-through, then back to idle.
        void SwingPose(float time, out Vector3 position, out Quaternion rotation)
        {
            SwingSettings swing = _definition.Swing;
            float strike = Mathf.Min(_strikeTime, swing.WindUp * 2f, swing.Duration);
            float strikeStart = Mathf.Max(0f, swing.WindUp - strike * 0.5f);
            float strikeEnd = Mathf.Min(swing.Duration, strikeStart + strike);

            Pose(_windUpPose, out Vector3 windUpPosition, out Quaternion windUpRotation);
            Pose(_followThroughPose, out Vector3 followPosition, out Quaternion followRotation);

            if (time < strikeStart)
            {
                float t = EaseOut(time / strikeStart);
                position = Vector3.Lerp(_swingFromPosition, windUpPosition, t);
                rotation = Quaternion.Slerp(_swingFromRotation, windUpRotation, t);
            }
            else if (time < strikeEnd)
            {
                float t = Mathf.SmoothStep(0f, 1f, (time - strikeStart) / (strikeEnd - strikeStart));
                position = Vector3.Lerp(windUpPosition, followPosition, t);
                rotation = Quaternion.Slerp(windUpRotation, followRotation, t);
            }
            else
            {
                float recover = swing.Duration - strikeEnd;
                float t = recover > 0f ? Mathf.SmoothStep(0f, 1f, (time - strikeEnd) / recover) : 1f;
                Pose(_idlePose, out Vector3 idlePosition, out Quaternion idleRotation);
                position = Vector3.Lerp(followPosition, idlePosition, t);
                rotation = Quaternion.Slerp(followRotation, idleRotation, t);
            }
        }

        // A pose marker, mirrored left-right around Idle on alternate swings.
        void Pose(Transform marker, out Vector3 position, out Quaternion rotation)
        {
            Transform source = marker != null ? marker : _idlePose;
            position = source.localPosition;
            rotation = source.localRotation;
            if (!_mirrored || _idlePose == null)
                return;

            Vector3 idle = _idlePose.localPosition;
            position.x = 2f * idle.x - position.x;
            Quaternion delta = Quaternion.Inverse(_idlePose.localRotation) * rotation;
            rotation = _idlePose.localRotation * new Quaternion(delta.x, -delta.y, -delta.z, delta.w);
        }

        void ApplyPose() => _posePivot.SetLocalPositionAndRotation(_posePosition, _poseRotation);

        static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

#if UNITY_EDITOR
        // Edit-mode preview in the prefab. In play the weapon always starts at Idle.
        [ButtonGroup("Preview"), Button("Idle")] void PreviewIdle() => Preview(_idlePose);
        [ButtonGroup("Preview"), Button("Wind-up")] void PreviewWindUp() => Preview(_windUpPose);
        [ButtonGroup("Preview"), Button("Follow-through")] void PreviewFollowThrough() => Preview(_followThroughPose);
        [ButtonGroup("Preview"), Button("Sprint")] void PreviewSprint() => Preview(_sprintPose);

        void Preview(Transform pose)
        {
            if (pose == null || _posePivot == null)
                return;
            UnityEditor.Undo.RecordObject(_posePivot, "Preview melee pose");
            _posePivot.SetLocalPositionAndRotation(pose.localPosition, pose.localRotation);
        }
#endif
    }
}
