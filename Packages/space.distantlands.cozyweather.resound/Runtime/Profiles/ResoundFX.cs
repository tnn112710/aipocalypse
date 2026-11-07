using UnityEngine;

namespace DistantLands.Cozy
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/FX/ReSound FX", order = 361)]
    public class ResoundFX : CozyFXProfile
    {
        [SerializeField]
        private ResoundTrack track;
        public ResoundTrack Track => track;

        [SerializeField]
        private int priority;

        [SerializeField]
        private CozyResoundModule.SongTransitionType transitionOnStart = CozyResoundModule.SongTransitionType.Crossfade;

        [SerializeField]
        private CozyResoundModule.SongTransitionType transitionOnEnd = CozyResoundModule.SongTransitionType.Crossfade;

        private CozyResoundModule resoundModule;
        private CozyResoundModule.TrackOverrideHandle overrideHandle;

        public override bool IsValid =>
            track != null
            && track.Clip != null
            && (resoundModule != null || InitializeEffect());
        public override TransitionTimeModifier TransitionTimeModifier => TransitionTimeModifier.Linear;

        public override bool InitializeEffect()
        {
            if (!Application.isPlaying || track == null || track.Clip == null)
                return false;

            CozyWeather weather = CozyWeather.Instance;
            if (weather == null || !weather.GetModule(out resoundModule))
                return false;

            return true;
        }

        public override void PlayEffect(float weight)
        {
            if (weight <= 0f)
            {
                StopEffect();
                return;
            }

            if (overrideHandle != null || !IsValid)
                return;

            overrideHandle = resoundModule.RequestTrackOverride(track, priority, transitionOnStart, transitionOnEnd);
        }

        public override void StopEffect()
        {
            overrideHandle?.Dispose();
            overrideHandle = null;
        }

        public override void DeinitializeEffect()
        {
            StopEffect();
            resoundModule = null;
        }
    }
}
