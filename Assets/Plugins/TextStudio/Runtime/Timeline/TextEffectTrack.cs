#if USE_TIMELINE
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace TextStudio.Timeline
{
    /// <summary>
    /// Timeline track for applying Text Studio effects over time.
    /// </summary>
    [TrackColor(0.8f, 0.4f, 0.2f)]
    [TrackClipType(typeof(TextEffectClip))]
    [TrackBindingType(typeof(TextStudioText))]
    public class TextEffectTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject owner, int inputCount)
        {
            ScriptPlayable<TextEffectMixerBehaviour> mixer = ScriptPlayable<TextEffectMixerBehaviour>.Create(graph, inputCount);
            mixer.GetBehaviour().ConfigureSampleOwner(graph.GetResolver() as PlayableDirector, this);
            return mixer;
        }
    }
}
#endif
