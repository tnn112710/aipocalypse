using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TextStudio.Editor
{
    /// <summary>Editor-owned, scene-free playback of qualified retained motion reactions.</summary>
#if UNITY_6000_7_OR_NEWER
    // Preview resources are disposed explicitly before entering Play Mode and on editor shutdown.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed class TextStudioReactionPreviewSession : IDisposable
    {
        static readonly HashSet<TextStudioReactionPreviewSession> s_active =
            new HashSet<TextStudioReactionPreviewSession>();

        static TextStudioReactionPreviewSession()
        {
            AssemblyReloadEvents.beforeAssemblyReload += DisposeAll;
            EditorApplication.quitting += DisposeAll;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) DisposeAll();
            };
        }

        static void DisposeAll()
        {
            if (s_active.Count == 0) return;
            TextStudioReactionPreviewSession[] sessions = new TextStudioReactionPreviewSession[s_active.Count];
            s_active.CopyTo(sessions);
            for (int i = 0; i < sessions.Length; i++)
                try { sessions[i].Dispose(); }
                catch (Exception exception) { Debug.LogException(exception); }
        }

        sealed class OutputLedger : ITextStudioReactionCaptureProvider,
            ITextStudioReactionVisualOutputProvider
        {
            readonly bool[] _active;
            readonly uint[] _versions;
            readonly int[] _units;
            readonly TextStudioReactionVisualSample[] _samples;

            internal OutputLedger(int capacity)
            {
                _active = new bool[capacity];
                _versions = new uint[capacity];
                _units = new int[capacity];
                _samples = new TextStudioReactionVisualSample[capacity];
            }

            internal int ActiveCount { get; private set; }
            internal int Capacity => _active.Length;

            public TextStudioReactionCaptureCapability QuerySupport(
                in TextStudioReactionCaptureRequest request) =>
                request.Intent == TextStudioReactionCaptureIntent.Copy &&
                (request.Resources & ~(TextStudioReactionResourceClass.GlyphGeometry |
                    TextStudioReactionResourceClass.MaterialState)) == 0
                    ? new TextStudioReactionCaptureCapability(TextStudioReactionCaptureSupport.Exact)
                    : TextStudioReactionCaptureCapability.Unsupported(
                        "This preview only owns copied glyph geometry and material state.");

            public bool TryCapture(in TextStudioReactionCaptureRequest request,
                out TextStudioReactionLease lease)
            {
                lease = default;
                if (!QuerySupport(in request).IsSupported) return false;
                for (int slot = 0; slot < _active.Length; slot++)
                {
                    if (_active[slot]) continue;
                    _active[slot] = true;
                    unchecked { _versions[slot]++; }
                    if (_versions[slot] == 0) _versions[slot] = 1;
                    _units[slot] = request.Context.UnitIndex;
                    _samples[slot] = default;
                    ActiveCount++;
                    lease = new TextStudioReactionLease(this, slot, _versions[slot]);
                    return true;
                }
                return false;
            }

            public bool TryGetAnchor(int slot, uint version, out TextStudioReactionAnchor anchor)
            {
                anchor = default;
                return IsCurrent(slot, version);
            }

            public bool TrySubmit(int slot, uint version,
                in TextStudioReactionVisualSample sample)
            {
                if (!IsCurrent(slot, version)) return false;
                _samples[slot] = sample;
                return true;
            }

            public void Release(int slot, uint version)
            {
                if (!IsCurrent(slot, version)) return;
                _active[slot] = false;
                _samples[slot] = default;
                ActiveCount--;
            }

            internal bool TryGet(int slot, out int unit,
                out TextStudioReactionVisualSample sample)
            {
                if (slot >= 0 && slot < _active.Length && _active[slot])
                {
                    unit = _units[slot];
                    sample = _samples[slot];
                    return true;
                }
                unit = -1;
                sample = default;
                return false;
            }

            internal void Clear()
            {
                for (int i = 0; i < _active.Length; i++)
                {
                    _active[i] = false;
                    _samples[i] = default;
                    unchecked { _versions[i]++; }
                    if (_versions[i] == 0) _versions[i] = 1;
                }
                ActiveCount = 0;
            }

            bool IsCurrent(int slot, uint version) =>
                slot >= 0 && slot < _active.Length && _active[slot] &&
                _versions[slot] == version;
        }

        sealed class PreviewHost : ITextStudioReactionActionHost
        {
            readonly OutputLedger _outputs;
            internal PreviewHost(OutputLedger outputs, TextStudioMotionPolicy motionPolicy)
            {
                _outputs = outputs;
                MotionPolicy = motionPolicy;
            }

            public TextStudioReactionActionRequirements Capabilities =>
                TextStudioReactionActionRequirements.RetainedGeometry;
            public TextStudioMotionPolicy MotionPolicy { get; }
            public bool TryGetReactionAnchor(in TextStudioReactionContext context,
                out TextStudioReactionAnchor anchor)
            {
                anchor = default;
                return false;
            }
            public int ResolveReactionTargetSlot(string key) => -1;
            public bool TryGetReactionTarget(int slot, out TextStudioReactionAnchor anchor)
            {
                anchor = default;
                return false;
            }
            public bool TryGetReactionTarget(string key, out TextStudioReactionAnchor anchor)
            {
                anchor = default;
                return false;
            }
            public bool TryCaptureReactionOutput(in TextStudioReactionCaptureRequest request,
                out TextStudioReactionLease lease) => _outputs.TryCapture(in request, out lease);
        }

        readonly TextStudioReactionLayer[] _layers;
        readonly TextStudioReactionActionAsset[] _ownedActions;
        readonly string _beforeText;
        readonly string _afterText;
        readonly int _capacity;
        readonly uint _seed;
        readonly TextStudioMotionPolicy _motionPolicy;
        TextStudioRuntimeCore _core;
        OutputLedger _outputs;
        TextStudioCompiledPlan _beforePlan;
        TextStudioCompiledPlan _afterPlan;
        bool[] _outgoingChanged;
        float _time;
        bool _disposed;

        TextStudioReactionPreviewSession(TextStudioReactionLayer[] layers,
            TextStudioReactionActionAsset[] ownedActions, int capacity, uint seed,
            string beforeText, string afterText, TextStudioMotionPolicy motionPolicy)
        {
            _layers = layers;
            _ownedActions = ownedActions;
            _capacity = capacity;
            _seed = seed;
            _beforeText = beforeText;
            _afterText = afterText;
            _motionPolicy = motionPolicy;
            try { Rebuild(); }
            catch
            {
                ReleaseRuntime();
                throw;
            }
            s_active.Add(this);
        }

        internal static bool TryCreate(IReadOnlyList<TextStudioReactionLayer> source,
            int capacity, uint seed, string beforeText, string afterText,
            TextStudioMotionPolicy motionPolicy, string soloLayerId,
            out TextStudioReactionPreviewSession session, out string reason)
        {
            return TryCreate(source, capacity, seed, beforeText, afterText,
                motionPolicy, soloLayerId, 1f, out session, out reason);
        }

        internal static bool TryCreate(IReadOnlyList<TextStudioReactionLayer> source,
            int capacity, uint seed, string beforeText, string afterText,
            TextStudioMotionPolicy motionPolicy, string soloLayerId,
            float intensityScale,
            out TextStudioReactionPreviewSession session, out string reason)
        {
            session = null;
            reason = null;
            if (Application.isPlaying)
            {
                reason = "The isolated editor preview is available only in Edit Mode.";
                return false;
            }
            if (source == null || source.Count == 0 || capacity < 1 || capacity > 4096 ||
                string.IsNullOrEmpty(beforeText) || string.IsNullOrEmpty(afterText) ||
                string.Equals(beforeText, afterText, StringComparison.Ordinal) ||
                float.IsNaN(intensityScale) || float.IsInfinity(intensityScale) ||
                intensityScale < 0f || intensityScale > 1f)
            {
                reason = "Choose a valid reaction stack and two different text samples.";
                return false;
            }

            if (!CanPreview(source, soloLayerId, out reason)) return false;
            bool soloExists = HasEnabledSolo(source, soloLayerId);
            TextStudioReactionLayer[] layers = TextStudioReactionLayer.CloneStack(source);
            List<TextStudioReactionActionAsset> owned = new List<TextStudioReactionActionAsset>();
            int eligible = 0;
            try
            {
                for (int i = 0; i < layers.Length; i++)
                {
                    TextStudioReactionLayer layer = layers[i];
                    if (!layer.Enabled || soloExists && source[i].Id != soloLayerId)
                    {
                        layer.Enabled = false;
                        continue;
                    }
                    TextStudioReactionActionAsset action = layer.CustomAction;
                    if (!action.TryCompile(out ITextStudioReactionCommand command,
                            out string error) || command == null ||
                        command.PreviewCapability != TextStudioReactionPreviewCapability.DeterministicSeek)
                    {
                        reason = "Layer '" + layer.Name + "' cannot seek safely: " + error;
                        return false;
                    }
                    TextStudioReactionActionAsset copy = UnityEngine.Object.Instantiate(action);
                    copy.hideFlags = HideFlags.HideAndDontSave;
                    owned.Add(copy);
                    layer.CustomAction = copy;
                    layer.Intensity *= intensityScale;
                    eligible++;
                }
                if (eligible == 0)
                {
                    reason = "No enabled retained-motion reaction is available to preview.";
                    return false;
                }
                session = new TextStudioReactionPreviewSession(layers, owned.ToArray(),
                    capacity, seed == 0 ? 1u : seed, beforeText, afterText, motionPolicy);
                return true;
            }
            catch (Exception exception)
            {
                reason = "The isolated reaction preview could not start: " + exception.Message;
                return false;
            }
            finally
            {
                if (session == null)
                    for (int i = 0; i < owned.Count; i++)
                        if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]);
            }
        }

        internal static bool CanPreview(IReadOnlyList<TextStudioReactionLayer> source,
            string soloLayerId, out string reason)
        {
            reason = null;
            if (source == null || source.Count == 0)
            {
                reason = "Add a reaction to preview.";
                return false;
            }
            bool soloExists = HasEnabledSolo(source, soloLayerId);
            int eligible = 0;
            for (int i = 0; i < source.Count; i++)
            {
                TextStudioReactionLayer layer = source[i];
                if (layer == null || !layer.Enabled || soloExists && layer.Id != soloLayerId)
                    continue;
                TextStudioReactionActionAsset action = layer.CustomAction;
                if (layer.Action != TextStudioReactionAction.Custom ||
                    layer.When != TextStudioReactionTrigger.TextChanged ||
                    layer.Which != TextStudioReactionTarget.ChangedOutgoingUnits ||
                    !string.IsNullOrEmpty(layer.DependsOnId) ||
                    action == null ||
                    action.GetType() != typeof(TextStudioReactionMotionActionAsset) &&
                    action.GetType() != typeof(TextStudioReactionBallisticMotionActionAsset))
                {
                    reason = "Layer '" + layer.Name +
                        "' is Play Mode-only for this text-change preview.";
                    return false;
                }
                eligible++;
            }
            if (eligible > 0) return true;
            reason = "Enable a retained outgoing-motion reaction to preview.";
            return false;
        }

        static bool HasEnabledSolo(IReadOnlyList<TextStudioReactionLayer> source,
            string soloLayerId)
        {
            if (string.IsNullOrEmpty(soloLayerId)) return false;
            for (int i = 0; i < source.Count; i++)
                if (source[i] != null && source[i].Enabled && source[i].Id == soloLayerId)
                    return true;
            return false;
        }

        internal int ActiveOutputCount => _outputs?.ActiveCount ?? 0;
        internal int OutputCapacity => _outputs?.Capacity ?? 0;
        internal float Time => _time;
        internal TextStudioCompiledPlan BeforePlan => _beforePlan;
        internal TextStudioCompiledPlan AfterPlan => _afterPlan;
        internal bool IsOutgoingChanged(int unit) =>
            _outgoingChanged != null && unit >= 0 && unit < _outgoingChanged.Length &&
            _outgoingChanged[unit];

        internal bool TryGetOutput(int slot, out int unit,
            out TextStudioReactionVisualSample sample)
        {
            if (_disposed || _outputs == null)
            {
                unit = -1;
                sample = default;
                return false;
            }
            return _outputs.TryGet(slot, out unit, out sample);
        }

        internal void Seek(float seconds)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TextStudioReactionPreviewSession));
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            try
            {
                if (seconds < _time) Rebuild();
                if (seconds == _time) return;
                _core.TickReveal(seconds - _time, false);
                _time = seconds;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        void Rebuild()
        {
            ReleaseRuntime();
            TextStudioRegistry.EnsureInitialized();
            _outputs = new OutputLedger(_capacity);
            TextStudioPipeline sourcePipeline = new TextStudioPipeline();
            sourcePipeline.Process(new StringTextSource(_beforeText));
            _beforePlan = sourcePipeline.Plan;
            _core = new TextStudioRuntimeCore();
            _core.Configure(RevealMode.Instant, null, RevealTimingsPreset.Uniform,
                true, 1f, .3f, .3f, false, .75f);
            _core.SetText(_beforeText);
            _core.ProcessPipeline(false);
            _core.SetReactionActionHost(new PreviewHost(_outputs, _motionPolicy));
            _core.ConfigureReactions(TextStudioReactionRecipe.Inline(_layers, _capacity, _seed));
            TextStudioCompiledPlan incoming = _core.CompileTextChangeUnits(_afterText,
                false, null);
            TextStudioGlyphChangeSet changes = TextStudioGlyphChangeSetBuilder.Build(
                _core.Plan, incoming, TextStudioTextChangeScope.ChangedGlyphs,
                _core.NumericDecimalSeparator);
            _outgoingChanged = new bool[changes.OutgoingGlyphCount];
            for (int unit = 0; unit < _outgoingChanged.Length; unit++)
                _outgoingChanged[unit] = changes.IsOutgoingGlyphChanged(unit);
            _core.CaptureOutgoingReactions(in changes, false);
            _core.SetText(_afterText);
            _core.ProcessPipeline(false);
            _afterPlan = _core.Plan;
            _core.TickReveal(0f, false);
            _time = 0f;
        }

        void ReleaseRuntime()
        {
            try
            {
                _core?.ConfigureReactions(TextStudioReactionRecipe.None);
            }
            finally
            {
                _core = null;
                _outputs?.Clear();
                _outputs = null;
                _beforePlan = null;
                _afterPlan = null;
                _outgoingChanged = null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            s_active.Remove(this);
            try { ReleaseRuntime(); }
            finally
            {
                for (int i = 0; i < _ownedActions.Length; i++)
                    if (_ownedActions[i] != null)
                        UnityEngine.Object.DestroyImmediate(_ownedActions[i]);
            }
        }
    }
}
