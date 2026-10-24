using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio
{
    /// <summary>Identifies whether Reaction settings are absent, selected from a built-in recipe, supplied by a custom recipe, or authored inline.</summary>
    public enum TextStudioReactionSource
    {
        None,
        BuiltIn,
        Custom,
        Inline
    }

    /// <summary>Chooses a built-in effect or a custom effect asset for a Reaction's effect response.</summary>
    public enum TextStudioReactionEffectSource
    {
        BuiltIn,
        CustomAsset
    }

    /// <summary>Semantic events emitted by the text lifecycle, independently of rendering.</summary>
    public enum TextStudioReactionTrigger
    {
        RevealStarted,
        UnitRevealed,
        ShowComplete,
        HideStarted,
        UnitHideStarted,
        UnitHidden,
        HideComplete,
        Marker,
        Signal,
        TextChanged,
        UnitRevealComplete
    }

    /// <summary>Selects the response performed by a Reaction: play an effect, send a signal, play an audio cue, or invoke a custom action.</summary>
    public enum TextStudioReactionAction { PlayEffect, SendSignal, PlayAudioCue, Custom }
    /// <summary>Chooses whether repeated Reaction triggers may overlap, restart the existing response, or are ignored while it runs.</summary>
    public enum TextStudioReactionOverlap { Allow, Restart, Ignore }
    /// <summary>Selects the predecessor's completion, arrival, impact, break, or return event that starts a dependent Reaction.</summary>
    public enum TextStudioReactionDependencyEvent { Completion, Arrival, Impact, Break, Return }
    /// <summary>Selects the text units affected by a Reaction: all text, the triggering unit, word or line, or changed incoming or outgoing units.</summary>
    public enum TextStudioReactionTarget
    {
        Text,
        [InspectorName("Event Unit")] EventUnit,
        [InspectorName("Event Word")] EventWord,
        [InspectorName("Event Line")] EventLine,
        [InspectorName("Changed Incoming Units")] ChangedUnits,
        [InspectorName("Changed Outgoing Units")] ChangedOutgoingUnits
    }
    /// <summary>Identifies a Reaction-triggering text change as replacement, append, replay, or no change.</summary>
    public enum TextStudioReactionChangeKind { None, Replace, Append, Replay }
    /// <summary>Identifies why a running Reaction was cancelled, including explicit requests, host or text changes, lost targets, unsupported capabilities, and action failures.</summary>
    public enum TextStudioReactionCancelReason
    {
        ExplicitCancel,
        Restarted,
        HostDisabled,
        HostDetached,
        TextReplaced,
        RecipeChanged,
        TargetLost,
        BudgetExceeded,
        ActionFailed,
        UnsupportedCapability,
        Disposed,
        InputDisabled
    }

    /// <summary>Immutable event data. Unit indices refer to the source generation that emitted the event.</summary>
    public readonly struct TextStudioReactionContext
    {
        public readonly TextStudioReactionTrigger Trigger;
        public readonly int UnitIndex;
        public readonly uint Generation;
        public readonly uint SessionId;
        public readonly uint SourceGeneration;
        public readonly ulong TransactionId;
        public readonly TextStudioReactionChangeKind ChangeKind;
        public readonly double EventTime;
        /// <summary>Half-open semantic word and line spans, or -1 when unavailable.</summary>
        public readonly int WordStart, WordEnd, LineStart, LineEnd;
        /// <summary>Half-open selected span, or -1/-1 for All or no selected scope.</summary>
        public readonly int SelectedStart, SelectedEnd;
        public readonly string Name;
        public readonly string TargetBindingKey;
        public readonly float Intensity;
        public readonly float ResponseDuration;
        /// <summary>Stable per-response variation seed; independent of Unity random and runtime session IDs.</summary>
        public readonly uint RandomSeed;

        /// <summary>Creates an immutable reaction event context containing text identity, semantic ranges, timing, targeting, intensity, and random seed.</summary>
        public TextStudioReactionContext(TextStudioReactionTrigger trigger, int unitIndex = -1,
            uint generation = 0, string name = null, float intensity = 1f, uint sessionId = 0,
            uint sourceGeneration = 0, ulong transactionId = 0,
            TextStudioReactionChangeKind changeKind = TextStudioReactionChangeKind.None,
            double eventTime = 0d, int wordStart = -1, int wordEnd = -1,
            int lineStart = -1, int lineEnd = -1,
            int selectedStart = -1, int selectedEnd = -1,
            string targetBindingKey = null, float responseDuration = 0f,
            uint randomSeed = 0)
        {
            Trigger = trigger;
            UnitIndex = unitIndex;
            Generation = generation;
            SessionId = sessionId;
            SourceGeneration = sourceGeneration;
            TransactionId = transactionId;
            ChangeKind = changeKind;
            EventTime = eventTime;
            WordStart = wordStart;
            WordEnd = wordEnd;
            LineStart = lineStart;
            LineEnd = lineEnd;
            SelectedStart = selectedStart;
            SelectedEnd = selectedEnd;
            Name = name;
            TargetBindingKey = targetBindingKey;
            Intensity = intensity;
            ResponseDuration = responseDuration;
            RandomSeed = randomSeed;
        }

        internal TextStudioReactionContext AtEventTime(double eventTime) =>
            new TextStudioReactionContext(Trigger, UnitIndex, Generation, Name, Intensity,
                SessionId, SourceGeneration, TransactionId, ChangeKind, eventTime,
                WordStart, WordEnd, LineStart, LineEnd, SelectedStart, SelectedEnd,
                TargetBindingKey, ResponseDuration, RandomSeed);
    }

    /// <summary>A finite response. Timing here controls the response, never typewriter progression.</summary>
    [Serializable, UxmlObject]
    public sealed partial class TextStudioReactionLayer
    {
        [SerializeField] string _id = Guid.NewGuid().ToString("N");
        [SerializeField] string _name = "Reaction";
        [SerializeField] bool _enabled = true;
        [SerializeField] TextStudioReactionTrigger _when = TextStudioReactionTrigger.UnitRevealed;
        [SerializeField] string _triggerName;
        [SerializeField] TextStudioReactionTarget _which = TextStudioReactionTarget.EventUnit;
        [SerializeField] TextStudioReactionAction _action;
        [SerializeField] TextStudioReactionEffectSource _effectSource;
        [SerializeField] TextStudioEffectDefinitionAsset _effect;
        [SerializeField] string _effectId;
        [SerializeField] string _signal;
        [SerializeField] string _audioCue;
        [SerializeField] TextStudioReactionActionAsset _customAction;
        [SerializeField] string _targetBindingKey;
        [SerializeField] string _enabledInputKey;
        [SerializeField, Min(0f)] float _delay;
        [SerializeField, Min(.001f)] float _duration = .3f;
        [SerializeField, Range(0f, 1f)] float _intensity = 1f;
        [SerializeField, Range(0f, 1f)] float _chance = 1f;
        [SerializeField, Min(0f)] float _cooldown;
        [SerializeField] TextStudioReactionOverlap _overlap;
        [SerializeField] string _dependsOnId;
        [SerializeField] TextStudioReactionDependencyEvent _dependencyEvent;
        [NonSerialized] uint _revision;

        internal uint Revision => _revision;
        internal event Action<TextStudioReactionLayer> Changed;

        void Set<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;
            field = value;
            unchecked { _revision++; }
            if (_revision == 0) _revision = 1;
            Changed?.Invoke(this);
        }

        [UxmlAttribute("id")]
        public string Id
        {
            get => _id;
            set => Set(ref _id, string.IsNullOrEmpty(value) ? Guid.NewGuid().ToString("N") : value);
        }
        [UxmlAttribute("name")]
        public string Name { get => _name; set => Set(ref _name, value ?? string.Empty); }
        [UxmlAttribute("enabled")]
        public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
        [UxmlAttribute("when")]
        public TextStudioReactionTrigger When { get => _when; set => Set(ref _when, value); }
        [UxmlAttribute("trigger-name")]
        public string TriggerName { get => _triggerName; set => Set(ref _triggerName, value ?? string.Empty); }
        [UxmlAttribute("which")]
        public TextStudioReactionTarget Which { get => _which; set => Set(ref _which, value); }
        [UxmlAttribute("action")]
        public TextStudioReactionAction Action { get => _action; set => Set(ref _action, value); }
        [UxmlAttribute("effect-source")]
        public TextStudioReactionEffectSource EffectSource { get => _effectSource; set => Set(ref _effectSource, value); }
        [UxmlAttribute("effect")]
        public TextStudioEffectDefinitionAsset Effect { get => _effect; set => Set(ref _effect, value); }
        [UxmlAttribute("effect-id")]
        public string EffectId { get => _effectId ?? string.Empty; set => Set(ref _effectId, value ?? string.Empty); }
        [UxmlAttribute("signal")]
        public string Signal { get => _signal; set => Set(ref _signal, value ?? string.Empty); }
        [UxmlAttribute("audio-cue")]
        public string AudioCue { get => _audioCue; set => Set(ref _audioCue, value ?? string.Empty); }
        [UxmlAttribute("custom-action")]
        public TextStudioReactionActionAsset CustomAction { get => _customAction; set => Set(ref _customAction, value); }
        [UxmlAttribute("target-binding")]
        public string TargetBindingKey { get => _targetBindingKey ?? string.Empty; set => Set(ref _targetBindingKey, value ?? string.Empty); }
        [UxmlAttribute("enabled-input")]
        public string EnabledInputKey { get => _enabledInputKey ?? string.Empty; set => Set(ref _enabledInputKey, value ?? string.Empty); }
        [UxmlAttribute("delay")]
        public float Delay { get => _delay; set => Set(ref _delay, value); }
        [UxmlAttribute("duration")]
        public float Duration { get => _duration; set => Set(ref _duration, value); }
        [UxmlAttribute("intensity")]
        public float Intensity { get => _intensity; set => Set(ref _intensity, value); }
        [UxmlAttribute("chance")]
        public float Chance { get => _chance; set => Set(ref _chance, value); }
        [UxmlAttribute("cooldown")]
        public float Cooldown { get => _cooldown; set => Set(ref _cooldown, value); }
        [UxmlAttribute("overlap")]
        public TextStudioReactionOverlap Overlap { get => _overlap; set => Set(ref _overlap, value); }
        [UxmlAttribute("depends-on-id")]
        public string DependsOnId { get => _dependsOnId ?? string.Empty; set => Set(ref _dependsOnId, value ?? string.Empty); }
        [UxmlAttribute("dependency-event")]
        public TextStudioReactionDependencyEvent DependencyEvent { get => _dependencyEvent; set => Set(ref _dependencyEvent, value); }

        internal void InitializeForTests(TextStudioReactionTrigger trigger, TextStudioReactionAction action,
            TextStudioEffectDefinitionAsset effect = null, string signal = null, float delay = 0f,
            float duration = .3f, TextStudioReactionOverlap overlap = TextStudioReactionOverlap.Allow,
            bool enabled = true, string triggerName = null, float cooldown = 0f)
        {
            _when = trigger;
            _action = action;
            _effectSource = effect != null
                ? TextStudioReactionEffectSource.CustomAsset
                : TextStudioReactionEffectSource.BuiltIn;
            _effect = effect;
            _effectId = string.Empty;
            _signal = signal;
            _audioCue = string.Empty;
            _delay = delay;
            _duration = duration;
            _overlap = overlap;
            _enabled = enabled;
            _triggerName = triggerName;
            _cooldown = cooldown;
            unchecked { _revision++; }
        }

        internal void InitializeAudioCueForTests(
            TextStudioReactionTrigger trigger,
            string audioCue,
            float delay = 0f,
            bool enabled = true,
            string triggerName = null,
            float cooldown = 0f)
        {
            InitializeForTests(
                trigger,
                TextStudioReactionAction.PlayAudioCue,
                delay: delay,
                enabled: enabled,
                triggerName: triggerName,
                cooldown: cooldown);
            _audioCue = audioCue;
        }

        internal static TextStudioReactionLayer CreateBuiltIn(
            string name,
            TextStudioReactionTrigger trigger,
            string effectId,
            TextStudioReactionTarget target,
            float duration,
            float intensity,
            TextStudioReactionOverlap overlap = TextStudioReactionOverlap.Allow,
            string triggerName = null)
        {
            return new TextStudioReactionLayer
            {
                _id = "builtin." + trigger + "." + name + "." + effectId,
                _name = name,
                _enabled = true,
                _when = trigger,
                _triggerName = triggerName,
                _which = target,
                _action = TextStudioReactionAction.PlayEffect,
                _effectSource = TextStudioReactionEffectSource.BuiltIn,
                _effect = null,
                _effectId = effectId,
                _signal = string.Empty,
                _audioCue = string.Empty,
                _delay = 0f,
                _duration = duration,
                _intensity = intensity,
                _chance = 1f,
                _cooldown = 0f,
                _overlap = overlap
            };
        }

        internal TextStudioReactionLayer Clone()
        {
            return new TextStudioReactionLayer
            {
                _id = Guid.NewGuid().ToString("N"),
                _name = _name,
                _enabled = _enabled,
                _when = _when,
                _triggerName = _triggerName,
                _which = _which,
                _action = _action,
                _effectSource = _effectSource,
                _effect = _effect,
                _effectId = _effectId,
                _signal = _signal,
                _audioCue = _audioCue,
                _customAction = _customAction,
                _targetBindingKey = _targetBindingKey,
                _enabledInputKey = _enabledInputKey,
                _delay = _delay,
                _duration = _duration,
                _intensity = _intensity,
                _chance = _chance,
                _cooldown = _cooldown,
                _overlap = _overlap,
                _dependsOnId = _dependsOnId,
                _dependencyEvent = _dependencyEvent
            };
        }

        internal static TextStudioReactionLayer[] CloneStack(IReadOnlyList<TextStudioReactionLayer> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<TextStudioReactionLayer>();

            TextStudioReactionLayer[] copies = new TextStudioReactionLayer[source.Count];
            Dictionary<string, string> ids = new Dictionary<string, string>(source.Count, StringComparer.Ordinal);
            for (int i = 0; i < source.Count; i++)
            {
                TextStudioReactionLayer original = source[i];
                if (original == null)
                    continue;
                TextStudioReactionLayer copy = original.Clone();
                copies[i] = copy;
                if (!string.IsNullOrEmpty(original.Id) && !ids.ContainsKey(original.Id))
                    ids.Add(original.Id, copy.Id);
            }
            for (int i = 0; i < copies.Length; i++)
            {
                TextStudioReactionLayer copy = copies[i];
                if (copy != null && !string.IsNullOrEmpty(copy.DependsOnId) &&
                    ids.TryGetValue(copy.DependsOnId, out string newParentId))
                    copy.DependsOnId = newParentId;
            }
            return copies;
        }
    }
}
