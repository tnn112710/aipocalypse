using Ami.BroAudio;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.Interaction;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // The whistle key: each frame finds the IWhistleTarget the player aims at within WhistleRange for the "[Q] Call" prompt
    // (a wall in the way takes the aim, except for targets on WhistleHeardMask near the aim: a whistle carries through
    // walls); a press whistles (always, after a short cooldown) and calls it.
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
        readonly Collider[] _heard = new Collider[16];
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
            Vector3 eye = _eye.position;
            Vector3 aim = _eye.forward;
            int count = Physics.SphereCastNonAlloc(new Ray(eye, aim), _tuning.WhistleRadius, _hits, _tuning.WhistleRange, _tuning.WhistleMask, QueryTriggerInteraction.Ignore);
            if (PhysicsQueries.Nearest(_hits, count, _actor.transform, out RaycastHit nearest)
                && nearest.collider.GetComponentInParent<IWhistleTarget>() is { } aimed)
                return aimed;
            return Heard(eye, aim);
        }

        // Nothing callable under the aim (a wall, a near miss): the heard target nearest the aim, within WhistleHeardAngle.
        IWhistleTarget Heard(Vector3 eye, Vector3 aim)
        {
            if (_tuning.WhistleHeardMask == 0)
                return null;
            float range = _tuning.WhistleRange;
            int count = Physics.OverlapSphereNonAlloc(eye, range, _heard, _tuning.WhistleHeardMask, QueryTriggerInteraction.Ignore);
            IWhistleTarget best = null;
            float bestAngle = _tuning.WhistleHeardAngle;
            for (int i = 0; i < count; i++)
            {
                Collider candidate = _heard[i];
                Vector3 toward = candidate.bounds.center - eye;
                float angle = Vector3.Angle(aim, toward);
                if (angle > bestAngle || toward.sqrMagnitude > range * range || candidate.transform.IsChildOf(_actor.transform))
                    continue;
                if (candidate.GetComponentInParent<IWhistleTarget>() is not { } target)
                    continue;
                best = target;
                bestAngle = angle;
            }
            return best;
        }
    }
}
