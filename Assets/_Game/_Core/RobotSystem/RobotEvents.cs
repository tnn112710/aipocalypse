namespace MB13.Aipocalypse.Core.RobotSystem
{
    // The player pressed E on a service robot: its menu's UI opens (nothing listens yet: the robot just greets).
    public readonly struct RobotServiceRequested
    {
        public readonly Robot Robot;
        public readonly RobotService Service;

        public RobotServiceRequested(Robot robot, RobotService service)
        {
            Robot = robot;
            Service = service;
        }
    }
}
