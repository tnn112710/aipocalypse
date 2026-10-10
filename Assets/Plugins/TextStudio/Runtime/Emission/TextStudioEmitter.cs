using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace TextStudio
{
    /// <summary>
    /// Owns bounded, temporary Text Studio hosts. Presentation remains in profiles and prefabs.
    /// </summary>
    [AddComponentMenu("Text Studio/Text Emitter")]
    [DisallowMultipleComponent]
    public sealed class TextStudioEmitter : MonoBehaviour
    {
        static readonly Vector3 InactiveHostLocalPosition =
            new Vector3(100000f, 100000f, 0f);

        [Header("Emission")]
        [SerializeField, Tooltip("Complete Text Studio host prefab used when a preset has no override.")]
        GameObject _defaultHostPrefab;
        [SerializeField, Tooltip("Preset used by EmitValue(value, target).")]
        TextStudioEmissionPreset _defaultPreset;
        [SerializeField, Tooltip("Optional parent for instantiated hosts. Empty uses this emitter.")]
        Transform _instanceParent;
        [SerializeField, Tooltip("Preset overrides to validate and prewarm with the emitter.")]
        List<TextStudioEmissionPreset> _registeredPresets = new List<TextStudioEmissionPreset>();

        [Header("Capacity")]
        [SerializeField, Min(0), Tooltip("Inactive hosts prepared for each registered host prefab.")]
        int _prewarmCount = 8;
        [SerializeField, Min(1), Tooltip("Maximum inactive hosts retained in each Unity object pool.")]
        int _maximumInactivePerHost = 32;
        [SerializeField, Min(1), Tooltip("Maximum active emissions across every host prefab.")]
        int _maximumActiveEmissions = 32;
        [SerializeField, Tooltip("Behavior when the active emission limit is reached.")]
        TextStudioEmissionOverflowPolicy _overflowPolicy =
            TextStudioEmissionOverflowPolicy.RecycleOldest;

        [Header("Placement")]
        [SerializeField, Tooltip("How request positions are applied to complete host prefabs.")]
        TextStudioEmissionOutputSpace _outputSpace =
            TextStudioEmissionOutputSpace.World;
        [SerializeField, Tooltip("Continue tracking request targets after emission.")]
        bool _followTargets = true;
        [SerializeField, Tooltip("Rotate world-space hosts to face the configured camera.")]
        bool _faceCamera = true;
        [SerializeField, Tooltip("Camera used for projection and camera-facing world text. Empty resolves Camera.main.")]
        Camera _camera;
        [SerializeField, Tooltip("Place matching anchors in stable vertical slots.")]
        bool _stackMatchingAnchors = true;
        [SerializeField, Min(0f), Tooltip("World units or canvas pixels between deterministic anchor slots.")]
        float _stackSpacing = 0.5f;

        readonly Dictionary<GameObject, ObjectPool<PooledHost>> _pools =
            new Dictionary<GameObject, ObjectPool<PooledHost>>();
        readonly List<PooledHost> _active = new List<PooledHost>(32);
        readonly List<PooledHost> _prewarmBuffer = new List<PooledHost>(32);
        bool[] _occupiedStackSlots = Array.Empty<bool>();
        PooledHost _deferredResetHost;

        Camera _resolvedCamera;
        RectTransform _parentRect;
        Canvas _parentCanvas;
        int _nextId;
        bool _tearingDown;

        /// <summary>Occurs after a new host accepts an emission.</summary>
        public event Action<TextStudioEmissionHandle> Emitted;
        /// <summary>Occurs after a request is combined with an active emission under its merge policy.</summary>
        public event Action<TextStudioEmissionHandle> Merged;
        /// <summary>Occurs immediately before an emission handle becomes invalid and its host returns to the pool.</summary>
        public event Action<TextStudioEmissionHandle> Released;

        public GameObject DefaultHostPrefab
        {
            get => _defaultHostPrefab;
            set => _defaultHostPrefab = value;
        }

        public TextStudioEmissionPreset DefaultPreset
        {
            get => _defaultPreset;
            set => _defaultPreset = value;
        }

        public Transform InstanceParent
        {
            get => _instanceParent;
            set
            {
                _instanceParent = value;
                ResolvePlacementContext();
                RefreshActivePlacements();
            }
        }

        public int PrewarmCount
        {
            get => _prewarmCount;
            set => _prewarmCount = Mathf.Max(0, value);
        }

        public int MaximumInactivePerHost
        {
            get => _maximumInactivePerHost;
            set => _maximumInactivePerHost = Mathf.Max(1, value);
        }

        public int MaximumActiveEmissions
        {
            get => _maximumActiveEmissions;
            set => _maximumActiveEmissions = Mathf.Max(1, value);
        }

        public TextStudioEmissionOverflowPolicy OverflowPolicy
        {
            get => _overflowPolicy;
            set => _overflowPolicy = value;
        }

        public TextStudioEmissionOutputSpace OutputSpace
        {
            get => _outputSpace;
            set
            {
                _outputSpace = value;
                RefreshActivePlacements();
            }
        }

        public bool FollowTargets
        {
            get => _followTargets;
            set
            {
                _followTargets = value;
                RefreshActivePlacements();
            }
        }

        public bool FaceCamera
        {
            get => _faceCamera;
            set
            {
                _faceCamera = value;
                RefreshActivePlacements();
            }
        }

        public Camera Camera
        {
            get => _camera;
            set
            {
                _camera = value;
                _resolvedCamera = value;
                RefreshActivePlacements();
            }
        }

        public bool StackMatchingAnchors
        {
            get => _stackMatchingAnchors;
            set => _stackMatchingAnchors = value;
        }

        public float StackSpacing
        {
            get => _stackSpacing;
            set
            {
                _stackSpacing = Mathf.Max(0f, value);
                RefreshActivePlacements();
            }
        }

        public int ActiveCount => _active.Count;

        public int InactiveCount
        {
            get
            {
                int count = 0;
                foreach (KeyValuePair<GameObject, ObjectPool<PooledHost>> pair in _pools)
                    count += pair.Value.CountInactive;
                return count;
            }
        }

        public int CreatedCount
        {
            get
            {
                int count = 0;
                foreach (KeyValuePair<GameObject, ObjectPool<PooledHost>> pair in _pools)
                    count += pair.Value.CountAll;
                return count;
            }
        }

        void Awake()
        {
            InitializePools();
        }

        void OnEnable()
        {
            ResolvePlacementContext();
            if (Application.isPlaying && _pools.Count == 0)
                InitializePools();
        }

        void OnValidate()
        {
            _prewarmCount = Mathf.Max(0, _prewarmCount);
            _maximumInactivePerHost = Mathf.Max(1, _maximumInactivePerHost);
            _maximumActiveEmissions = Mathf.Max(1, _maximumActiveEmissions);
            _stackSpacing = Mathf.Max(0f, _stackSpacing);
            if (Application.isPlaying)
                RefreshActivePlacements();
        }

        void Update()
        {
            float deltaTime = Mathf.Max(0f, Time.deltaTime);
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                PooledHost host = _active[i];
                if (host == null || !host.Active || host.Root == null)
                {
                    _active.RemoveAt(i);
                    continue;
                }

                host.MergeElapsed += deltaTime;
                if (RequiresContinuousPlacement(host))
                    UpdatePlacement(host);

                if (host.Dismissing)
                {
                    if (host.Hub == null || (!host.Hub.IsDisappearing && !host.Hub.HasActiveReactions))
                        ReleaseHost(host);
                    continue;
                }

                if (!host.ShowCompleted &&
                    host.Hub != null &&
                    !host.Hub.IsRevealing &&
                    host.Hub.RevealProgress >= 0.999f)
                {
                    host.ShowCompleted = true;
                    host.ActiveElapsed = 0f;
                }

                if (!host.ShowCompleted ||
                    host.Preset == null ||
                    host.Preset.ManualDismissal)
                {
                    continue;
                }

                host.ActiveElapsed += deltaTime;
                if (host.ActiveElapsed >= host.Preset.ActiveDuration)
                    DismissHost(host);
            }
        }

        void OnDisable()
        {
            TearDownPools();
        }

        void OnDestroy()
        {
            TearDownPools();
        }

        /// <summary>Formats and emits a number with the default preset at a target transform.</summary>
        public TextStudioEmissionHandle EmitValue(double value, Transform target)
        {
            return EmitValue(_defaultPreset, value, target);
        }

        /// <summary>Formats and emits a number with an explicit preset at a target transform.</summary>
        public TextStudioEmissionHandle EmitValue(
            TextStudioEmissionPreset preset,
            double value,
            Transform target)
        {
            return Emit(TextStudioEmissionRequest.ForValue(preset, value, target));
        }

        /// <summary>Formats and emits a number at a world position with an optional merge-group key.</summary>
        public TextStudioEmissionHandle EmitValue(
            TextStudioEmissionPreset preset,
            double value,
            Vector3 worldPosition,
            string groupKey = null)
        {
            TextStudioEmissionRequest request =
                TextStudioEmissionRequest.ForValue(preset, value, null);
            request.Position = worldPosition;
            request.GroupKey = groupKey ?? string.Empty;
            return Emit(request);
        }

        /// <summary>Emits text with an explicit preset at a target transform.</summary>
        public TextStudioEmissionHandle EmitText(
            TextStudioEmissionPreset preset,
            string text,
            Transform target)
        {
            return Emit(TextStudioEmissionRequest.ForText(preset, text, target));
        }

        /// <summary>Emits text at a world position with an optional merge-group key.</summary>
        public TextStudioEmissionHandle EmitText(
            TextStudioEmissionPreset preset,
            string text,
            Vector3 worldPosition,
            string groupKey = null)
        {
            TextStudioEmissionRequest request =
                TextStudioEmissionRequest.ForText(preset, text, null);
            request.Position = worldPosition;
            request.GroupKey = groupKey ?? string.Empty;
            return Emit(request);
        }

        /// <summary>Emits, replaces, or merges one complete request and returns a generation-safe handle.</summary>
        /// <returns>An invalid handle when the emitter is inactive, tearing down, or cannot resolve a host.</returns>
        public TextStudioEmissionHandle Emit(TextStudioEmissionRequest request)
        {
            if (!isActiveAndEnabled || _tearingDown)
                return TextStudioEmissionHandle.Invalid;

            TextStudioEmissionPreset preset =
                request.Preset != null ? request.Preset : _defaultPreset;
            if (preset == null)
                return TextStudioEmissionHandle.Invalid;

            request.Preset = preset;
            request.Text ??= string.Empty;
            request.GroupKey ??= string.Empty;
            if (request.Target != null)
                request.Position = request.Target.position;

            PooledHost mergeHost = FindMergeHost(request);
            if (mergeHost != null)
                return MergeInto(mergeHost, request);

            PooledHost recycledHost = null;
            if (_active.Count >= _maximumActiveEmissions)
            {
                if (_overflowPolicy == TextStudioEmissionOverflowPolicy.RejectNew)
                    return TextStudioEmissionHandle.Invalid;
                recycledHost = RecycleOldest(preset);
            }

            GameObject prefab = ResolveHostPrefab(preset);
            ObjectPool<PooledHost> pool = GetOrCreatePool(prefab);
            if (pool == null)
            {
                CompleteDeferredReset(recycledHost);
                return TextStudioEmissionHandle.Invalid;
            }

            PooledHost host = pool.Get();
            if (!ReferenceEquals(host, recycledHost))
                CompleteDeferredReset(recycledHost);
            if (host == null || host.Hub == null)
                return TextStudioEmissionHandle.Invalid;

            host.Pool = pool;
            host.Active = true;
            host.Id = NextId();
            host.Generation++;
            host.Preset = preset;
            host.Target = request.Target;
            host.Position = request.Position;
            host.Offset = request.Offset;
            host.GroupKey = request.GroupKey;
            host.Value = request.Value;
            host.IsValue = request.IsValue;
            host.ActiveElapsed = 0f;
            host.MergeElapsed = 0f;
            host.ShowCompleted = false;
            host.Dismissing = false;
            host.StackSlot = ResolveStackSlot(request);

            _active.Add(host);
            SubscribeHost(host);
            ApplyPreset(host);
            UpdatePlacement(host);

            string markup = ResolveMarkup(preset, request.IsValue, request.Value, request.Text);
            host.Hub.SetText(markup, false);
            host.Hub.StartReveal();
            if (!host.Hub.IsRevealing && host.Hub.RevealProgress >= 0.999f)
                host.ShowCompleted = true;

            TextStudioEmissionHandle handle = CreateHandle(host);
            Emitted?.Invoke(handle);
            return handle;
        }

        /// <summary>Creates inactive pooled hosts for the default and registered preset prefabs.</summary>
        public void Prewarm()
        {
            PrewarmPrefab(_defaultHostPrefab);
            for (int i = 0; i < _registeredPresets.Count; i++)
            {
                TextStudioEmissionPreset preset = _registeredPresets[i];
                if (preset != null && preset.HostPrefabOverride != null)
                    PrewarmPrefab(preset.HostPrefabOverride);
            }
        }

        /// <summary>Dismisses all active emissions immediately and returns their hosts to the pool.</summary>
        public void ReleaseAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                ReleaseHost(_active[i]);
        }

        internal bool IsHandleValid(int id, uint generation)
        {
            return FindHost(id, generation) != null;
        }

        internal bool TrySetText(int id, uint generation, string text)
        {
            PooledHost host = FindHost(id, generation);
            if (host == null || host.Dismissing)
                return false;

            host.IsValue = false;
            host.Hub.SetText(text ?? string.Empty);
            return true;
        }

        internal bool TrySetValue(int id, uint generation, double value)
        {
            PooledHost host = FindHost(id, generation);
            if (host == null || host.Dismissing || host.Preset == null)
                return false;

            host.IsValue = true;
            host.Value = value;
            host.Hub.SetText(host.Preset.FormatValue(value));
            return true;
        }

        internal bool TryDismiss(int id, uint generation)
        {
            PooledHost host = FindHost(id, generation);
            return host != null && DismissHost(host);
        }

        void InitializePools()
        {
            if (!Application.isPlaying || _tearingDown)
                return;
            ResolvePlacementContext();
            Prewarm();
        }

        void ResolvePlacementContext()
        {
            Transform parent = ResolveParent();
            _parentRect = parent as RectTransform;
            _parentCanvas = _parentRect != null
                ? _parentRect.GetComponentInParent<Canvas>()
                : null;
            _resolvedCamera = _camera != null ? _camera : Camera.main;
        }

        Transform ResolveParent()
        {
            return _instanceParent != null ? _instanceParent : transform;
        }

        GameObject ResolveHostPrefab(TextStudioEmissionPreset preset)
        {
            return preset != null && preset.HostPrefabOverride != null
                ? preset.HostPrefabOverride
                : _defaultHostPrefab;
        }

        ObjectPool<PooledHost> GetOrCreatePool(GameObject prefab)
        {
            if (prefab == null)
                return null;
            if (_pools.TryGetValue(prefab, out ObjectPool<PooledHost> existing))
                return existing;
            if (prefab.GetComponentInChildren<TextStudioText>(true) == null)
            {
                Debug.LogError(
                    "Text Studio Emitter requires a complete host prefab containing TextStudioText.",
                    this);
                return null;
            }

            int maximumInactive = _maximumInactivePerHost;
            ObjectPool<PooledHost> pool = new ObjectPool<PooledHost>(
                () => CreateHost(prefab, maximumInactive),
                ActivateHost,
                ResetHost,
                DestroyHost,
                true,
                Mathf.Max(1, Mathf.Min(_prewarmCount, maximumInactive)),
                maximumInactive);
            _pools.Add(prefab, pool);
            return pool;
        }

        PooledHost CreateHost(GameObject prefab, int poolMaximumInactive)
        {
            GameObject root = Instantiate(prefab, ResolveParent(), false);
            root.name = prefab.name + " (Pooled)";
            TextStudioText hub = root.GetComponentInChildren<TextStudioText>(true);
            PooledHost host = new PooledHost
            {
                Root = root,
                Hub = hub,
                OriginalBehaviorProfile = hub.BehaviorProfile,
                OriginalRevealEffects = hub.RevealEffectsEnabled,
                OriginalActiveEffects = hub.ActiveEffectsEnabled,
                OriginalHideEffects = hub.HideEffectsEnabled,
                OriginalLocalScale = root.transform.localScale,
                PoolMaximumInactive = poolMaximumInactive
            };
            host.ShowCompleteCallback = () => HandleShowComplete(host);
            host.DisappearCompleteCallback = () => HandleDisappearComplete(host);
            root.SetActive(true);
            hub.SetTextVisibility(false, false);
            return host;
        }

        static void ActivateHost(PooledHost host)
        {
            if (host?.Root == null)
                return;

            host.Root.SetActive(true);
            host.Hub?.ClearVisibilityOverrides();
        }

        void ResetHost(PooledHost host)
        {
            if (host == null)
                return;

            // An overflow release is usually rented again within the same Emit, and that rent replaces the text and
            // profile. Such a release keeps both until then instead of hiding and restoring them only to undo it.
            bool deferVisibleReset = ReferenceEquals(host, _deferredResetHost);
            _deferredResetHost = null;
            UnsubscribeHost(host);
            if (host.Hub != null)
            {
                host.Hub.StopReveal();
                host.Hub.StopDisappear();
                host.Hub.ClearVisibilityOverrides();
                host.Hub.ResetInternal();
                // Hiding prepares the reveal, which compiles pending text. Hiding before the original profile returns
                // keeps the outgoing text from recompiling only to be replaced: overflow reuses the host at once.
                if (!deferVisibleReset)
                    host.Hub.SetTextVisibility(false, false);
                host.Hub.ConfigureEffectFamilies(
                    host.OriginalRevealEffects,
                    host.OriginalActiveEffects,
                    host.OriginalHideEffects);
                if (!deferVisibleReset)
                    host.Hub.SetBehaviorProfile(host.OriginalBehaviorProfile);
            }
            host.ResetDeferred = deferVisibleReset && host.Hub != null;

            if (host.Root != null)
            {
                Transform hostTransform = host.Root.transform;
                hostTransform.SetParent(ResolveParent(), false);
                // A backend can finish its disappearance frame after the completion callback.
                // Park warm pooled roots offscreen so that late write cannot flash at the emitter origin.
                hostTransform.SetLocalPositionAndRotation(
                    InactiveHostLocalPosition,
                    Quaternion.identity);
                hostTransform.localScale = host.OriginalLocalScale;
            }

            host.Active = false;
            host.Preset = null;
            host.Target = null;
            host.GroupKey = string.Empty;
            host.IsValue = false;
            host.Value = 0d;
            host.ShowCompleted = false;
            host.Dismissing = false;
            host.ActiveElapsed = 0f;
            host.MergeElapsed = 0f;
            host.StackSlot = 0;
        }

        // Finishes a deferred overflow release when a Released handler or a different pool left the host retained.
        void CompleteDeferredReset(PooledHost host)
        {
            if (host == null || !host.ResetDeferred || host.Active)
                return;
            host.ResetDeferred = false;
            if (host.Hub == null)
                return;
            host.Hub.SetTextVisibility(false, false);
            host.Hub.SetBehaviorProfile(host.OriginalBehaviorProfile);
        }

        void DestroyHost(PooledHost host)
        {
            if (host == null)
                return;
            UnsubscribeHost(host);
            if (host.Root == null)
                return;
            if (Application.isPlaying)
                Destroy(host.Root);
            else
                DestroyImmediate(host.Root);
        }

        void PrewarmPrefab(GameObject prefab)
        {
            ObjectPool<PooledHost> pool = GetOrCreatePool(prefab);
            if (pool == null)
                return;

            int target = Mathf.Min(_prewarmCount, _maximumInactivePerHost);
            int needed = Mathf.Max(0, target - pool.CountInactive);
            _prewarmBuffer.Clear();
            for (int i = 0; i < needed; i++)
            {
                PooledHost host = pool.Get();
                host.Pool = pool;
                _prewarmBuffer.Add(host);
            }
            for (int i = 0; i < _prewarmBuffer.Count; i++)
                pool.Release(_prewarmBuffer[i]);
            _prewarmBuffer.Clear();
        }

        PooledHost FindMergeHost(TextStudioEmissionRequest request)
        {
            TextStudioEmissionPreset preset = request.Preset;
            if (preset == null ||
                preset.MergeMode == TextStudioEmissionMergeMode.None ||
                preset.MergeWindow <= 0f)
            {
                return null;
            }
            if (request.Target == null && string.IsNullOrEmpty(request.GroupKey))
                return null;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                PooledHost host = _active[i];
                if (!host.Active ||
                    host.Dismissing ||
                    host.Preset != preset ||
                    host.Target != request.Target ||
                    !string.Equals(host.GroupKey, request.GroupKey, StringComparison.Ordinal) ||
                    host.MergeElapsed > preset.MergeWindow)
                {
                    continue;
                }

                if (preset.MergeMode == TextStudioEmissionMergeMode.Sum &&
                    (!host.IsValue || !request.IsValue))
                {
                    continue;
                }
                return host;
            }
            return null;
        }

        TextStudioEmissionHandle MergeInto(
            PooledHost host,
            TextStudioEmissionRequest request)
        {
            if (host.Preset.MergeMode == TextStudioEmissionMergeMode.Sum)
            {
                host.Value += request.Value;
                host.IsValue = true;
            }
            else
            {
                host.Value = request.Value;
                host.IsValue = request.IsValue;
            }

            string markup = ResolveMarkup(
                host.Preset,
                host.IsValue,
                host.Value,
                request.Text);
            // Several merges in one frame compile and lay out only the last total, on the host's next update.
            host.Hub.SetTextDeferred(markup);
            if (host.Preset.RestartEffectsOnMerge)
                host.Hub.RestartEffects();
            host.ActiveElapsed = 0f;
            host.MergeElapsed = 0f;

            TextStudioEmissionHandle handle = CreateHandle(host);
            Merged?.Invoke(handle);
            return handle;
        }

        static string ResolveMarkup(
            TextStudioEmissionPreset preset,
            bool isValue,
            double value,
            string text)
        {
            return isValue
                ? preset.FormatValue(value)
                : text ?? string.Empty;
        }

        int ResolveStackSlot(TextStudioEmissionRequest request)
        {
            if (!_stackMatchingAnchors)
                return 0;
            if (request.Target == null && string.IsNullOrEmpty(request.GroupKey))
                return 0;

            // Matching hosts occupy at most one slot each, so the lowest free slot lies within the active count.
            int slotLimit = Mathf.Min(_maximumActiveEmissions, _active.Count + 1);
            if (_occupiedStackSlots.Length < slotLimit)
                _occupiedStackSlots = new bool[Mathf.NextPowerOfTwo(Mathf.Max(8, slotLimit))];
            Array.Clear(_occupiedStackSlots, 0, slotLimit);
            for (int i = 0; i < _active.Count; i++)
            {
                PooledHost active = _active[i];
                int slot = active.StackSlot;
                if (slot >= 0 &&
                    slot < slotLimit &&
                    active.Target == request.Target &&
                    string.Equals(active.GroupKey, request.GroupKey, StringComparison.Ordinal))
                {
                    _occupiedStackSlots[slot] = true;
                }
            }
            for (int candidate = 0; candidate < slotLimit; candidate++)
            {
                if (!_occupiedStackSlots[candidate])
                    return candidate;
            }
            return 0;
        }

        void UpdatePlacement(PooledHost host)
        {
            if (host.Root == null)
                return;

            Vector3 anchor = host.Target != null && _followTargets
                ? host.Target.position
                : host.Position;
            Vector3 stackOffset = Vector3.up * (_stackSpacing * host.StackSlot);
            Transform hostTransform = host.Root.transform;
            switch (_outputSpace)
            {
                case TextStudioEmissionOutputSpace.ParentLocal:
                    Transform parent = ResolveParent();
                    Vector3 localAnchor = parent != null
                        ? parent.InverseTransformPoint(anchor)
                        : anchor;
                    hostTransform.localPosition = localAnchor + host.Offset + stackOffset;
                    break;

                case TextStudioEmissionOutputSpace.ScreenProjected:
                    if (_parentRect == null)
                        break;
                    Camera projectionCamera = ResolveCamera();
                    Vector3 screenPoint = projectionCamera != null
                        ? projectionCamera.WorldToScreenPoint(anchor + host.Offset)
                        : anchor + host.Offset;
                    Camera canvasCamera = _parentCanvas != null &&
                                          _parentCanvas.renderMode ==
                                          RenderMode.ScreenSpaceOverlay
                        ? null
                        : projectionCamera;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            _parentRect,
                            screenPoint,
                            canvasCamera,
                            out Vector2 localPoint))
                    {
                        hostTransform.localPosition =
                            new Vector3(
                                localPoint.x,
                                localPoint.y + stackOffset.y,
                                hostTransform.localPosition.z);
                    }
                    break;

                default:
                    hostTransform.position = anchor + host.Offset + stackOffset;
                    if (_faceCamera)
                    {
                        Camera faceCamera = ResolveCamera();
                        if (faceCamera != null)
                            hostTransform.rotation = faceCamera.transform.rotation;
                    }
                    break;
            }
        }

        bool RequiresContinuousPlacement(PooledHost host)
        {
            return _outputSpace == TextStudioEmissionOutputSpace.ScreenProjected ||
                   (_followTargets && host.Target != null) ||
                   (_faceCamera && _outputSpace == TextStudioEmissionOutputSpace.World);
        }

        void RefreshActivePlacements()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                PooledHost host = _active[i];
                if (host != null && host.Active && host.Root != null)
                    UpdatePlacement(host);
            }
        }

        Camera ResolveCamera()
        {
            if (_camera != null)
                return _camera;
            if (_resolvedCamera == null)
                _resolvedCamera = Camera.main;
            return _resolvedCamera;
        }

        void ApplyPreset(PooledHost host)
        {
            TextStudioBehaviorProfile profile = host.Preset.BehaviorProfile != null
                ? host.Preset.BehaviorProfile
                : host.OriginalBehaviorProfile;
            uint revision = ProfileRevision(profile);
            bool keepAppliedProfile = host.ResetDeferred && KeepsAppliedProfile(host, profile, revision);
            host.ResetDeferred = false;
            if (keepAppliedProfile)
                return;
            host.Hub.SetBehaviorProfile(profile);
            host.AppliedProfileRevision = revision;
        }

        // A recycled host still runs the profile it last applied. Hosts with Reactions are reassigned anyway because the
        // restore and reassignment rebuild or rebind and reclock their reaction runtime.
        static bool KeepsAppliedProfile(
            PooledHost host,
            TextStudioBehaviorProfile profile,
            uint revision)
        {
            return ReferenceEquals(host.Hub.BehaviorProfile, profile) &&
                   host.AppliedProfileRevision == revision &&
                   host.Hub.Reactions.Source == TextStudioReactionSource.None &&
                   !ProvidesReactions(profile) &&
                   !ProvidesReactions(host.OriginalBehaviorProfile);
        }

        static uint ProfileRevision(TextStudioBehaviorProfile profile)
        {
            return profile != null ? profile.ReactionSerializationRevision : 0u;
        }

        static bool ProvidesReactions(TextStudioBehaviorProfile profile)
        {
            return profile != null &&
                   profile.Provides(TextStudioBehaviorDomain.Reactions);
        }

        bool DismissHost(PooledHost host)
        {
            if (host == null || !host.Active || host.Dismissing)
                return false;
            host.Dismissing = true;
            host.Hub.StartDisappear();
            return true;
        }

        void HandleShowComplete(PooledHost host)
        {
            if (host == null || !host.Active)
                return;
            host.ShowCompleted = true;
            host.ActiveElapsed = 0f;
        }

        void HandleDisappearComplete(PooledHost host)
        {
            if (host == null || !host.Active)
                return;
            host.Dismissing = true;
            if (host.Hub != null && host.Hub.HasActiveReactions) return;
            ReleaseHost(host);
        }

        void SubscribeHost(PooledHost host)
        {
            if (host.Subscribed || host.Hub == null)
                return;
            host.Hub.OnShowComplete += host.ShowCompleteCallback;
            host.Hub.OnDisappearComplete += host.DisappearCompleteCallback;
            host.Subscribed = true;
        }

        void UnsubscribeHost(PooledHost host)
        {
            if (!host.Subscribed || host.Hub == null)
                return;
            host.Hub.OnShowComplete -= host.ShowCompleteCallback;
            host.Hub.OnDisappearComplete -= host.DisappearCompleteCallback;
            host.Subscribed = false;
        }

        // Returns the released host whose hide and profile restore wait for its next rent, or null.
        PooledHost RecycleOldest(TextStudioEmissionPreset nextPreset)
        {
            if (_active.Count == 0)
                return null;
            PooledHost oldest = _active[0];
            bool deferReset = CanDeferRecycleReset(oldest, nextPreset);
            if (deferReset)
                _deferredResetHost = oldest;
            try
            {
                ReleaseHost(oldest);
            }
            finally
            {
                _deferredResetHost = null;
            }
            return deferReset ? oldest : null;
        }

        // The next emission normally rents the host it just released when it resolves the same pool and that pool retains
        // the host. When it rents another one, CompleteDeferredReset finishes this release before Emit returns.
        bool CanDeferRecycleReset(PooledHost oldest, TextStudioEmissionPreset nextPreset)
        {
            if (oldest == null ||
                !oldest.Active ||
                oldest.Pool == null ||
                oldest.Hub == null ||
                oldest.Pool.CountInactive >= oldest.PoolMaximumInactive)
            {
                return false;
            }
            GameObject prefab = ResolveHostPrefab(nextPreset);
            return prefab != null &&
                   _pools.TryGetValue(prefab, out ObjectPool<PooledHost> pool) &&
                   ReferenceEquals(pool, oldest.Pool);
        }

        void ReleaseHost(PooledHost host)
        {
            if (host == null || !host.Active)
                return;

            TextStudioEmissionHandle handle = CreateHandle(host);
            _active.Remove(host);
            ObjectPool<PooledHost> pool = host.Pool;
            host.Active = false;
            if (pool != null)
                pool.Release(host);
            else
                DestroyHost(host);
            Released?.Invoke(handle);
        }

        PooledHost FindHost(int id, uint generation)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                PooledHost host = _active[i];
                if (host.Active && host.Id == id && host.Generation == generation)
                    return host;
            }
            return null;
        }

        TextStudioEmissionHandle CreateHandle(PooledHost host)
        {
            return new TextStudioEmissionHandle(this, host.Id, host.Generation);
        }

        int NextId()
        {
            _nextId++;
            if (_nextId == 0)
                _nextId++;
            return _nextId;
        }

        internal void TearDownPools()
        {
            if (_tearingDown)
                return;
            _tearingDown = true;
            ReleaseAll();
            foreach (KeyValuePair<GameObject, ObjectPool<PooledHost>> pair in _pools)
                pair.Value.Clear();
            _pools.Clear();
            _prewarmBuffer.Clear();
            _tearingDown = false;
        }

        sealed class PooledHost
        {
            public GameObject Root;
            public TextStudioText Hub;
            public TextStudioBehaviorProfile OriginalBehaviorProfile;
            public bool OriginalRevealEffects;
            public bool OriginalActiveEffects;
            public bool OriginalHideEffects;
            public Vector3 OriginalLocalScale;
            public ObjectPool<PooledHost> Pool;
            public Action ShowCompleteCallback;
            public Action DisappearCompleteCallback;
            public TextStudioEmissionPreset Preset;
            public Transform Target;
            public Vector3 Position;
            public Vector3 Offset;
            public string GroupKey;
            public double Value;
            public bool IsValue;
            public bool Active;
            public bool Subscribed;
            public bool ShowCompleted;
            public bool Dismissing;
            public int Id;
            public uint Generation;
            public int StackSlot;
            public float ActiveElapsed;
            public float MergeElapsed;
            public int PoolMaximumInactive;
            public uint AppliedProfileRevision;
            public bool ResetDeferred;
        }
    }
}
