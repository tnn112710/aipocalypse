#if USE_TIMELINE
using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace TextStudio.Timeline
{
    /// <summary>
    /// Timeline clip asset for deterministic text reveal animation.
    /// </summary>
    [Serializable]
    public class TextRevealClip : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("Text to display. If empty, uses the controller's existing text.")]
        [TextArea(2, 5)]
        public string text;

        [Tooltip("Reveal curve. X is normalized clip time and Y is reveal progress.")]
        public AnimationCurve revealCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Complete the reveal when this clip reaches its end during forward playback.")]
        public bool autoSkipOnEnd = true;

        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            ScriptPlayable<TextRevealBehaviour> playable = ScriptPlayable<TextRevealBehaviour>.Create(graph);
            TextRevealBehaviour behaviour = playable.GetBehaviour();
            behaviour.text = text;
            behaviour.revealCurve = revealCurve;
            behaviour.autoSkipOnEnd = autoSkipOnEnd;
            return playable;
        }
    }
}
#endif
