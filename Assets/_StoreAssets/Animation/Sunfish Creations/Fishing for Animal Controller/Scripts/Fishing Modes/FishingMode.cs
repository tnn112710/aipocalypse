using UnityEngine;

namespace SunfishCreations.Controller
{
    public abstract class FishingMode
    {
        public abstract void OnEnter(Fishing fishingState);
        public abstract void OnBite(Fishing fishingState);
        public abstract void OnRodCommand(Fishing fishingState);
        public abstract void OnBiteWindowEnd(Fishing fishingState);
        public abstract void OnExit(Fishing fishingState);
    }
}