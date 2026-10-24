using SunfishCreations;
using UnityEngine;

namespace SunfishCreations.Controller
{
    public class ReelingFishingMode : FishingMode
    {
        public override void OnEnter(Fishing fishingState) { }

        public override void OnBite(Fishing fishingState)
        {
            fishingState._fishOnHook = true;
            if (fishingState.animal != null)
                fishingState.animal.Stance_Activate(fishingState.ReelingStance);
            fishingState.OnFishBiting?.Invoke();
        }

        public override void OnRodCommand(Fishing fishingState)
        {
            // In Reeling mode, rod command does nothing during bite/reeling.
            // Success is triggered externally (e.g., by ReelingInComplete).
            // Only allow exit if holding a fish.
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

            // Debug: if RodIK reeling debug lock is active, hold the fish on the line
            // so the character stays in the reel pose for IK target adjustment.
            if (fishingState.animal != null)
            {
                RodIK rodIK = fishingState.animal.GetComponentInChildren<RodIK>();
                if (rodIK != null && rodIK.IsReelingDebugLocked)
                {
                    Debug.LogWarning("[Fishing] Fish escape intercepted — RodIK Debug Lock On Reeling is active. Character held in reel pose.");
                    return;
                }
            }

            fishingState.FDebug("The fish got away... try again!", true, "red");

            fishingState.ClearFishableArea();
            fishingState.OnFishGotAway?.Invoke();

            // Reset stance only in Reeling mode
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