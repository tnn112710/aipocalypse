namespace MB13.Aipocalypse.Core.RaidSystem
{
    // The same raid every night (RaidSettings.Plan), until quiet nights and a difficulty ladder replace it.
    public sealed class EveryNightSchedule : IRaidSchedule
    {
        readonly RaidSettings _settings;

        public EveryNightSchedule(RaidSettings settings) => _settings = settings;

        public RaidPlan PlanFor(int night) => _settings.Plan;
    }
}
