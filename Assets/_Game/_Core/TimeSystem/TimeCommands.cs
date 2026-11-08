using MB13.Aipocalypse.Core.Debugging;

namespace MB13.Aipocalypse.Core.TimeSystem
{
    // Console commands on game time (editor and development builds), as owner-keyed TimeService requests released on Dispose.
    [CommandGroup("time")]
    public sealed class TimeCommands : ConsoleCommands
    {
        readonly TimeService _time;
        bool _paused;

        public TimeCommands(CommandRegistry registry, TimeService time) : base(registry) => _time = time;

        [Command("Game speed, 0–1 (slow motion); 1 drops the console's request")]
        float Scale
        {
            get => _time.Scale;
            set
            {
                if (value >= 1f)
                    _time.Release(this);
                else
                    _time.Request(this, value);
            }
        }

        [Command("Pause the game, or resume it", Name = "pause")]
        string Pause()
        {
            _paused = !_paused;
            if (_paused)
                _time.RequestPause(this);
            else
                _time.ReleasePause(this);
            return _paused ? "Paused (pause again to resume)" : "Resumed";
        }

        public override void Dispose()
        {
            base.Dispose();
            _time.Release(this);
            _time.ReleasePause(this);
        }
    }
}
