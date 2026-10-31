using Aipocalypse.Core.AudioSystem;
using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // One enemy's sounds from its kind's EnemySoundSet (none = silent), rate-limited so a volley or a crowd doesn't stack
    // cries. Footsteps are counted here and played by EnemySoundManager.
    public sealed class EnemyVoice
    {
        // A jump farther than this between two steps is a teleport or a respawn, not walking.
        const float MaxStep = 3f;

        readonly AudioService _audio;
        readonly Transform _body;
        EnemySoundSet _sounds;
        float _nextHurt;
        float _nextShout;
        float _walked;
        Vector3 _lastPosition;

        public EnemyVoice(AudioService audio, Transform body)
        {
            _audio = audio;
            _body = body;
        }

        public EnemySoundSet Sounds => _sounds;

        // On every activation.
        public void Reset(EnemySoundSet sounds)
        {
            _sounds = sounds;
            _nextHurt = 0f;
            _nextShout = 0f;
            _walked = 0f;
            _lastPosition = _body.position;
        }

        public void Swing()
        {
            if (_sounds == null)
                return;
            _audio.PlayAt(_sounds.Swing, _body.position);
            if (Random.value < _sounds.AttackVoiceChance)
                _audio.PlayAt(_sounds.Attack, _body.position);
        }

        public void Impact(Vector3 point)
        {
            if (_sounds != null)
                _audio.PlayAt(_sounds.Impact, point);
        }

        public void Hurt()
        {
            if (_sounds == null || Time.time < _nextHurt)
                return;
            _nextHurt = Time.time + _sounds.HurtCooldown;
            _audio.PlayAt(_sounds.Hurt, _body.position);
        }

        public void Death()
        {
            if (_sounds != null)
                _audio.PlayAt(_sounds.Death, _body.position);
        }

        public void Shout()
        {
            if (_sounds == null || Time.time < _nextShout)
                return;
            _nextShout = Time.time + _sounds.ShoutCooldown;
            _audio.PlayAt(_sounds.Shout, _body.position);
        }

        // Counts the distance walked since the last call while `audible`; true when a stride is complete.
        public bool Walk(bool audible)
        {
            Vector3 position = _body.position;
            float moved = Mathf.Sqrt(MeleeHit.SqrFlatDistance(position, _lastPosition));
            _lastPosition = position;
            if (_sounds == null || !audible || moved > MaxStep)
            {
                _walked = 0f;
                return false;
            }
            _walked += moved;
            if (_walked < _sounds.StrideLength)
                return false;
            _walked %= _sounds.StrideLength;
            return true;
        }

        public void Step() => _audio.PlayAt(_sounds.Footstep, _body.position);
    }
}
