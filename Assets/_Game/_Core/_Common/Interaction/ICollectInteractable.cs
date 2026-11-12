using UnityEngine;

namespace MB13.Aipocalypse.Core.Interaction
{
    // A use of an IInteractable on its own key, Collect (C): taking what a farm animal holds. Shown as a second prompt.
    public interface ICollectInteractable
    {
        // False when there's nothing to collect right now: no prompt, and the key does nothing.
        bool TryGetCollectPrompt(GameObject actor, out InteractionPrompt prompt);
        void Collect(GameObject actor);
    }
}
