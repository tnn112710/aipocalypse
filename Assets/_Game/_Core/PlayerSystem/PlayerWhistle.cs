using Ami.BroAudio;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.Interaction;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // The whistle key: each frame finds the IWhistleTarget the player aims at within WhistleRange (anything else in the way,
    // a wall, takes the aim) for the "[Q] Call" prompt; a press whistles (always, after a short cooldown) and calls it.
    public sealed class PlayerWhistle
    {
        // Seconds between aim casts for the prompt (a 50 m sphere cast); a press always casts afresh.
        const float AimInterval = 0.05f;

        readonly Transform _eye;
        readonly GameObject _actor;
        readonly PlayerInteractionTuning _tuning;
        readonly AudioService _audio;
        readonly SoundID _sound;
        readonly RaycastHit[] _hits = new RaycastHit[32];
        float _readyAt = float.NegativeInfinity;
        float _nextAim = float.NegativeInfinity;

        public PlayerWhistle(Transform eye, GameObject actor, PlayerInteractionTuning tuning, AudioService audio, SoundID sound)
        {
            _eye = eye;
            _actor = actor;
            _tuning = tuning;
            _audio = audio;
            _sound = sound;
        }

        // What the aim is on (null: nothing to call), and its prompt.
        public IWhistleTarget Target { get; private set; }
        public bool HasPrompt { get; private set; }
        public InteractionPrompt Prompt { get; private set; }

        // The target that answered, or null.
        public IWhistleTarget Tick(bool pressed, float time)
        {
            bool whistles = pressed && time >= _readyAt;
            if (whistles || time >= _nextAim || time < _nextAim - AimInterval)
            {
                _nextAim = time + AimInterval;
                Target = FindTarget();
            }
            InteractionPrompt prompt = default;
            HasPrompt = Target != null && Target.TryGetWhistlePrompt(_actor, out prompt);
            Prompt = prompt;
            if (!whistles)
                return null;
            _readyAt = time + _tuning.WhistleCooldown;
            _audio?.PlayAt(_sound, _eye.position);
            return Target != null && Target.TryWhistle(_actor) ? Target : null;
        }

        // The player can't act (a menu, a sequence): no prompt.
        public void Clear()
        {
            Target = null;
            HasPrompt = false;
            _nextAim = float.NegativeInfinity;
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
