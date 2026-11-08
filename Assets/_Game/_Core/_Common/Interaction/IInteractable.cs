using UnityEngine;

namespace MB13.Aipocalypse.Core.Interaction
{
    // Something the player can look at and use (pickups; later doors, loot). Put it on the collider's object or a parent;
    // `actor` is the interactor's root: read what you need with TryGetComponent (IEquipmentOwner for pickups).
    public interface IInteractable
    {
        // False when the actor can't use it right now: no prompt is shown and the key does nothing.
        bool TryGetPrompt(GameObject actor, out InteractionPrompt prompt);
        void Interact(GameObject actor);
    }

    // Shown as "[key] Verb Target", e.g. "[E] Pick up Rifle".
    public readonly struct InteractionPrompt
    {
        public readonly string Verb;
        public readonly string Target;

        public InteractionPrompt(string verb, string target)
        {
            Verb = verb;
            Target = target;
        }
    }
}
