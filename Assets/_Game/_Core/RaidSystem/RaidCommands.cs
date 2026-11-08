using MB13.Aipocalypse.Core.Debugging;

namespace MB13.Aipocalypse.Core.RaidSystem
{
    // Console commands on the scene's raids (editor and development builds); they exist only in scenes with a RaidInstaller.
    [CommandGroup("raid")]
    public sealed class RaidCommands : ConsoleCommands
    {
        readonly RaidDirector _director;

        public RaidCommands(CommandRegistry registry, RaidDirector director) : base(registry) => _director = director;

        [Command("Start tonight's raid now, even by day; it runs until cleared, stopped or the player dies")]
        CommandResult Start()
        {
            if (_director.IsRunning)
                return CommandResult.Fail("A raid is running: raid.stop first");
            _director.StartNow();
            return _director.IsRunning ? CommandResult.Ok(Status()) : CommandResult.Fail("Tonight is a quiet night (no raid plan)");
        }

        [Command("End the raid and despawn its raiders")]
        CommandResult Stop()
        {
            if (!_director.IsRunning)
                return CommandResult.Fail("No raid is running");
            _director.Stop();
            return CommandResult.Ok(Status());
        }

        [Command("Start the next wave now (the rest of this one is skipped)")]
        CommandResult Next()
        {
            if (!_director.IsRunning)
                return CommandResult.Fail("No raid is running");
            int wave = _director.Wave;
            _director.NextWave();
            return _director.Wave != wave ? CommandResult.Ok(Status()) : CommandResult.Fail("This is the last wave");
        }

        [Command("Night, wave, raiders spawned and alive, and whether the raid runs or how it ended")]
        string Status()
        {
            RaidPlan plan = _director.Plan;
            if (plan == null)
                return "No raid yet";
            string state = _director.IsRunning ? "running" : $"ended: {_director.LastEnd}";
            return $"Night {_director.Night}: wave {_director.Wave + 1}/{plan.Waves.Count}, spawned {_director.Spawned}/{plan.TotalEnemies}, " +
                $"alive {_director.Alive}, {state}";
        }
    }
}
