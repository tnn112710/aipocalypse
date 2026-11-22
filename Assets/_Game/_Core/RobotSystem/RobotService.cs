namespace MB13.Aipocalypse.Core.RobotSystem
{
    // A menu robot: E asks for its menu (RobotServiceRequested, which the menu's UI answers). Assets in Data/Robots/Services/.
    [UnityEngine.CreateAssetMenu(fileName = "RobotService", menuName = "Aipocalypse/Robots/Robot Service")]
    public sealed class RobotService : RobotRole
    {
        public RobotMenu Menu;
    }

    // The menus a service robot can open.
    public enum RobotMenu
    {
        Shop,
        Storage,
        Upgrades,
    }
}
