using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Chooses whether a spawned Reaction object follows a live text unit or uses a captured outgoing-unit pose.</summary>
    public enum TextStudioReactionSpawnAnchor
    {
        LiveUnit,
        OutgoingUnitSnapshot
    }

    /// <summary>Plays one pooled prefab per selected unit; particle prefabs use the same lifetime.</summary>
    [CreateAssetMenu(fileName = "Prefab Reaction", menuName = "Text Studio/Reactions/Prefab", order = 105)]
    public sealed class TextStudioReactionPrefabActionAsset : TextStudioReactionActionAsset
    {
        [SerializeField] GameObject _prefab;
        [SerializeField] TextStudioReactionSpawnAnchor _anchor = TextStudioReactionSpawnAnchor.LiveUnit;
        [SerializeField, Tooltip("Optional renderer anchor, such as a 3D socket name or Top, Bottom, Front, or Back. Empty uses the standard unit anchor.")]
        string _anchorKey;
        [SerializeField] bool _followLiveUnit;
        [SerializeField] bool _flyToTarget;
        [SerializeField, Tooltip("Treat this prefab as decorative motion. Reduced motion scales its movement and size; No Motion skips it. Arrival-driven gameplay prefabs must leave this off.")]
        bool _respectMotionPolicy;
        [SerializeField, Tooltip("Offset from the selected anchor, rotated with the anchor.")]
        Vector3 _localOffset;
        [SerializeField, Tooltip("Additional rotation after the selected anchor's rotation.")]
        Vector3 _localEulerAngles;
        [SerializeField, Tooltip("Multiplies the prefab's authored local scale.")]
        Vector3 _scaleMultiplier = Vector3.one;
        [SerializeField, Tooltip("Size the prefab and its offset with the text host's transform, so scaled or screen-projected text gets proportionate effects. Off keeps the prefab's authored world size.")]
        bool _scaleWithHost;
        [SerializeField, Range(0, 64)] int _prewarmCount = 1;
        [SerializeField, Range(0, 256), Tooltip("Maximum simultaneous prefabs from this action across its reaction layers. Zero uses the recipe limit.")]
        int _maxConcurrentInstances;

        public GameObject Prefab { get => _prefab; set { _prefab = value; NotifyChanged(); } }
        public TextStudioReactionSpawnAnchor Anchor { get => _anchor; set { _anchor = value; NotifyChanged(); } }
        public string AnchorKey { get => _anchorKey ?? string.Empty; set { _anchorKey = value; NotifyChanged(); } }
        public bool FollowLiveUnit { get => _followLiveUnit; set { _followLiveUnit = value; NotifyChanged(); } }
        public bool FlyToTarget { get => _flyToTarget; set { _flyToTarget = value; NotifyChanged(); } }
        public bool RespectMotionPolicy { get => _respectMotionPolicy; set { _respectMotionPolicy = value; NotifyChanged(); } }
        public Vector3 LocalOffset { get => _localOffset; set { _localOffset = value; NotifyChanged(); } }
        public Vector3 LocalEulerAngles { get => _localEulerAngles; set { _localEulerAngles = value; NotifyChanged(); } }
        public Vector3 ScaleMultiplier { get => _scaleMultiplier; set { _scaleMultiplier = value; NotifyChanged(); } }
        public bool ScaleWithHost { get => _scaleWithHost; set { _scaleWithHost = value; NotifyChanged(); } }
        public int PrewarmCount { get => _prewarmCount; set { _prewarmCount = value; NotifyChanged(); } }
        public int MaxConcurrentInstances { get => _maxConcurrentInstances; set { _maxConcurrentInstances = value; NotifyChanged(); } }

        public override bool TryCompile(out ITextStudioReactionCommand command, out string error)
        {
            if (_prefab == null || _prewarmCount < 0 || _prewarmCount > 64 ||
                _maxConcurrentInstances < 0 || _maxConcurrentInstances > 256 ||
                _respectMotionPolicy && _flyToTarget ||
                _followLiveUnit && _anchor != TextStudioReactionSpawnAnchor.LiveUnit ||
                (uint)_anchor > (uint)TextStudioReactionSpawnAnchor.OutgoingUnitSnapshot ||
                !Finite(_localOffset) || !Finite(_localEulerAngles) ||
                !Finite(_scaleMultiplier))
            {
                command = null;
                error = "Prefab reactions need a prefab, finite pose settings, a valid anchor mode, a prewarm count from 0 to 64, and a concurrent instance limit from 0 to 256; outgoing snapshots cannot follow a live unit, and decorative motion cannot drive arrival events.";
                return false;
            }
            command = new Command(_prefab, _anchor, _followLiveUnit, _flyToTarget,
                _respectMotionPolicy, _scaleWithHost,
                _localOffset, Quaternion.Euler(_localEulerAngles), _scaleMultiplier,
                string.IsNullOrWhiteSpace(_anchorKey) ? null : _anchorKey.Trim(),
                _prewarmCount, _maxConcurrentInstances);
            error = null;
            return true;
        }

        static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        sealed class Command : ITextStudioReactionCommand, ITextStudioReactionPrewarmableCommand,
            ITextStudioReactionAnchorSelectionCommand, ITextStudioReactionConcurrencyCommand,
            ITextStudioReactionTriggerAnchorCommand, ITextStudioReactionStartPolicy
        {
            readonly GameObject _prefab;
            readonly TextStudioReactionSpawnAnchor _anchor;
            readonly bool _followLiveUnit, _flyToTarget, _respectMotionPolicy, _scaleWithHost;
            readonly Vector3 _localOffset, _scaleMultiplier;
            readonly Quaternion _localRotation;
            readonly string _anchorKey;
            internal Command(GameObject prefab, TextStudioReactionSpawnAnchor anchor,
                bool followLiveUnit, bool flyToTarget, bool respectMotionPolicy, bool scaleWithHost,
                Vector3 localOffset,
                Quaternion localRotation, Vector3 scaleMultiplier, string anchorKey,
                int prewarmCount, int maxConcurrent)
            {
                _prefab = prefab;
                _anchor = anchor;
                _followLiveUnit = followLiveUnit;
                _flyToTarget = flyToTarget;
                _respectMotionPolicy = respectMotionPolicy;
                _scaleWithHost = scaleWithHost;
                _localOffset = localOffset;
                _localRotation = localRotation;
                _scaleMultiplier = scaleMultiplier;
                _anchorKey = anchorKey;
                PrewarmCount = maxConcurrent > 0 ? Math.Min(prewarmCount, maxConcurrent) : prewarmCount;
                MaxConcurrent = maxConcurrent;
            }

            public int PrewarmCount { get; }
            public int MaxConcurrent { get; }
            public bool CanStart(in TextStudioReactionContext context,
                ITextStudioReactionActionHost host) =>
                host != null && (!_respectMotionPolicy ||
                    TextStudioMotionSettings.Resolve(host.MotionPolicy) != TextStudioMotionPolicy.None);
            public bool CaptureTriggerAnchor =>
                _anchor == TextStudioReactionSpawnAnchor.LiveUnit && !_followLiveUnit;
            public string AnchorKey => _anchorKey;
            public TextStudioReactionActionRequirements Requirements =>
                TextStudioReactionActionRequirements.WorldObjectOutput |
                (_anchor == TextStudioReactionSpawnAnchor.OutgoingUnitSnapshot
                    ? TextStudioReactionActionRequirements.RetainedAnchor
                    : TextStudioReactionActionRequirements.LiveAnchor) |
                (_flyToTarget ? TextStudioReactionActionRequirements.NamedTarget : 0);
            public TextStudioReactionActionEvents SupportedEvents => _flyToTarget
                ? TextStudioReactionActionEvents.Arrived : TextStudioReactionActionEvents.None;
            public TextStudioReactionPreviewCapability PreviewCapability =>
                TextStudioReactionPreviewCapability.PlayModeOnly;
            public TextStudioReactionCaptureIntent CaptureIntent => TextStudioReactionCaptureIntent.Copy;
            public TextStudioReactionResourceClass CaptureResources => TextStudioReactionResourceClass.None;
            public ITextStudioReactionActionState CreateState() =>
                new State(_prefab, _anchor, _followLiveUnit, _flyToTarget,
                    _respectMotionPolicy, _scaleWithHost,
                    _localOffset, _localRotation, _scaleMultiplier, _anchorKey);
        }

        sealed class State : ITextStudioReactionActionState, ITextStudioReactionPrewarmableState
        {
            readonly GameObject _prefab;
            readonly TextStudioReactionSpawnAnchor _anchor;
            readonly bool _followLiveUnit, _flyToTarget, _respectMotionPolicy, _scaleWithHost;
            readonly Vector3 _localOffset, _scaleMultiplier;
            readonly Quaternion _localRotation;
            readonly string _anchorKey;
            GameObject _root, _instance;
            Transform _output;
            Vector3 _prefabLocalScale;
            ParticleSystem[] _particles;
            ITextStudioReactionActionHost _host;
            Vector3 _origin;
            Quaternion _originRotation;
            float _motionScale = 1f;
            int _targetSlot = -1;

            internal State(GameObject prefab, TextStudioReactionSpawnAnchor anchor,
                bool followLiveUnit, bool flyToTarget, bool respectMotionPolicy, bool scaleWithHost,
                Vector3 localOffset,
                Quaternion localRotation, Vector3 scaleMultiplier, string anchorKey)
            {
                _prefab = prefab;
                _anchor = anchor;
                _followLiveUnit = followLiveUnit;
                _flyToTarget = flyToTarget;
                _respectMotionPolicy = respectMotionPolicy;
                _scaleWithHost = scaleWithHost;
                _localOffset = localOffset;
                _localRotation = localRotation;
                _scaleMultiplier = scaleMultiplier;
                _anchorKey = anchorKey;
            }

            public void Prewarm(ITextStudioReactionActionHost host)
            {
                if (!Application.isPlaying || _root != null || host == null ||
                    _respectMotionPolicy && TextStudioMotionSettings.Resolve(host.MotionPolicy) ==
                    TextStudioMotionPolicy.None ||
                    !(host is Component component) || component == null) return;
                _root = new GameObject("Text Studio Reaction Prefab Pool");
                _root.hideFlags = HideFlags.HideInHierarchy;
                _root.SetActive(false);
                // Host-scaled prefabs inherit the text's transform; others keep their authored world size.
                _root.transform.SetParent(component.transform, !_scaleWithHost);
                try
                {
                    _instance = UnityEngine.Object.Instantiate(_prefab, _root.transform, false);
                    _output = _instance.transform;
                    _prefabLocalScale = _output.localScale;
                    _particles = _instance.GetComponentsInChildren<ParticleSystem>(true);
                }
                catch
                {
                    UnityEngine.Object.Destroy(_root);
                    _root = null;
                    _instance = null;
                    _output = null;
                    _particles = null;
                    throw;
                }
            }

            public void Start(in TextStudioReactionContext context,
                ITextStudioReactionActionHost host, in TextStudioReactionLease lease)
            {
                Prewarm(host);
                if (_root == null || _output == null)
                    throw new InvalidOperationException("The prefab output is unavailable in Play Mode.");
                bool anchored = _anchor == TextStudioReactionSpawnAnchor.OutgoingUnitSnapshot ||
                                lease.IsValid && !_followLiveUnit
                    ? lease.TryGetAnchor(out TextStudioReactionAnchor retained) &&
                      TrySetOrigin(in retained)
                    : TryGetLiveAnchor(host, in context, out TextStudioReactionAnchor live) &&
                      TrySetOrigin(in live);
                if (!anchored)
                    throw new InvalidOperationException("The prefab needs a world-space unit anchor.");
                _host = host;
                _motionScale = _respectMotionPolicy &&
                    TextStudioMotionSettings.Resolve(host.MotionPolicy) == TextStudioMotionPolicy.Reduced
                    ? .35f : 1f;
                _targetSlot = _flyToTarget ? host.ResolveReactionTargetSlot(context.TargetBindingKey) : -1;
                if (_flyToTarget && (_targetSlot < 0 || !host.TryGetReactionTarget(_targetSlot, out _)))
                    throw new InvalidOperationException("The prefab's named fly-to target is unavailable.");
                ApplyOriginPose();
                _root.SetActive(true);
                for (int i = 0; i < _particles.Length; i++)
                {
                    _particles[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    _particles[i].Play(false);
                }
            }

            bool TrySetOrigin(in TextStudioReactionAnchor anchor)
            {
                if (anchor.Space != TextStudioReactionAnchorSpace.World) return false;
                _origin = anchor.Position;
                _originRotation = anchor.Rotation;
                return true;
            }

            bool TryGetLiveAnchor(ITextStudioReactionActionHost host,
                in TextStudioReactionContext context, out TextStudioReactionAnchor anchor)
            {
                if (string.IsNullOrEmpty(_anchorKey))
                    return host.TryGetReactionAnchor(in context, out anchor);
                if (host is ITextStudioReactionNamedAnchorHost namedHost)
                    return namedHost.TryGetReactionAnchor(in context, _anchorKey, out anchor);
                anchor = default;
                return false;
            }

            Vector3 WorldOffset() => _originRotation * (_localOffset * (_motionScale * HostScale()));

            float HostScale()
            {
                if (!_scaleWithHost || _root == null) return 1f;
                Vector3 scale = _root.transform.lossyScale;
                return Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            }

            void ApplyOriginPose()
            {
                _output.SetPositionAndRotation(_origin + WorldOffset(),
                    _originRotation * _localRotation);
                _output.localScale = Vector3.Scale(_prefabLocalScale, _scaleMultiplier) * _motionScale;
            }

            public TextStudioReactionActionSample Sample(in TextStudioReactionContext context,
                float progress, double hostTime)
            {
                if (_root == null || !_root.activeSelf) return default;
                if (_followLiveUnit && TryGetLiveAnchor(_host, in context,
                        out TextStudioReactionAnchor live) &&
                    TrySetOrigin(in live))
                    ApplyOriginPose();
                if (!_flyToTarget)
                    return default;
                if (!_host.TryGetReactionTarget(_targetSlot, out TextStudioReactionAnchor target) ||
                    target.Space != TextStudioReactionAnchorSpace.World)
                    throw new InvalidOperationException("The prefab's fly-to target was lost.");
                float t = Mathf.Clamp01(progress);
                t = t * t * (3f - 2f * t);
                _output.position = Vector3.LerpUnclamped(
                    _origin + WorldOffset(), target.Position, t);
                return progress >= 1f
                    ? new TextStudioReactionActionSample(TextStudioReactionActionEvents.Arrived, hostTime)
                    : default;
            }

            public void Complete(in TextStudioReactionContext context) => Stop();
            public void Cancel(in TextStudioReactionContext context,
                TextStudioReactionCancelReason reason) => Stop();

            void Stop()
            {
                if (_root != null)
                {
                    for (int i = 0; i < _particles.Length; i++)
                        _particles[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    _root.SetActive(false);
                }
                _host = null;
                _targetSlot = -1;
            }

            public void Dispose()
            {
                Stop();
                if (_root != null) UnityEngine.Object.Destroy(_root);
                _root = null;
                _instance = null;
                _output = null;
                _particles = null;
            }
        }
    }
}
