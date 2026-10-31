namespace Aipocalypse.Core.RaidSystem
{
    // Which raid runs on a night; null = a quiet night. Quiet nights, a difficulty ladder and Blood Moons plug in here.
    public interface IRaidSchedule
    {
        RaidPlan PlanFor(int night);
    }
}
