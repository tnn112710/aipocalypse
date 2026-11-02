using UnityEngine;

namespace Aipocalypse.Core.Interaction
{
    // A second use of an IInteractable on holding the key (a gate: tap to open, hold to repair). While one is offered, the tap
    // fires on release instead of on press.
    public interface IHoldInteractable
    {
        // False when there's nothing to do by holding right now: the tap then fires on press as usual.
        bool TryGetHoldPrompt(GameObject actor, out InteractionPrompt prompt);
        void InteractHold(GameObject actor);
    }
}
