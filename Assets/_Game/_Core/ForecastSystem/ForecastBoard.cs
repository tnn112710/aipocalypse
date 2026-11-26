using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.Events;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.ForecastSystem
{
    // The forecast board in the yard: each plaque shows the fight its period brings as a painted mark (a blank plaque =
    // no fight). Injected by the scene's ForecastInstaller; without one every plaque stays blank. The reveal (spin,
    // rope, bell…) comes later: for now the marks just change.
    [DisallowMultipleComponent]
    public sealed class ForecastBoard : MonoBehaviour
    {
        [Serializable]
        public struct Plaque
        {
            public NightPeriod Period;
            [Tooltip("The painted fight mark on the plaque's face")] public Renderer Mark;
        }

        [SerializeField] Plaque[] _plaques = Array.Empty<Plaque>();
        [SerializeField, Tooltip("The painted mark material per fight size; None leaves the plaque blank")]
        Dictionary<FightSize, Material> _marks = new();

        ForecastService _forecast;
        IDisposable _subscription;

        public IReadOnlyList<Plaque> Plaques => _plaques;
        public IReadOnlyDictionary<FightSize, Material> Marks => _marks;

        [Inject]
        void Construct(ForecastService forecast, ISubscriber<ForecastChanged> changed)
        {
            _forecast = forecast;
            _subscription = changed.Subscribe(_ => Refresh());
        }

        void Start() => Refresh();

        void OnDestroy() => _subscription?.Dispose();

        void Refresh()
        {
            foreach (Plaque plaque in _plaques)
            {
                if (plaque.Mark == null)
                    continue;
                FightSize fight = _forecast?.Fight(plaque.Period) ?? FightSize.None;
                bool painted = fight != FightSize.None && _marks.TryGetValue(fight, out Material mark) && mark != null;
                plaque.Mark.enabled = painted;
                if (painted)
                    plaque.Mark.sharedMaterial = _marks[fight];
            }
        }
    }
}
