using System;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;
using Random = UnityEngine.Random;

namespace MB13.Aipocalypse.Core.AnimalSystem
{
    // Runs the herd on scaled time: each animal thinks (its Manual tree) every ThinkInterval and earns over the world clock's
    // waking hours; tells the herd where the player is, makes animals step aside from them and run from gunshots; hands
    // Tokens over when the player collects them (Animal's Collect key). Shuts the pen gate once a called herd is home.
    public sealed class AnimalManager : ITickable, IDisposable
    {
        readonly AnimalHerd _herd;
        readonly AnimalSettings _settings;
        readonly WorldClock _clock;
        readonly PlayerRegistry _players;
        readonly AudioService _audio;
        readonly IDisposable _shots;
        double _lastHours = double.NaN;

        public AnimalManager(AnimalHerd herd, AnimalSettings settings, WorldClock clock, PlayerRegistry players, AudioService audio,
            ISubscriber<ShotFired> shots)
        {
            _herd = herd;
            _settings = settings;
            _clock = clock;
            _players = players;
            _audio = audio;
            _shots = shots.Subscribe(OnShot);
        }

        public void Dispose() => _shots.Dispose();

        public void Tick() => Advance(Time.deltaTime);

        public void Advance(float deltaTime)
        {
            if (!_herd.Enabled)
                return;
            double now = _clock.Now.TotalHours;
            // The first frame, and a clock set backwards, earn nothing.
            double from = double.IsNaN(_lastHours) || _lastHours > now ? now : _lastHours;
            _lastHours = now;
            float awakeHours = (float)AnimalEarnings.AwakeHours(from, now, _settings.Awake);
            float dayLength = AnimalEarnings.DayLength(_settings.Awake);
            float hour = _clock.Now.Hour;
            _herd.Asleep = !_settings.Awake.Contains(hour);
            _herd.Dusk = !_herd.Asleep && !_settings.Awake.Contains(hour + _settings.DuskHours);

            IPlayer player = _players.Current;
            _herd.HasPlayer = player != null && !player.IsDead;
            _herd.PlayerPosition = _herd.HasPlayer ? player.Transform.position : default;
            float time = Time.time;
            // Backwards: nothing here removes an animal, but a tree may one day.
            for (int i = _herd.Animals.Count - 1; i >= 0; i--)
            {
                Animal animal = _herd.Animals[i];
                animal.Advance(deltaTime, awakeHours, dayLength);
                // Awake and about its own business: walked into, it steps aside.
                float bump = _settings.BumpRadius * animal.Definition.ScareScale;
                if (_herd.HasPlayer && !animal.Startled && !animal.Sleeping && !animal.GoingHome
                    && (animal.transform.position - _herd.PlayerPosition).sqrMagnitude <= bump * bump)
                    animal.Startle(_herd.PlayerPosition, _settings.StepAside, run: false);
                if (time >= animal.NextThinkAt)
                {
                    animal.NextThinkAt = time + _settings.ThinkInterval;
                    animal.Think();
                }
            }

            HerdOrders orders = _herd.Orders;
            if (orders.Called && orders.GateOpen && _herd.Pen.Gate != null && _herd.CountHome() == _herd.Animals.Count)
                _herd.Pen.Gate.Close();
        }

        // Every animal near the shot runs from it, even out of its sleep; one walking home keeps walking.
        void OnShot(ShotFired shot)
        {
            if (!_herd.Enabled)
                return;
            foreach (Animal animal in _herd.Animals)
            {
                float reach = _settings.ShotRadius * animal.Definition.ScareScale;
                if (!animal.GoingHome && (animal.transform.position - shot.Position).sqrMagnitude <= reach * reach)
                    animal.Startle(shot.Position, _settings.FleeDistance, run: true);
            }
        }

        // The clock was put somewhere else (a loaded save): the next frame earns nothing for the jump.
        public void ForgetClock() => _lastHours = double.NaN;

        // The animal's whole Tokens leave the coin over its back as a few pickups and arc to `to` (the player's feet), each
        // landing within BurstScatter of it: inside the pickup magnet, which takes them from there. They pass through
        // `catcher` (the player's body), the herd and the pen's fence, so none comes to rest on someone's back or drops back
        // into the pen off a rail.
        public void HandOver(Animal animal, Vector3 to, Collider catcher = null)
        {
            CurrencyDefinition tokens = _settings.Tokens;
            int left = animal.Earnings.Collect();
            if (left <= 0 || tokens == null || tokens.Prefab == null)
                return;
            Vector3 from = animal.TokenPoint;
            _audio.PlayAt(_settings.CollectSound, from);
            if (_settings.TokenNumber != null)
                _settings.TokenNumber.Spawn(from + Vector3.up * 0.3f, left);
            for (int piece = Mathf.Min(_settings.BurstPieces, left); piece > 0; piece--)
            {
                int share = Mathf.CeilToInt(left / (float)piece);
                left -= share;
                WorldItem pickup = WorldItem.Spawn(tokens.CreateInstance(share), from, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), null);
                if (pickup == null)
                    continue;
                Vector2 scatter = Random.insideUnitCircle * _settings.BurstScatter;
                float time = _settings.BurstFlightTime * Random.Range(0.85f, 1.2f);
                pickup.Throw(ArcVelocity(from, to + new Vector3(scatter.x, 0f, scatter.y), time), catcher);
                foreach (Animal other in _herd.Animals)
                    pickup.PassThrough(other.Body);
                foreach (Collider rail in _herd.FenceColliders)
                    pickup.PassThrough(rail);
            }
        }

        // The launch velocity that lands a thrown thing on `to` after `time` seconds under gravity.
        public static Vector3 ArcVelocity(Vector3 from, Vector3 to, float time) => (to - from) / time - 0.5f * time * Physics.gravity;
    }
}
