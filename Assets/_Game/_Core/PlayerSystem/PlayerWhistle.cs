using Ami.BroAudio;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.Interaction;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // The whistle key: the player whistles (always, after a short cooldown) and calls the IWhistleTarget they aim at within
    // WhistleRange. Anything else in the way (a wall) takes the call.
    public sealed class PlayerWhistle
    {
        readonly Transform _eye;
        readonly GameObject _actor;
        readonly PlayerInteractionTuning _tuning;
        readonly AudioService _audio;
        readonly SoundID _sound;
        readonly RaycastHit[] _hits = new RaycastHit[16];
        float _readyAt = float.NegativeInfinity;

        public PlayerWhistle(Transform eye, GameObject actor, PlayerInteractionTuning tuning, AudioService audio, SoundID sound)
        {
            _eye = eye;
            _actor = actor;
            _tuning = tuning;
            _audio = audio;
            _sound = sound;
        }

        // The target that answered, or null.
        public IWhistleTarget Tick(bool pressed, float time)
        {
            if (!pressed || time < _readyAt)
                return null;
            _readyAt = time + _tuning.WhistleCooldown;
            _audio?.PlayAt(_sound, _eye.position);
            IWhistleTarget target = FindTarget();
            return target != null && target.TryWhistle(_actor) ? target : null;
        }

        IWhistleTarget FindTarget()
        {
            Ray ray = new(_eye.position, _eye.forward);
            int count = Physics.SphereCastNonAlloc(ray, _tuning.WhistleRadius, _hits, _tuning.WhistleRange, _tuning.WhistleMask, QueryTriggerInteraction.Ignore);
            return PhysicsQueries.Nearest(_hits, count, _actor.transform, out RaycastHit nearest)
                ? nearest.collider.GetComponentInParent<IWhistleTarget>()
                : null;
        }
    }
}
