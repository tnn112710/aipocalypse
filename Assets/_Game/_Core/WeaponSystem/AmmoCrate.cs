using System.Collections.Generic;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.Interaction;
using Ami.BroAudio;
using MB13.Aipocalypse.Core.LocalizationSystem;
using UnityEngine;
using UnityEngine.Localization;
using VContainer;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // Test aid: a crate that never runs out. Taking from it refills each ammo type the actor's hotbar guns use up to the
    // starting reserve. Plays its sound only when injected (WorldMap: listed in the scene scope's Auto Inject Game Objects).
    [DisallowMultipleComponent]
    public sealed class AmmoCrate : MonoBehaviour, IInteractable
    {
        static readonly LocalizedString TakeVerb = Loc.Interaction("verb.take");
        static readonly LocalizedString AmmoTarget = Loc.Interaction("target.ammo");

        [SerializeField, Tooltip("Each type is refilled up to its starting amount here: the player's AmmoConfig")]
        AmmoConfig _ammo;

        [SerializeField] SoundID _takeSound;

        readonly List<AmmoType> _types = new();
        AudioService _audio;

        [Inject]
        void Construct(AudioService audio) => _audio = audio;

        // Shown whenever the actor carries a gun, even at a full reserve: taking then adds nothing.
        public bool TryGetPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (!TryFindTypes(actor, out _))
                return false;
            prompt = new InteractionPrompt(TakeVerb.Text(), AmmoTarget.Text());
            return true;
        }

        public void Interact(GameObject actor)
        {
            if (!TryFindTypes(actor, out AmmoReserve reserve))
                return;
            TopUp(reserve, _types, _ammo);
            _audio?.Play(_takeSound);
        }

        // Adds what each type lacks to reach its starting amount; a type already there or above gets nothing. Returns the rounds added.
        public static int TopUp(AmmoReserve reserve, IReadOnlyList<AmmoType> types, AmmoConfig ammo)
        {
            int added = 0;
            for (int i = 0; i < types.Count; i++)
            {
                int missing = ammo.StartingAmount(types[i]) - reserve.Count(types[i]);
                if (missing <= 0)
                    continue;
                reserve.Add(types[i], missing);
                added += missing;
            }
            return added;
        }

        // Fills _types with the hotbar guns' ammo types; false when the actor has no reserve, no hotbar or no gun.
        bool TryFindTypes(GameObject actor, out AmmoReserve reserve)
        {
            reserve = null;
            if (_ammo == null || !actor.TryGetComponent(out IGunUser user) || user.Ammo == null
                || !actor.TryGetComponent(out IEquipmentOwner owner) || owner.Equipment == null)
                return false;

            HotbarAmmo.TypesInUse(owner.Equipment.Hotbar, _types);
            reserve = user.Ammo;
            return _types.Count > 0;
        }
    }
}
