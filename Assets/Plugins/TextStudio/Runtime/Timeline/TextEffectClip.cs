#if USE_TIMELINE
using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace TextStudio.Timeline
{
    /// <summary>
    /// Timeline clip that activates a Text Studio effect for its duration.
    /// </summary>
    [Serializable]
    public class TextEffectClip : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("Effect or Composed Effect asset to apply.")]
        public TextStudioEffectDefinitionAsset effect;

        [Tooltip("Effect intensity curve over the clip duration.")]
        public AnimationCurve intensityCurve = AnimationCurve.Constant(0f, 1f, 1f);

        [Tooltip("Semantic text-unit range with an exclusive end. Use .. for all text.")]
        public TextStudioTextRange range = default;

        public ClipCaps clipCaps => ClipCaps.Blending;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            ScriptPlayable<TextEffectBehaviour> playable = ScriptPlayable<TextEffectBehaviour>.Create(graph);
            TextEffectBehaviour behaviour = playable.GetBehaviour();
            behaviour.effect = effect;
            behaviour.intensityCurve = intensityCurve;
            behaviour.range = range;
            return playable;
        }
    }
}
#endif
