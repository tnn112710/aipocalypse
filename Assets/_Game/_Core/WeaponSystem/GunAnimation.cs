using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;
using Aipocalypse.Core.HandSystem;

namespace Aipocalypse.Core.WeaponSystem
{
    [RequireComponent(typeof(Animator), typeof(RigBuilder))]
    public sealed class GunAnimation : MonoBehaviour
    {
        public const string Equipped = "Equipped";
        public const string Idle = "Idle";
        public const string Firing = "Firing";
        public const string Reloading = "Reloading";
        public const string ReloadingEmpty = "ReloadingEmpty";
        public const string Repairing = "Repairing";
        public const string RepairingStubborn = "RepairingStubborn";
        public const string RepairFocus = "RepairFocus";
        public const string RepairEnd = "RepairEnd";
        public const string Cycling = "Cycling";

        [SerializeField] Animator _animator;
        [SerializeField] RigBuilder _rig;
        [SerializeField] Transform _magazine;
        [SerializeField] PooledEffect _droppedMagazine;
        [SerializeField, Min(0)] int _magazinePrewarm = 3;
        [SerializeField, Range(0f, 1f)] float _releaseTime = 0.3f;
        [SerializeField] Vector3 _dropVelocity = new(-0.25f, -0.8f, 0.1f);
        [SerializeField] Vector3 _dropAngularVelocity = new(2f, 0f, -3f);
        [SerializeField, HideInInspector] float _repairImpactCount;
        // Stepped clip curve: 1 while the latest repair impact is a punch, 0 for a slap.
        [SerializeField, HideInInspector] float _repairPunch;

        PlayableGraph _graph;
        AnimatorControllerPlayable _controller;
        AnimatorOverrideController _overrides;
        EffectPool _effects;
        string _state = Idle;
        float _elapsed;
        bool _released;
        string _repairState = Repairing;
        bool _returnFromSequence;
        Transform[] _returnParts;
        Vector3[] _returnPositions;
        Quaternion[] _returnRotations;
        HandGrip[] _returnGrips;
        HandShape[] _returnShapes;
        int _notifiedImpacts;

        public event System.Action RepairSlapped;

        public string State => _state;
        public bool ImpactIsPunch => _repairPunch > 0.5f;
        public float Progress { get; private set; }

        public void Bind(EffectPool effects)
        {
            _effects = effects;
            _effects?.Prewarm(_droppedMagazine, _magazinePrewarm);
        }

        public void Show()
        {
            SetState(Equipped);
            Evaluate(0f);
        }

        public void Fired()
        {
            if (!IsAction)
                SetState(Firing);
        }

        public void StartAction(GunAction action, bool empty, bool stubborn = false)
        {
            _repairState = stubborn ? RepairingStubborn : Repairing;
            SetState(action switch
            {
                GunAction.Reloading => empty ? ReloadingEmpty : Reloading,
                GunAction.ClearingJam => _repairState,
                GunAction.Cycling => Cycling,
                _ => Idle,
            });
        }

        public void EndAction() => SetState(Idle);

        public void FocusRepair(bool focus)
        {
            int impacts = _notifiedImpacts;
            SetState(focus ? RepairFocus : _repairState);
            _notifiedImpacts = impacts;
        }
        public void FinishRepair()
        {
            bool capture = _state is Repairing or RepairingStubborn;
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
            SetState(RepairEnd);
            _returnFromSequence = capture;
        }

        public void Tick(float deltaTime, float actionProgress)
        {
            if (deltaTime <= 0f)
                return;
            if (_returnFromSequence)
            {
                _elapsed += deltaTime;
                float progress = Mathf.Clamp01(_elapsed / ClipLength(RepairEnd));
                if (progress >= 1f)
                {
                    SetState(Idle);
                    Evaluate(0f);
                }
                else
                {
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
                return;
            }
            if (IsAction && _state != RepairFocus)
            {
                Evaluate(Mathf.Clamp01(actionProgress));
                if (_state is Repairing or RepairingStubborn)
                {
                    int impacts = Mathf.FloorToInt(_repairImpactCount + 0.001f);
                    while (_notifiedImpacts < impacts)
                    {
                        _notifiedImpacts++;
                        RepairSlapped?.Invoke();
                    }
                }
                if (!_released && (_state is Reloading or ReloadingEmpty) && Progress >= _releaseTime)
                    ReleaseMagazine();
                return;
            }

            _elapsed += deltaTime;
            float duration = ClipLength(_state);
            bool loop = _state is Idle or RepairFocus;
            if (!loop && _elapsed >= duration)
            {
                _elapsed -= duration;
                _state = Idle;
                duration = ClipLength(Idle);
            }
            Evaluate(_state is Idle or RepairFocus ? Mathf.Repeat(_elapsed / duration, 1f) : Mathf.Clamp01(_elapsed / duration));
        }

        bool IsAction => _state is Reloading or ReloadingEmpty or Repairing or RepairingStubborn or RepairFocus or Cycling;

        void SetState(string state)
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

        float ClipLength(string state)
        {
            EnsureGraph();
            AnimationClip clip = _overrides[state];
            return clip != null ? Mathf.Max(0.001f, clip.length) : 1f;
        }

        void EnsureGraph()
        {
            if (_graph.IsValid())
                return;
            _overrides = _animator.runtimeAnimatorController as AnimatorOverrideController;
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

        void Evaluate(float progress, string sequence = null)
        {
            EnsureGraph();
            Progress = progress;
            _controller.Play(Animator.StringToHash("Base Layer." + (sequence ?? _state)), 0, progress);
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

        public void Stop()
        {
            if (_rig != null)
                _rig.Clear();
            if (_graph.IsValid())
                _graph.Destroy();
            if (_animator != null && _overrides != null)
                _animator.runtimeAnimatorController = _overrides;
            _overrides = null;
            SetState(Idle);
        }

        void OnDisable() => Stop();
    }
}
