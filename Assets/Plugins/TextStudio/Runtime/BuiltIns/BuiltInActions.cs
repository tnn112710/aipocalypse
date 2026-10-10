using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio.BuiltIns
{
    /// <summary>
    /// Base for built-in actions that can be invoked from markup, e.g.
    /// &lt;wait=1.2&gt;, &lt;pause/&gt;, &lt;speed=2&gt;, &lt;event=onBoss/&gt;.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // Subsystem registration clears the wait cache while retaining its shared collection identity.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public abstract partial class CodeActionBase : ITextStudioCue, ITextStudioBuiltIn, ITextStudioActionDefinitionProvider
    {
        public abstract string Id { get; }
        public virtual string Category => "Actions";
        public virtual string DisplayName => Id;
        public virtual string Description => "";
        TextStudioActionDefinition _actionDefinition;

        public virtual TextStudioActionDefinition ActionDefinition => _actionDefinition ??= CreateActionDefinition();

        /// <summary>Creates the immutable schema retained by this action instance.</summary>
        protected virtual TextStudioActionDefinition CreateActionDefinition() => new TextStudioActionDefinition(
            Id,
            DisplayName,
            Description,
            examples: new[] { "<" + Id + ">" });

        public abstract IEnumerator Execute(ActionContext context);

        // WaitForSeconds cache - shared across all actions to eliminate per-frame allocations.
        static readonly Dictionary<int, WaitForSeconds> s_WaitCache = new Dictionary<int, WaitForSeconds>(16);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetWaitCacheForFastEnterPlayMode() => s_WaitCache.Clear();

        protected static WaitForSeconds GetWait(float seconds)
        {
            int key = Mathf.RoundToInt(seconds * 1000f);
            if (!s_WaitCache.TryGetValue(key, out WaitForSeconds w))
            {
                w = new WaitForSeconds(seconds);
                s_WaitCache[key] = w;
            }
            return w;
        }
    }

    // ────────────────────────── Timing actions ──────────────────────────

    public sealed class WaitAction : CodeActionBase, ITextStudioEditModePreviewAction
    {
        internal const float DefaultSeconds = 1f;

        public override string Id => TextStudioBuiltInActionIds.Wait;
        public override string DisplayName => "Wait";
        public override string Description => "Pauses reveal for N seconds. First parameter = seconds.";
        protected override TextStudioActionDefinition CreateActionDefinition() => new TextStudioActionDefinition(
            Id,
            DisplayName,
            Description,
            aliases: new[] { "waitfor" },
            examples: new[] { "<wait=0.5>", "<wait seconds=0.5>" },
            parameters: new[]
            {
                new TextStudioTagParameterDefinition(
                    "seconds",
                    TextStudioTagParameterKind.Float,
                    positionalIndex: 0,
                    description: "Seconds to pause reveal.",
                    aliases: new[] { "time", "duration" },
                    defaultValue: "1")
            });

        public override IEnumerator Execute(ActionContext context)
        {
            context.Marker.TryGetFloat(0, out float seconds);
            if (seconds <= 0f) seconds = DefaultSeconds;
            if (context.Controller == null)
            {
                yield return GetWait(seconds);
                yield break;
            }

            context.Controller.PauseReveal();
            ITextStudioWaitLifecycleSink waitSink = context.Controller as ITextStudioWaitLifecycleSink;
            waitSink?.NotifyTypewriterWaitStarted(context.Marker, TextStudioTypewriterWaitKind.Timed, seconds, false);
            try
            {
                float elapsed = 0f;
                while (elapsed < seconds && !context.SkipRequested)
                {
                    elapsed += context.DeltaTime;
                    if (elapsed >= seconds || context.SkipRequested)
                        break;

                    yield return null;
                }
            }
            finally
            {
                context.Controller.ResumeReveal();
                waitSink?.NotifyTypewriterWaitFinished(context.Marker, TextStudioTypewriterWaitKind.Timed, seconds, false);
            }
        }
    }

    public sealed class PauseAction : CodeActionBase, ITextStudioEditModePreviewAction
    {
        public override string Id => TextStudioBuiltInActionIds.Pause;
        public override string DisplayName => "Pause";
        public override string Description => "Pauses reveal indefinitely until external code calls controller.Resume or SetProgress.";
        protected override TextStudioActionDefinition CreateActionDefinition() => new TextStudioActionDefinition(
            Id,
            DisplayName,
            Description,
            examples: new[] { "<pause>" });

        public override IEnumerator Execute(ActionContext context)
        {
            if (context.Controller != null)
                context.Controller.PauseReveal();
            yield break;
        }
    }

    public sealed class ResumeAction : CodeActionBase, ITextStudioEditModePreviewAction
    {
        public override string Id => TextStudioBuiltInActionIds.Resume;
        public override string DisplayName => "Resume";
        public override string Description => "Resumes reveal after a pause or wait.";
        protected override TextStudioActionDefinition CreateActionDefinition() => new TextStudioActionDefinition(
            Id,
            DisplayName,
            Description,
            examples: new[] { "<resume>" });

        public override IEnumerator Execute(ActionContext context)
        {
            if (context.Controller != null)
                context.Controller.ResumeReveal();
            yield break;
        }
    }

    public sealed class SpeedAction : CodeActionBase, ITextStudioEditModePreviewAction
    {
        public override string Id => TextStudioBuiltInActionIds.Speed;
        public override string DisplayName => "Set Speed";
        public override string Description => "Sets reveal speed multiplier from the first parameter.";
        protected override TextStudioActionDefinition CreateActionDefinition() => new TextStudioActionDefinition(
            Id,
            DisplayName,
            Description,
            examples: new[] { "<speed=1.25>", "<speed multiplier=1.25>" },
            parameters: new[]
            {
                new TextStudioTagParameterDefinition(
                    "multiplier",
                    TextStudioTagParameterKind.Float,
                    positionalIndex: 0,
                    description: "Reveal speed multiplier.",
                    aliases: new[] { "speed", "value" },
                    defaultValue: "2")
            });

        public override IEnumerator Execute(ActionContext context)
        {
            context.Marker.TryGetFloat(0, out float multiplier);
            if (multiplier <= 0f) multiplier = 2f;
            if (context.Controller != null)
                context.Controller.SetRevealSpeed(multiplier);
            yield break;
        }
    }

    public sealed class SpeedUpAction : CodeActionBase, ITextStudioEditModePreviewAction
    {
        public override string Id => TextStudioBuiltInActionIds.SpeedUp;
        public override string DisplayName => "Speed Up";
        public override string Description => "Multiplies reveal speed by the first parameter (default 2x).";
        protected override TextStudioActionDefinition CreateActionDefinition() => new TextStudioActionDefinition(
            Id,
            DisplayName,
            Description,
            examples: new[] { "<speedUp=2>", "<speedUp factor=2>" },
            parameters: new[]
            {
                new TextStudioTagParameterDefinition(
                    "factor",
                    TextStudioTagParameterKind.Float,
                    positionalIndex: 0,
                    description: "Speed factor. Defaults to 2.",
                    defaultValue: "2")
            });

        public override IEnumerator Execute(ActionContext context)
        {
            context.Marker.TryGetFloat(0, out var factor);
            if (factor <= 0f) factor = 2f;
            if (context.Controller != null)
                context.Controller.SetRevealSpeed(factor);
            yield break;
        }
    }

    public sealed class SpeedDownAction : CodeActionBase, ITextStudioEditModePreviewAction
    {
        public override string Id => TextStudioBuiltInActionIds.SpeedDown;
        public override string DisplayName => "Speed Down";
        public override string Description => "Divides reveal speed by the first parameter (default 2x).";
        protected override TextStudioActionDefinition CreateActionDefinition() => new TextStudioActionDefinition(
            Id,
            DisplayName,
            Description,
            examples: new[] { "<speedDown=2>", "<speedDown factor=2>" },
            parameters: new[]
            {
                new TextStudioTagParameterDefinition(
                    "factor",
                    TextStudioTagParameterKind.Float,
                    positionalIndex: 0,
                    description: "Speed factor. Defaults to 2.",
                    defaultValue: "2")
            });

        public override IEnumerator Execute(ActionContext context)
        {
            context.Marker.TryGetFloat(0, out var factor);
            if (factor <= 0f) factor = 2f;
            if (context.Controller != null)
                context.Controller.SetRevealSpeed(1f / factor);
            yield break;
        }
    }

    // ────────────────────────── Event / utility actions ──────────────────────────

#if UNITY_6000_7_OR_NEWER
    // The default input adapter is a stateless code-lifetime singleton.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public sealed partial class WaitForInputAction : CodeActionBase, ITextStudioEditModePreviewAction
    {
        static readonly ITextStudioInputProvider s_defaultInput =
            new UnityInputManagerTextStudioInputProvider();

        public override string Id => TextStudioBuiltInActionIds.WaitForInput;
        public override string DisplayName => "Wait For Input";
        public override string Description =>
            "Pauses reveal until the input provider advances, or ActionContext.RequestSkip is called.";
        protected override TextStudioActionDefinition CreateActionDefinition() => new TextStudioActionDefinition(
            Id,
            DisplayName,
            Description,
            aliases: new[] { "waitinput" },
            examples: new[] { "<waitForInput>", "<waitForInput input=submit timeout=5>" },
            parameters: new[]
            {
                new TextStudioTagParameterDefinition(
                    "input",
                    TextStudioTagParameterKind.String,
                    positionalIndex: 0,
                    description: "Input key requested from an input provider. Use any or leave empty for any advance input.",
                    aliases: new[] { "key", "action" },
                    defaultValue: "any"),
                new TextStudioTagParameterDefinition(
                    "timeout",
                    TextStudioTagParameterKind.Float,
                    positionalIndex: 1,
                    description: "Maximum seconds to wait. Use 0 or less for no timeout.",
                    aliases: new[] { "maxSeconds", "max", "seconds" },
                    defaultValue: "0")
            });

        public override IEnumerator Execute(ActionContext context)
        {
            if (context.Controller == null) yield break;
            context.Controller.PauseReveal();
            float timeoutForEvent = context.Marker.GetFloat("timeout", 1, -1f);
            ITextStudioWaitLifecycleSink waitSink = context.Controller as ITextStudioWaitLifecycleSink;
            waitSink?.NotifyTypewriterWaitStarted(
                context.Marker,
                TextStudioTypewriterWaitKind.Input,
                Mathf.Max(0f, timeoutForEvent),
                false);
            try
            {
                ITextStudioInputProvider input = TextStudioRuntimeServices.InputProvider ?? s_defaultInput;
                string inputKey = context.Marker.GetString("input", 0);
                float timeout = timeoutForEvent;
                float elapsed = 0f;
                while (!context.SkipRequested && !HasAdvanceInput(input, inputKey, context.IsEditModePreview))
                {
                    if (timeout > 0f)
                    {
                        elapsed += context.DeltaTime;
                        if (elapsed >= timeout)
                            break;
                    }

                    yield return null;
                }
            }
            finally
            {
                context.Controller.ResumeReveal();
                waitSink?.NotifyTypewriterWaitFinished(
                    context.Marker,
                    TextStudioTypewriterWaitKind.Input,
                    Mathf.Max(0f, timeoutForEvent),
                    false);
            }
        }

        static bool HasAdvanceInput(ITextStudioInputProvider input, string inputKey, bool isEditModePreview)
        {
            if (isEditModePreview)
                return false;

            if (!string.IsNullOrEmpty(inputKey) &&
                !string.Equals(inputKey, "any", StringComparison.OrdinalIgnoreCase) &&
                input is ITextStudioKeyedInputProvider keyedInput)
            {
                return keyedInput.WasAdvancePressed(inputKey);
            }

            return input.WasAnyAdvancePressed();
        }
    }

    public sealed class RaiseEventAction : CodeActionBase
    {
        public override string Id => TextStudioBuiltInActionIds.Event;
        public override string DisplayName => "Raise Event";
        public override string Description =>
            "Raises a named event via the global TextStudioEvents bus. First parameter = event name.";
        protected override TextStudioActionDefinition CreateActionDefinition() => new TextStudioActionDefinition(
            Id,
            DisplayName,
            Description,
            isMarker: true,
            examples: new[] { "<event=choiceA>", "<event route=choiceA payload=confirmed>" },
            parameters: new[]
            {
                new TextStudioTagParameterDefinition(
                    "route",
                    TextStudioTagParameterKind.RouteId,
                    true,
                    0,
                    description: "Named event route.",
                    suggestionKey: TextStudioTagSuggestionKeys.EventRoute,
                    exampleValue: "choiceA"),
                new TextStudioTagParameterDefinition(
                    "payload",
                    TextStudioTagParameterKind.String,
                    positionalIndex: 1,
                    description: "Optional payload values after the route.",
                    exampleValue: "confirmed")
            });

        public override IEnumerator Execute(ActionContext context)
        {
            string name = context.Marker.GetString(0);
            if (!string.IsNullOrEmpty(name))
                TextStudioEvents.Raise(name, context.Marker.parameters);
            yield break;
        }
    }

    /// <summary>
    /// Global event bus for &lt;event=name/&gt; markers. Subscribe from anywhere in your code.
    /// Zero-alloc (no closures), type-safe by name.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
#endif
    public static partial class TextStudioEvents
    {
        public static event System.Action<string, string[]> Raised = null;
        internal static bool HasListeners => Raised != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetForFastEnterPlayMode() => Raised = null;

        public static void Raise(string eventName, string[] parameters)
        {
            Raised?.Invoke(eventName, parameters);
        }
    }

    // ────────────────────────── Registry ──────────────────────────

#if UNITY_6000_7_OR_NEWER
    // Built-in action definitions are immutable after construction.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static partial class BuiltInActions
    {
        public static readonly WaitAction Wait = new WaitAction();
        public static readonly PauseAction Pause = new PauseAction();
        public static readonly ResumeAction Resume = new ResumeAction();
        public static readonly SpeedAction Speed = new SpeedAction();
        public static readonly SpeedUpAction SpeedUp = new SpeedUpAction();
        public static readonly SpeedDownAction SpeedDown = new SpeedDownAction();
        public static readonly WaitForInputAction WaitForInput = new WaitForInputAction();
        public static readonly RaiseEventAction Event = new RaiseEventAction();

        public static bool TryGetCanonicalActionId(string tagName, out string actionId)
        {
            actionId = string.Empty;
            if (string.IsNullOrEmpty(tagName)) return false;

            if (string.Equals(tagName, Wait.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tagName, "waitfor", StringComparison.OrdinalIgnoreCase))
            {
                actionId = Wait.Id;
                return true;
            }

            if (string.Equals(tagName, "waitinput", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tagName, WaitForInput.Id, StringComparison.OrdinalIgnoreCase))
            {
                actionId = WaitForInput.Id;
                return true;
            }

            if (string.Equals(tagName, Pause.Id, StringComparison.OrdinalIgnoreCase))
            {
                actionId = Pause.Id;
                return true;
            }

            if (string.Equals(tagName, Resume.Id, StringComparison.OrdinalIgnoreCase))
            {
                actionId = Resume.Id;
                return true;
            }

            if (string.Equals(tagName, Speed.Id, StringComparison.OrdinalIgnoreCase))
            {
                actionId = Speed.Id;
                return true;
            }

            if (string.Equals(tagName, SpeedUp.Id, StringComparison.OrdinalIgnoreCase))
            {
                actionId = SpeedUp.Id;
                return true;
            }

            if (string.Equals(tagName, SpeedDown.Id, StringComparison.OrdinalIgnoreCase))
            {
                actionId = SpeedDown.Id;
                return true;
            }

            if (string.Equals(tagName, Event.Id, StringComparison.OrdinalIgnoreCase))
            {
                actionId = Event.Id;
                return true;
            }

            return false;
        }

        public static bool IsWaitTag(string tagName)
        {
            return TryGetCanonicalActionId(tagName, out string actionId) &&
                   (string.Equals(actionId, Wait.Id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(actionId, WaitForInput.Id, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsMarkerTag(string tagName)
        {
            return string.Equals(tagName, Event.Id, StringComparison.OrdinalIgnoreCase);
        }

        public static IEnumerable<CodeActionBase> All()
        {
            yield return Wait;
            yield return Pause;
            yield return Resume;
            yield return Speed;
            yield return SpeedUp;
            yield return SpeedDown;
            yield return WaitForInput;
            yield return Event;
        }
    }
}
