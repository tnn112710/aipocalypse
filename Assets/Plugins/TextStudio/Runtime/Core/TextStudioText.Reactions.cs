using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TextStudio
{
    public partial class TextStudioText
    {
        [SerializeField] TextStudioReactionRecipe _reactions = TextStudioReactionRecipe.None;
        [NonSerialized] uint _reactionSerializationRevision = 1;
        [SerializeField] List<TextStudioReactionWorldTargetBinding> _reactionTargetBindings;
        TextStudioReactionAnchorCaptureProvider _reactionAnchorCapture;
        TextStudioReactionCompositeCaptureProvider _reactionCompositeCapture;
        ReactionCaptureCandidate[] _reactionCaptureCandidates;
        string _lastReactionCaptureDiagnostic;
        TextStudioCanvasReactionParticles _reactionCanvasParticles;

        struct ReactionCaptureCandidate
        {
            internal ITextStudioReactionCaptureProvider Provider;
            internal TextStudioReactionCaptureCapability Capability;
        }

        public IReadOnlyList<TextStudioReactionWorldTargetBinding> ReactionTargets =>
            _reactionTargetBindings ?? (IReadOnlyList<TextStudioReactionWorldTargetBinding>)Array.Empty<TextStudioReactionWorldTargetBinding>();

        internal bool ReactionsRequirePhysicsWorld => _runtime.ReactionsRequirePhysicsWorld;

        /// <summary>Assigns world target bindings and configures this text component as the reaction action host.</summary>
        public void SetReactionTargets(List<TextStudioReactionWorldTargetBinding> targets)
        {
            _reactionTargetBindings = targets;
            _runtime.SetReactionActionHost(this, deferLiveOutputSampling: true);
        }

        TextStudioReactionActionRequirements ITextStudioReactionActionHost.Capabilities =>
            TextStudioReactionActionRequirements.LiveAnchor |
            TextStudioReactionActionRequirements.NamedTarget |
            (_extensions != null && _extensions.HasReactionBodyProvider
                ? TextStudioReactionActionRequirements.PhysicsWorld
                : TextStudioReactionActionRequirements.None) |
            (SupportsWorldObjectReactionOutput()
                ? TextStudioReactionActionRequirements.WorldObjectOutput
                : TextStudioReactionActionRequirements.None) |
            (Application.isPlaying && ResolveReactionCanvasSource() != null
                ? TextStudioReactionActionRequirements.UIParticles
                : TextStudioReactionActionRequirements.None);

        bool ITextStudioReactionUIParticleHost.CanAcquireReactionUIParticles(int count)
        {
            RectTransform source = ResolveReactionCanvasSource();
            if (source == null) return false;
            if (_reactionCanvasParticles != null &&
                !_reactionCanvasParticles.MatchesSource(source))
            {
                _reactionCanvasParticles.Dispose();
                _reactionCanvasParticles = null;
            }
            if (_reactionCanvasParticles == null)
                _reactionCanvasParticles = new TextStudioCanvasReactionParticles(source);
            return _reactionCanvasParticles.CanAcquire(source, count);
        }

        bool ITextStudioReactionUIParticleHost.TryAcquireReactionUIParticles(
            in TextStudioReactionAnchor anchor, int count,
            out TextStudioReactionUIParticleLease lease)
        {
            lease = default;
            RectTransform source = ResolveReactionCanvasSource();
            if (source == null) return false;
            if (_reactionCanvasParticles != null &&
                !_reactionCanvasParticles.MatchesSource(source))
            {
                _reactionCanvasParticles.Dispose();
                _reactionCanvasParticles = null;
            }
            if (_reactionCanvasParticles == null)
                _reactionCanvasParticles = new TextStudioCanvasReactionParticles(source);
            return _reactionCanvasParticles.TryAcquire(source, in anchor, count, out lease);
        }

        RectTransform ResolveReactionCanvasSource()
        {
            if (!Application.isPlaying) return null;
            Component target = _activeTextBackend?.TargetObject as Component;
            if (target == null && _activeTextBackend != null) return null;
            RectTransform source = target != null
                ? target.transform as RectTransform
                : transform as RectTransform;
            return source != null && source.GetComponentInParent<Canvas>() != null
                ? source : null;
        }

        bool ITextStudioReactionBodyActionHost.TryReleaseReactionBody(
            in TextStudioReactionContext context,
            Vector3 velocity, Vector3 angularVelocity,
            out TextStudioReactionBodyEventSession session)
        {
            if (Application.isPlaying && IsCurrentReactionContext(in context) &&
                _extensions != null)
                return _extensions.TryReleaseReactionBody(context.UnitIndex,
                    velocity, angularVelocity, out session);
            session = default;
            return false;
        }

        bool ITextStudioReactionBodyActionHost.CanReleaseReactionBody(
            in TextStudioReactionContext context) =>
            Application.isPlaying && IsCurrentReactionContext(in context) &&
            _extensions != null &&
            _extensions.CanReleaseReactionBody(context.UnitIndex);

        bool SupportsWorldObjectReactionOutput()
        {
            if (!TryGetComponent<TextMeshProUGUI>(out _)) return true;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ||
                GetComponentInParent<Mask>() != null ||
                GetComponentInParent<RectMask2D>() != null ||
                GetComponentInParent<CanvasGroup>() != null)
                return false;
            return canvas.sortingOrder == 0 && !canvas.overrideSorting;
        }

        /// <summary>Attempts to resolve a configured world target to its current position and rotation using its binding key.</summary>
        public bool TryGetReactionTarget(string key, out TextStudioReactionAnchor anchor)
        {
            return TryGetReactionTarget(ResolveReactionTargetSlot(key), out anchor);
        }

        /// <summary>Finds the exact named world target binding, or returns -1 when no binding matches.</summary>
        public int ResolveReactionTargetSlot(string key)
        {
            if (string.IsNullOrEmpty(key) || _reactionTargetBindings == null) return -1;
            for (int i = 0; i < _reactionTargetBindings.Count; i++)
                if (_reactionTargetBindings[i] != null &&
                    string.Equals(_reactionTargetBindings[i].Key, key, StringComparison.Ordinal))
                    return i;
            return -1;
        }

        /// <summary>Attempts to resolve a configured world target to its current position and rotation using its slot.</summary>
        public bool TryGetReactionTarget(int slot, out TextStudioReactionAnchor anchor)
        {
            if (_reactionTargetBindings != null && slot >= 0 && slot < _reactionTargetBindings.Count)
            {
                Transform target = _reactionTargetBindings[slot]?.Target;
                if (target != null)
                {
                    anchor = new TextStudioReactionAnchor(target.position, target.rotation,
                        TextStudioReactionAnchorSpace.World);
                    return true;
                }
            }
            anchor = default;
            return false;
        }

        /// <summary>Attempts to retain supported reaction output in Play Mode and records the capture diagnostic.</summary>
        public bool TryCaptureReactionOutput(in TextStudioReactionCaptureRequest request,
            out TextStudioReactionLease lease)
        {
            if (!Application.isPlaying)
            {
                _lastReactionCaptureDiagnostic = "Reaction capture runs only in Play Mode.";
                lease = default;
                return false;
            }
            if (request.Resources == TextStudioReactionResourceClass.None)
            {
                _reactionAnchorCapture ??= new TextStudioReactionAnchorCaptureProvider(this);
                bool accepted = _reactionAnchorCapture.TryCapture(in request, out lease);
                _lastReactionCaptureDiagnostic = accepted ? null :
                    "The requested semantic anchor is not available in the current frame.";
                return accepted;
            }
            int candidateCount = 0;
            string unsupported = "No active renderer supports the requested reaction resources.";
            TextStudioReactionLease captured = default;
            int capturedCount = 0;
            int compositeSlot = -1;
            uint compositeVersion = 0;
            try
            {
                if (_activeTextBackend == null && _tmpBridge != null &&
                    IsTextBackendVisualOutputEnabled)
                {
                    TextStudioReactionCaptureCapability capability =
                        _tmpBridge.QuerySupport(in request);
                    if (capability.IsSupported)
                        AddReactionCaptureCandidate(_tmpBridge, in capability, ref candidateCount);
                    else if (!string.IsNullOrEmpty(capability.Explanation))
                        unsupported = capability.Explanation;
                }
                ITextStudioReactionCaptureProvider backendProvider =
                    _activeTextBackend as ITextStudioReactionCaptureProvider ??
                    (_activeTextBackend as ITextStudioReactionCaptureProviderSource)?.ReactionCaptureProvider;
                if (backendProvider != null &&
                    _activeTextBackend.IsAvailable &&
                    _activeMeshBackend != null && _activeMeshBackend.IsVisualOutputEnabled)
                {
                    TextStudioReactionCaptureCapability capability =
                        backendProvider.QuerySupport(in request);
                    if (capability.IsSupported)
                        AddReactionCaptureCandidate(backendProvider, in capability,
                            ref candidateCount);
                    else if (!string.IsNullOrEmpty(capability.Explanation))
                        unsupported = capability.Explanation;
                }
                for (int i = 0; i < _renderers.Count; i++)
                {
                    ITextStudioRenderer renderer = _renderers[i];
                    if (renderer == null ||
                        renderer is UnityEngine.Object unityObject && unityObject == null ||
                        !renderer.IsAvailable ||
                        renderer is Behaviour behaviour && !behaviour.isActiveAndEnabled ||
                        ReferenceEquals(renderer, _activeTextBackend) ||
                        !(renderer is ITextStudioReactionCaptureProvider provider))
                        continue;
                    TextStudioReactionCaptureCapability capability = provider.QuerySupport(in request);
                    if (!capability.IsSupported)
                    {
                        if (!string.IsNullOrEmpty(capability.Explanation))
                            unsupported = capability.Explanation;
                        continue;
                    }
                    AddReactionCaptureCandidate(provider, in capability, ref candidateCount);
                }
                if (candidateCount == 0)
                {
                    _lastReactionCaptureDiagnostic = unsupported;
                    lease = default;
                    return false;
                }
                if (candidateCount > 1)
                {
                    _reactionCompositeCapture ??= new TextStudioReactionCompositeCaptureProvider();
                    if (!_reactionCompositeCapture.TryBegin(candidateCount,
                            out compositeSlot, out compositeVersion))
                    {
                        _lastReactionCaptureDiagnostic = "The retained output capacity is exhausted.";
                        lease = default;
                        return false;
                    }
                }
                string limitation = null;
                for (int i = 0; i < candidateCount; i++)
                {
                    ReactionCaptureCandidate candidate = _reactionCaptureCandidates[i];
                    if (candidate.Capability.Support ==
                        TextStudioReactionCaptureSupport.SupportedWithDeclaredLimitations &&
                        limitation == null) limitation = candidate.Capability.Explanation;
                    if (!candidate.Provider.TryCapture(in request, out captured) || !captured.IsValid)
                    {
                        _lastReactionCaptureDiagnostic =
                            "A renderer declined outgoing capture after capability validation.";
                        break;
                    }
                    if (compositeSlot < 0)
                    {
                        _lastReactionCaptureDiagnostic = limitation;
                        lease = captured;
                        return true;
                    }
                    if (!_reactionCompositeCapture.Add(compositeSlot, compositeVersion, in captured))
                    {
                        _lastReactionCaptureDiagnostic = "The retained output capacity is exhausted.";
                        break;
                    }
                    capturedCount++;
                    captured = default;
                }
                if (compositeSlot >= 0 && capturedCount == candidateCount)
                {
                    lease = _reactionCompositeCapture.Seal(compositeSlot, compositeVersion);
                    if (lease.IsValid)
                    {
                        _lastReactionCaptureDiagnostic = limitation;
                        return true;
                    }
                }
            }
            catch (Exception exception)
            {
                _lastReactionCaptureDiagnostic = "The renderer threw during outgoing capture.";
                Debug.LogException(exception, this);
            }
            finally
            {
                if (candidateCount != 0)
                    Array.Clear(_reactionCaptureCandidates, 0, candidateCount);
            }
            try { captured.Release(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
            finally
            {
                if (compositeSlot >= 0)
                    _reactionCompositeCapture.Release(compositeSlot, compositeVersion);
            }
            lease = default;
            return false;
        }

        void AddReactionCaptureCandidate(ITextStudioReactionCaptureProvider provider,
            in TextStudioReactionCaptureCapability capability, ref int count)
        {
            if (_reactionCaptureCandidates == null)
                _reactionCaptureCandidates = new ReactionCaptureCandidate[4];
            else if (count == _reactionCaptureCandidates.Length)
                Array.Resize(ref _reactionCaptureCandidates, count * 2);
            _reactionCaptureCandidates[count++] = new ReactionCaptureCandidate
            {
                Provider = provider,
                Capability = capability
            };
        }

        /// <summary>Finite event responses, independent of typewriter timing and default effects.</summary>
        public TextStudioReactionRecipe Reactions
        {
            get => _reactions;
            set
            {
                if (_reactions.Equals(value)) return;
                bool previouslyRequiredOutgoingCapture = _runtime.ReactionsRequireOutgoingCapture;
                _reactions = value;
                SyncReactions();
                if (!previouslyRequiredOutgoingCapture &&
                    _runtime.ReactionsRequireOutgoingCapture &&
                    Application.isPlaying && isActiveAndEnabled &&
                    !string.IsNullOrEmpty(_markup))
                    ForceMeshRefresh(0f);
            }
        }

        /// <summary>Raised when a reaction emits a named signal, with the triggering text context.</summary>
        public event Action<TextStudioReactionContext> OnReactionSignal
        {
            add => _runtime.ReactionSignal += value;
            remove => _runtime.ReactionSignal -= value;
        }

        /// <summary>Raised after the rendered state commits when a reaction requests an Audio Set cue.</summary>
        public event Action<TextStudioReactionContext> OnReactionAudioCue
        {
            add => _runtime.ReactionAudioCue += value;
            remove => _runtime.ReactionAudioCue -= value;
        }

        /// <summary>
        /// Resolves a reaction's semantic unit to its current world-space text pose. A context
        /// without a unit index resolves to the center of the current text layout.
        /// </summary>
        bool ITextStudioReactionBooleanInputProvider.TryGetReactionBooleanInput(
            string key, out bool value)
        {
            for (int i = 0; i < _renderers.Count; i++)
            {
                ITextStudioRenderer renderer = _renderers[i];
                if (renderer == null ||
                    renderer is UnityEngine.Object unityObject && unityObject == null ||
                    !renderer.IsAvailable ||
                    renderer is Behaviour behaviour && !behaviour.isActiveAndEnabled ||
                    !(renderer is ITextStudioReactionBooleanInputProvider provider))
                    continue;
                if (provider.TryGetReactionBooleanInput(key, out value))
                    return true;
            }
            value = false;
            return false;
        }

        /// <summary>Attempts to resolve an active renderer's anchor for a reaction context belonging to the current text state.</summary>
        public bool TryGetReactionAnchor(
            in TextStudioReactionContext context,
            out TextStudioReactionAnchor anchor)
        {
            if (!IsCurrentReactionContext(in context))
            {
                anchor = default;
                return false;
            }
            for (int i = 0; i < _renderers.Count; i++)
            {
                ITextStudioRenderer renderer = _renderers[i];
                if (renderer == null ||
                    renderer is UnityEngine.Object unityObject && unityObject == null ||
                    !renderer.IsAvailable ||
                    renderer is Behaviour behaviour && !behaviour.isActiveAndEnabled ||
                    !(renderer is ITextStudioReactionAnchorProvider provider))
                    continue;
                return provider.TryGetReactionAnchor(context.UnitIndex, out anchor);
            }
            Vector3 localPosition;
            Quaternion localRotation;
            if (context.UnitIndex < 0)
            {
                if (!TextStudioReactionAnchorUtility.TryResolveTextCenter(
                        _layout.BasePositions,
                        _layout.GlyphCount,
                        _placementEngine.Placements,
                        _placementEngine.Count,
                        _runtime.Animator.States,
                        _runtime.Animator.StateCount,
                        _layout.EffectUnitScales,
                        out localPosition))
                {
                    Transform hostTransform = transform;
                    anchor = new TextStudioReactionAnchor(
                        hostTransform.position,
                        hostTransform.rotation,
                        TextStudioReactionAnchorSpace.World);
                    return true;
                }
                localRotation = Quaternion.identity;
            }
            else if (!TextStudioReactionAnchorUtility.TryResolveUnitPose(
                         context.UnitIndex,
                         _layout.BasePositions,
                         _layout.GlyphCount,
                         _placementEngine.Placements,
                         _placementEngine.Count,
                         _runtime.Animator.States,
                         _runtime.Animator.StateCount,
                         _layout.EffectUnitScales,
                         out localPosition,
                         out localRotation))
            {
                anchor = default;
                return false;
            }

            anchor = new TextStudioReactionAnchor(
                transform.TransformPoint(localPosition),
                transform.rotation * localRotation,
                TextStudioReactionAnchorSpace.World);
            return true;
        }

        bool ITextStudioReactionNamedAnchorHost.TryGetReactionAnchor(
            in TextStudioReactionContext context, string anchorKey,
            out TextStudioReactionAnchor anchor)
        {
            if (string.IsNullOrEmpty(anchorKey))
                return TryGetReactionAnchor(in context, out anchor);
            anchor = default;
            if (!IsCurrentReactionContext(in context)) return false;
            for (int i = 0; i < _renderers.Count; i++)
            {
                ITextStudioRenderer renderer = _renderers[i];
                if (renderer == null ||
                    renderer is UnityEngine.Object unityObject && unityObject == null ||
                    !renderer.IsAvailable ||
                    renderer is Behaviour behaviour && !behaviour.isActiveAndEnabled ||
                    !(renderer is ITextStudioReactionNamedAnchorProvider provider))
                    continue;
                return provider.TryGetReactionAnchor(context.UnitIndex, anchorKey, out anchor);
            }
            return false;
        }

        internal bool IsCurrentReactionContext(in TextStudioReactionContext context) =>
            (context.SessionId == 0 || context.SessionId == _runtime.ReactionSessionId) &&
            (context.Generation == 0 || context.Generation == _runtime.ReactionGeneration) &&
            (context.SourceGeneration == 0 ||
             context.SourceGeneration == _runtime.ReactionSourceGeneration);

        /// <summary>Returns the primary character only when the reaction context belongs to the current compiled text unit.</summary>
        public bool TryGetReactionUnitCharacter(in TextStudioReactionContext context, out char character)
        {
            TextStudioCompiledPlan plan = _runtime.Plan;
            if (IsCurrentReactionContext(in context) && plan != null &&
                context.UnitIndex >= 0 && context.UnitIndex < plan.VisibleGlyphCount)
            {
                character = plan.GetPrimaryChar(context.UnitIndex);
                return true;
            }
            character = default;
            return false;
        }

        /// <summary>Invokes matching named responses. Edit Mode is intentionally side-effect free.</summary>
        public void PlayReaction(string signal, float intensity = 1f, int unitIndex = -1)
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            SyncReactions();
            _runtime.SignalReactions(signal, intensity, unitIndex);
        }

        /// <summary>Stops accepting new triggers while existing finite responses finish.</summary>
        public void StopReactions() => _runtime.StopReactions();
        /// <summary>Cancels every response and invalidates pending callbacks.</summary>
        public void CancelReactions() => _runtime.CancelReactions();
        public bool HasActiveReactions => _runtime.HasReactionWork;
        public int SuppressedReactionCount => _runtime.SuppressedReactions;
        public int CancelledReactionCount => _runtime.CancelledReactions;
        public TextStudioReactionCancelReason LastReactionCancellationReason =>
            _runtime.LastReactionCancellationReason;
        public string LastReactionCaptureDiagnostic => _lastReactionCaptureDiagnostic;

        void SyncReactions()
        {
            _runtime.SetReactionActionHost(this, deferLiveOutputSampling: true);
            bool profileBacked = _behaviorProfile != null &&
                                 _behaviorProfile.Provides(TextStudioBehaviorDomain.Reactions);
            TextStudioReactionRecipe resolved = profileBacked
                ? _behaviorProfile.Reactions : _reactions;
            uint revision = profileBacked
                ? _behaviorProfile.ReactionSerializationRevision
                : _reactionSerializationRevision;
            _runtime.ConfigureReactions(Application.isPlaying ? resolved : TextStudioReactionRecipe.None,
                revision);
            PrepareReactionCaptureProvider();
            _runtime.SetReactionClock(_time);
        }

        void PrepareReactionCaptureProvider()
        {
            bool prepared = Application.isPlaying && _runtime.ReactionsRequireOutgoingCapture;
            if (_activeTextSourceBackend is ITextStudioReactionCapturePreparation direct)
            {
                direct.SetOutgoingCapturePrepared(prepared);
                return;
            }
            if (_activeTextSourceBackend is ITextStudioReactionCaptureProviderSource source &&
                source.ReactionCaptureProvider is ITextStudioReactionCapturePreparation provider)
                provider.SetOutgoingCapturePrepared(prepared);
        }
    }
}
