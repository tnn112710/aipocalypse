namespace SunfishCreations.Controller
{
    public class TugOfWarMode : FishingMode
    {
        public override void OnEnter(Fishing fishingState) { }

        public override void OnBite(Fishing fishingState)
        {
            fishingState._fishOnHook = true; 
            fishingState.OnFishBiting?.Invoke();

            if (fishingState.animal != null)
                fishingState.animal.Stance_Activate(fishingState.ReelingStance);
            
        }

        public override void OnRodCommand(Fishing fishingState)
        {
            // In Tug of War mode, rod command does nothing during bite/reeling.
            // Success is triggered externally by ReelingInComplete.
            if (fishingState._fishCatcher.FishBeingPresented)
            {
                fishingState.ContinueAfterFishPresentation();
            }
            if (!fishingState._fishProcessed && !fishingState._hooked && !fishingState._fishOnHook)
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

            // Reset stance
            if (fishingState.animal != null)
            {
                fishingState.animal.Stance_ResetDefaultValue();
                fishingState.animal.Stance_Reset();
            }

            fishingState.SetEnterStatus(fishingState.FishMissed);
        }

        public override void OnExit(Fishing fishingState)
        {
            if (fishingState.animal != null)
            {
                fishingState.animal.Stance_ResetDefaultValue();
                fishingState.animal.Stance_Reset();
            }
        }
    }
}