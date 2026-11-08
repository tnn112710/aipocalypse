using MB13.Aipocalypse.Input;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.WorldTimeSystem
{
    // Dev builds only: N jumps to the next whole hour, M to the start of the next phase (Debug action map).
    public sealed class WorldClockCheats : ITickable
    {
        readonly WorldClock _clock;
        readonly InputService _input;

        public WorldClockCheats(WorldClock clock, InputService input)
        {
            _clock = clock;
            _input = input;
        }

        public void Tick()
        {
            if (_input.DebugNextHourPressed)
                _clock.JumpToNextHour();
            if (_input.DebugNextPhasePressed)
                _clock.JumpToNextPhase();
        }
    }
}
