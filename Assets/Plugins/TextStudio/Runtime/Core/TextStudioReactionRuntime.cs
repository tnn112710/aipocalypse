using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Host-owned bounded scheduler. No Unity objects, subscriptions or frame driver are created.</summary>
#if UNITY_6000_7_OR_NEWER
    // Session IDs reset to the same initial value as the baseline subsystem callback.
    [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
#endif
    internal sealed partial class TextStudioReactionRuntime
    {
        static int s_nextSessionId = 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSessionIds() => s_nextSessionId = 0;
        readonly struct Definition
        {
            internal readonly TextStudioReactionTrigger Trigger;
            internal readonly TextStudioReactionAction Action;
            internal readonly TextStudioReactionTarget Target;
            internal readonly TextStudioReactionOverlap Overlap;
            internal readonly ITextStudioEffectDefinition Effect;
            internal readonly ITextStudioReactionCommand Command;
            internal readonly TextStudioReactionActionAsset ActionAsset;
            internal readonly int MaxConcurrent;
            internal readonly int MaxUnitsPerChange;
            internal readonly bool CaptureTriggerAnchor;
            internal readonly TextStudioReactionActionRequirements Requirements;
            internal readonly TextStudioReactionCaptureIntent CaptureIntent;
            internal readonly TextStudioReactionResourceClass CaptureResources;
            internal readonly string AnchorKey;
            internal readonly string TriggerName, Signal, AudioCue;
            internal readonly string TargetBindingKey;
            internal readonly string EnabledInputKey;
            internal readonly double Delay, Duration, Cooldown;
            internal readonly float Intensity, Chance;
            internal readonly uint Seed;
            internal readonly string Id;
            internal readonly string DependencyId;
            internal readonly TextStudioReactionDependencyEvent DependencyEvent;

            internal Definition(TextStudioReactionLayer layer, uint recipeSeed,
                ITextStudioReactionCommand command)
            {
                Trigger = layer.When;
                Action = layer.Action;
                Target = layer.Which;
                Overlap = layer.Overlap;
                ITextStudioEffectDefinition effect = layer.EffectSource == TextStudioReactionEffectSource.CustomAsset
                    ? layer.Effect
                    : null;
                if (effect == null && !string.IsNullOrEmpty(layer.EffectId))
                    TextStudioRegistry.Effects.TryGetQuiet(layer.EffectId, out effect);
                Effect = effect;
                Command = command;
                ActionAsset = layer.Action == TextStudioReactionAction.Custom ? layer.CustomAction : null;
                MaxConcurrent = (command as ITextStudioReactionConcurrencyCommand)?.MaxConcurrent ?? 0;
                MaxUnitsPerChange = (command as ITextStudioReactionChangeLimitCommand)?.MaxUnitsPerChange ?? 0;
                bool captureTriggerAnchor =
                    (command as ITextStudioReactionTriggerAnchorCommand)?.CaptureTriggerAnchor ?? false;
                bool captureOutgoingAnchor = layer.Which == TextStudioReactionTarget.ChangedOutgoingUnits &&
                    captureTriggerAnchor &&
                    (command.Requirements & TextStudioReactionActionRequirements.LiveAnchor) != 0;
                CaptureTriggerAnchor = captureTriggerAnchor && !captureOutgoingAnchor;
                TextStudioReactionActionRequirements requirements =
                    command?.Requirements ?? TextStudioReactionActionRequirements.None;
                if (captureOutgoingAnchor)
                    requirements = (requirements & ~TextStudioReactionActionRequirements.LiveAnchor) |
                                   TextStudioReactionActionRequirements.RetainedAnchor;
                Requirements = requirements;
                CaptureIntent = command?.CaptureIntent ?? TextStudioReactionCaptureIntent.Copy;
                CaptureResources = command?.CaptureResources ?? TextStudioReactionResourceClass.None;
                AnchorKey = (command as ITextStudioReactionAnchorSelectionCommand)?.AnchorKey;
                TriggerName = layer.TriggerName;
                Signal = layer.Signal;
                AudioCue = layer.AudioCue;
                TargetBindingKey = layer.TargetBindingKey;
                EnabledInputKey = string.IsNullOrWhiteSpace(layer.EnabledInputKey)
                    ? null : layer.EnabledInputKey.Trim();
                Delay = FinitePositive(layer.Delay);
                Duration = Math.Max(.001, FinitePositive(layer.Duration));
                Cooldown = FinitePositive(layer.Cooldown);
                Intensity = Mathf.Clamp01(FinitePositive(layer.Intensity));
                Chance = Mathf.Clamp01(FinitePositive(layer.Chance));
                Seed = StableHash(layer.Id) ^ recipeSeed;
                Id = layer.Id;
                DependencyId = layer.DependsOnId;
                DependencyEvent = layer.DependencyEvent;
            }
        }

        struct Run
        {
            internal int Definition;
            internal TextStudioReactionContext Context;
            internal TextStudioReactionContext SourceContext;
            internal double Start;
            internal long TargetKey;
            internal int SelectionStart, SelectionEnd;
            internal uint LastSampleEpoch;
            internal int CustomStateSlot;
            internal bool CustomStarted;
            internal bool CustomCompleting;
            internal TextStudioReactionActionEvents DeliveredEvents;
            internal TextStudioReactionLease Lease;
            internal bool PendingCommit;
            internal int Next, Previous;
        }

        struct PendingChain
        {
            internal int DefinitionIndex;
            internal TextStudioReactionContext Context;
            internal double EventTime;
        }

        struct PendingDispatch
        {
            internal TextStudioReactionContext Context;
            internal TextStudioGlyphChangeSet Changes;
            internal float Age;
        }

        sealed class TextStudioReactionStatePool
        {
            readonly ITextStudioReactionCommand _command;
            readonly int _capacity;
            ITextStudioReactionActionState[] _states;
            int[] _nextFree;
            int _created;
            int _freeHead = -1;

            internal TextStudioReactionStatePool(ITextStudioReactionCommand command, int capacity)
            {
                _command = command;
                _capacity = capacity;
                int initial = Math.Min(8, capacity);
                _states = new ITextStudioReactionActionState[initial];
                _nextFree = new int[initial];
            }

            internal bool TryAcquire(out int slot, out ITextStudioReactionActionState state)
            {
                if (_freeHead >= 0)
                {
                    slot = _freeHead;
                    _freeHead = _nextFree[slot];
                    state = _states[slot];
                    return true;
                }
                if (_created == _capacity)
                {
                    slot = -1;
                    state = null;
                    return false;
                }
                if (_created == _states.Length)
                {
                    int size = Math.Min(_capacity, _states.Length * 2);
                    Array.Resize(ref _states, size);
                    Array.Resize(ref _nextFree, size);
                }
                state = _command.CreateState();
                if (state == null)
                {
                    slot = -1;
                    return false;
                }
                slot = _created++;
                _states[slot] = state;
                return true;
            }

            internal ITextStudioReactionActionState Get(int slot) => _states[slot];

            internal void Prewarm(ITextStudioReactionActionHost host)
            {
                if (!Application.isPlaying || host == null ||
                    !(_command is ITextStudioReactionPrewarmableCommand policy)) return;
                TextStudioReactionActionRequirements required = _command.Requirements &
                    ~(TextStudioReactionActionRequirements.RetainedAnchor |
                      TextStudioReactionActionRequirements.RetainedGeometry);
                if ((required & ~host.Capabilities) != 0) return;
                int desired = Math.Min(_capacity, Math.Max(0, policy.PrewarmCount));
                while (_created < desired)
                {
                    if (_created == _states.Length)
                    {
                        int size = Math.Min(_capacity, Math.Max(1, _states.Length * 2));
                        Array.Resize(ref _states, size);
                        Array.Resize(ref _nextFree, size);
                    }
                    ITextStudioReactionActionState state = null;
                    try
                    {
                        state = _command.CreateState();
                        if (!(state is ITextStudioReactionPrewarmableState prepared))
                            throw new InvalidOperationException("A prewarmable command must create a prewarmable state.");
                        prepared.Prewarm(host);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        state?.Dispose();
                        break;
                    }
                    int slot = _created++;
                    _states[slot] = state;
                    Release(slot);
                }
            }

            internal void Release(int slot)
            {
                _nextFree[slot] = _freeHead;
                _freeHead = slot;
            }

            internal void DisposeAll()
            {
                for (int i = 0; i < _created; i++)
                {
                    try { _states[i]?.Dispose(); }
                    catch (Exception exception) { Debug.LogException(exception); }
                    _states[i] = null;
                }
                _created = 0;
                _freeHead = -1;
            }
        }

        readonly Definition[] _definitions;
        readonly uint _sessionId;
        readonly int[] _triggerOffsets;
        readonly TextStudioReactionStatePool[] _statePools;
        readonly int[] _outgoingLimitCounts;
        readonly int[] _outgoingLimitOwners;
        ulong _outgoingLimitTransactionId;
        readonly int[] _targetSlots;
        ITextStudioReactionActionHost _boundActionHost;
        readonly int[] _firstDependent;
        readonly int[] _nextDependent;
        PendingChain[] _pendingChains;
        int _pendingChainCount;
        PendingDispatch[] _pendingDispatches;
        int _pendingDispatchCount;
        Run[] _runs;
        readonly int _capacity;
        readonly double[] _nextAllowed;
        readonly Dictionary<long, double> _nextAllowedByTarget;
        readonly long[] _expiredCooldownKeys;
        readonly Dictionary<long, int> _activeByTarget;
        readonly uint[] _occurrences;
        TextStudioReactionContext[] _signals;
        int _signalCount;
        TextStudioReactionContext[] _audioCues;
        int _audioCueCount;
        double _time;
        uint _sampleEpoch;
        uint _generation = 1;
        bool _accepting = true;
        bool _flushing;
        bool _sampling;
        bool _cancelling;
        bool _hasDeferredCancellation;
        TextStudioReactionCancelReason _deferredCancellationReason;
        bool _disposeRequested;
        bool _disposed;
        bool _hasOutputToRestore;
        int _freeHead, _activeHead = -1, _activeTail = -1;

        internal int ActiveCount { get; private set; }
        internal int SuppressedCount { get; private set; }
        internal int CancelledCount { get; private set; }
        internal TextStudioReactionCancelReason LastCancellationReason { get; private set; }
        internal bool HasWork => ActiveCount > 0 || _pendingChainCount > 0 || _pendingDispatchCount > 0 ||
            _signalCount > 0 || _audioCueCount > 0 || _hasOutputToRestore;
        internal uint Generation => _generation;
        internal uint SessionId => _sessionId;
        internal bool RequiresGeometry { get; private set; }
        internal bool RequiresAnchor { get; private set; }
        internal bool RequiresSemanticRanges { get; private set; }
        internal bool RequiresChangedUnits { get; private set; }
        internal bool RequiresOutgoingCapture { get; private set; }
        internal bool RequiresPhysicsWorld { get; private set; }
        internal bool HasCustomActions { get; private set; }

        internal static TextStudioReactionRuntime Compile(IReadOnlyList<TextStudioReactionLayer> layers, int capacity,
            uint recipeSeed = 1)
        {
            return Compile(layers, capacity, recipeSeed, out _);
        }

        internal static TextStudioReactionRuntime Compile(IReadOnlyList<TextStudioReactionLayer> layers, int capacity,
            uint recipeSeed, out string error)
        {
            error = ValidateDependencies(layers, capacity,
                out Dictionary<TextStudioReactionLayer, ITextStudioReactionCommand> commands);
            if (error != null) return null;
            int count = 0;
            if (layers != null)
                for (int i = 0; i < layers.Count; i++)
                    if (IsUsable(layers[i])) count++;
            return count == 0 ? null : new TextStudioReactionRuntime(layers, count, capacity, recipeSeed, commands);
        }

        TextStudioReactionRuntime(IReadOnlyList<TextStudioReactionLayer> layers, int count, int capacity,
            uint recipeSeed, Dictionary<TextStudioReactionLayer, ITextStudioReactionCommand> commands)
        {
            _sessionId = unchecked((uint)Interlocked.Increment(ref s_nextSessionId));
            _definitions = new Definition[count];
            _statePools = new TextStudioReactionStatePool[count];
            _triggerOffsets = new int[(int)TextStudioReactionTrigger.UnitRevealComplete + 2];
            int write = 0;
            for (int trigger = 0; trigger < _triggerOffsets.Length - 1; trigger++)
            {
                _triggerOffsets[trigger] = write;
                for (int i = 0; i < layers.Count; i++)
                    if (IsUsable(layers[i]) && (int)layers[i].When == trigger)
                    {
                        ITextStudioReactionCommand command = null;
                        commands?.TryGetValue(layers[i], out command);
                        _definitions[write++] = new Definition(layers[i], recipeSeed, command);
                    }
            }
            _triggerOffsets[_triggerOffsets.Length - 1] = write;
            bool hasOutgoingLimit = false;
            for (int i = 0; i < count; i++)
                hasOutgoingLimit |= _definitions[i].MaxUnitsPerChange > 0;
            if (hasOutgoingLimit)
            {
                _outgoingLimitCounts = new int[count];
                _outgoingLimitOwners = new int[count];
                for (int i = 0; i < count; i++)
                {
                    int owner = i;
                    for (int previous = 0; previous < i; previous++)
                        if (_definitions[i].ActionAsset != null &&
                            ReferenceEquals(_definitions[i].ActionAsset,
                                _definitions[previous].ActionAsset) &&
                            _definitions[previous].MaxUnitsPerChange > 0)
                        {
                            owner = _outgoingLimitOwners[previous];
                            break;
                        }
                    _outgoingLimitOwners[i] = owner;
                }
            }
            _capacity = Mathf.Clamp(capacity, 1, 4096);
            Dictionary<string, int> indices = null;
            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrEmpty(_definitions[i].DependencyId)) continue;
                indices = new Dictionary<string, int>(count, StringComparer.Ordinal);
                for (int j = 0; j < count; j++)
                    indices.Add(_definitions[j].Id, j);
                break;
            }
            if (indices != null)
            {
                _firstDependent = new int[count];
                _nextDependent = new int[count];
                for (int i = 0; i < count; i++) _firstDependent[i] = -1;
                for (int i = count - 1; i >= 0; i--)
                {
                    if (string.IsNullOrEmpty(_definitions[i].DependencyId)) continue;
                    int parent = indices[_definitions[i].DependencyId];
                    _nextDependent[i] = _firstDependent[parent];
                    _firstDependent[parent] = i;
                }
                _pendingChains = new PendingChain[Math.Min(8, _capacity)];
            }
            _runs = new Run[Math.Min(8, _capacity)];
            _nextAllowed = new double[count];
            _occurrences = new uint[count];
            bool hasSignalOutput = false;
            bool hasTargetCooldown = false;
            bool hasGroupedOverlap = false;
            bool hasNamedTargets = false;
            for (int i = 0; i < _definitions.Length; i++)
            {
                hasSignalOutput |= _definitions[i].Action == TextStudioReactionAction.SendSignal;
                hasTargetCooldown |= _definitions[i].Target != TextStudioReactionTarget.Text &&
                                     _definitions[i].Cooldown > 0d;
                hasGroupedOverlap |= _definitions[i].Overlap != TextStudioReactionOverlap.Allow;
                RequiresAnchor |= _definitions[i].Action == TextStudioReactionAction.SendSignal;
                RequiresAnchor |= (_definitions[i].Requirements & TextStudioReactionActionRequirements.LiveAnchor) != 0;
                RequiresAnchor |= (_definitions[i].Requirements & TextStudioReactionActionRequirements.RetainedAnchor) != 0;
                hasNamedTargets |= (_definitions[i].Requirements & TextStudioReactionActionRequirements.NamedTarget) != 0;
                RequiresSemanticRanges |= _definitions[i].Target == TextStudioReactionTarget.EventWord ||
                                          _definitions[i].Target == TextStudioReactionTarget.EventLine;
                RequiresChangedUnits |= _definitions[i].Trigger == TextStudioReactionTrigger.TextChanged &&
                                        _definitions[i].Target == TextStudioReactionTarget.ChangedUnits;
                RequiresOutgoingCapture |= _definitions[i].Trigger == TextStudioReactionTrigger.TextChanged &&
                                           _definitions[i].Target == TextStudioReactionTarget.ChangedOutgoingUnits;
                RequiresPhysicsWorld |= (_definitions[i].Requirements &
                                         TextStudioReactionActionRequirements.PhysicsWorld) != 0;
                if (_definitions[i].Command != null)
                {
                    if (_definitions[i].ActionAsset is TextStudioReactionPrefabActionAsset)
                        for (int previous = 0; previous < i; previous++)
                            if (ReferenceEquals(_definitions[previous].ActionAsset,
                                    _definitions[i].ActionAsset))
                            {
                                _statePools[i] = _statePools[previous];
                                break;
                            }
                    _statePools[i] ??= new TextStudioReactionStatePool(_definitions[i].Command, _capacity);
                    HasCustomActions = true;
                }
                if (_definitions[i].Action != TextStudioReactionAction.PlayAudioCue)
                    continue;
                _audioCues = new TextStudioReactionContext[_runs.Length];
            }
            if (hasSignalOutput)
                _signals = new TextStudioReactionContext[_runs.Length];
            if (hasTargetCooldown)
            {
                _nextAllowedByTarget = new Dictionary<long, double>(_capacity);
                _expiredCooldownKeys = new long[_capacity];
            }
            if (hasGroupedOverlap)
                _activeByTarget = new Dictionary<long, int>(_capacity);
            if (hasNamedTargets)
            {
                _targetSlots = new int[count];
                for (int i = 0; i < count; i++) _targetSlots[i] = -1;
            }
            if (HasCustomActions)
                _pendingDispatches = new PendingDispatch[Math.Min(8, _capacity)];
            InitializeSlots();
            for (int i = 0; i < _definitions.Length; i++)
                RequiresGeometry |= _definitions[i].Action == TextStudioReactionAction.PlayEffect ||
                    (_definitions[i].Requirements & TextStudioReactionActionRequirements.RetainedGeometry) != 0;
        }

        internal bool ListensTo(TextStudioReactionTrigger trigger)
        {
            int index = (int)trigger;
            return index >= 0 && index < _triggerOffsets.Length - 1 &&
                   _triggerOffsets[index] != _triggerOffsets[index + 1];
        }

        internal void PrepareEffects(TextStudioRuntimeCore core)
        {
            for (int i = 0; i < _definitions.Length; i++)
                if (_definitions[i].Action == TextStudioReactionAction.PlayEffect)
                    core.PrepareReactionEffect(_definitions[i].Effect);
        }

        internal void Advance(float deltaTime)
        {
            _time += FinitePositive(deltaTime);
        }

        internal void SetTime(double time)
        {
            if (!double.IsNaN(time) && !double.IsInfinity(time) && time >= _time) _time = time;
        }

        internal void Dispatch(in TextStudioReactionContext context, float age = 0f)
        {
            TextStudioGlyphChangeSet changes = default;
            Dispatch(in context, in changes, age);
        }

        internal void BindHost(ITextStudioReactionActionHost host, bool force = false)
        {
            if (!force && ReferenceEquals(_boundActionHost, host)) return;
            _boundActionHost = host;
            if (Application.isPlaying && host != null)
                for (int i = 0; i < _statePools.Length; i++)
                    _statePools[i]?.Prewarm(host);
            if (_targetSlots == null) return;
            for (int i = 0; i < _definitions.Length; i++)
            {
                _targetSlots[i] = -1;
                if (host == null ||
                    (_definitions[i].Requirements & TextStudioReactionActionRequirements.NamedTarget) == 0)
                    continue;
                try { _targetSlots[i] = host.ResolveReactionTargetSlot(_definitions[i].TargetBindingKey); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }

        internal void Dispatch(in TextStudioReactionContext context,
            in TextStudioGlyphChangeSet changes, float age = 0f)
        {
            if (_cancelling) return;
            if (_sampling)
            {
                if (_pendingDispatchCount == _capacity) { SuppressedCount++; return; }
                _pendingDispatches ??= new PendingDispatch[Math.Min(8, _capacity)];
                if (_pendingDispatchCount == _pendingDispatches.Length)
                    Array.Resize(ref _pendingDispatches, Math.Min(_capacity, _pendingDispatches.Length * 2));
                _pendingDispatches[_pendingDispatchCount++] = new PendingDispatch
                {
                    Context = context,
                    Changes = changes,
                    Age = age
                };
                return;
            }
            int trigger = (int)context.Trigger;
            if (!_accepting || trigger < 0 || trigger >= _triggerOffsets.Length - 1 ||
                (context.Generation != 0 && context.Generation != _generation) ||
                (context.SessionId != 0 && context.SessionId != _sessionId) ||
                !(context.Intensity > 0f) || float.IsInfinity(context.Intensity)) return;
            double eventTime = Math.Max(0d, _time - FinitePositive(age));
            for (int i = _triggerOffsets[trigger]; i < _triggerOffsets[trigger + 1]; i++)
            {
                ref readonly Definition definition = ref _definitions[i];
                if (!string.IsNullOrEmpty(definition.DependencyId)) continue;
                if (definition.Target == TextStudioReactionTarget.ChangedOutgoingUnits) continue;
                if (definition.Target == TextStudioReactionTarget.ChangedUnits)
                {
                    if (trigger != (int)TextStudioReactionTrigger.TextChanged || !changes.IsDefined)
                        continue;
                    for (int unit = 0; unit < changes.IncomingGlyphCount; unit++)
                    {
                        if (!changes.IsIncomingGlyphChanged(unit))
                            continue;
                        TextStudioReactionContext selected = new TextStudioReactionContext(
                            context.Trigger, unit, context.Generation, context.Name, context.Intensity,
                            context.SessionId, context.SourceGeneration, context.TransactionId,
                            context.ChangeKind, context.EventTime);
                        DispatchDefinition(i, in selected, eventTime);
                    }
                    continue;
                }
                DispatchDefinition(i, in context, eventTime);
            }
        }

        internal bool CaptureOutgoing(ITextStudioReactionActionHost host,
            in TextStudioGlyphChangeSet changes, uint sourceGeneration,
            ulong transactionId, TextStudioReactionChangeKind changeKind,
            bool deferUntilCommit)
        {
            if (!RequiresOutgoingCapture || !changes.IsDefined ||
                changes.OutgoingChangedGlyphCount == 0 || !_accepting) return false;
            BindHost(host);
            bool captured = false;
            if (_outgoingLimitCounts != null &&
                _outgoingLimitTransactionId != transactionId)
            {
                Array.Clear(_outgoingLimitCounts, 0, _outgoingLimitCounts.Length);
                _outgoingLimitTransactionId = transactionId;
            }
            int trigger = (int)TextStudioReactionTrigger.TextChanged;
            double eventTime = _time;
            for (int i = _triggerOffsets[trigger]; i < _triggerOffsets[trigger + 1]; i++)
            {
                ref readonly Definition definition = ref _definitions[i];
                if (definition.Target != TextStudioReactionTarget.ChangedOutgoingUnits) continue;
                for (int unit = 0; unit < changes.OutgoingGlyphCount; unit++)
                {
                    if (!changes.IsOutgoingGlyphChanged(unit)) continue;
                    TextStudioReactionContext context = new TextStudioReactionContext(
                        TextStudioReactionTrigger.TextChanged, unit, _generation,
                        sourceGeneration: sourceGeneration, transactionId: transactionId,
                        changeKind: changeKind, sessionId: _sessionId);
                    captured |= DispatchDefinition(i, in context, eventTime, changes, deferUntilCommit);
                }
            }
            if (!captured) return false;
            for (int slot = _activeHead; slot >= 0; slot = _runs[slot].Next)
                if (_runs[slot].Context.TransactionId == transactionId &&
                    _runs[slot].Lease.IsValid)
                    return true;
            return false;
        }

        bool DispatchDefinition(int i, in TextStudioReactionContext context, double eventTime,
            TextStudioGlyphChangeSet changes = default, bool deferUntilCommit = false)
        {
            ref readonly Definition definition = ref _definitions[i];
            if (!string.IsNullOrEmpty(definition.TriggerName) &&
                !string.Equals(definition.TriggerName, context.Name, StringComparison.Ordinal)) return false;
            if (!InputAllows(in definition)) return false;
            if (!TrySelect(definition.Target, context, out int selectionStart, out int selectionEnd) ||
                (selectionStart < 0 && definition.Target != TextStudioReactionTarget.Text &&
                 definition.Action == TextStudioReactionAction.PlayEffect)) return false;
            if (!IsCooldownReady(i, selectionStart, eventTime)) return false;
            uint occurrence = ++_occurrences[i];
            uint eventBits = RandomBits(definition.Seed, occurrence);
            if (Random01(eventBits) >= definition.Chance) return false;
            int slot = -1;
            long targetKey = TargetKey(i, selectionStart);
            if (definition.Overlap != TextStudioReactionOverlap.Allow &&
                _activeByTarget.TryGetValue(targetKey, out int existingSlot))
            {
                if (definition.Overlap == TextStudioReactionOverlap.Ignore) return false;
                slot = existingSlot;
            }
            if (definition.MaxUnitsPerChange > 0)
            {
                int owner = _outgoingLimitOwners[i];
                if (_outgoingLimitCounts[owner] >= definition.MaxUnitsPerChange)
                {
                    if (SuppressedCount < int.MaxValue) SuppressedCount++;
                    return false;
                }
                _outgoingLimitCounts[owner]++;
            }
            if (slot < 0 && definition.MaxConcurrent > 0 && definition.ActionAsset != null)
            {
                int oldest = -1;
                int concurrent = 0;
                for (int active = _activeHead; active >= 0; active = _runs[active].Next)
                {
                    if (_definitions[_runs[active].Definition].ActionAsset != definition.ActionAsset)
                        continue;
                    if (oldest < 0) oldest = active;
                    concurrent++;
                }
                if (concurrent >= definition.MaxConcurrent && oldest >= 0)
                {
                    uint generationBeforeCancel = _generation;
                    if (_runs[oldest].CustomStarted)
                        CancelCustomRun(ref _runs[oldest], TextStudioReactionCancelReason.BudgetExceeded);
                    if (generationBeforeCancel != _generation || _disposeRequested || !_accepting)
                        return false;
                    ReleaseSlot(oldest);
                    LastCancellationReason = TextStudioReactionCancelReason.BudgetExceeded;
                    if (CancelledCount < int.MaxValue) CancelledCount++;
                }
            }
            if (slot < 0) slot = AcquireSlot();
            if (slot < 0)
            {
                SuppressedCount++;
                LastCancellationReason = TextStudioReactionCancelReason.BudgetExceeded;
                return false;
            }
            ref Run run = ref _runs[slot];
            uint generation = _generation;
            if (run.CustomStateSlot >= 0 && run.CustomStarted)
                CancelCustomRun(ref run, TextStudioReactionCancelReason.Restarted);
            if (generation != _generation || _disposeRequested || !_accepting) return false;
            ReleaseLease(ref run);
            if (generation != _generation || _disposeRequested || !_accepting) return false;
            run.Definition = i;
            run.Context = new TextStudioReactionContext(context.Trigger, context.UnitIndex, _generation,
                context.Name, Mathf.Clamp01(context.Intensity) * definition.Intensity, _sessionId,
                context.SourceGeneration, context.TransactionId, context.ChangeKind, eventTime,
                context.WordStart, context.WordEnd, context.LineStart, context.LineEnd,
                selectionStart, selectionEnd, definition.TargetBindingKey,
                (float)definition.Duration, RandomBits(eventBits, 0xA511E9B3u));
            run.SourceContext = context;
            run.Start = eventTime + definition.Delay;
            run.TargetKey = targetKey;
            run.SelectionStart = selectionStart;
            run.SelectionEnd = selectionEnd;
            run.CustomStateSlot = -1;
            run.CustomStarted = false;
            run.CustomCompleting = false;
            run.PendingCommit = deferUntilCommit;
            run.DeliveredEvents = TextStudioReactionActionEvents.None;
            if (definition.Target == TextStudioReactionTarget.ChangedOutgoingUnits)
            {
                TextStudioReactionCaptureRequest request = new TextStudioReactionCaptureRequest(
                    in run.Context, in changes, definition.CaptureIntent,
                    definition.CaptureResources, definition.AnchorKey);
                TextStudioReactionLease lease = default;
                try
                {
                    if (_boundActionHost == null ||
                        !_boundActionHost.TryCaptureReactionOutput(in request, out lease) ||
                        !lease.IsValid ||
                        (definition.Command.Requirements &
                         TextStudioReactionActionRequirements.RetainedGeometry) != 0 &&
                        !lease.SupportsVisualOutput)
                    {
                        ReleaseLease(lease);
                        SuppressedCount++;
                        LastCancellationReason = TextStudioReactionCancelReason.UnsupportedCapability;
                        ReleaseSlot(slot);
                        return false;
                    }
                    run.Lease = lease;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    ReleaseLease(lease);
                    SuppressedCount++;
                    LastCancellationReason = TextStudioReactionCancelReason.ActionFailed;
                    ReleaseSlot(slot);
                    return false;
                }
            }
            if (definition.Overlap != TextStudioReactionOverlap.Allow)
                _activeByTarget[targetKey] = slot;
            if (definition.Cooldown > 0d)
            {
                if (definition.Target != TextStudioReactionTarget.Text && selectionStart >= 0)
                    _nextAllowedByTarget[targetKey] = eventTime + definition.Cooldown;
                else
                    _nextAllowed[i] = eventTime + definition.Cooldown;
            }
            return true;
        }

        static bool TrySelect(TextStudioReactionTarget target, in TextStudioReactionContext context,
            out int start, out int end)
        {
            start = end = -1;
            switch (target)
            {
                case TextStudioReactionTarget.Text:
                    return true;
                case TextStudioReactionTarget.EventUnit:
                case TextStudioReactionTarget.ChangedUnits:
                case TextStudioReactionTarget.ChangedOutgoingUnits:
                    if (context.UnitIndex < 0) return true;
                    start = context.UnitIndex;
                    end = context.UnitIndex + 1;
                    return true;
                case TextStudioReactionTarget.EventWord:
                    start = context.WordStart;
                    end = context.WordEnd;
                    return start >= 0 && end > start;
                case TextStudioReactionTarget.EventLine:
                    start = context.LineStart;
                    end = context.LineEnd;
                    return start >= 0 && end > start;
                default:
                    return false;
            }
        }

        bool IsCooldownReady(int definitionIndex, int selectionStart, double eventTime)
        {
            ref readonly Definition definition = ref _definitions[definitionIndex];
            if (definition.Cooldown <= 0d)
                return true;
            if (definition.Target == TextStudioReactionTarget.Text || selectionStart < 0)
                return eventTime >= _nextAllowed[definitionIndex];
            long key = TargetKey(definitionIndex, selectionStart);
            if (_nextAllowedByTarget.TryGetValue(key, out double nextAllowed))
                return eventTime >= nextAllowed;
            if (_nextAllowedByTarget.Count < _capacity)
                return true;
            int expiredCount = 0;
            foreach (KeyValuePair<long, double> entry in _nextAllowedByTarget)
                if (entry.Value <= eventTime)
                    _expiredCooldownKeys[expiredCount++] = entry.Key;
            for (int i = 0; i < expiredCount; i++)
                _nextAllowedByTarget.Remove(_expiredCooldownKeys[i]);
            if (_nextAllowedByTarget.Count < _capacity)
                return true;
            SuppressedCount++;
            return false;
        }

        static long TargetKey(int definitionIndex, int unitIndex) =>
            ((long)definitionIndex << 32) | (uint)(unitIndex + 1);

        internal void Sample(TextStudioRuntimeCore core, ITextStudioReactionActionHost host = null,
            bool deferLiveOutput = false, bool continueFrame = false)
        {
            if (_sampling) return;
            BindHost(host);
            if (!continueFrame)
                _hasOutputToRestore = false;
            int deferredCount = _pendingDispatchCount;
            _pendingDispatchCount = 0;
            for (int deferred = 0; deferred < deferredCount; deferred++)
            {
                PendingDispatch dispatch = _pendingDispatches[deferred];
                _pendingDispatches[deferred] = default;
                Dispatch(in dispatch.Context, in dispatch.Changes, dispatch.Age);
            }
            if (!continueFrame || _sampleEpoch == 0)
            {
                unchecked { _sampleEpoch++; }
                if (_sampleEpoch == 0) _sampleEpoch = 1;
            }
            uint generation = _generation;
            _sampling = true;
            try
            {
                for (int round = 0; round <= _definitions.Length; round++)
                {
                    int pendingCount = _pendingChainCount;
                    _pendingChainCount = 0;
                    for (int pending = 0; pending < pendingCount; pending++)
                    {
                        PendingChain chain = _pendingChains[pending];
                        _pendingChains[pending] = default;
                        DispatchDefinition(chain.DefinitionIndex, in chain.Context, chain.EventTime);
                    }
                    int i = _activeHead;
                    while (i >= 0)
                    {
                        ref Run run = ref _runs[i];
                        int next = run.Next;
                        ref readonly Definition definition = ref _definitions[run.Definition];
                        if (!InputAllows(in definition))
                        {
                            if (run.CustomStarted)
                                CancelCustomRun(ref run, TextStudioReactionCancelReason.InputDisabled);
                            if (generation != _generation) return;
                            ReleaseSlot(i);
                            LastCancellationReason = TextStudioReactionCancelReason.InputDisabled;
                            if (CancelledCount < int.MaxValue) CancelledCount++;
                            i = next;
                            continue;
                        }
                        if (run.PendingCommit || run.LastSampleEpoch == _sampleEpoch)
                        { i = next; continue; }
                        if (!deferLiveOutput && definition.CaptureTriggerAnchor && !run.Lease.IsValid)
                        {
                            TextStudioGlyphChangeSet noChanges = default;
                            TextStudioReactionCaptureRequest request = new TextStudioReactionCaptureRequest(
                                in run.Context, in noChanges, TextStudioReactionCaptureIntent.Copy,
                                TextStudioReactionResourceClass.None, definition.AnchorKey);
                            TextStudioReactionLease captured = default;
                            bool accepted = false;
                            try
                            {
                                accepted = host != null &&
                                    host.TryCaptureReactionOutput(in request, out captured) &&
                                    captured.IsValid;
                            }
                            catch (Exception exception) { Debug.LogException(exception); }
                            if (!accepted)
                            {
                                ReleaseLease(captured);
                                ReleaseSlot(i);
                                LastCancellationReason = TextStudioReactionCancelReason.TargetLost;
                                if (SuppressedCount < int.MaxValue) SuppressedCount++;
                                i = next;
                                continue;
                            }
                            run.Lease = captured;
                        }
                        if (_time < run.Start) { i = next; continue; }
                        if (deferLiveOutput && definition.Action == TextStudioReactionAction.Custom &&
                            (definition.Requirements & (TextStudioReactionActionRequirements.LiveAnchor |
                                TextStudioReactionActionRequirements.WorldObjectOutput |
                                TextStudioReactionActionRequirements.PhysicsWorld)) != 0)
                        {
                            i = next;
                            continue;
                        }
                        run.LastSampleEpoch = _sampleEpoch;
                        if (definition.Action == TextStudioReactionAction.Custom)
                        {
                            SampleCustomRun(i, host);
                            if (generation != _generation) return;
                            i = next;
                            continue;
                        }
                        if (definition.Action == TextStudioReactionAction.SendSignal ||
                            definition.Action == TextStudioReactionAction.PlayAudioCue)
                        {
                            if (definition.Action == TextStudioReactionAction.SendSignal)
                            {
                                if (_signalCount == _signals.Length && _signals.Length < _capacity)
                                    Array.Resize(ref _signals, Math.Min(_capacity, _signals.Length * 2));
                                if (_signalCount < _signals.Length)
                                    _signals[_signalCount++] = new TextStudioReactionContext(TextStudioReactionTrigger.Signal,
                                        run.Context.UnitIndex, run.Context.Generation, definition.Signal, run.Context.Intensity,
                                        _sessionId, run.Context.SourceGeneration, run.Context.TransactionId,
                                        run.Context.ChangeKind, run.Context.EventTime,
                                        run.Context.WordStart, run.Context.WordEnd,
                                        run.Context.LineStart, run.Context.LineEnd,
                                        run.SelectionStart, run.SelectionEnd,
                                        run.Context.TargetBindingKey,
                                        run.Context.ResponseDuration, run.Context.RandomSeed);
                                else SuppressedCount++;
                            }
                            else
                            {
                                if (_audioCueCount == _audioCues.Length && _audioCues.Length < _capacity)
                                    Array.Resize(ref _audioCues, Math.Min(_capacity, _audioCues.Length * 2));
                                if (_audioCueCount < _audioCues.Length)
                                    _audioCues[_audioCueCount++] = new TextStudioReactionContext(
                                        run.Context.Trigger, run.Context.UnitIndex, run.Context.Generation,
                                        definition.AudioCue, run.Context.Intensity, _sessionId,
                                        run.Context.SourceGeneration, run.Context.TransactionId,
                                        run.Context.ChangeKind, run.Context.EventTime,
                                        run.Context.WordStart, run.Context.WordEnd,
                                        run.Context.LineStart, run.Context.LineEnd,
                                        run.SelectionStart, run.SelectionEnd,
                                        run.Context.TargetBindingKey,
                                        run.Context.ResponseDuration, run.Context.RandomSeed);
                                else SuppressedCount++;
                            }
                            QueueDependents(in run, TextStudioReactionDependencyEvent.Completion, run.Start);
                            ReleaseSlot(i);
                            i = next;
                            continue;
                        }
                        float progress = (float)((_time - run.Start) / definition.Duration);
                        TextStudioTextRange range = run.SelectionStart < 0
                            ? TextStudioTextRange.All
                            : new TextStudioTextRange(run.SelectionStart, run.SelectionEnd);
                        _hasOutputToRestore |= core.ApplyExternalEffect(definition.Effect, Mathf.Clamp01(progress),
                            run.Context.Intensity, range);
                        if (progress >= 1f)
                        {
                            QueueDependents(in run, TextStudioReactionDependencyEvent.Completion,
                                run.Start + definition.Duration);
                            ReleaseSlot(i);
                        }
                        i = next;
                    }
                    if (_pendingChainCount == 0) break;
                }
            }
            finally
            {
                _sampling = false;
                if (_disposeRequested && !_cancelling) DisposePools();
            }
        }

        bool InputAllows(in Definition definition)
        {
            if (definition.EnabledInputKey == null ||
                !(_boundActionHost is ITextStudioReactionBooleanInputProvider inputs))
                return true;
            try
            {
                return !inputs.TryGetReactionBooleanInput(definition.EnabledInputKey, out bool enabled) ||
                       enabled;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        void SampleCustomRun(int slot, ITextStudioReactionActionHost host)
        {
            ref Run run = ref _runs[slot];
            ref readonly Definition definition = ref _definitions[run.Definition];
            TextStudioReactionStatePool pool = _statePools[run.Definition];
            uint generation = _generation;
            ITextStudioReactionActionState state;
            TextStudioReactionActionRequirements requirements = definition.Requirements;
            if (run.Lease.IsValid)
            {
                if (!run.Lease.TryGetAnchor(out _))
                {
                    if (run.CustomStarted) CancelCustomRun(ref run, TextStudioReactionCancelReason.TargetLost);
                    if (generation != _generation) return;
                    ReleaseSlot(slot);
                    LastCancellationReason = TextStudioReactionCancelReason.TargetLost;
                    if (CancelledCount < int.MaxValue) CancelledCount++;
                    return;
                }
                requirements &= ~(TextStudioReactionActionRequirements.RetainedGeometry |
                                  TextStudioReactionActionRequirements.RetainedAnchor |
                                  TextStudioReactionActionRequirements.LiveAnchor);
            }
            if (requirements != TextStudioReactionActionRequirements.None &&
                (host == null || (requirements & ~host.Capabilities) != 0))
            {
                bool started = run.CustomStarted;
                if (started) CancelCustomRun(ref run, TextStudioReactionCancelReason.UnsupportedCapability);
                if (generation != _generation) return;
                ReleaseSlot(slot);
                if (started)
                {
                    if (CancelledCount < int.MaxValue) CancelledCount++;
                }
                else SuppressedCount++;
                LastCancellationReason = TextStudioReactionCancelReason.UnsupportedCapability;
                return;
            }
            bool missingLiveAnchor =
                (requirements & TextStudioReactionActionRequirements.LiveAnchor) != 0 &&
                !host.TryGetReactionAnchor(in run.Context, out _);
            bool missingNamedTarget =
                (requirements & TextStudioReactionActionRequirements.NamedTarget) != 0 &&
                (_targetSlots[run.Definition] < 0 ||
                 !host.TryGetReactionTarget(_targetSlots[run.Definition], out _));
            if (missingLiveAnchor || missingNamedTarget)
            {
                if (missingLiveAnchor && !missingNamedTarget && !run.CustomStarted &&
                    _time < run.Start + definition.Duration &&
                    host is ITextStudioReactionPendingAnchorHost pendingHost &&
                    pendingHost.IsReactionAnchorPending(in run.Context))
                    return;
                if (run.CustomStarted) CancelCustomRun(ref run, TextStudioReactionCancelReason.TargetLost);
                if (generation != _generation) return;
                ReleaseSlot(slot);
                LastCancellationReason = TextStudioReactionCancelReason.TargetLost;
                if (CancelledCount < int.MaxValue) CancelledCount++;
                return;
            }
            if (!run.CustomStarted)
            {
                if (definition.Command is ITextStudioReactionStartPolicy ||
                    definition.Command is ITextStudioReactionLeaseStartPolicy)
                {
                    bool canStart;
                    try
                    {
                        canStart = !(definition.Command is ITextStudioReactionStartPolicy startPolicy) ||
                                   startPolicy.CanStart(in run.Context, host);
                        if (canStart && definition.Command is ITextStudioReactionLeaseStartPolicy leasePolicy)
                            canStart = leasePolicy.CanStart(in run.Context, host, in run.Lease);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        canStart = false;
                    }
                    if (generation != _generation) return;
                    if (!canStart)
                    {
                        SuppressedCount++;
                        LastCancellationReason = TextStudioReactionCancelReason.TargetLost;
                        ReleaseSlot(slot);
                        return;
                    }
                }
                int stateSlot;
                try
                {
                    if (!pool.TryAcquire(out stateSlot, out state))
                    {
                        SuppressedCount++;
                        ReleaseSlot(slot);
                        return;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    SuppressedCount++;
                    ReleaseSlot(slot);
                    return;
                }
                run.CustomStateSlot = stateSlot;
                run.CustomStarted = true;
                try { state.Start(in run.Context, host, in run.Lease); }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    if (generation == _generation)
                    {
                        CancelCustomRun(ref run, TextStudioReactionCancelReason.ActionFailed);
                        if (generation == _generation) ReleaseSlot(slot);
                    }
                    return;
                }
                if (generation != _generation) return;
            }
            state = pool.Get(run.CustomStateSlot);
            float progress = Mathf.Clamp01((float)((_time - run.Start) / definition.Duration));
            TextStudioReactionActionSample sample;
            try { sample = state.Sample(in run.Context, progress, _time); }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (generation == _generation)
                {
                    CancelCustomRun(ref run, TextStudioReactionCancelReason.ActionFailed);
                    if (generation == _generation) ReleaseSlot(slot);
                }
                return;
            }
            if (generation != _generation) return;
            if (sample.TargetLost)
            {
                CancelCustomRun(ref run, TextStudioReactionCancelReason.TargetLost);
                if (generation != _generation) return;
                ReleaseSlot(slot);
                LastCancellationReason = TextStudioReactionCancelReason.TargetLost;
                if (CancelledCount < int.MaxValue) CancelledCount++;
                return;
            }
            TextStudioReactionActionEvents events = sample.Events & definition.Command.SupportedEvents;
            double eventTime = double.IsNaN(sample.EventTime) || double.IsInfinity(sample.EventTime) ||
                sample.EventTime < run.Start || sample.EventTime > _time
                    ? _time : sample.EventTime;
            if ((events & TextStudioReactionActionEvents.Arrived) != 0 &&
                (run.DeliveredEvents & TextStudioReactionActionEvents.Arrived) == 0)
                QueueDependents(in run, TextStudioReactionDependencyEvent.Arrival, eventTime);
            if ((events & TextStudioReactionActionEvents.Impacted) != 0 &&
                (run.DeliveredEvents & TextStudioReactionActionEvents.Impacted) == 0)
                QueueDependents(in run, TextStudioReactionDependencyEvent.Impact, eventTime);
            if ((events & TextStudioReactionActionEvents.Broke) != 0 &&
                (run.DeliveredEvents & TextStudioReactionActionEvents.Broke) == 0)
                QueueDependents(in run, TextStudioReactionDependencyEvent.Break, eventTime);
            if ((events & TextStudioReactionActionEvents.Returned) != 0 &&
                (run.DeliveredEvents & TextStudioReactionActionEvents.Returned) == 0)
                QueueDependents(in run, TextStudioReactionDependencyEvent.Return, eventTime);
            run.DeliveredEvents |= events;
            if (progress < 1f && (events & TextStudioReactionActionEvents.Completed) == 0)
                return;
            try
            {
                run.CustomCompleting = true;
                state.Complete(in run.Context);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (generation == _generation)
                {
                    run.CustomCompleting = false;
                    CancelCustomRun(ref run, TextStudioReactionCancelReason.ActionFailed);
                    if (generation == _generation) ReleaseSlot(slot);
                }
                return;
            }
            if (generation != _generation) return;
            run.CustomCompleting = false;
            double completionTime = (events & TextStudioReactionActionEvents.Completed) != 0
                ? eventTime : run.Start + definition.Duration;
            QueueDependents(in run, TextStudioReactionDependencyEvent.Completion, completionTime);
            pool.Release(run.CustomStateSlot);
            run.CustomStateSlot = -1;
            run.CustomStarted = false;
            ReleaseSlot(slot);
        }

        void CancelCustomRun(ref Run run, TextStudioReactionCancelReason reason)
        {
            if (!run.CustomStarted || run.CustomStateSlot < 0) return;
            TextStudioReactionStatePool pool = _statePools[run.Definition];
            ITextStudioReactionActionState state = pool.Get(run.CustomStateSlot);
            bool wasCancelling = _cancelling;
            _cancelling = true;
            try
            {
                if (!run.CustomCompleting)
                    state.Cancel(in run.Context, reason);
            }
            catch (Exception exception) { Debug.LogException(exception); }
            finally
            {
                pool.Release(run.CustomStateSlot);
                run.CustomStateSlot = -1;
                run.CustomStarted = false;
                run.CustomCompleting = false;
                _cancelling = wasCancelling;
                if (!wasCancelling) DrainDeferredCancellation();
            }
        }

        void DrainDeferredCancellation()
        {
            if (_cancelling || !_hasDeferredCancellation) return;
            TextStudioReactionCancelReason reason = _deferredCancellationReason;
            _hasDeferredCancellation = false;
            Cancel(reason);
        }

        int AcquireSlot()
        {
            if (_freeHead < 0 && _runs.Length < _capacity)
            {
                int previousLength = _runs.Length;
                Array.Resize(ref _runs, Math.Min(_capacity, previousLength * 2));
                for (int i = previousLength; i < _runs.Length; i++) _runs[i].Next = i + 1;
                _runs[_runs.Length - 1].Next = -1;
                _freeHead = previousLength;
            }
            if (_freeHead < 0) return -1;
            int slot = _freeHead;
            _freeHead = _runs[slot].Next;
            _runs[slot].Previous = _activeTail;
            _runs[slot].Next = -1;
            if (_activeTail >= 0) _runs[_activeTail].Next = slot;
            else _activeHead = slot;
            _activeTail = slot;
            ActiveCount++;
            return slot;
        }

        void ReleaseSlot(int slot)
        {
            ref Run run = ref _runs[slot];
            ref readonly Definition definition = ref _definitions[run.Definition];
            if (definition.Overlap != TextStudioReactionOverlap.Allow &&
                _activeByTarget.TryGetValue(run.TargetKey, out int mappedSlot) && mappedSlot == slot)
                _activeByTarget.Remove(run.TargetKey);
            int next = _runs[slot].Next;
            int previous = _runs[slot].Previous;
            if (previous >= 0) _runs[previous].Next = next;
            else _activeHead = next;
            if (next >= 0) _runs[next].Previous = previous;
            else _activeTail = previous;
            TextStudioReactionLease lease = run.Lease;
            _runs[slot] = default;
            _runs[slot].Next = _freeHead;
            _freeHead = slot;
            ActiveCount--;
            ReleaseLease(lease);
        }

        static void ReleaseLease(TextStudioReactionLease lease)
        {
            try { lease.Release(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        static void ReleaseLease(ref Run run)
        {
            TextStudioReactionLease lease = run.Lease;
            run.Lease = default;
            ReleaseLease(lease);
        }

        void InitializeSlots()
        {
            _freeHead = 0;
            _activeHead = _activeTail = -1;
            for (int i = 0; i < _runs.Length; i++) _runs[i].Next = i + 1;
            _runs[_runs.Length - 1].Next = -1;
        }

        /// <summary>Callbacks run only after the host has committed its rendered state.</summary>
        internal void FlushSignals(Action<TextStudioReactionContext> callback)
        {
            if (_flushing || _signalCount == 0) return;
            _flushing = true;
            uint generation = _generation;
            int count = _signalCount;
            _signalCount = 0;
            try
            {
                for (int i = 0; i < count && generation == _generation; i++)
                {
                    TextStudioReactionContext context = _signals[i];
                    _signals[i] = default;
                    callback?.Invoke(context);
                }
            }
            finally
            {
                Array.Clear(_signals, 0, count);
                _flushing = false;
            }
        }

        void QueueDependents(in Run run, TextStudioReactionDependencyEvent eventType,
            double eventTime)
        {
            if (_firstDependent == null) return;
            for (int child = _firstDependent[run.Definition]; child >= 0; child = _nextDependent[child])
            {
                if (_definitions[child].DependencyEvent != eventType) continue;
                if (_pendingChainCount == _capacity) { SuppressedCount++; continue; }
                if (_pendingChainCount == _pendingChains.Length)
                    Array.Resize(ref _pendingChains, Math.Min(_capacity, _pendingChains.Length * 2));
                _pendingChains[_pendingChainCount++] = new PendingChain
                {
                    DefinitionIndex = child,
                    Context = run.SourceContext,
                    EventTime = eventTime
                };
            }
        }

        internal void FlushAudioCues(Action<TextStudioReactionContext> callback)
        {
            if (_flushing || _audioCueCount == 0)
                return;
            _flushing = true;
            uint generation = _generation;
            int count = _audioCueCount;
            _audioCueCount = 0;
            try
            {
                for (int i = 0; i < count && generation == _generation; i++)
                {
                    TextStudioReactionContext context = _audioCues[i];
                    _audioCues[i] = default;
                    callback?.Invoke(context);
                }
            }
            finally
            {
                Array.Clear(_audioCues, 0, count);
                _flushing = false;
            }
        }

        internal void Stop() => _accepting = false;

        internal void AbortPendingOutgoingCapture()
        {
            for (int slot = _activeHead; slot >= 0;)
            {
                int next = _runs[slot].Next;
                if (_runs[slot].PendingCommit)
                {
                    ReleaseSlot(slot);
                    if (CancelledCount < int.MaxValue) CancelledCount++;
                }
                slot = next;
            }
        }

        internal void RetireForTextChange(ulong transactionId)
        {
            if (_cancelling)
            {
                _hasDeferredCancellation = true;
                _deferredCancellationReason = TextStudioReactionCancelReason.TextReplaced;
                return;
            }
            bool hasRetained = false;
            for (int slot = _activeHead; slot >= 0; slot = _runs[slot].Next)
                if (_runs[slot].Lease.IsValid) { hasRetained = true; break; }
            if (!hasRetained)
            {
                Reset(TextStudioReactionCancelReason.TextReplaced);
                return;
            }
            _cancelling = true;
            LastCancellationReason = TextStudioReactionCancelReason.TextReplaced;
            unchecked { _generation++; }
            if (_generation == 0) _generation = 1;
            try
            {
                for (int slot = _activeHead; slot >= 0;)
                {
                    int next = _runs[slot].Next;
                    if (!_runs[slot].Lease.IsValid ||
                        _runs[slot].PendingCommit &&
                        _runs[slot].Context.TransactionId != transactionId)
                    {
                        CancelCustomRun(ref _runs[slot], TextStudioReactionCancelReason.TextReplaced);
                        ReleaseSlot(slot);
                        if (CancelledCount < int.MaxValue) CancelledCount++;
                    }
                    else if (_runs[slot].PendingCommit)
                    {
                        _runs[slot].PendingCommit = false;
                        _runs[slot].Start = _time + _definitions[_runs[slot].Definition].Delay;
                        _runs[slot].Context = _runs[slot].Context.AtEventTime(_time);
                    }
                    slot = next;
                }
                int queued = _pendingChainCount + _pendingDispatchCount + _signalCount + _audioCueCount;
                CancelledCount = queued > int.MaxValue - CancelledCount
                    ? int.MaxValue : CancelledCount + queued;
                if (_signals != null) Array.Clear(_signals, 0, _signalCount);
                if (_audioCues != null) Array.Clear(_audioCues, 0, _audioCueCount);
                if (_pendingChains != null) Array.Clear(_pendingChains, 0, _pendingChainCount);
                if (_pendingDispatches != null) Array.Clear(_pendingDispatches, 0, _pendingDispatchCount);
                _signalCount = _audioCueCount = _pendingChainCount = _pendingDispatchCount = 0;
                _activeByTarget?.Clear();
                Array.Clear(_nextAllowed, 0, _nextAllowed.Length);
                _nextAllowedByTarget?.Clear();
                Array.Clear(_occurrences, 0, _occurrences.Length);
                _hasOutputToRestore = false;
                _accepting = true;
            }
            finally
            {
                _cancelling = false;
                DrainDeferredCancellation();
                if (_disposeRequested && !_sampling) DisposePools();
            }
        }

        internal void Cancel(TextStudioReactionCancelReason reason)
        {
            if (_cancelling)
            {
                _hasDeferredCancellation = true;
                _deferredCancellationReason = reason;
                return;
            }
            _cancelling = true;
            int cancelled = ActiveCount + _pendingChainCount + _pendingDispatchCount +
                _signalCount + _audioCueCount;
            CancelledCount = cancelled > int.MaxValue - CancelledCount
                ? int.MaxValue
                : CancelledCount + cancelled;
            LastCancellationReason = reason;
            unchecked { _generation++; }
            if (_generation == 0) _generation = 1;
            try
            {
                for (int slot = _activeHead; slot >= 0; slot = _runs[slot].Next)
                {
                    CancelCustomRun(ref _runs[slot], reason);
                    ReleaseLease(ref _runs[slot]);
                }
                Array.Clear(_runs, 0, _runs.Length);
                InitializeSlots();
                if (_signals != null)
                    Array.Clear(_signals, 0, _signals.Length);
                if (_audioCues != null)
                    Array.Clear(_audioCues, 0, _audioCues.Length);
                if (_pendingChains != null)
                    Array.Clear(_pendingChains, 0, _pendingChainCount);
                if (_pendingDispatches != null)
                    Array.Clear(_pendingDispatches, 0, _pendingDispatchCount);
                _pendingChainCount = _pendingDispatchCount = 0;
                _activeByTarget?.Clear();
                ActiveCount = _signalCount = _audioCueCount = 0;
                _hasOutputToRestore = false;
            }
            finally
            {
                _cancelling = false;
                DrainDeferredCancellation();
                if (_disposeRequested && !_sampling) DisposePools();
            }
        }

        internal void Reset(TextStudioReactionCancelReason reason = TextStudioReactionCancelReason.ExplicitCancel)
        {
            Cancel(reason);
            _outgoingLimitTransactionId = 0;
            if (_outgoingLimitCounts != null)
                Array.Clear(_outgoingLimitCounts, 0, _outgoingLimitCounts.Length);
            Array.Clear(_nextAllowed, 0, _nextAllowed.Length);
            _nextAllowedByTarget?.Clear();
            Array.Clear(_occurrences, 0, _occurrences.Length);
            _accepting = true;
        }

        internal void Dispose(TextStudioReactionCancelReason reason = TextStudioReactionCancelReason.Disposed)
        {
            if (_disposed) return;
            Cancel(reason);
            _accepting = false;
            _disposeRequested = true;
            if (!_sampling && !_cancelling) DisposePools();
        }

        void DisposePools()
        {
            if (_disposed) return;
            _disposed = true;
            for (int i = 0; i < _statePools.Length; i++)
                _statePools[i]?.DisposeAll();
        }

        internal static string ValidateDependencies(IReadOnlyList<TextStudioReactionLayer> layers, int capacity) =>
            ValidateDependencies(layers, capacity, out _);

        static string ValidateDependencies(IReadOnlyList<TextStudioReactionLayer> layers, int capacity,
            out Dictionary<TextStudioReactionLayer, ITextStudioReactionCommand> commands)
        {
            commands = null;
            if (capacity < 1 || capacity > 4096)
                return "Reaction concurrent-response capacity must be between 1 and 4096.";
            if (layers == null) return null;
            Dictionary<string, TextStudioReactionLayer> byId = null;
            for (int i = 0; i < layers.Count; i++)
            {
                TextStudioReactionLayer layer = layers[i];
                if (!IsUsable(layer)) continue;
                if (string.IsNullOrEmpty(layer.Id))
                    return "Enabled reaction layers need a stable ID.";
                if (byId == null)
                    byId = new Dictionary<string, TextStudioReactionLayer>(layers.Count, StringComparer.Ordinal);
                if (byId.ContainsKey(layer.Id))
                    return "Duplicate reaction layer ID: " + layer.Id;
                byId.Add(layer.Id, layer);
                if (layer.Which == TextStudioReactionTarget.ChangedOutgoingUnits &&
                    layer.Action != TextStudioReactionAction.Custom)
                    return "Layer '" + layer.Name + "' needs a retained-output custom action for outgoing units.";
                if (layer.Action != TextStudioReactionAction.Custom) continue;
                ITextStudioReactionCommand command;
                string compileError;
                try
                {
                    if (!layer.CustomAction.TryCompile(out command, out compileError) || command == null)
                        return "Layer '" + layer.Name + "' action cannot compile: " +
                            (string.IsNullOrEmpty(compileError) ? "No command was supplied." : compileError);
                }
                catch (Exception exception)
                {
                    return "Layer '" + layer.Name + "' action compiler failed: " + exception.Message;
                }
                if (command is ITextStudioReactionConcurrencyCommand concurrency &&
                    (concurrency.MaxConcurrent < 0 || concurrency.MaxConcurrent > 4096))
                    return "Layer '" + layer.Name + "' action declares an invalid concurrent-response limit.";
                if (command is ITextStudioReactionChangeLimitCommand changeLimit &&
                    (changeLimit.MaxUnitsPerChange < 0 || changeLimit.MaxUnitsPerChange > 4096 ||
                     changeLimit.MaxUnitsPerChange > 0 &&
                     layer.Which != TextStudioReactionTarget.ChangedOutgoingUnits))
                    return "Layer '" + layer.Name + "' action needs an outgoing-unit target and a per-change limit between 0 and 4096.";
                const TextStudioReactionActionRequirements knownRequirements =
                    TextStudioReactionActionRequirements.LiveAnchor |
                    TextStudioReactionActionRequirements.RetainedGeometry |
                    TextStudioReactionActionRequirements.NamedTarget |
                    TextStudioReactionActionRequirements.PhysicsWorld |
                    TextStudioReactionActionRequirements.RetainedAnchor |
                    TextStudioReactionActionRequirements.WorldObjectOutput |
                    TextStudioReactionActionRequirements.UIParticles;
                if ((command.Requirements & ~knownRequirements) != 0 ||
                    (uint)command.PreviewCapability > (uint)TextStudioReactionPreviewCapability.BoundedResimulation)
                    return "Layer '" + layer.Name + "' action declares invalid capabilities.";
                bool capturesOutgoingAnchor =
                    (command as ITextStudioReactionTriggerAnchorCommand)?.CaptureTriggerAnchor == true &&
                    (command.Requirements & TextStudioReactionActionRequirements.LiveAnchor) != 0;
                if (layer.Which == TextStudioReactionTarget.ChangedOutgoingUnits &&
                    (command.Requirements & (TextStudioReactionActionRequirements.RetainedGeometry |
                                             TextStudioReactionActionRequirements.RetainedAnchor)) == 0 &&
                    !capturesOutgoingAnchor)
                    return "Layer '" + layer.Name + "' needs a retained output or anchor for outgoing units.";
                if (layer.Which != TextStudioReactionTarget.ChangedOutgoingUnits &&
                    (command.Requirements & (TextStudioReactionActionRequirements.RetainedGeometry |
                                             TextStudioReactionActionRequirements.RetainedAnchor)) != 0)
                    return "Layer '" + layer.Name + "' must select changed outgoing units for a retained output.";
                const TextStudioReactionResourceClass knownResources =
                    TextStudioReactionResourceClass.GlyphGeometry |
                    TextStudioReactionResourceClass.MaterialState |
                    TextStudioReactionResourceClass.InlineSprite |
                    TextStudioReactionResourceClass.Decoration |
                    TextStudioReactionResourceClass.MultiLayerGeometry;
                if ((uint)command.CaptureIntent > (uint)TextStudioReactionCaptureIntent.Detach ||
                    (command.CaptureResources & ~knownResources) != 0 ||
                    (command.Requirements & TextStudioReactionActionRequirements.RetainedGeometry) != 0 &&
                    command.CaptureResources == TextStudioReactionResourceClass.None ||
                    command.CaptureIntent == TextStudioReactionCaptureIntent.Detach &&
                    command.CaptureResources == TextStudioReactionResourceClass.None)
                    return "Layer '" + layer.Name + "' declares invalid capture resources.";
                if ((command.Requirements & TextStudioReactionActionRequirements.NamedTarget) != 0 &&
                    string.IsNullOrEmpty(layer.TargetBindingKey))
                    return "Layer '" + layer.Name + "' needs a named target binding key.";
                const TextStudioReactionActionEvents knownEvents =
                    TextStudioReactionActionEvents.Completed |
                    TextStudioReactionActionEvents.Arrived |
                    TextStudioReactionActionEvents.Impacted |
                    TextStudioReactionActionEvents.Broke |
                    TextStudioReactionActionEvents.Returned;
                if ((command.SupportedEvents & ~knownEvents) != 0)
                    return "Layer '" + layer.Name + "' action declares invalid events.";
                commands ??= new Dictionary<TextStudioReactionLayer, ITextStudioReactionCommand>();
                commands.Add(layer, command);
            }
            if (byId == null) return null;
            foreach (KeyValuePair<string, TextStudioReactionLayer> entry in byId)
            {
                TextStudioReactionLayer layer = entry.Value;
                if (string.IsNullOrEmpty(layer.DependsOnId)) continue;
                if (layer.Which == TextStudioReactionTarget.ChangedOutgoingUnits)
                    return "Outgoing capture layers cannot depend on a later event.";
                TextStudioReactionLayer cursor = layer;
                for (int step = 0; !string.IsNullOrEmpty(cursor.DependsOnId); step++)
                {
                    if (step >= byId.Count)
                        return "Reaction dependency cycle includes layer '" + layer.Name + "'.";
                    if (!byId.TryGetValue(cursor.DependsOnId, out TextStudioReactionLayer parent))
                        return "Layer '" + cursor.Name + "' depends on a missing, disabled, or invalid layer ID: " + cursor.DependsOnId;
                    if (parent.Which == TextStudioReactionTarget.ChangedOutgoingUnits &&
                        (layer.Which != TextStudioReactionTarget.Text ||
                         layer.Action != TextStudioReactionAction.SendSignal &&
                         layer.Action != TextStudioReactionAction.PlayAudioCue))
                        return "A retained outgoing response can only start a text-wide signal or audio cue; spatial follow-ups need a retained anchor.";
                    if (parent.Which == TextStudioReactionTarget.ChangedOutgoingUnits &&
                        layer.Action == TextStudioReactionAction.PlayAudioCue &&
                        string.Equals(layer.AudioCue, TextStudioAudioPlayer.GlyphCueRoute,
                            StringComparison.Ordinal))
                        return "The current-glyph audio cue cannot follow a retained outgoing response; use a named cue from the Audio Set.";
                    if (cursor.DependencyEvent != TextStudioReactionDependencyEvent.Completion)
                    {
                        if ((uint)cursor.DependencyEvent > (uint)TextStudioReactionDependencyEvent.Return)
                            return "Layer '" + cursor.Name + "' has an invalid dependency event.";
                        TextStudioReactionActionEvents requiredEvent = cursor.DependencyEvent switch
                        {
                            TextStudioReactionDependencyEvent.Arrival => TextStudioReactionActionEvents.Arrived,
                            TextStudioReactionDependencyEvent.Impact => TextStudioReactionActionEvents.Impacted,
                            TextStudioReactionDependencyEvent.Break => TextStudioReactionActionEvents.Broke,
                            TextStudioReactionDependencyEvent.Return => TextStudioReactionActionEvents.Returned,
                            _ => TextStudioReactionActionEvents.None
                        };
                        if (parent.Action != TextStudioReactionAction.Custom ||
                            !commands.TryGetValue(parent, out ITextStudioReactionCommand parentCommand) ||
                            (parentCommand.SupportedEvents & requiredEvent) == 0)
                            return "Layer '" + cursor.Name + "' needs an action that supports " + cursor.DependencyEvent + ".";
                    }
                    if (cursor.When != parent.When ||
                        !string.Equals(cursor.TriggerName, parent.TriggerName, StringComparison.Ordinal))
                        return "Layer '" + cursor.Name + "' and its prerequisite must use the same event and trigger name.";
                    cursor = parent;
                }
            }
            return null;
        }

        static bool IsUsable(TextStudioReactionLayer layer) => layer != null && layer.Enabled &&
            (uint)layer.When <= (uint)TextStudioReactionTrigger.UnitRevealComplete &&
            (uint)layer.Which <= (uint)TextStudioReactionTarget.ChangedOutgoingUnits &&
            (layer.Which != TextStudioReactionTarget.ChangedUnits &&
             layer.Which != TextStudioReactionTarget.ChangedOutgoingUnits ||
             layer.When == TextStudioReactionTrigger.TextChanged) &&
            FinitePositive(layer.Intensity) > 0f && FinitePositive(layer.Chance) > 0f &&
            ((layer.Action == TextStudioReactionAction.PlayEffect &&
              (layer.EffectSource == TextStudioReactionEffectSource.CustomAsset
                  ? layer.Effect != null
                  : !string.IsNullOrEmpty(layer.EffectId) &&
                    TextStudioRegistry.Effects.TryGetQuiet(layer.EffectId, out _))) ||
             (layer.Action == TextStudioReactionAction.SendSignal && !string.IsNullOrEmpty(layer.Signal)) ||
             (layer.Action == TextStudioReactionAction.PlayAudioCue && !string.IsNullOrEmpty(layer.AudioCue)) ||
             (layer.Action == TextStudioReactionAction.Custom && layer.CustomAction != null));

        static float FinitePositive(float value) => value > 0f && !float.IsInfinity(value) ? value : 0f;

        static uint StableHash(string value)
        {
            uint hash = 2166136261;
            if (value != null)
                for (int i = 0; i < value.Length; i++) hash = unchecked((hash ^ value[i]) * 16777619);
            return hash;
        }

        static uint RandomBits(uint seed, uint occurrence)
        {
            uint value = unchecked(seed ^ (occurrence * 747796405u));
            value ^= value >> 16;
            value = unchecked(value * 2246822519u);
            value ^= value >> 13;
            return value;
        }

        static float Random01(uint bits) => (bits & 0x00ffffff) / 16777216f;
    }
}
