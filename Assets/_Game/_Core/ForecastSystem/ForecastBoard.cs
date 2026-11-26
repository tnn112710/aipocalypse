using System;
using System.Collections.Generic;
using Ami.BroAudio;
using DG.Tweening;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.Effects;
using MB13.Aipocalypse.Core.EnvironmentSystem;
using MB13.Aipocalypse.Core.Events;
using MessagePipe;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.ForecastSystem
{
    // The forecast board in the yard: each plaque is a reel on a pin showing '?' until the forecast is revealed, then the
    // fight its period brings (blank = no fight). A live reveal is a slot machine: the yank jolts the plaques, they rock
    // back and spin while the frame rattles, tick as they slow and land one after another with a pop and wood chips; the
    // last one stops short, teeters on its edge and tips over (longer for a boss, which always lands last, thuds and shakes
    // the board). Shots spin a plaque round its pin (it always lands face out), and the plaques rock in the wind.
    // Injected by the scene's ForecastInstaller; without one every plaque stays blank.
    [DisallowMultipleComponent]
    public sealed class ForecastBoard : MonoBehaviour, IHitReaction
    {
        [Serializable]
        public struct Plaque
        {
            public NightPeriod Period;
            [Tooltip("Turns on the plaque's pin (local X) when it spins")] public Transform Reel;
            [Tooltip("The painted mark on the reel's face")] public Renderer Mark;
        }

        [SerializeField] Plaque[] _plaques = Array.Empty<Plaque>();
        [SerializeField, Tooltip("The painted mark per fight size; None leaves the plaque blank")]
        Dictionary<FightSize, Material> _marks = new();
        [SerializeField, Tooltip("Painted while the forecast is hidden")] Material _unknown;

        [BoxGroup("Spin"), SerializeField, Min(0f), Tooltip("Metres the plaques jump on their ropes when the rope is yanked")]
        float _yank = 0.05f;
        [BoxGroup("Spin"), SerializeField, Range(0f, 60f), Tooltip("Degrees the reels rock back before they spin")] float _windUp = 26f;
        [BoxGroup("Spin"), SerializeField, Min(0.01f), Tooltip("Seconds of that rock back, then a beat of hold")] float _windUpTime = 0.18f;
        [BoxGroup("Spin"), SerializeField, Min(0f)] float _windUpHold = 0.08f;
        [BoxGroup("Spin"), SerializeField, Min(1), Tooltip("Whole turns of the first plaque to land; each later one turns TurnsStep more")]
        int _turns = 5;
        [BoxGroup("Spin"), SerializeField, Min(0)] int _turnsStep = 2;
        [BoxGroup("Spin"), SerializeField, Min(0.1f), Tooltip("Seconds the first plaque spins")] float _spinTime = 1.6f;
        [BoxGroup("Spin"), SerializeField, Min(0f), Tooltip("Seconds between two landings")] float _landGap = 0.55f;
        [BoxGroup("Spin"), SerializeField, Range(0f, 3f), Tooltip("Degrees the frame rattles while reels spin")] float _rattle = 0.5f;

        [BoxGroup("Near miss"), SerializeField, Range(0f, 120f), Tooltip("Degrees short of its face the last plaque stops")]
        float _nearMiss = 70f;
        [BoxGroup("Near miss"), SerializeField, Range(0f, 30f), Tooltip("Degrees it teeters back and forth there")] float _teeter = 12f;
        [BoxGroup("Near miss"), SerializeField, Min(0f), Tooltip("Seconds it teeters (a boss: BossTeeter)")] float _teeterTime = 0.55f;
        [BoxGroup("Near miss"), SerializeField, Min(0f)] float _bossTeeterTime = 1.1f;
        [BoxGroup("Near miss"), SerializeField, Min(0.01f), Tooltip("Seconds it takes to tip over into place")] float _tipTime = 0.2f;

        [BoxGroup("Landing"), SerializeField, Range(0f, 45f), Tooltip("Degrees a reel overshoots and wobbles as it lands")] float _landKick = 16f;
        [BoxGroup("Landing"), SerializeField, Range(0f, 60f), Tooltip("The same for a boss")] float _bossKick = 34f;
        [BoxGroup("Landing"), SerializeField, Range(0f, 0.5f), Tooltip("Squash and stretch as it lands (share of its size)")] float _landPop = 0.12f;
        [BoxGroup("Landing"), SerializeField, Min(0f), Tooltip("Metres a plaque dips on its ropes as it lands")] float _landDip = 0.035f;
        [BoxGroup("Landing"), SerializeField, Range(0f, 5f), Tooltip("Degrees the whole board shakes when a boss lands")] float _bossShake = 2f;
        [BoxGroup("Landing"), SerializeField, Tooltip("Wood chips at a plaque's bottom edge as it lands (pooled); optional")]
        PooledEffect _landChips;
        [BoxGroup("Landing"), SerializeField, Min(0.1f), Tooltip("Seconds the plaques take to turn back to '?' for a new day")]
        float _hideTime = 0.8f;

        [BoxGroup("Shots"), SerializeField, Min(0f), Tooltip("Metres from the pin a hit must land to spin the plaque; closer only wobbles")]
        float _spinLever = 0.06f;
        [BoxGroup("Shots"), SerializeField, Min(0.01f), Tooltip("Each this many metres further adds a turn")] float _leverPerTurn = 0.08f;
        [BoxGroup("Shots"), SerializeField, Min(1)] int _maxShotTurns = 3;
        [BoxGroup("Shots"), SerializeField, Range(0f, 45f), Tooltip("Degrees a hit near the pin wobbles the plaque")] float _shotWobble = 14f;

        [BoxGroup("Wind"), SerializeField, Range(0f, 10f), Tooltip("Degrees the plaques rock per unit of wind strength (Calm 0.4 … Gale 2.8)")]
        float _swayPerWind = 2.4f;
        [BoxGroup("Wind"), SerializeField, Min(0f), Tooltip("Wind strength used when the scene has no weather")] float _stillWind = 0.6f;
        [BoxGroup("Wind"), SerializeField, Min(0.01f), Tooltip("Rocks per second")] float _swaySpeed = 0.55f;

        [BoxGroup("Sounds"), SerializeField, Tooltip("A reel passing edge-on")] SoundID _tickSound;
        [BoxGroup("Sounds"), SerializeField, Min(0.01f), Tooltip("Shortest gap between two ticks, all reels together (seconds)")]
        float _tickGap = 0.05f;
        [BoxGroup("Sounds"), SerializeField] SoundID _landSound;
        [BoxGroup("Sounds"), SerializeField] SoundID _bossLandSound;

        ForecastService _forecast;
        ForecastReveal _reveal;
        AudioService _audio;
        EffectPool _effects;
        WeatherService _weather;
        IDisposable _subscriptions;
        Sequence[] _spins = Array.Empty<Sequence>();
        float[] _angles = Array.Empty<float>();
        Quaternion[] _hangRest = Array.Empty<Quaternion>();
        Vector3[] _reelScale = Array.Empty<Vector3>();
        Tween _shake;
        Quaternion _rest;
        float _lastTick = float.MinValue;
        // Shots don't move the plaques until the reveal's spin is over.
        float _revealEnd;

        public IReadOnlyList<Plaque> Plaques => _plaques;
        public IReadOnlyDictionary<FightSize, Material> Marks => _marks;
        public Material Unknown => _unknown;
        public bool IsSpinning => Array.Exists(_spins, s => s != null && s.IsActive());

        [Inject]
        void Construct(ForecastService forecast, ForecastReveal reveal, AudioService audio, EffectPool effects, IObjectResolver resolver,
            ISubscriber<ForecastChanged> changed, ISubscriber<ForecastRevealChanged> revealed)
        {
            _forecast = forecast;
            _reveal = reveal;
            _audio = audio;
            _effects = effects;
            resolver.TryResolve(out _weather);
            _subscriptions = DisposableBag.Create(
                changed.Subscribe(_ => OnForecastChanged()),
                revealed.Subscribe(e =>
                {
                    if (e.Live)
                        Spin(e.Revealed);
                    else
                        Show();
                }));
        }

        void Awake()
        {
            _rest = transform.localRotation;
            int count = _plaques.Length;
            _spins = new Sequence[count];
            _angles = new float[count];
            _hangRest = new Quaternion[count];
            _reelScale = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                Transform reel = _plaques[i].Reel;
                if (reel == null)
                    continue;
                _hangRest[i] = reel.parent != null ? reel.parent.localRotation : Quaternion.identity;
                _reelScale[i] = reel.localScale;
            }
        }

        void Start() => Show();

        void OnDestroy()
        {
            _subscriptions?.Dispose();
            Stop();
        }

        // The plaques rock on their pins with the wind, a little out of step, plus gusts.
        void Update()
        {
            float wind = _weather != null ? _weather.Mix.Strength + 0.5f * _weather.Mix.Gust : _stillWind;
            float time = Time.time;
            for (int i = 0; i < _plaques.Length; i++)
            {
                Transform hanger = _plaques[i].Reel != null ? _plaques[i].Reel.parent : null;
                if (hanger == null)
                    continue;
                float phase = i * 1.7f;
                float rock = Mathf.Sin((time * _swaySpeed + phase) * Mathf.PI * 2f) * 0.6f
                    + (Mathf.PerlinNoise(time * 0.35f, phase) - 0.5f) * 1.2f;
                hanger.localRotation = _hangRest[i] * Quaternion.Euler(rock * _swayPerWind * wind, 0f, rock * 0.15f * wind);
            }
        }

        bool Revealed => _reveal == null || _reveal.IsRevealed;

        // An override (console) while shown spins to the new marks; hidden plaques stay '?'.
        void OnForecastChanged()
        {
            if (Revealed)
                Spin(true);
        }

        // Shows the current state at once.
        void Show()
        {
            Stop();
            bool revealed = Revealed;
            foreach (Plaque plaque in _plaques)
                Paint(plaque, Target(plaque, revealed));
        }

        void Spin(bool revealed)
        {
            Stop();
            // Landing order: left to right, a boss last.
            var order = new List<int>(_plaques.Length);
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < _plaques.Length; i++)
                    if (IsBoss(_plaques[i], revealed) == (pass == 1))
                        order.Add(i);

            float longest = 0f;
            for (int rank = 0; rank < order.Count; rank++)
            {
                int index = order[rank];
                Plaque plaque = _plaques[index];
                if (plaque.Reel == null || plaque.Mark == null)
                    continue;
                bool boss = IsBoss(plaque, revealed);
                bool last = rank == order.Count - 1;
                if (revealed)
                {
                    float time = _spinTime + rank * _landGap;
                    _spins[index] = Reel(index, Target(plaque, true), _turns + rank * _turnsStep, time, last, boss);
                    longest = Mathf.Max(longest, time);
                    if (_yank > 0f && plaque.Reel.parent != null)
                        plaque.Reel.parent.DOPunchPosition(Vector3.up * _yank, 0.35f, 5, 0.7f).SetLink(gameObject);
                }
                else
                    _spins[index] = Hide(index, _hideTime + rank * 0.12f);
            }
            foreach (Sequence spin in _spins)
                if (spin != null)
                    _revealEnd = Mathf.Max(_revealEnd, Time.time + spin.Duration());
            if (revealed && _rattle > 0f)
                Shake(longest, _rattle, 22);
        }

        // One reel: rock back, spin `turns` whole turns slowing down (the face swaps while it looks away in the last turn),
        // the last one stopping short to teeter before it tips over, then land.
        Sequence Reel(int index, Material target, int turns, float time, bool nearMiss, bool boss)
        {
            Plaque plaque = _plaques[index];
            float total = 360f * turns;
            float stop = nearMiss ? total - _nearMiss : total;
            bool swapped = false;
            int edge = 0;
            void Set(float a)
            {
                Turn(index, a);
                if (!swapped && a >= total - 180f)
                {
                    swapped = true;
                    Paint(plaque, target);
                }
                int passed = Mathf.FloorToInt((a + 90f) / 180f);
                if (passed != edge)
                {
                    edge = passed;
                    Tick(plaque.Reel.position, 1f);
                }
            }

            Sequence sequence = DOTween.Sequence().SetLink(gameObject);
            sequence.Append(DOVirtual.Float(0f, -_windUp, _windUpTime, Set).SetEase(Ease.OutQuad));
            sequence.AppendInterval(_windUpHold);
            sequence.Append(DOVirtual.Float(-_windUp, stop, time, Set).SetEase(nearMiss ? Ease.OutCubic : Ease.OutQuart));
            if (nearMiss)
            {
                // Teeter: back and forth on the edge, each swing ticking, then it tips over into place.
                float teeterTime = boss ? _bossTeeterTime : _teeterTime;
                int swings = boss ? 3 : 2;
                sequence.Append(DOVirtual.Float(0f, 1f, teeterTime, t =>
                {
                    float wave = Mathf.Sin(t * Mathf.PI * swings);
                    Turn(index, stop - _teeter * wave * (1f - 0.35f * t));
                }).SetEase(Ease.Linear));
                sequence.AppendCallback(() => Tick(plaque.Reel.position, 1f));
                sequence.Append(DOVirtual.Float(stop, total, _tipTime, Set).SetEase(Ease.InQuad));
            }
            sequence.AppendCallback(() => Land(index, boss));
            return sequence;
        }

        // Back to '?' for a new day: one quick turn.
        Sequence Hide(int index, float time)
        {
            Plaque plaque = _plaques[index];
            bool swapped = false;
            Sequence sequence = DOTween.Sequence().SetLink(gameObject);
            sequence.Append(DOVirtual.Float(0f, 360f, time, a =>
            {
                Turn(index, a);
                if (!swapped && a >= 180f)
                {
                    swapped = true;
                    Paint(plaque, _unknown);
                }
            }).SetEase(Ease.InOutQuad));
            sequence.AppendCallback(() => Land(index, false, 0.6f));
            return sequence;
        }

        void Land(int index, bool boss, float loud = 1f)
        {
            Plaque plaque = _plaques[index];
            Transform reel = plaque.Reel;
            Turn(index, 0f);
            _audio?.PlayAt(boss ? _bossLandSound : _landSound, reel.position, loud);
            reel.DOPunchRotation(new Vector3(boss ? _bossKick : _landKick, 0f, 0f), boss ? 0.8f : 0.5f, boss ? 5 : 6, 0.7f).SetLink(gameObject);
            if (_landPop > 0f)
                reel.DOPunchScale(new Vector3(-_landPop, _landPop, 0f) * (boss ? 1.6f : 1f), 0.35f, 4, 0.6f).SetLink(gameObject)
                    .OnKill(() => reel.localScale = _reelScale[index]);
            if (reel.parent != null)
                reel.parent.DOPunchPosition(Vector3.down * (boss ? _landDip * 2f : _landDip), 0.4f, 6, 0.8f).SetLink(gameObject);
            Chips(plaque, boss ? 2 : 1);
            if (!boss)
                return;
            Shake(0.7f, _bossShake, 14);
            // The other plaques jump at the thud.
            for (int i = 0; i < _plaques.Length; i++)
                if (i != index && _plaques[i].Reel != null)
                    _plaques[i].Reel.DOPunchRotation(new Vector3(_landKick * 0.6f, 0f, 0f), 0.5f, 6, 0.7f).SetLink(gameObject);
        }

        // A hit spins the plaque round its pin, by where it landed: near the pin it wobbles, further out it turns 1-3 times,
        // always ending face out. Ignored while the forecast spins.
        public void React(Collider collider, in DamageInfo info)
        {
            int index = PlaqueOf(collider);
            if (index < 0 || Time.time < _revealEnd)
                return;
            Plaque plaque = _plaques[index];
            Transform reel = plaque.Reel;
            Vector3 push = info.Force.sqrMagnitude > 1e-6f ? info.Force.normalized
                : info.Source != null ? (info.Point - info.Source.transform.position).normalized : transform.forward;
            float torque = Vector3.Dot(Vector3.Cross(info.Point - reel.position, push), reel.right);
            float lever = Mathf.Abs(torque);
            float sign = Mathf.Sign(torque);
            _spins[index]?.Kill();
            if (lever < _spinLever)
            {
                reel.DOKill(true);
                Turn(index, 0f);
                reel.DOPunchRotation(new Vector3(sign * _shotWobble * Mathf.Max(0.3f, lever / _spinLever), 0f, 0f), 0.45f, 6, 0.7f)
                    .SetLink(gameObject);
                return;
            }
            int turns = Mathf.Min(_maxShotTurns, 1 + Mathf.FloorToInt((lever - _spinLever) / _leverPerTurn));
            float from = _angles[index];
            float to = sign > 0f ? Mathf.Ceil((from + 360f * turns) / 360f) * 360f : Mathf.Floor((from - 360f * turns) / 360f) * 360f;
            int edge = Mathf.FloorToInt((from + 90f) / 180f);
            reel.DOKill(true);
            _spins[index] = DOTween.Sequence().SetLink(gameObject)
                .Append(DOVirtual.Float(from, to, 0.45f + 0.3f * Mathf.Abs(to - from) / 360f, a =>
                {
                    Turn(index, a);
                    int passed = Mathf.FloorToInt((a + 90f) / 180f);
                    if (passed != edge)
                    {
                        edge = passed;
                        Tick(reel.position, 0.7f);
                    }
                }).SetEase(Ease.OutCubic))
                .AppendCallback(() =>
                {
                    Turn(index, 0f);
                    _audio?.PlayAt(_landSound, reel.position, 0.6f);
                    reel.DOPunchRotation(new Vector3(sign * _landKick * 0.7f, 0f, 0f), 0.4f, 6, 0.7f).SetLink(gameObject);
                });
        }

        int PlaqueOf(Collider collider)
        {
            for (int i = 0; i < _plaques.Length; i++)
                if (_plaques[i].Reel != null && collider.transform.IsChildOf(_plaques[i].Reel))
                    return i;
            return -1;
        }

        void Chips(Plaque plaque, int count)
        {
            if (_landChips == null || _effects == null)
                return;
            Bounds bounds = plaque.Mark.bounds;
            var rotation = Quaternion.LookRotation(-transform.forward * 0.6f + Vector3.down * 0.8f);
            for (int i = 0; i < count; i++)
            {
                float x = count == 1 ? 0f : (i == 0 ? -0.3f : 0.3f);
                _effects.Spawn(_landChips, new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) + transform.right * x * bounds.extents.x, rotation);
            }
        }

        void Shake(float time, float degrees, int vibrato)
        {
            _shake?.Kill();
            _shake = transform.DOShakeRotation(time, new Vector3(degrees, 0f, degrees), vibrato, 90f, true)
                .SetLink(gameObject).OnKill(() => transform.localRotation = _rest);
        }

        void Tick(Vector3 at, float loud)
        {
            if (Time.time - _lastTick < _tickGap)
                return;
            _lastTick = Time.time;
            _audio?.PlayAt(_tickSound, at, loud);
        }

        void Stop()
        {
            for (int i = 0; i < _spins.Length; i++)
            {
                _spins[i]?.Kill();
                _spins[i] = null;
            }
            _shake?.Kill();
            _revealEnd = 0f;
            for (int i = 0; i < _plaques.Length; i++)
            {
                Transform reel = _plaques[i].Reel;
                if (reel == null)
                    continue;
                reel.DOKill(true);
                Turn(i, 0f);
                // A landing dip completes back to where the plaque hangs.
                if (reel.parent != null)
                    reel.parent.DOKill(true);
            }
        }

        void Turn(int index, float angle)
        {
            _angles[index] = angle;
            _plaques[index].Reel.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }

        FightSize Fight(Plaque plaque) => _forecast?.Fight(plaque.Period) ?? FightSize.None;

        bool IsBoss(Plaque plaque, bool revealed) => revealed && Fight(plaque) == FightSize.Boss;

        Material Target(Plaque plaque, bool revealed)
        {
            if (_forecast == null || !_forecast.Enabled)
                return null;
            if (!revealed)
                return _unknown;
            return _marks.TryGetValue(Fight(plaque), out Material mark) ? mark : null;
        }

        static void Paint(Plaque plaque, Material material)
        {
            if (plaque.Mark == null)
                return;
            plaque.Mark.enabled = material != null;
            if (material != null)
                plaque.Mark.sharedMaterial = material;
        }
    }
}
