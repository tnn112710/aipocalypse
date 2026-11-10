using MB13.Aipocalypse.Core.Interaction;
using MB13.Aipocalypse.Core.PlayerSystem;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.SleepSystem
{
    // A bed to sleep in: "[E] Sleep" opens the wake-up clock (SleepService). Works only in a scene with a SleepInstaller,
    // which injects it.
    [DisallowMultipleComponent]
    public sealed class Bed : MonoBehaviour, IInteractable
    {
        const string Verb = "Sleep";

        [SerializeField, Tooltip("On the floor under the bed: x (red) points to the side you get in from, z (blue) towards the pillow. " +
            "The sleep sequences are placed in its space")]
        Transform _anchor;

        SleepService _sleep;

        public Transform Anchor => _anchor != null ? _anchor : transform;

        [Inject]
        void Construct(SleepService sleep) => _sleep = sleep;

        public bool TryGetPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (_sleep == null || !_sleep.Enabled || _sleep.IsBusy || !actor.TryGetComponent(out IPlayer _))
                return false;
            prompt = new InteractionPrompt(Verb, string.Empty);
            return true;
        }

        public void Interact(GameObject actor)
        {
            if (_sleep != null && actor.TryGetComponent(out IPlayer player))
                _sleep.Begin(this, player);
        }
    }
}
