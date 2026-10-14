using UnityEngine;

namespace Aipocalypse.Core.Interaction
{
    // Something in the world the player can look at and use (pickups; later doors, loot, workbenches).
    // Put it on the object that owns the collider or on a parent of it. `actor` is the root of whoever interacts:
    // read what you need from it with TryGetComponent (IEquipmentOwner for pickups).
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
