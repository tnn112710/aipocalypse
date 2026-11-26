using System;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using MessagePipe;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.ForecastSystem
{
    // Whether today's forecast is shown or still '?': hidden again at NewForecastHour each day, revealed by Reveal() (the
    // rope, or any later mechanic) or by itself at SelfRevealHour. The trigger is swappable: anything may call Reveal().
    // Without a WorldClock, or with HideUntilRevealed off, the forecast always shows.
    public sealed class ForecastReveal : IStartable, ITickable, IDisposable
    {
        readonly ForecastSettings _settings;
        readonly IPublisher<ForecastRevealChanged> _changed;
        readonly IDisposable _jumps;
        readonly WorldClock _clock;
        int _revealedDay = int.MinValue;
        bool _shown;

        public ForecastReveal(ForecastSettings settings, IPublisher<ForecastRevealChanged> changed, ISubscriber<TimeJumped> jumped,
            IObjectResolver resolver)
        {
            _settings = settings;
            _changed = changed;
            resolver.TryResolve(out _clock);
            // A jump (load, cheat) resyncs without the spin.
            _jumps = jumped.Subscribe(_ => Sync(false));
        }

        public bool Hides => _settings.HideUntilRevealed && _clock != null;
        public bool IsRevealed => !Hides || ForecastRules.IsRevealed(_clock.Now, _revealedDay, _settings);
        // The day whose forecast was revealed last (saved).
        public int RevealedDay => _revealedDay;

        public void Start() => _shown = IsRevealed;

        public void Tick() => Sync(true);

        public void Dispose() => _jumps.Dispose();

        public void Reveal()
        {
            if (IsRevealed)
                return;
            _revealedDay = ForecastRules.DayOf(_clock.Now, _settings.NewForecastHour);
            Sync(true);
        }

        // Back to '?' (console); stays shown past SelfRevealHour.
        public void Hide()
        {
            _revealedDay = int.MinValue;
            Sync(true);
        }

        public void Restore(int revealedDay)
        {
            _revealedDay = revealedDay;
            Sync(false);
        }

        void Sync(bool live)
        {
            bool revealed = IsRevealed;
            if (revealed == _shown)
                return;
            _shown = revealed;
            _changed.Publish(new ForecastRevealChanged(revealed, live));
        }
    }
}
