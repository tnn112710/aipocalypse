using System;
using System.Collections.Generic;
using Ami.BroAudio;
using DG.Tweening;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.Events;
using MessagePipe;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.ForecastSystem
{
    // The forecast board in the yard: each plaque is a reel on a pin showing '?' until the forecast is revealed, then the
    // fight its period brings (blank = no fight). A live reveal spins the reels like a slot machine: they rock back, spin,
    // tick as they slow and land one after another, a boss last and heaviest. Injected by the scene's ForecastInstaller;
    // without one every plaque stays blank.
    [DisallowMultipleComponent]
    public sealed class ForecastBoard : MonoBehaviour
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

        [BoxGroup("Spin"), SerializeField, Min(1), Tooltip("Whole turns of the first plaque to land; each later one turns once more")]
        int _turns = 3;
        [BoxGroup("Spin"), SerializeField, Min(0.1f), Tooltip("Seconds the first plaque spins")] float _spinTime = 1.5f;
        [BoxGroup("Spin"), SerializeField, Min(0f), Tooltip("Seconds between two landings")] float _landGap = 0.5f;
        [BoxGroup("Spin"), SerializeField, Min(0f), Tooltip("Extra seconds a boss plaque spins (it always lands last)")]
        float _bossSuspense = 0.7f;
        [BoxGroup("Spin"), SerializeField, Range(0f, 45f), Tooltip("Degrees the reels rock back before they spin")] float _windUp = 18f;
        [BoxGroup("Spin"), SerializeField, Min(0.01f), Tooltip("Seconds of that rock back")] float _windUpTime = 0.16f;
        [BoxGroup("Spin"), SerializeField, Range(0f, 45f), Tooltip("Degrees a reel overshoots and wobbles as it lands")] float _landKick = 14f;
        [BoxGroup("Spin"), SerializeField, Range(0f, 60f), Tooltip("The same for a boss")] float _bossKick = 30f;
        [BoxGroup("Spin"), SerializeField, Min(0f), Tooltip("Metres a plaque dips on its ropes as it lands")] float _landDip = 0.03f;
        [BoxGroup("Spin"), SerializeField, Range(0f, 5f), Tooltip("Degrees the whole board shakes when a boss lands")] float _bossShake = 1.5f;
        [BoxGroup("Spin"), SerializeField, Min(0.1f), Tooltip("Seconds the plaques take to turn back to '?' for a new day")]
        float _hideTime = 0.8f;

        [BoxGroup("Sounds"), SerializeField, Tooltip("A reel passing edge-on")] SoundID _tickSound;
        [BoxGroup("Sounds"), SerializeField, Min(0.01f), Tooltip("Shortest gap between two ticks, all reels together (seconds)")]
        float _tickGap = 0.055f;
        [BoxGroup("Sounds"), SerializeField] SoundID _landSound;
        [BoxGroup("Sounds"), SerializeField] SoundID _bossLandSound;

        ForecastService _forecast;
        ForecastReveal _reveal;
        AudioService _audio;
        IDisposable _subscriptions;
        Sequence[] _spins = Array.Empty<Sequence>();
        Tween _shake;
        Quaternion _rest;
        float _lastTick = float.MinValue;

        public IReadOnlyList<Plaque> Plaques => _plaques;
        public IReadOnlyDictionary<FightSize, Material> Marks => _marks;
        public Material Unknown => _unknown;
        public bool IsSpinning => Array.Exists(_spins, s => s != null && s.IsActive());

        [Inject]
        void Construct(ForecastService forecast, ForecastReveal reveal, AudioService audio, ISubscriber<ForecastChanged> changed,
            ISubscriber<ForecastRevealChanged> revealed)
        {
            _forecast = forecast;
            _reveal = reveal;
            _audio = audio;
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
            _spins = new Sequence[_plaques.Length];
        }

        void Start() => Show();

        void OnDestroy()
        {
            _subscriptions?.Dispose();
            Stop();
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
            {
                if (plaque.Reel != null)
                    plaque.Reel.localRotation = Quaternion.identity;
                Paint(plaque, Target(plaque, revealed));
            }
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

            for (int rank = 0; rank < order.Count; rank++)
            {
                int index = order[rank];
                Plaque plaque = _plaques[index];
                if (plaque.Reel == null || plaque.Mark == null)
                    continue;
                bool boss = IsBoss(plaque, revealed);
                _spins[index] = revealed
                    ? Reel(plaque, Target(plaque, true), _turns + rank, _spinTime + rank * _landGap + (boss ? _bossSuspense : 0f), _windUp, boss)
                    : Reel(plaque, _unknown, 1, _hideTime + rank * 0.12f, _windUp * 0.5f, false);
            }
        }

        // One reel: rock back, spin `turns` whole turns slowing down (the face swaps while it looks away in the last turn),
        // then land with an overshoot.
        Sequence Reel(Plaque plaque, Material target, int turns, float time, float windUp, bool boss)
        {
            Transform reel = plaque.Reel;
            float total = 360f * turns;
            bool swapped = false;
            int edge = 0;
            Sequence sequence = DOTween.Sequence().SetLink(gameObject);
            sequence.Append(DOVirtual.Float(0f, -windUp, _windUpTime, a => Turn(reel, a)).SetEase(Ease.OutQuad));
            sequence.Append(DOVirtual.Float(-windUp, total, time, a =>
            {
                Turn(reel, a);
                if (!swapped && a >= total - 180f)
                {
                    swapped = true;
                    Paint(plaque, target);
                }
                int passed = Mathf.FloorToInt((a + 90f) / 180f);
                if (passed != edge)
                {
                    edge = passed;
                    Tick(reel.position);
                }
            }).SetEase(Ease.OutQuart));
            sequence.AppendCallback(() => Land(reel, boss));
            sequence.Append(reel.DOPunchRotation(new Vector3(boss ? _bossKick : _landKick, 0f, 0f), boss ? 0.8f : 0.45f, boss ? 5 : 6, 0.7f));
            return sequence;
        }

        void Land(Transform reel, bool boss)
        {
            _audio?.PlayAt(boss ? _bossLandSound : _landSound, reel.position);
            if (reel.parent != null)
                reel.parent.DOPunchPosition(Vector3.down * (boss ? _landDip * 2f : _landDip), 0.4f, 6, 0.8f).SetLink(gameObject);
            if (!boss || _bossShake <= 0f)
                return;
            _shake?.Kill(true);
            _shake = transform.DOShakeRotation(0.6f, new Vector3(_bossShake, 0f, _bossShake), 14, 90f, true)
                .SetLink(gameObject).OnKill(() => transform.localRotation = _rest);
        }

        void Tick(Vector3 at)
        {
            if (Time.time - _lastTick < _tickGap)
                return;
            _lastTick = Time.time;
            _audio?.PlayAt(_tickSound, at);
        }

        void Stop()
        {
            for (int i = 0; i < _spins.Length; i++)
            {
                _spins[i]?.Kill();
                _spins[i] = null;
            }
            _shake?.Kill();
            foreach (Plaque plaque in _plaques)
                if (plaque.Reel != null)
                {
                    plaque.Reel.DOKill();
                    plaque.Reel.localRotation = Quaternion.identity;
                    // A landing dip completes back to where the plaque hangs.
                    if (plaque.Reel.parent != null)
                        plaque.Reel.parent.DOKill(true);
                }
        }

        static void Turn(Transform reel, float angle) => reel.localRotation = Quaternion.Euler(angle, 0f, 0f);

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
