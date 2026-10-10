using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio
{
    public sealed partial class TextStudioBinding :
        ITextStudioReactionUIParticleHost, ITextStudioReactionPendingAnchorHost
    {
        [SerializeField] TextStudioReactionRecipe _reactions = TextStudioReactionRecipe.None;
        [SerializeField] List<TextStudioReactionLayer> _inlineReactionLayers;
        [SerializeField] List<TextStudioReactionPanelTargetBinding> _reactionTargetBindings;
        Dictionary<string, VisualElement> _reactionTargetCache;
        IPanel _reactionTargetPanel;
        double _reactionTargetRetryAfter;
        TextStudioReactionAnchorCaptureProvider _reactionAnchorCapture;
        TextStudioUITKReactionParticles _reactionParticles;
#if UNITY_6000_2_OR_NEWER
        TextStudioUITKReactionCapture _reactionPanelCapture;
#endif
        string _lastReactionCaptureDiagnostic;

        public string LastReactionCaptureDiagnostic => _lastReactionCaptureDiagnostic;

        internal bool WantsReactionGlyphCapture =>
            _runtime.ReactionsRequireOutgoingCapture && Application.isPlaying &&
            _element?.panel != null;

#if UNITY_6000_2_OR_NEWER
        internal void CapturePresentedReactionGlyphs(TextElement.GlyphsEnumerable glyphs)
        {
            if (!WantsReactionGlyphCapture) return;
            _reactionPanelCapture ??= new TextStudioUITKReactionCapture(this);
            _reactionPanelCapture.CapturePresentedGlyphs(glyphs);
        }
#endif

        [UxmlObjectReference("reaction-target")]
        public List<TextStudioReactionPanelTargetBinding> ReactionTargets
        {
            get => _reactionTargetBindings;
            set
            {
                _reactionTargetBindings = value;
                RefreshReactionTargets();
                _runtime.SetReactionActionHost(this, deferLiveOutputSampling: true);
            }
        }

        TextStudioReactionActionRequirements ITextStudioReactionActionHost.Capabilities =>
            TextStudioReactionActionRequirements.LiveAnchor |
            TextStudioReactionActionRequirements.NamedTarget |
            (Application.isPlaying && _element?.panel != null
                ? TextStudioReactionActionRequirements.UIParticles
                : TextStudioReactionActionRequirements.None);

        bool ITextStudioReactionUIParticleHost.CanAcquireReactionUIParticles(int count)
        {
            if (!Application.isPlaying || _element?.panel == null) return false;
            _reactionParticles ??= new TextStudioUITKReactionParticles(this);
            return _reactionParticles.CanAcquire(count);
        }

        bool ITextStudioReactionUIParticleHost.TryAcquireReactionUIParticles(
            in TextStudioReactionAnchor anchor, int count,
            out TextStudioReactionUIParticleLease lease)
        {
            lease = default;
            if (!Application.isPlaying || _element?.panel == null) return false;
            _reactionParticles ??= new TextStudioUITKReactionParticles(this);
            return _reactionParticles.TryAcquire(in anchor, count, out lease);
        }

        /// <summary>Clears cached UI Toolkit target elements so future reaction target lookups resolve them again.</summary>
        public void RefreshReactionTargets()
        {
            if (_reactionTargetCache != null)
                foreach (KeyValuePair<string, VisualElement> entry in _reactionTargetCache)
                    entry.Value?.UnregisterCallback<DetachFromPanelEvent>(HandleReactionTargetDetached);
            _reactionTargetCache?.Clear();
            _reactionTargetPanel = null;
            _reactionTargetRetryAfter = 0d;
        }

        void HandleReactionTargetDetached(DetachFromPanelEvent evt) => RefreshReactionTargets();

        /// <summary>Attempts to resolve a configured UI Toolkit target to an anchor on the binding's attached panel using its binding key.</summary>
        public bool TryGetReactionTarget(string key, out TextStudioReactionAnchor anchor)
        {
            return TryGetReactionTarget(ResolveReactionTargetSlot(key), out anchor);
        }

        /// <summary>Finds the exact named reaction target binding, or returns -1 when no binding matches.</summary>
        public int ResolveReactionTargetSlot(string key)
        {
            if (string.IsNullOrEmpty(key) || _reactionTargetBindings == null) return -1;
            for (int i = 0; i < _reactionTargetBindings.Count; i++)
                if (_reactionTargetBindings[i] != null &&
                    string.Equals(_reactionTargetBindings[i].Key, key, StringComparison.Ordinal))
                    return i;
            return -1;
        }

        /// <summary>Attempts to resolve a configured UI Toolkit target to an anchor on the binding's attached panel using its slot.</summary>
        public bool TryGetReactionTarget(int slot, out TextStudioReactionAnchor anchor)
        {
            anchor = default;
            TextElement element = _element;
            if (_reactionTargetBindings == null || slot < 0 || slot >= _reactionTargetBindings.Count ||
                element == null || element.panel == null)
                return false;
            string key = _reactionTargetBindings[slot]?.Key;
            if (string.IsNullOrEmpty(key)) return false;
            IPanel panel = element.panel;
            if (_reactionTargetCache == null || _reactionTargetPanel != panel)
            {
                RefreshReactionTargets();
                _reactionTargetPanel = panel;
                if (_reactionTargetBindings != null && _reactionTargetBindings.Count > 0)
                {
                    _reactionTargetCache = new Dictionary<string, VisualElement>(
                        _reactionTargetBindings.Count, StringComparer.Ordinal);
                    for (int i = 0; i < _reactionTargetBindings.Count; i++)
                    {
                        TextStudioReactionPanelTargetBinding binding = _reactionTargetBindings[i];
                        if (binding == null || string.IsNullOrEmpty(binding.Key) ||
                            string.IsNullOrEmpty(binding.ElementName) ||
                            _reactionTargetCache.ContainsKey(binding.Key))
                            continue;
                        VisualElement target = panel.visualTree.Q<VisualElement>(binding.ElementName);
                        _reactionTargetCache.Add(binding.Key, target);
                        target?.RegisterCallback<DetachFromPanelEvent>(HandleReactionTargetDetached);
                    }
                    _reactionTargetRetryAfter = Time.realtimeSinceStartupAsDouble + .1d;
                }
            }
            if (_reactionTargetCache == null ||
                !_reactionTargetCache.TryGetValue(key, out VisualElement resolved))
                return false;
            if (resolved == null || resolved.panel != panel)
            {
                double now = Time.realtimeSinceStartupAsDouble;
                if (now < _reactionTargetRetryAfter) return false;
                _reactionTargetRetryAfter = now + .1d;
                for (int i = 0; i < _reactionTargetBindings.Count; i++)
                {
                    TextStudioReactionPanelTargetBinding binding = _reactionTargetBindings[i];
                    if (binding == null || !string.Equals(binding.Key, key, StringComparison.Ordinal))
                        continue;
                    resolved = panel.visualTree.Q<VisualElement>(binding.ElementName);
                    _reactionTargetCache[key] = resolved;
                    resolved?.RegisterCallback<DetachFromPanelEvent>(HandleReactionTargetDetached);
                    break;
                }
                if (resolved == null || resolved.panel != panel)
                    return false;
            }
            Rect bounds = resolved.worldBound;
            anchor = new TextStudioReactionAnchor(bounds.center,
                ResolvePanelRotation(resolved.worldTransform, Quaternion.identity),
                TextStudioReactionAnchorSpace.Panel);
            return true;
        }

        /// <summary>Attempts to retain reaction output from an attached Play Mode panel, recording a diagnostic if capture fails.</summary>
        public bool TryCaptureReactionOutput(in TextStudioReactionCaptureRequest request,
            out TextStudioReactionLease lease)
        {
            if (!Application.isPlaying || _element?.panel == null)
            {
                _lastReactionCaptureDiagnostic = "UI Toolkit Reaction capture requires an attached Play Mode panel.";
                lease = default;
                return false;
            }
            if (request.Resources != TextStudioReactionResourceClass.None)
            {
#if UNITY_6000_2_OR_NEWER
                if (_reactionPanelCapture != null)
                {
                    bool accepted = _reactionPanelCapture.TryCapture(in request, out lease);
                    _lastReactionCaptureDiagnostic = accepted ? null :
                        _reactionPanelCapture.QuerySupport(in request).Explanation;
                    return accepted;
                }
                _lastReactionCaptureDiagnostic =
                    "The UI Toolkit text has not published a retained glyph frame yet.";
#else
                _lastReactionCaptureDiagnostic =
                    "This Unity version does not expose the final UI Toolkit glyph callback required for retained output.";
#endif
                lease = default;
                return false;
            }
            _reactionAnchorCapture ??= new TextStudioReactionAnchorCaptureProvider(this);
            if (request.Context.Trigger == TextStudioReactionTrigger.TextChanged &&
                request.Changes.IsDefined)
            {
#if UNITY_6000_2_OR_NEWER
                if (_reactionPanelCapture != null &&
                    _reactionPanelCapture.TryGetPresentedOutgoingAnchor(in request,
                        out TextStudioReactionAnchor presentedAnchor) &&
                    _reactionAnchorCapture.TryCaptureAnchor(in presentedAnchor, out lease))
                {
                    _lastReactionCaptureDiagnostic = null;
                    return true;
                }
                _lastReactionCaptureDiagnostic =
                    "The old UI Toolkit glyph position was not presented before the text change.";
#else
                _lastReactionCaptureDiagnostic =
                    "This Unity version cannot capture the old UI Toolkit glyph position.";
#endif
                lease = default;
                return false;
            }
            bool captured = _reactionAnchorCapture.TryCapture(in request, out lease);
            _lastReactionCaptureDiagnostic = captured ? null :
                "The requested panel anchor is unavailable in the current layout.";
            return captured;
        }

        [UxmlAttribute("reaction-source")]
        public TextStudioReactionSource ReactionSource
        {
            get => _reactions.Source;
            set
            {
                if (_reactions.Source == value) return;
                _reactions.Source = value;
                if (value == TextStudioReactionSource.Inline)
                    CaptureInlineReactionLayers();
                SyncReactions();
                WakeAutomaticDriver();
            }
        }

        [UxmlAttribute("reaction-preset")]
        public string BuiltInReactionPresetId
        {
            get => _reactions.BuiltInPresetId ?? string.Empty;
            set
            {
                string normalized = value ?? string.Empty;
                if (string.Equals(_reactions.BuiltInPresetId, normalized, StringComparison.Ordinal)) return;
                _reactions.BuiltInPresetId = normalized;
                SyncReactions();
                WakeAutomaticDriver();
            }
        }

        [UxmlAttribute("reaction-capacity")]
        public int InlineReactionCapacity
        {
            get => _reactions.InlineCapacity;
            set
            {
                _reactions.SetInlineCapacity(value);
                SyncReactions();
                WakeAutomaticDriver();
            }
        }

        [UxmlAttribute("reaction-seed")]
        public int InlineReactionSeed
        {
            get => (int)_reactions.InlineSeed;
            set
            {
                if (_reactions.InlineSeed == (uint)Mathf.Max(1, value)) return;
                _reactions.SetInlineSeed(value);
                SyncReactions();
                WakeAutomaticDriver();
            }
        }

        [UxmlAttribute("reactions")]
        public TextStudioReactionAsset ReactionAsset
        {
            get => _reactions.CustomAsset;
            set
            {
                if (_reactions.CustomAsset == value) return;
                _reactions.CustomAsset = value;
                _reactions.Source = TextStudioReactionSource.Custom;
                SyncReactions();
                WakeAutomaticDriver();
            }
        }

        [UxmlObjectReference("reaction-layer")]
        public List<TextStudioReactionLayer> InlineReactionLayers
        {
            get => _inlineReactionLayers;
            set
            {
                _inlineReactionLayers = value;
                CaptureInlineReactionLayers();
                SyncReactions();
                WakeAutomaticDriver();
            }
        }

        public TextStudioReactionRecipe Reactions
        {
            get => _reactions;
            set
            {
                if (_reactions.Equals(value)) return;
                _reactions = value;
                TextStudioReactionLayer[] layers = _reactions.InlineLayers;
                _inlineReactionLayers = layers != null && layers.Length > 0
                    ? new List<TextStudioReactionLayer>(layers)
                    : null;
                SyncReactions();
                WakeAutomaticDriver();
            }
        }

        void CaptureInlineReactionLayers()
        {
            if (_inlineReactionLayers == null || _inlineReactionLayers.Count == 0)
            {
                _reactions.InlineLayers = Array.Empty<TextStudioReactionLayer>();
                return;
            }
            TextStudioReactionLayer[] layers = new TextStudioReactionLayer[_inlineReactionLayers.Count];
            _inlineReactionLayers.CopyTo(layers);
            _reactions.InlineLayers = layers;
        }

        /// <summary>Raised when a reaction emits a named signal, with the triggering text context.</summary>
        public event Action<TextStudioReactionContext> OnReactionSignal
        {
            add => _runtime.ReactionSignal += value;
            remove => _runtime.ReactionSignal -= value;
        }

        /// <summary>Raised after the retained text state commits when a reaction requests an Audio Set cue.</summary>
        public event Action<TextStudioReactionContext> OnReactionAudioCue
        {
            add => _runtime.ReactionAudioCue += value;
            remove => _runtime.ReactionAudioCue -= value;
        }

        /// <summary>
        /// Resolves a reaction's semantic unit to UI Toolkit panel coordinates. A context
        /// without a unit index resolves to the center of the current text layout.
        /// </summary>
        public bool TryGetReactionAnchor(
            in TextStudioReactionContext context,
            out TextStudioReactionAnchor anchor)
        {
            if (!IsCurrentReactionContext(in context))
            {
                anchor = default;
                return false;
            }
            VisualElement element = _element;
            TextStudioCompiledPlan plan = _runtime.Plan;
            int unitCount = plan != null ? plan.VisibleGlyphCount : 0;
            Vector3 localPosition;
            Quaternion localRotation;
            if (element == null || element.panel == null)
            {
                anchor = default;
                return false;
            }

            if (context.UnitIndex < 0)
            {
                if (!TextStudioReactionAnchorUtility.TryResolveTextCenter(
                        _layoutPositions,
                        unitCount,
                        _placementEngine.Placements,
                        _placementEngine.Count,
                        _runtime.Animator.States,
                        _runtime.Animator.StateCount,
                        null,
                        out localPosition))
                {
                    Rect panelBounds = element.worldBound;
                    if (panelBounds.width <= 0f && panelBounds.height <= 0f)
                    {
                        anchor = default;
                        return false;
                    }
                    anchor = new TextStudioReactionAnchor(
                        panelBounds.center,
                        ResolvePanelRotation(element.worldTransform, Quaternion.identity),
                        TextStudioReactionAnchorSpace.Panel);
                    return true;
                }
                localRotation = Quaternion.identity;
            }
            else if (!TextStudioReactionAnchorUtility.TryResolveUnitPose(
                         context.UnitIndex,
                         _layoutPositions,
                         unitCount,
                         _placementEngine.Placements,
                         _placementEngine.Count,
                         _runtime.Animator.States,
                         _runtime.Animator.StateCount,
                         null,
                         out localPosition,
                         out localRotation))
            {
                anchor = default;
                return false;
            }

            Matrix4x4 panelTransform = element.worldTransform;
            Vector3 panelPosition = panelTransform.MultiplyPoint3x4(localPosition);
            anchor = new TextStudioReactionAnchor(
                panelPosition,
                ResolvePanelRotation(panelTransform, localRotation),
                TextStudioReactionAnchorSpace.Panel);
            return true;
        }

        bool ITextStudioReactionPendingAnchorHost.IsReactionAnchorPending(
            in TextStudioReactionContext context)
        {
            TextStudioCompiledPlan plan = _runtime.Plan;
            return IsCurrentReactionContext(in context) && _element?.panel != null &&
                   plan != null && context.UnitIndex >= 0 &&
                   context.UnitIndex < plan.VisibleGlyphCount;
        }

        static Quaternion ResolvePanelRotation(Matrix4x4 panelTransform, Quaternion localRotation)
        {
            Vector3 panelRight = panelTransform.MultiplyVector(localRotation * Vector3.right);
            if (panelRight.sqrMagnitude <= 0.000001f)
                panelRight = panelTransform.MultiplyVector(Vector3.right);
            float angle = Mathf.Atan2(panelRight.y, panelRight.x) * Mathf.Rad2Deg;
            return Quaternion.AngleAxis(angle, Vector3.forward);
        }

        public bool HasActiveReactions => _runtime.HasReactionWork;
        public int SuppressedReactionCount => _runtime.SuppressedReactions;
        public int CancelledReactionCount => _runtime.CancelledReactions;
        public TextStudioReactionCancelReason LastReactionCancellationReason =>
            _runtime.LastReactionCancellationReason;

        /// <summary>Invokes matching named responses on an attached Play Mode panel and wakes their automatic driver.</summary>
        public void PlayReaction(string signal, float intensity = 1f, int unitIndex = -1)
        {
            if (!Application.isPlaying || _element == null || _element.panel == null) return;
            SyncReactions();
            _runtime.SignalReactions(signal, intensity, unitIndex);
            if (_runtime.HasReactionWork) WakeAutomaticDriver();
        }

        /// <summary>Stops accepting new reaction triggers while existing finite responses finish.</summary>
        public void StopReactions() => _runtime.StopReactions();
        /// <summary>Cancels all active responses and invalidates their pending callbacks.</summary>
        public void CancelReactions() => _runtime.CancelReactions();
        bool NeedsReactionSynchronization =>
            _runtime.HasReactionRuntime ||
            (_behaviorProfile != null && _behaviorProfile.Provides(TextStudioBehaviorDomain.Reactions)
                ? _behaviorProfile.Reactions.Source != TextStudioReactionSource.None
                : _reactions.Source != TextStudioReactionSource.None);

        void SyncReactions()
        {
            _runtime.SetReactionActionHost(this, deferLiveOutputSampling: true);
            TextStudioReactionRecipe resolved = _behaviorProfile != null && _behaviorProfile.Provides(TextStudioBehaviorDomain.Reactions)
                ? _behaviorProfile.Reactions : _reactions;
            _runtime.ConfigureReactions(Application.isPlaying ? resolved : TextStudioReactionRecipe.None);
            if (!Application.isPlaying || resolved.Source == TextStudioReactionSource.None)
            {
                _reactionParticles?.Dispose();
                _reactionParticles = null;
            }
#if UNITY_6000_2_OR_NEWER
            if (!Application.isPlaying || !_runtime.ReactionsRequireOutgoingCapture)
            {
                _reactionPanelCapture?.Dispose();
                _reactionPanelCapture = null;
            }
            else if (_element?.panel != null && _reactionPanelCapture == null)
            {
                _reactionPanelCapture = new TextStudioUITKReactionCapture(this);
                _element.MarkDirtyRepaint();
            }
#endif
            if (_updateMode == TextStudioUITKUpdateMode.Automatic && Application.isPlaying)
                _runtime.SetReactionClock(Time.timeAsDouble);
        }

        bool IsCurrentReactionContext(in TextStudioReactionContext context) =>
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
    }
}
