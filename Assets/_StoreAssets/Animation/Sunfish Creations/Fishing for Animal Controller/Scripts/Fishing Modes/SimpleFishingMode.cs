namespace SunfishCreations.Controller
{
    public class SimpleFishingMode : FishingMode
    {
        public override void OnEnter(Fishing fishingState) { }

        public override void OnBite(Fishing fishingState)
        {
            fishingState._fishOnHook = true;
            fishingState.OnFishBiting?.Invoke();
        }

        public override void OnRodCommand(Fishing fishingState)
        {
            if (fishingState._fishCatcher.FishBeingPresented)
            {
                fishingState.ContinueAfterFishPresentation();
            }
            else if (fishingState._fishOnHook && !fishingState._fishProcessed)
            {
                if (fishingState.m_debug) fishingState.FDebug("Congrats you hooked a fish!", false, "green");
                fishingState._hooked = true;

                // Broadcast OnFishHooked before FishHooked() sets _isPlayerFishing to false,
                // since FishableArea.RespondToHookedEvent requires it to still be true.
                FishData caughtFish = fishingState._currentFishableArea?.GetCurrentFish();
                fishingState.OnFishHooked?.Invoke(caughtFish);
                fishingState._currentFishableArea?.FishHooked();
                fishingState._fishProcessed = true;
                fishingState.SetEnterStatus(fishingState.FishHooked);
            }
            else if (!fishingState._fishProcessed && !fishingState._hooked)
            {
                if (fishingState.m_debug) fishingState.FDebug("You Failed to hook fish!", false, "red");
                fishingState._fishProcessed = true;
                fishingState.OnFishMissed?.Invoke();
                fishingState.SetEnterStatus(fishingState.FishMissed);
            }
        }

        public override void OnBiteWindowEnd(Fishing fishingState)
        {
            if (fishingState._fishCatcher.FishBeingPresented) return;
            if (fishingState._hooked) return;

            fishingState.FDebug("The fish got away... try again!", true, "red");

            fishingState.ClearFishableArea();
            fishingState.OnFishGotAway?.Invoke();

            fishingState.SetEnterStatus(fishingState.FishMissed);
        }

        public override void OnExit(Fishing fishingState) { }
    }
}