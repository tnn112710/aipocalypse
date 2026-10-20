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
    public class ProfilePlayableBehaviour : PlayableBehaviour
    {
        public VolumeProfile profile = null;
        public float intensity = 1f;
        public AnimationCurve intensityCurve;

        private Volume volume;
        private PostProcessingVolumeRenderer renderer;

        public override void OnPlayableCreate(Playable playable)
        {
            volume = QuickVolume.Create(-1, 1000f, profile);
            volume.weight = 0f;

            renderer = volume.gameObject.AddComponent<PostProcessingVolumeRenderer>();
            renderer.hideFlags = HideFlags.DontSave;
            
            renderer.volume = volume;
            renderer.GetEffectsFromVolume();

            //Debug.Log("VolumePlayableAsset.OnPlayableCreate");
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (volume == null)
            {
                return;
            }

            if (profile == null)
            {
                volume.weight = 0f;
                return;
            }

            float progress = GetNormalizedProgress(playable);
            float curveMultiplier = intensityCurve.Evaluate(progress);

            volume.sharedProfile = profile;
            volume.weight = intensity * curveMultiplier;
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            SetVolumeWeight(0f);
        }

        public override void OnGraphStop(Playable playable)
        {
            SetVolumeWeight(0f);
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            if (volume != null)
            {
                volume.weight = 0f;
                QuickVolume.Destroy(volume);
                volume = null;
            }

            if (renderer != null)
            {
                CoreUtils.Destroy(renderer);
                renderer = null;
            }

            //Debug.Log("VolumePlayableAsset.OnPlayableDestroy");
        }

        private static float GetNormalizedProgress(Playable playable)
        {
            double duration = playable.GetDuration();

            if (duration <= 0.0 || double.IsInfinity(duration))
            {
                return 0f;
            }

            return Mathf.Clamp01((float)(playable.GetTime() / duration));
        }

        private void SetVolumeWeight(float weight)
        {
            if (volume != null)
            {
                volume.weight = weight;
            }
        }
    }
}
#endif