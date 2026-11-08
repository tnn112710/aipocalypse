using MB13.Aipocalypse.Core.Debugging;
using UnityEngine;

namespace MB13.Aipocalypse.Core.WorldTimeSystem
{
    // Console commands on the scene's world clock (editor and development builds); they exist only in scenes with one.
    // Speed and freeze are the console's own speed request, released on Dispose.
    [CommandGroup("clock")]
    public sealed class WorldClockCommands : ConsoleCommands
    {
        readonly WorldClock _clock;

        public WorldClockCommands(CommandRegistry registry, WorldClock clock) : base(registry) => _clock = clock;

        [Command("Day, time and phase")]
        string Now() => $"{_clock.Now} {_clock.Phase}";

        [Command("Jump to the next time the clock shows this hour (7.5 = 07:30)")]
        CommandResult Set(float hour)
        {
            if (hour < 0f || hour >= 24f)
                return CommandResult.Fail("The hour goes from 0 to 24 (not included)");
            _clock.JumpTo(hour);
            return CommandResult.Ok(Now());
        }

        [Command("Jump to the start of a phase")]
        string Phase(DayPhase phase)
        {
            _clock.JumpToPhase(phase);
            return Now();
        }

        [Command("Jump to the next whole hour (N)")]
        string NextHour()
        {
            _clock.JumpToNextHour();
            return Now();
        }

        [Command("Jump to the start of the next phase (M)")]
        string NextPhase()
        {
            _clock.JumpToNextPhase();
            return Now();
        }

        [Command("Fast-forward to an hour over some real seconds; every hour in between passes")]
        CommandResult Skip(float hour, float seconds)
        {
            if (hour < 0f || hour >= 24f)
                return CommandResult.Fail("The hour goes from 0 to 24 (not included)");
            _clock.SkipTo(hour, seconds);
            return CommandResult.Ok();
        }

        [Command("Clock speed, 0–1 (0 freezes); 1 drops the console's request")]
        float Speed
        {
            get => _clock.SpeedScale;
            set
            {
                if (value >= 1f)
                    _clock.Release(this);
                else
                    _clock.RequestSpeed(this, Mathf.Max(0f, value));
            }
        }

        [Command("Stop the clock, or let it run again")]
        string Freeze(bool on)
        {
            if (on)
                _clock.Freeze(this);
            else
                _clock.Release(this);
            return on ? "Frozen" : "Running";
        }

        public override void Dispose()
        {
            base.Dispose();
            _clock.Release(this);
        }
    }
}
