using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace TextStudio
{
    /// <summary>Chooses sequential, random, or named clip selection for a sound action invoked from text markup.</summary>
    public enum TextStudioSoundClipSelection
    {
        Sequential,
        Random,
        Named
    }

    /// <summary>
    /// Reusable Text Studio action asset that plays one of its configured audio clips
    /// when a matching markup action is reached during text playback.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TextStudioPlaySoundAction",
        menuName = "Text Studio/Actions/Play Sound",
        order = 105)]
    public sealed class TextStudioPlaySoundAction : TextStudioActionAsset
    {
        [SerializeField] AudioClip[] _clips = Array.Empty<AudioClip>();
        [SerializeField] TextStudioSoundClipSelection _selection = TextStudioSoundClipSelection.Sequential;
        [SerializeField] AudioMixerGroup _output;
        [SerializeField, Range(0f, 1f)] float _volume = 1f;
        [SerializeField] Vector2 _pitchRange = Vector2.one;
        [SerializeField, Range(0f, 1f)] float _spatialBlend;
        [SerializeField] bool _waitForCompletion;
        [SerializeField, Min(0f)] float _timeout;
        [SerializeField] bool _stopOnSkip = true;

        int _sequenceCursor;
        uint _randomState = 0x9E3779B9u;

        protected override string DefaultId => TextStudioPlaySoundActionUtility.DefaultActionId;

        public override TextStudioActionDefinition ActionDefinition => TextStudioPlaySoundActionUtility.CreateDefinition(Id);

        public override IEnumerator Execute(ActionContext context)
        {
            return TextStudioPlaySoundActionUtility.Execute(
                context,
                _clips,
                _selection,
                _output,
                _volume,
                _pitchRange,
                _spatialBlend,
                _waitForCompletion,
                _timeout,
                _stopOnSkip,
                ref _sequenceCursor,
                ref _randomState,
                null);
        }
    }

    /// <summary>
    /// Scene-bound sound action that can play through a supplied
    /// <see cref="AudioSource"/> and optionally pause text reveal until the clip ends.
    /// </summary>
    [AddComponentMenu("Text Studio/Actions/Play Sound Action")]
    public sealed class TextStudioPlaySoundActionComponent : TextStudioActionComponent
    {
        [SerializeField] AudioClip[] _clips = Array.Empty<AudioClip>();
        [SerializeField] TextStudioSoundClipSelection _selection = TextStudioSoundClipSelection.Sequential;
        [SerializeField] AudioSource _source;
        [SerializeField] AudioMixerGroup _output;
        [SerializeField, Range(0f, 1f)] float _volume = 1f;
        [SerializeField] Vector2 _pitchRange = Vector2.one;
        [SerializeField, Range(0f, 1f)] float _spatialBlend;
        [SerializeField] bool _waitForCompletion;
        [SerializeField, Min(0f)] float _timeout;
        [SerializeField] bool _stopOnSkip = true;

        int _sequenceCursor;
        uint _randomState = 0x9E3779B9u;

        protected override string DefaultId => TextStudioPlaySoundActionUtility.DefaultActionId;

        public override TextStudioActionDefinition ActionDefinition => TextStudioPlaySoundActionUtility.CreateDefinition(Id);

        public override IEnumerator Execute(ActionContext context)
        {
            return TextStudioPlaySoundActionUtility.Execute(
                context,
                _clips,
                _selection,
                _output,
                _volume,
                _pitchRange,
                _spatialBlend,
                _waitForCompletion,
                _timeout,
                _stopOnSkip,
                ref _sequenceCursor,
                ref _randomState,
                _source);
        }
    }

    internal static class TextStudioPlaySoundActionUtility
    {
        internal const string DefaultActionId = "playSound";

        public static TextStudioActionDefinition CreateDefinition(string id)
        {
            return new TextStudioActionDefinition(
                id,
                "Play Sound",
                "Plays a configured audio clip and can pause reveal until playback completes.",
                examples: new[]
                {
                    "<" + id + ">",
                    "<" + id + " clip=confirm wait=true>",
                    "<" + id + " wait=true timeout=2>"
                },
                parameters: new[]
                {
                    new TextStudioTagParameterDefinition(
                        "clip",
                        TextStudioTagParameterKind.String,
                        positionalIndex: 0,
                        description: "Configured clip name. Leave empty to use the action's selection mode.",
                        aliases: new[] { "name", "id" }),
                    new TextStudioTagParameterDefinition(
                        "wait",
                        TextStudioTagParameterKind.Boolean,
                        positionalIndex: 1,
                        description: "Pause reveal until the clip completes.",
                        aliases: new[] { "waitForCompletion", "blocking" }),
                    new TextStudioTagParameterDefinition(
                        "timeout",
                        TextStudioTagParameterKind.Float,
                        positionalIndex: 2,
                        description: "Maximum wait in seconds. Zero uses the clip duration.",
                        aliases: new[] { "maxSeconds", "max" }),
                    new TextStudioTagParameterDefinition(
                        "volume",
                        TextStudioTagParameterKind.Float,
                        description: "Per-tag volume multiplier."),
                    new TextStudioTagParameterDefinition(
                        "pitch",
                        TextStudioTagParameterKind.Float,
                        description: "Per-tag pitch override.")
                });
        }

        public static IEnumerator Execute(
            ActionContext context,
            AudioClip[] clips,
            TextStudioSoundClipSelection selection,
            AudioMixerGroup output,
            float volume,
            Vector2 pitchRange,
            float spatialBlend,
            bool waitForCompletion,
            float configuredTimeout,
            bool stopOnSkip,
            ref int sequenceCursor,
            ref uint randomState,
            AudioSource configuredSource)
        {
            if (context == null || context.IsEditModePreview)
                return Empty();

            string requestedName = context.Marker.GetString("clip", 0);
            AudioClip clip = SelectClip(clips, selection, requestedName, ref sequenceCursor, ref randomState);
            if (clip == null)
                return Empty();

            float pitch = ResolvePitch(context, pitchRange, ref randomState);
            return ExecuteSelected(
                context,
                clip,
                output,
                volume,
                pitch,
                spatialBlend,
                waitForCompletion,
                configuredTimeout,
                stopOnSkip,
                configuredSource);
        }

        static IEnumerator ExecuteSelected(
            ActionContext context,
            AudioClip clip,
            AudioMixerGroup output,
            float volume,
            float pitch,
            float spatialBlend,
            bool waitForCompletion,
            float configuredTimeout,
            bool stopOnSkip,
            AudioSource configuredSource)
        {
            float effectiveVolume = Mathf.Clamp01(volume * Mathf.Max(0f, context.Marker.GetFloat("volume", -1, 1f)));
            AudioSource source = configuredSource;
            GameObject temporaryObject = null;
            if (source == null)
            {
                temporaryObject = new GameObject("Text Studio Sound Cue");
                MonoBehaviour coroutineHost = context.Controller != null ? context.Controller.CoroutineHost : null;
                if (coroutineHost != null)
                    temporaryObject.transform.SetParent(coroutineHost.transform, false);
                source = temporaryObject.AddComponent<AudioSource>();
            }

            source.playOnAwake = false;
            source.loop = false;
            source.outputAudioMixerGroup = output;
            source.volume = effectiveVolume;
            source.pitch = pitch;
            source.spatialBlend = Mathf.Clamp01(spatialBlend);
            source.Stop();
            source.clip = clip;
            source.Play();

            bool shouldWait = context.Marker.TagParameters.TryGetBool("wait", out bool waitOverride)
                ? waitOverride
                : waitForCompletion;
            float clipDuration = clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
            float timeout = context.Marker.GetFloat("timeout", 2, configuredTimeout);
            float waitDuration = timeout > 0f ? Mathf.Min(clipDuration, timeout) : clipDuration;
            ITextStudioController controller = context.Controller;
            ITextStudioWaitLifecycleSink waitSink = controller as ITextStudioWaitLifecycleSink;

            if (shouldWait && controller != null)
            {
                controller.PauseReveal();
                waitSink?.NotifyTypewriterWaitStarted(context.Marker, TextStudioTypewriterWaitKind.Timed, waitDuration, false);
            }

            float elapsed = 0f;
            try
            {
                while (shouldWait && elapsed < waitDuration && !context.SkipRequested)
                {
                    elapsed += context.DeltaTime;
                    if (elapsed >= waitDuration || context.SkipRequested)
                        break;
                    yield return null;
                }
            }
            finally
            {
                if (context.SkipRequested && stopOnSkip && source != null)
                    source.Stop();

                if (shouldWait && controller != null)
                {
                    controller.ResumeReveal();
                    waitSink?.NotifyTypewriterWaitFinished(context.Marker, TextStudioTypewriterWaitKind.Timed, waitDuration, false);
                }

                if (temporaryObject != null)
                    UnityEngine.Object.Destroy(temporaryObject, Mathf.Max(0f, clipDuration - elapsed) + 0.05f);
            }
        }

        static IEnumerator Empty()
        {
            yield break;
        }

        internal static AudioClip SelectClip(
            AudioClip[] clips,
            TextStudioSoundClipSelection selection,
            string requestedName,
            ref int sequenceCursor,
            ref uint randomState)
        {
            if (clips == null || clips.Length == 0)
                return null;

            if (!string.IsNullOrEmpty(requestedName) || selection == TextStudioSoundClipSelection.Named)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    AudioClip candidate = clips[i];
                    if (candidate != null && string.Equals(candidate.name, requestedName, StringComparison.OrdinalIgnoreCase))
                        return candidate;
                }
                if (selection == TextStudioSoundClipSelection.Named)
                    return null;
            }

            int startIndex;
            if (selection == TextStudioSoundClipSelection.Random)
            {
                randomState = randomState * 1664525u + 1013904223u;
                startIndex = (int)(randomState % (uint)clips.Length);
            }
            else
            {
                startIndex = Mathf.Abs(sequenceCursor) % clips.Length;
                sequenceCursor = (startIndex + 1) % clips.Length;
            }

            for (int i = 0; i < clips.Length; i++)
            {
                AudioClip candidate = clips[(startIndex + i) % clips.Length];
                if (candidate != null)
                    return candidate;
            }

            return null;
        }

        static float ResolvePitch(ActionContext context, Vector2 pitchRange, ref uint randomState)
        {
            float explicitPitch = context.Marker.GetFloat("pitch", -1, 0f);
            if (!Mathf.Approximately(explicitPitch, 0f))
                return Mathf.Clamp(explicitPitch, -3f, 3f);

            float min = Mathf.Clamp(Mathf.Min(pitchRange.x, pitchRange.y), 0.01f, 3f);
            float max = Mathf.Clamp(Mathf.Max(pitchRange.x, pitchRange.y), min, 3f);
            randomState = randomState * 1664525u + 1013904223u;
            float value = (randomState & 0x00FFFFFFu) / 16777215f;
            return Mathf.Lerp(min, max, value);
        }
    }
}
