//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;

namespace DistantLands.Cozy
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/FX/Event FX", order = 361)]
    public class EventFX : CozyFXProfile
    {
        private bool isPlaying;
        public delegate void OnStartEvent();
        public event OnStartEvent OnStart;
        public delegate void OnUpdateEvent();
        public event OnStartEvent OnUpdate;
        public delegate void OnEndEvent();
        public event OnEndEvent OnEnd;

        public override bool IsValid => true;
        public override TransitionTimeModifier TransitionTimeModifier => TransitionTimeModifier.Linear;

        public override void PlayEffect(float weight)
        {
            if (weight > 0.5f)
                StartEvent();
            else
                StopEvent();
        }

        public override void StopEffect()
        {
            StopEvent();
        }

        public void StartEvent()
        {
            OnUpdate?.Invoke();

            if (!isPlaying)
            {
                isPlaying = true;
                OnStart?.Invoke();
            }
        }

        public void StopEvent()
        {
            if (isPlaying)
            {
                isPlaying = false;
                OnEnd?.Invoke();
            }
        }

        public override bool InitializeEffect()
        {
            return true;
        }

        public override void DeinitializeEffect()
        {

        }
    }

}
