using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Parsed action data from text markup. Immutable once created.
    /// </summary>
    public readonly struct ActionMarker
    {
        public readonly string actionId;
        public readonly int sourceIndex;
        public readonly string[] parameters;
        public readonly TextStudioTagParameters TagParameters;

        /// <summary>Creates a marker from positional parameters.</summary>
        public ActionMarker(string actionId, int sourceIndex, string[] parameters)
            : this(actionId, sourceIndex, parameters, CreateTagParameters(parameters))
        {
        }

        /// <summary>Creates a marker with positional values and parsed named parameters.</summary>
        public ActionMarker(string actionId, int sourceIndex, string[] parameters, TextStudioTagParameters tagParameters)
        {
            this.actionId = actionId;
            this.sourceIndex = sourceIndex;
            this.parameters = parameters;
            TagParameters = tagParameters;
        }

        /// <summary>Tries to parse a positional parameter as an invariant floating-point value.</summary>
        public bool TryGetFloat(int paramIndex, out float value)
        {
            value = 0f;
            if (parameters == null || paramIndex >= parameters.Length) return false;
            return float.TryParse(parameters[paramIndex], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        /// <summary>Tries a named floating-point parameter, then falls back to a positional index.</summary>
        public bool TryGetFloat(string name, int positionalIndex, out float value)
        {
            if (TagParameters.TryGetFloat(name, out value))
                return true;

            return TryGetFloat(positionalIndex, out value);
        }

        /// <summary>Gets a named or positional floating-point parameter, or the supplied fallback.</summary>
        public float GetFloat(string name, int positionalIndex, float defaultValue)
        {
            return TryGetFloat(name, positionalIndex, out float value) ? value : defaultValue;
        }

        /// <summary>Gets a positional string or the supplied fallback.</summary>
        public string GetString(int paramIndex, string defaultValue = "")
        {
            if (parameters == null || paramIndex >= parameters.Length) return defaultValue;
            return parameters[paramIndex];
        }

        /// <summary>Gets a named string, then a positional string, then the supplied fallback.</summary>
        public string GetString(string name, int positionalIndex, string defaultValue = "")
        {
            if (TagParameters.TryGetString(name, out string value))
                return value;

            return GetString(positionalIndex, defaultValue);
        }

        static TextStudioTagParameters CreateTagParameters(string[] parameters)
        {
            if (parameters == null || parameters.Length == 0)
                return TextStudioTagParameters.Empty;

            string[] positional = new string[parameters.Length];
            Array.Copy(parameters, positional, parameters.Length);
            return new TextStudioTagParameters(
                string.Join(",", positional),
                positional,
                Array.Empty<TextStudioNamedTagParameter>());
        }
    }

    /// <summary>Immutable discovery metadata and parameter schema for a registered action tag.</summary>
    public sealed class TextStudioActionDefinition
    {
        readonly ReadOnlyCollection<string> _aliases;
        readonly ReadOnlyCollection<string> _examples;
        readonly ReadOnlyCollection<TextStudioTagParameterDefinition> _parameters;

        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public bool IsMarker { get; }
        public IReadOnlyList<string> Aliases => _aliases;
        public IReadOnlyList<string> Examples => _examples;
        public IReadOnlyList<TextStudioTagParameterDefinition> Parameters => _parameters;

        /// <summary>Creates an action definition with aliases, examples, and parameter metadata.</summary>
        public TextStudioActionDefinition(
            string id,
            string displayName = null,
            string description = null,
            bool isMarker = false,
            string[] aliases = null,
            string[] examples = null,
            TextStudioTagParameterDefinition[] parameters = null)
        {
            Id = id ?? string.Empty;
            DisplayName = string.IsNullOrEmpty(displayName) ? Id : displayName;
            Description = description ?? string.Empty;
            IsMarker = isMarker;
            _aliases = Array.AsReadOnly(aliases != null ? (string[])aliases.Clone() : Array.Empty<string>());
            _examples = Array.AsReadOnly(examples != null ? (string[])examples.Clone() : Array.Empty<string>());
            _parameters = Array.AsReadOnly(parameters != null
                ? (TextStudioTagParameterDefinition[])parameters.Clone()
                : Array.Empty<TextStudioTagParameterDefinition>());
        }
    }

    /// <summary>Exposes discoverable tag metadata for an action implementation.</summary>
    public interface ITextStudioActionDefinitionProvider
    {
        TextStudioActionDefinition ActionDefinition { get; }
    }

    /// <summary>Matches action implementations against their canonical identifiers and aliases.</summary>
    public static class TextStudioActionDefinitionUtility
    {
        /// <summary>Tries to obtain a valid metadata definition from an action.</summary>
        public static bool TryGetDefinition(ITextStudioAction action, out TextStudioActionDefinition definition)
        {
            definition = null;
            if (action is ITextStudioActionDefinitionProvider provider)
                definition = provider.ActionDefinition;

            return definition != null && !string.IsNullOrEmpty(definition.Id);
        }

        /// <summary>Tests an action identifier and its declared aliases using ordinal case-insensitive matching.</summary>
        public static bool MatchesName(ITextStudioAction action, string name)
        {
            if (action == null || string.IsNullOrEmpty(name))
                return false;

            if (string.Equals(action.Id, name, StringComparison.OrdinalIgnoreCase))
                return true;

            return TryGetDefinition(action, out TextStudioActionDefinition definition) &&
                   MatchesName(definition, name);
        }

        /// <summary>Tests a definition's identifier and aliases using ordinal case-insensitive matching.</summary>
        public static bool MatchesName(TextStudioActionDefinition definition, string name)
        {
            if (definition == null || string.IsNullOrEmpty(name))
                return false;

            if (string.Equals(definition.Id, name, StringComparison.OrdinalIgnoreCase))
                return true;

            IReadOnlyList<string> aliases = definition.Aliases;
            for (int i = 0; i < aliases.Count; i++)
            {
                if (string.Equals(aliases[i], name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Action factory interface. Actions are invoked during text reveal
    /// (wait, speed change, events, etc.).
    /// Priority chain: Local component → Global singleton → SO database.
    /// </summary>
    public interface ITextStudioAction
    {
        string Id { get; }
        /// <summary>Executes the action as part of reveal playback.</summary>
        /// <param name="context">Marker data and restricted playback controls for this invocation.</param>
        /// <returns>Coroutine enumerator, or <see langword="null"/> for an immediate action.</returns>
        IEnumerator Execute(ActionContext context);
    }

    /// <summary>
    /// User-facing marker for timeline instructions such as wait, speed, event,
    /// and input markers. Cues keep the existing action execution contract.
    /// </summary>
    public interface ITextStudioCue : ITextStudioAction
    {
    }

    /// <summary>
    /// Optional metadata marker for actions that are designed to behave predictably
    /// during edit-mode preview. Runtime-only actions still require the TextStudioText
    /// Run Actions toggle so scene side effects stay opt-in.
    /// </summary>
    public interface ITextStudioEditModePreviewAction
    {
    }

    /// <summary>
    /// Context passed to action execution.
    /// </summary>
    public class ActionContext
    {
        public ActionMarker Marker { get; }
        public ITextStudioController Controller { get; }
        /// <summary>The scene object that owns this invocation, or null for a non-scene binding. Available during opted-in Edit Mode preview.</summary>
        public UnityEngine.Object Owner { get; internal set; }
        public float SpeedMultiplier { get; set; }
        bool _skipRequested;
        Func<float> _deltaTimeProvider;

        public bool SkipRequested => _skipRequested;
        public bool IsEditModePreview { get; internal set; }
        public float DeltaTime => _deltaTimeProvider != null ? Mathf.Max(0f, _deltaTimeProvider()) : Time.deltaTime;

        /// <summary>Creates invocation context for one parsed marker and playback controller.</summary>
        public ActionContext(ActionMarker marker, ITextStudioController controller)
        {
            Marker = marker;
            Controller = controller;
            SpeedMultiplier = 1f;
        }

        /// <summary>Requests cooperative cancellation of the executing action's wait.</summary>
        public void RequestSkip() => _skipRequested = true;

        internal void SetDeltaTimeProvider(Func<float> deltaTimeProvider)
        {
            _deltaTimeProvider = deltaTimeProvider;
        }
    }

    /// <summary>
    /// ScriptableObject base for actions that can be registered in the database.
    /// </summary>
    public abstract class TextStudioActionAsset : ScriptableObject, ITextStudioCue, ITextStudioActionDefinitionProvider
    {
        [SerializeField] string _id;

        public string Id => !string.IsNullOrEmpty(_id) ? _id : DefaultId;

        public virtual TextStudioActionDefinition ActionDefinition => new TextStudioActionDefinition(
            Id,
            Id,
            "Runs a custom Text Studio action.",
            examples: new[] { "<" + Id + ">" });

        protected virtual string DefaultId => GetType().Name;

        public abstract IEnumerator Execute(ActionContext context);
    }

    /// <summary>
    /// Scene-local action base. Attach next to a TextStudioText hub to make the tag
    /// available only for that hub, without touching global project settings.
    /// </summary>
    [ExecuteAlways]
    public abstract class TextStudioActionComponent : MonoBehaviour, ITextStudioCue, ITextStudioActionDefinitionProvider
    {
        [SerializeField] string _id;

        public string Id => !string.IsNullOrEmpty(_id) ? _id : DefaultId;

        public virtual TextStudioActionDefinition ActionDefinition => new TextStudioActionDefinition(
            Id,
            Id,
            "Runs a scene-local Text Studio action.",
            examples: new[] { "<" + Id + ">" });

        protected virtual string DefaultId => GetType().Name;

        protected virtual void OnEnable() => NotifyAvailabilityChanged();
        protected virtual void OnDisable() => NotifyAvailabilityChanged();

        void NotifyAvailabilityChanged()
        {
            if (TryGetComponent(out TextStudioText hub)) hub.RefreshLocalActionAvailability();
        }

        public abstract IEnumerator Execute(ActionContext context);
    }

    /// <summary>
    /// Restricted playback controller exposed to custom actions while they execute.
    /// </summary>
    public interface ITextStudioController
    {
        /// <summary>Gets normalized reveal progress from 0 to 1.</summary>
        float RevealProgress { get; }

        /// <summary>Gets the number of semantic glyphs currently revealed.</summary>
        int RevealedGlyphCount { get; }

        /// <summary>Gets the number of visible semantic glyphs in the current plan.</summary>
        int TotalGlyphCount { get; }

        /// <summary>Gets whether reveal playback is currently advancing.</summary>
        bool IsRevealing { get; }

        /// <summary>Gets whether reveal playback is paused.</summary>
        bool IsRevealPaused { get; }

        /// <summary>Gets the component that owns action coroutines.</summary>
        MonoBehaviour CoroutineHost { get; }

        /// <summary>Changes the reveal-rate multiplier for subsequent updates.</summary>
        /// <param name="multiplier">Non-negative multiplier, where 1 uses authored timing.</param>
        void SetRevealSpeed(float multiplier);

        /// <summary>Pauses reveal while preserving its current position.</summary>
        void PauseReveal();

        /// <summary>Resumes reveal from its paused position.</summary>
        void ResumeReveal();

        /// <summary>Requests cooperative cancellation of the active wait and resumes reveal.</summary>
        void SkipCurrentWait();

        /// <summary>Occurs when playback reaches an authored marker.</summary>
        event Action<string, string[]> OnMarkerReached;
    }

    /// <summary>
    /// Receives lifecycle notifications for waits that suspend reveal or hide playback.
    /// Implementations must not retain the supplied marker beyond the notification.
    /// </summary>
    public interface ITextStudioWaitLifecycleSink
    {
        /// <summary>Notifies the sink immediately before a wait suspends playback.</summary>
        /// <param name="marker">Marker that initiated the wait.</param>
        /// <param name="kind">Kind of wait being started.</param>
        /// <param name="duration">Known duration in seconds, or a negative value for an externally completed wait.</param>
        /// <param name="isHide">Whether the wait belongs to hide playback.</param>
        void NotifyTypewriterWaitStarted(ActionMarker marker, TextStudioTypewriterWaitKind kind, float duration, bool isHide);

        /// <summary>Notifies the sink after a wait completes or is skipped.</summary>
        /// <param name="marker">Marker that initiated the wait.</param>
        /// <param name="kind">Kind of wait that finished.</param>
        /// <param name="duration">Configured duration in seconds, or a negative value for an externally completed wait.</param>
        /// <param name="isHide">Whether the wait belonged to hide playback.</param>
        void NotifyTypewriterWaitFinished(ActionMarker marker, TextStudioTypewriterWaitKind kind, float duration, bool isHide);
    }
}
