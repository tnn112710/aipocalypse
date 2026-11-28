using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using UnityEngine;
using UnityEngine.Localization;
using VContainer;

namespace MB13.Aipocalypse.Core.UI
{
    // Shows the world clock. Polled: at 30 s per game hour the minute changes every half second, and the view skips repeats.
    // Hidden in scenes without a clock. At low sanity it shows a wrong time now and then (SanityGlitch).
    public sealed class ClockHud : MonoBehaviour
    {
        [SerializeField] ClockView _view;

        WorldClock _clock;
        PlayerRegistry _players;
        int _phase = -1;
        Locale _phaseLocale;

        [Inject]
        void Construct(IObjectResolver resolver, PlayerRegistry players)
        {
            resolver.TryResolve(out _clock);
            _players = players;
        }

        void Start() => _view.Show(_clock != null);

        void LateUpdate()
        {
            if (_clock == null)
                return;

            WorldTime now = _clock.Now;
            if (SanityGlitch.TryRead(_players?.Current, out float sanity, out SanityEffectSettings effects)
                && SanityGlitch.ClockLies(sanity, Time.time, effects.LiesFrom, out int hour, out int minute))
                _view.SetTime(hour, minute);
            else
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
