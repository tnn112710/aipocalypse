using System;
using Aipocalypse.Core.HandSystem;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;

namespace Aipocalypse.Core.WeaponSystem
{
    // The states of Animations/Shared/Gun.controller. Each name is also the shared clip a gun's override controller replaces.
    public enum GunClip
    {
        Equipped,
        Idle,
        Firing,
        Reloading,
        ReloadingEmpty,
        Repairing,
        RepairingStubborn,
        RepairFocus,
        RepairEnd,
        Cycling,
    }

    // A gun's clips and hand constraints, sampled by hand once per frame from HeldGun.Tick (the Animator never updates
    // on its own). Actions follow Gun.ActionProgress; Equipped, Firing and RepairEnd run on their own time, then Idle loops.
    [RequireComponent(typeof(Animator), typeof(RigBuilder))]
    public sealed class GunAnimation : MonoBehaviour
    {
        static readonly string[] ClipNames = Enum.GetNames(typeof(GunClip));
        static readonly int[] StateHashes = Array.ConvertAll(ClipNames, clip => Animator.StringToHash("Base Layer." + clip));

        [SerializeField, Tooltip("Its controller must be an override of Gun.controller")] Animator _animator;
        [SerializeField] RigBuilder _rig;

        [Header("Dropped magazine")]
        [SerializeField, Tooltip("The magazine in the gun; the dropped copy spawns at its pose. None = nothing drops (shotguns)")]
        Transform _magazine;
        [SerializeField, Tooltip("Pooled physics copy thrown out on reload")] PooledEffect _droppedMagazine;
        [SerializeField, Min(0)] int _magazinePrewarm = 3;
        [SerializeField, Range(0f, 1f), Tooltip("Reload progress at which it drops")] float _releaseTime = 0.3f;
        [SerializeField, Tooltip("Launch velocity, in the gun's space")] Vector3 _dropVelocity = new(-0.25f, -0.8f, 0.1f);
        [SerializeField] Vector3 _dropAngularVelocity = new(2f, 0f, -3f);

        // Keyed in the repair clips: impacts so far, and 1 while the latest one is a punch (0 = slap).
        [SerializeField, HideInInspector] float _repairImpactCount;
        [SerializeField, HideInInspector] float _repairPunch;

        readonly float[] _clipLengths = new float[ClipNames.Length];

        PlayableGraph _graph;
        AnimatorControllerPlayable _controller;
        AnimatorOverrideController _overrides;
        EffectPool _effects;
        GunClip _state = GunClip.Idle;
        GunClip _repairState = GunClip.Repairing;
        float _elapsed;
        bool _released;
        int _notifiedImpacts;

        // RepairEnd after a QTE success: blends from the pose the repair was cut at.
        bool _returnFromSequence;
        Transform[] _returnParts;
        Vector3[] _returnPositions;
        Quaternion[] _returnRotations;
        HandGrip[] _returnGrips;
        HandShape[] _returnShapes;

        // Once per slap or punch keyed in a repair clip, even when a long frame skips past several.
        public event Action RepairSlapped;

        public GunClip State => _state;
        public bool ImpactIsPunch => _repairPunch > 0.5f;
        // 0..1 through the clip last sampled.
        public float Progress { get; private set; }

        bool IsAction => _state is GunClip.Reloading or GunClip.ReloadingEmpty or GunClip.Repairing or GunClip.RepairingStubborn
            or GunClip.RepairFocus or GunClip.Cycling;
        bool IsRepairing => _state is GunClip.Repairing or GunClip.RepairingStubborn;
        bool Loops => _state is GunClip.Idle or GunClip.RepairFocus;

        public void Bind(EffectPool effects)
        {
            _effects = effects;
            _effects?.Prewarm(_droppedMagazine, _magazinePrewarm);
        }

        public void Show()
        {
            SetState(GunClip.Equipped);
            Evaluate(0f);
        }

        public void Fired()
        {
            if (!IsAction)
                SetState(GunClip.Firing);
        }

        public void StartAction(GunAction action, bool empty, bool stubborn = false)
        {
            _repairState = stubborn ? GunClip.RepairingStubborn : GunClip.Repairing;
            SetState(action switch
            {
                GunAction.Reloading => empty ? GunClip.ReloadingEmpty : GunClip.Reloading,
                GunAction.ClearingJam => _repairState,
                GunAction.Cycling => GunClip.Cycling,
                _ => GunClip.Idle,
            });
        }

        public void EndAction() => SetState(GunClip.Idle);

        // Holds the repair on its looping focus clip (while a QTE waits), or goes back to the repair sequence.
        public void FocusRepair(bool focus)
        {
            int impacts = _notifiedImpacts;
            SetState(focus ? GunClip.RepairFocus : _repairState);
            _notifiedImpacts = impacts;
        }

        // The repair succeeded early: RepairEnd plays from the current pose back to the grips.
        public void FinishRepair()
        {
            bool capture = IsRepairing;
            if (capture)
            {
                EnsureGraph();
                for (int i = 0; i < _returnParts.Length; i++)
                {
                    _returnPositions[i] = _returnParts[i].localPosition;
                    _returnRotations[i] = _returnParts[i].localRotation;
                }
                for (int i = 0; i < _returnGrips.Length; i++)
                    _returnShapes[i] = _returnGrips[i].Shape;
            }
            SetState(GunClip.RepairEnd);
            _returnFromSequence = capture;
        }

        public void Tick(float deltaTime, float actionProgress)
        {
            if (deltaTime <= 0f)
                return;
            if (_returnFromSequence)
            {
                TickReturn(deltaTime);
                return;
            }
            if (IsAction && _state != GunClip.RepairFocus)
            {
                Evaluate(Mathf.Clamp01(actionProgress));
                if (IsRepairing)
                    NotifyImpacts();
                if (!_released && (_state is GunClip.Reloading or GunClip.ReloadingEmpty) && Progress >= _releaseTime)
                    ReleaseMagazine();
                return;
            }

            _elapsed += deltaTime;
            float duration = ClipLength(_state);
            if (!Loops && _elapsed >= duration)
            {
                _elapsed -= duration;
                _state = GunClip.Idle;
                duration = ClipLength(GunClip.Idle);
            }
            Evaluate(Loops ? Mathf.Repeat(_elapsed / duration, 1f) : Mathf.Clamp01(_elapsed / duration));
        }

        // Back to rest and the shared controller, so the prefab previews and re-equips cleanly.
        public void Stop()
        {
            if (_rig != null)
                _rig.Clear();
            if (_graph.IsValid())
                _graph.Destroy();
            if (_animator != null && _overrides != null)
                _animator.runtimeAnimatorController = _overrides;
            _overrides = null;
            SetState(GunClip.Idle);
        }

        void OnDisable() => Stop();

        void TickReturn(float deltaTime)
        {
            _elapsed += deltaTime;
            float progress = Mathf.Clamp01(_elapsed / ClipLength(GunClip.RepairEnd));
            if (progress >= 1f)
            {
                SetState(GunClip.Idle);
                Evaluate(0f);
                return;
            }

            Evaluate(progress);
            float blend = Mathf.SmoothStep(0f, 1f, progress);
            for (int i = 0; i < _returnParts.Length; i++)
            {
                Transform part = _returnParts[i];
                part.SetLocalPositionAndRotation(Vector3.Lerp(_returnPositions[i], part.localPosition, blend),
                    Quaternion.Slerp(_returnRotations[i], part.localRotation, blend));
            }
            for (int i = 0; i < _returnGrips.Length; i++)
                _returnGrips[i].SetAnimatedShape(HandShape.Lerp(_returnShapes[i], _returnGrips[i].Shape, blend));
        }

        void NotifyImpacts()
        {
            int impacts = Mathf.FloorToInt(_repairImpactCount + 0.001f);
            while (_notifiedImpacts < impacts)
            {
                _notifiedImpacts++;
                RepairSlapped?.Invoke();
            }
        }

        void SetState(GunClip state)
        {
            _state = state;
            _elapsed = 0f;
            _released = false;
            Progress = 0f;
            _returnFromSequence = false;
            _repairImpactCount = 0f;
            _repairPunch = 0f;
            _notifiedImpacts = 0;
        }

        float ClipLength(GunClip clip)
        {
            EnsureGraph();
            return _clipLengths[(int)clip];
        }

        void EnsureGraph()
        {
            if (_graph.IsValid())
                return;
            _overrides = _animator.runtimeAnimatorController as AnimatorOverrideController;
            for (int i = 0; i < ClipNames.Length; i++)
            {
                AnimationClip clip = _overrides != null ? _overrides[ClipNames[i]] : null;
                _clipLengths[i] = clip != null ? Mathf.Max(0.001f, clip.length) : 1f;
            }
            if (_returnParts == null)
            {
                _returnParts = GetComponentsInChildren<Transform>(true);
                _returnPositions = new Vector3[_returnParts.Length];
                _returnRotations = new Quaternion[_returnParts.Length];
                _returnGrips = GetComponentsInChildren<HandGrip>(true);
                _returnShapes = new HandShape[_returnGrips.Length];
            }
            // An attached controller would run again after Player.Update and overwrite the manually sampled pose.
            _animator.runtimeAnimatorController = null;
            _rig.enabled = false;
            _graph = PlayableGraph.Create(name + " Weapon Animation");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _controller = AnimatorControllerPlayable.Create(_graph, _overrides);
            _controller.SetSpeed(0d);
            var output = AnimationPlayableOutput.Create(_graph, "Weapon", _animator);
            output.SetSourcePlayable(_controller);
            _rig.Build(_graph);
            _graph.Play();
        }

        void Evaluate(float progress)
        {
            EnsureGraph();
            Progress = progress;
            _controller.Play(StateHashes[(int)_state], 0, progress);
            _rig.SyncLayers();
            _graph.Evaluate(0f);
        }

        void ReleaseMagazine()
        {
            _released = true;
            if (_effects == null || _magazine == null || _droppedMagazine == null)
                return;
            PooledEffect dropped = _effects.Spawn(_droppedMagazine, _magazine.position, _magazine.rotation);
            if (dropped != null && dropped.TryGetComponent(out Rigidbody body))
            {
                body.linearVelocity = transform.TransformDirection(_dropVelocity);
                body.angularVelocity = transform.TransformDirection(_dropAngularVelocity);
                body.WakeUp();
            }
        }
    }
}
