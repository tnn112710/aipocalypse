using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using UnityEngine;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.AnimalSystem
{
    // Runs the herd on scaled time: each animal thinks (its Manual tree) every ThinkInterval, earns over the world clock's
    // waking hours, and hands its Tokens over when the player collects them (Animal's interact). Shuts the pen gate once a
    // called herd is home.
    public sealed class AnimalManager : ITickable
    {
        readonly AnimalHerd _herd;
        readonly AnimalSettings _settings;
        readonly WorldClock _clock;
        readonly AudioService _audio;
        double _lastHours = double.NaN;

        public AnimalManager(AnimalHerd herd, AnimalSettings settings, WorldClock clock, AudioService audio)
        {
            _herd = herd;
            _settings = settings;
            _clock = clock;
            _audio = audio;
        }

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
            _herd.Asleep = !_settings.Awake.Contains(_clock.Now.Hour);

            float time = Time.time;
            // Backwards: nothing here removes an animal, but a tree may one day.
            for (int i = _herd.Animals.Count - 1; i >= 0; i--)
            {
                Animal animal = _herd.Animals[i];
                animal.Advance(deltaTime, awakeHours, dayLength);
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

        // The clock was put somewhere else (a loaded save): the next frame earns nothing for the jump.
        public void ForgetClock() => _lastHours = double.NaN;

        // The animal's whole Tokens leave the coin over its back as a few pickups and arc to `to` (the player's feet), each
        // landing within BurstScatter of it: inside the pickup magnet, which takes them from there. They pass through
        // `catcher` (the player's body) and the herd, so none comes to rest on someone's back.
        public void HandOver(Animal animal, Vector3 to, Collider catcher = null)
        {
            CurrencyDefinition tokens = _settings.Tokens;
            int left = animal.Earnings.Collect();
            if (left <= 0 || tokens == null || tokens.Prefab == null)
                return;
            Vector3 from = animal.TokenPoint;
            _audio.PlayAt(_settings.CollectSound, from);
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
            }
        }

        // The launch velocity that lands a thrown thing on `to` after `time` seconds under gravity.
        public static Vector3 ArcVelocity(Vector3 from, Vector3 to, float time) => (to - from) / time - 0.5f * time * Physics.gravity;
    }
}
