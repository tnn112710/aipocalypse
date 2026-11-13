using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using UnityEngine;
using UnityEngine.Localization;
using VContainer;

namespace MB13.Aipocalypse.Core.UI
{
    // Shows the world clock. Polled: at 30 s per game hour the minute changes every half second, and the view skips repeats.
    // Hidden in scenes without a clock.
    public sealed class ClockHud : MonoBehaviour
    {
        [SerializeField] ClockView _view;

        WorldClock _clock;
        int _phase = -1;
        Locale _phaseLocale;

        [Inject]
        void Construct(IObjectResolver resolver) => resolver.TryResolve(out _clock);

        void Start() => _view.Show(_clock != null);

        void LateUpdate()
        {
            if (_clock == null)
                return;

            WorldTime now = _clock.Now;
            _view.SetTime(now.WholeHour, now.Minute);
            _view.SetDay(now.Day);

            DayPhase phase = _clock.Phase;
            if (((int)phase != _phase || _phaseLocale != Loc.Current) && _view.Style != null)
            {
                _phase = (int)phase;
                _phaseLocale = Loc.Current;
                _view.SetPhase(_view.Style.PhaseText(phase), _view.Style.PhaseColor(phase));
            }
        }
    }
}
