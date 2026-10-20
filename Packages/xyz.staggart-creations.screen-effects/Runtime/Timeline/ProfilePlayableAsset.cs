// SC Screen Effects © Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//
// ⚠️ WARNING: UNAUTHORIZED USE OR DISTRIBUTION IS STRICTLY PROHIBITED
// • Copying, referencing, or reverse-engineering this source code for the creation of new Asset Store or derivative products,
//   or any other publicly distributed content is strictly forbidden and will result in legal action.
// • Studying this file for the purpose of reproducing its functionality in your own assets or tools is not permitted.
// • If you are viewing this file as a reference, please close it immediately to avoid unintentional design influence or potential EULA violations.
// • Uploading this file or any derivative of it to a public GitHub or similar repository will trigger an automated DMCA takedown request.
// • Studying to understand for personal, educational or integration purposes is allowed, studying to reproduce is not.

#if TIMELINE
using System;
using sc.screeneffects.runtime;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace sc.screeneffects.timeline.runtime
{
    public class ProfilePlayableAsset : PlayableAsset
    {
        public VolumeProfile profile;
        [Range(0f, 1f)]
        public float intensity = 1.0f;
        public AnimationCurve intensityCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.2f, 1f),
            new Keyframe(0.8f, 1f),
            new Keyframe(1f, 0f)
        );

        private ProfilePlayableBehaviour behaviour;
    
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<ProfilePlayableBehaviour>.Create(graph);

            behaviour = playable.GetBehaviour();
            behaviour.profile = profile;
            behaviour.intensity = intensity;
            behaviour.intensityCurve = intensityCurve;

            return playable;
        }
    }
}
#endif