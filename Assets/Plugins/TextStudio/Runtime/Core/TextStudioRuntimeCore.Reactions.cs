using System;
using System.Collections.Generic;

namespace TextStudio
{
    internal sealed partial class TextStudioRuntimeCore
    {
        TextStudioReactionRuntime _reactions;
        ITextStudioReactionActionHost _reactionActionHost;
        bool _deferReactionLiveOutputSampling;
        bool _reactionPostSamplePending;
        TextStudioReactionRecipe _reactionRecipe;
        int _reactionRecipeSignature;
        bool _reactionSignatureUsesRevisions;
        bool _hasReactionRecipe;
        bool _reactionRequiresMaterial;
        TextStudioSourceContextRequirements _reactionSourceRequirements;
        bool _reactionClockPrepared;
        uint _reactionEffectRevision;
        uint _reactionRegistryRevision;
        uint _reactionActionContentRevision;
        internal int CachedExternalEffectCount => _externalEffectPlans.Count;
        bool _suppressUnitReactions;
        bool _listeningForGlyphDisappearStart;
        bool _listeningForGlyphAppearanceComplete;
        uint _reactionSourceGeneration = 1;
        ulong _reactionTransactionId;
        TextStudioReactionChangeKind _reactionChangeKind;
        bool _reactionAwaitingReveal;
        TextStudioGlyphChangeSet _pendingReactionChanges;

        public event Action<TextStudioReactionContext> ReactionSignal;
        public event Action<TextStudioReactionContext> ReactionAudioCue;
        internal bool HasReactionWork => _reactions != null && _reactions.HasWork;
        internal bool HasReactionRuntime => _reactions != null;
        internal bool ReactionsRequireGeometry => _reactions != null && _reactions.RequiresGeometry;
        internal bool ReactionsRequireAnchor => _reactions != null && _reactions.RequiresAnchor;
        internal bool ReactionsRequireChangedUnits => _reactions != null && _reactions.RequiresChangedUnits;
        internal bool ReactionsRequireOutgoingCapture => _reactions != null && _reactions.RequiresOutgoingCapture;
        internal bool ReactionsRequirePhysicsWorld => _reactions != null && _reactions.RequiresPhysicsWorld;
        internal int SuppressedReactions => _reactions?.SuppressedCount ?? 0;
        internal int CancelledReactions => _reactions?.CancelledCount ?? 0;
        internal TextStudioReactionCancelReason LastReactionCancellationReason =>
            _reactions?.LastCancellationReason ?? TextStudioReactionCancelReason.ExplicitCancel;
        internal uint ReactionGeneration => _reactions?.Generation ?? 0;
        internal uint ReactionSessionId => _reactions?.SessionId ?? 0;
        internal uint ReactionSourceGeneration => _reactionSourceGeneration;
        internal void SetReactionActionHost(ITextStudioReactionActionHost host,
            bool deferLiveOutputSampling = false)
        {
            _reactionActionHost = host;
            _deferReactionLiveOutputSampling = deferLiveOutputSampling;
            _reactions?.BindHost(host, true);
        }

        void SampleReactionsBeforePresentation()
        {
            if (_reactions == null) return;
            _reactions.Sample(this, _reactionActionHost,
                deferLiveOutput: _deferReactionLiveOutputSampling);
            _reactionPostSamplePending = _deferReactionLiveOutputSampling;
        }

        internal void SampleReactionsAfterPresentation()
        {
            if (!_deferReactionLiveOutputSampling || _reactions == null)
                return;
            _reactions.Sample(this, _reactionActionHost,
                continueFrame: _reactionPostSamplePending);
            _reactionPostSamplePending = false;
        }

        internal void ConfigureReactions(TextStudioReactionAsset asset)
        {
            ConfigureReactions(TextStudioReactionRecipe.Custom(asset));
        }

        internal void ConfigureReactions(TextStudioReactionRecipe recipe)
        {
            ConfigureReactions(recipe, 0, false);
        }

        internal void ConfigureReactions(TextStudioReactionRecipe recipe,
            uint serializedRevision)
        {
            ConfigureReactions(recipe, serializedRevision, true);
        }

        void ConfigureReactions(TextStudioReactionRecipe recipe,
            uint serializedRevision, bool useRevisions)
        {
            if (recipe.Source == TextStudioReactionSource.None)
            {
                if (_hasReactionRecipe && _reactionRecipe.Source == TextStudioReactionSource.None &&
                    _reactions == null)
                    return;

                if (_reactions != null) ClearExternalEffectCache();
                _reactions?.Dispose(TextStudioReactionCancelReason.RecipeChanged);
                _reactions = null;
                _reactionRecipe = TextStudioReactionRecipe.None;
                _reactionRecipeSignature = 0;
                _reactionSignatureUsesRevisions = false;
                _hasReactionRecipe = true;
                _reactionClockPrepared = false;
                _reactionRequiresMaterial = false;
                _reactionSourceRequirements = TextStudioSourceContextRequirements.None;
                _pendingReactionChanges = default;
                _reactionPostSamplePending = false;
                SetListenForGlyphDisappearStarts(false);
                SetListenForGlyphAppearanceCompletions(false);
                return;
            }

            TextStudioRegistry.EnsureInitialized();
            int signature = useRevisions
                ? recipe.GetRuntimeSignature(serializedRevision)
                : recipe.GetContentSignature();
            if (_hasReactionRecipe && _reactionRecipe.Equals(recipe) &&
                _reactionRecipeSignature == signature &&
                _reactionSignatureUsesRevisions == useRevisions &&
                (_reactions != null || _reactionRegistryRevision == TextStudioRegistry.Effects.Revision) &&
                (_reactions == null || !_reactions.HasCustomActions ||
                 _reactionActionContentRevision == TextStudioReactionActionAsset.ContentRevision))
            {
                if (_reactions != null && (_reactionEffectRevision != TextStudioEffectDefinitionAsset.ContentRevision ||
                    _reactionRegistryRevision != TextStudioRegistry.Effects.Revision))
                    PrepareReactions();
                return;
            }
            if (_reactions != null) ClearExternalEffectCache();
            _reactions?.Dispose(TextStudioReactionCancelReason.RecipeChanged);
            _pendingReactionChanges = default;
            _reactionPostSamplePending = false;
            recipe.Resolve(out IReadOnlyList<TextStudioReactionLayer> layers, out int capacity);
            _reactionRecipe = recipe;
            _reactionRecipeSignature = signature;
            _reactionSignatureUsesRevisions = useRevisions;
            _reactionActionContentRevision = TextStudioReactionActionAsset.ContentRevision;
            _hasReactionRecipe = true;
            _reactions = recipe.Source != TextStudioReactionSource.None
                ? TextStudioReactionRuntime.Compile(layers, capacity, recipe.Seed)
                : null;
            _reactions?.BindHost(_reactionActionHost, true);
            SetListenForGlyphDisappearStarts(
                _reactions != null && _reactions.ListensTo(TextStudioReactionTrigger.UnitHideStarted));
            SetListenForGlyphAppearanceCompletions(
                _reactions != null && _reactions.ListensTo(TextStudioReactionTrigger.UnitRevealComplete));
            PrepareReactions();
        }

        void SetListenForGlyphAppearanceCompletions(bool listen)
        {
            if (_listeningForGlyphAppearanceComplete == listen)
                return;
            _listeningForGlyphAppearanceComplete = listen;
            if (listen)
                _reveal.OnGlyphAppearanceCompleted += ForwardGlyphAppearanceCompleted;
            else
                _reveal.OnGlyphAppearanceCompleted -= ForwardGlyphAppearanceCompleted;
            _reveal.SetAppearanceCompletionNotifications(listen);
        }

        void SetListenForGlyphDisappearStarts(bool listen)
        {
            if (_listeningForGlyphDisappearStart == listen)
                return;
            _listeningForGlyphDisappearStart = listen;
            _reveal.NotifyGlyphDisappearStarted = listen;
            if (listen)
                _reveal.OnGlyphDisappearStarted += ForwardGlyphDisappearStarted;
            else
                _reveal.OnGlyphDisappearStarted -= ForwardGlyphDisappearStarted;
        }

        internal void ConfigureInlineReactions(IReadOnlyList<TextStudioReactionLayer> layers, int capacity)
        {
            TextStudioReactionLayer[] snapshot = new TextStudioReactionLayer[layers?.Count ?? 0];
            for (int i = 0; i < snapshot.Length; i++)
                snapshot[i] = layers[i];
            ConfigureReactions(TextStudioReactionRecipe.Inline(snapshot, capacity));
        }

        void PrepareReactions()
        {
            _reactionEffectRevision = TextStudioEffectDefinitionAsset.ContentRevision;
            _reactionRegistryRevision = TextStudioRegistry.Effects.Revision;
            _reactionRequiresMaterial = false;
            _reactionSourceRequirements = TextStudioSourceContextRequirements.None;
            if (_reactions == null)
                return;
            _reactions?.PrepareEffects(this);
            SetDirty(DirtyFlags.Layout | DirtyFlags.Animation);
        }

        internal void PrepareReactionEffect(ITextStudioEffectDefinition effect)
        {
            EnsureExternalEffectCacheCurrent();
            if (!_externalEffectPlans.TryGetValue(effect, out ExternalEffectPlan plan))
            {
                if (!TryBuildExternalEffectPlan(effect, out plan)) return;
                _externalEffectPlans.Add(effect, plan);
            }
            _reactionRequiresMaterial |= plan.RequiresMaterial;
            _reactionSourceRequirements |= plan.SourceRequirements;
        }

        void RevealAllWithReactions()
        {
            if (_reactions != null && _reactions.ListensTo(TextStudioReactionTrigger.UnitRevealed))
                for (int i = 0; i < _reveal.TotalCount; i++)
                    if (!_reveal.IsGlyphRevealed(i))
                        DispatchReaction(TextStudioReactionTrigger.UnitRevealed, i);
            _reveal.RevealAll();
        }

        internal void SignalReactions(string name, float intensity = 1f, int unitIndex = -1)
        {
            if (string.IsNullOrEmpty(name)) return;
            DispatchReaction(TextStudioReactionTrigger.Signal, unitIndex, name, intensity);
        }

        internal void DispatchReaction(TextStudioReactionTrigger trigger, int unitIndex = -1,
            string name = null, float intensity = 1f, float age = 0f)
        {
            if (_reactions == null) return;
            if (_suppressUnitReactions && (trigger == TextStudioReactionTrigger.UnitRevealed ||
                trigger == TextStudioReactionTrigger.UnitRevealComplete ||
                trigger == TextStudioReactionTrigger.UnitHideStarted ||
                trigger == TextStudioReactionTrigger.UnitHidden)) return;
            int wordStart = -1, wordEnd = -1, lineStart = -1, lineEnd = -1;
            if (unitIndex >= 0 && _reactions.RequiresSemanticRanges)
            {
                TextStudioCompiledPlan plan = _pipeline.Plan;
                if (plan != null && unitIndex < plan.VisibleGlyphCount)
                {
                    int word = plan.GlyphToWordIndex != null && unitIndex < plan.GlyphToWordIndex.Length
                        ? plan.GlyphToWordIndex[unitIndex] : -1;
                    if (word >= 0 && word < plan.WordCount && plan.WordRanges != null)
                    {
                        WordRange range = plan.WordRanges[word];
                        wordStart = range.glyphStart;
                        wordEnd = range.GlyphEnd;
                    }
                    int line = plan.GlyphToLineIndex != null && unitIndex < plan.GlyphToLineIndex.Length
                        ? plan.GlyphToLineIndex[unitIndex] : -1;
                    if (line >= 0 && line < plan.LineCount && plan.LineRanges != null)
                    {
                        LineRange range = plan.LineRanges[line];
                        lineStart = range.glyphStart;
                        lineEnd = range.GlyphEnd;
                    }
                }
            }
            TextStudioReactionContext context = new TextStudioReactionContext(
                trigger, unitIndex, _reactions.Generation, name, intensity, _reactions.SessionId,
                _reactionSourceGeneration, _reactionTransactionId, _reactionChangeKind,
                wordStart: wordStart, wordEnd: wordEnd, lineStart: lineStart, lineEnd: lineEnd);
            if (trigger == TextStudioReactionTrigger.TextChanged)
            {
                _reactions.Dispatch(in context, in _pendingReactionChanges, age);
                _pendingReactionChanges = default;
            }
            else
                _reactions.Dispatch(in context, age);
        }

        internal void PrepareReactionTextChange(in TextStudioGlyphChangeSet changes)
        {
            if (ReactionsRequireChangedUnits && changes.IsDefined)
                _pendingReactionChanges = changes;
        }

        internal bool CaptureOutgoingReactions(in TextStudioGlyphChangeSet changes, bool append,
            bool deferUntilCommit = false)
        {
            if (_reactions == null || !_reactions.RequiresOutgoingCapture) return false;
            ulong nextTransaction = unchecked(_reactionTransactionId + 1);
            if (nextTransaction == 0) nextTransaction = 1;
            return _reactions.CaptureOutgoing(_reactionActionHost, in changes,
                _reactionSourceGeneration, nextTransaction,
                append ? TextStudioReactionChangeKind.Append : TextStudioReactionChangeKind.Replace,
                deferUntilCommit);
        }

        internal void AbortPendingOutgoingReactions() =>
            _reactions?.AbortPendingOutgoingCapture();

        void EnsureReactionTextChangeMap(string incomingMarkup)
        {
            if (!ReactionsRequireChangedUnits || _pendingReactionChanges.IsDefined)
                return;
            TextStudioCompiledPlan incoming = CompileTextChangeUnits(incomingMarkup, false, null);
            _pendingReactionChanges = TextStudioGlyphChangeSetBuilder.Build(
                _pipeline.Plan, incoming, TextStudioTextChangeScope.ChangedGlyphs,
                NumericDecimalSeparator);
        }

        void RecordReactionTextChange(TextStudioReactionChangeKind kind)
        {
            ulong nextTransaction = unchecked(_reactionTransactionId + 1);
            if (nextTransaction == 0) nextTransaction = 1;
            _reactions?.RetireForTextChange(nextTransaction);
            unchecked { _reactionSourceGeneration++; _reactionTransactionId++; }
            if (_reactionSourceGeneration == 0) _reactionSourceGeneration = 1;
            if (_reactionTransactionId == 0) _reactionTransactionId = 1;
            _reactionChangeKind = kind;
            _reactionAwaitingReveal = true;
        }

        void DispatchRevealStartedReaction()
        {
            if (_reactions == null)
                return;
            if (!_reactionAwaitingReveal)
            {
                unchecked { _reactionTransactionId++; }
                if (_reactionTransactionId == 0) _reactionTransactionId = 1;
                _reactionChangeKind = TextStudioReactionChangeKind.Replay;
            }
            _reactionAwaitingReveal = false;
            DispatchReaction(TextStudioReactionTrigger.RevealStarted);
        }

        internal void FlushReactionSignals()
        {
            TextStudioReactionRuntime reactions = _reactions;
            if (reactions == null)
                return;
            reactions.FlushSignals(ReactionSignal);
            reactions.FlushAudioCues(ReactionAudioCue);
        }
        internal void SetReactionClock(double time)
        {
            if (_reactions == null) return;
            _reactions.SetTime(time);
            _reactionClockPrepared = true;
        }

        void TickReactionClock(float deltaTime)
        {
            if (!_reactionClockPrepared) _reactions?.Advance(deltaTime);
            _reactionClockPrepared = false;
            _reactionPostSamplePending = false;
        }
        internal void StopReactions() => _reactions?.Stop();
        internal void CancelReactions(TextStudioReactionCancelReason reason = TextStudioReactionCancelReason.ExplicitCancel) =>
            _reactions?.Cancel(reason);
        internal void ResetReactions(TextStudioReactionCancelReason reason = TextStudioReactionCancelReason.ExplicitCancel) =>
            _reactions?.Reset(reason);

        internal void ReleaseReactions(TextStudioReactionCancelReason reason = TextStudioReactionCancelReason.Disposed)
        {
            _reactionActionHost = null;
            _deferReactionLiveOutputSampling = false;
            _reactionPostSamplePending = false;
            SetListenForGlyphDisappearStarts(false);
            SetListenForGlyphAppearanceCompletions(false);
            if (_reactions != null) ClearExternalEffectCache();
            _reactionClockPrepared = false;
            _reactions?.Dispose(reason);
            _reactions = null;
            _reactionRecipe = TextStudioReactionRecipe.None;
            _reactionRecipeSignature = 0;
            _hasReactionRecipe = false;
            _reactionAwaitingReveal = false;
            _pendingReactionChanges = default;
            _reactionRequiresMaterial = false;
            _reactionSourceRequirements = TextStudioSourceContextRequirements.None;
        }
    }
}
