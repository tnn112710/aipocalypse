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

using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

#if TIMELINE
namespace sc.screeneffects.timeline.runtime
{
    [Serializable]
    [TrackClipType(typeof(ProfilePlayableAsset))]
    //[TrackBindingType(typeof(CinemachineBrain), TrackBindingFlags.None)]
    [TrackColor(0.53f, 0.0f, 0.08f)]
    public class PostProcessingTrack : TrackAsset
    {
        /// <summary>
        /// TrackAsset implementation
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="go"></param>
        /// <param name="inputCount"></param>
        /// <returns></returns>
        public override Playable CreateTrackMixer( PlayableGraph graph, GameObject go, int inputCount)
        {
            var mixer = ScriptPlayable<ProfilePlayableBehaviour>.Create(graph);
            mixer.SetInputCount(inputCount);
            return mixer;
        }
    }
}
#endif