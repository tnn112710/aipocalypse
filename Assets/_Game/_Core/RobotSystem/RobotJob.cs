namespace MB13.Aipocalypse.Core.RobotSystem
{
    // A working robot's job (collecting coins, guarding…): each kind is a subclass whose asset makes the work for one robot.
    // The tree's Work branch (above home life, below every reaction) runs it while it wants to work.
    public abstract class RobotJob : RobotRole
    {
        public abstract IRobotWork CreateWork(Robot robot);
    }

    // One robot's work, ticked by the Work branch: it starts when WantsToWork turns true and runs until Tick returns false
    // or something above (a whistle, a raid, the night) takes over, which ends it.
    public interface IRobotWork
    {
        bool WantsToWork(float now);
        void Begin(float now);
        // False when this stint of work is done.
        bool Tick(float now);
        void End(float now);
    }
}
