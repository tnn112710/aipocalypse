#if __MICROVERSE_ROADS__
namespace Rowlan.Genesis.Roads
{

    /// <summary>
    /// Setup of the project. Contains constants for menu, paths and folders.
    /// </summary>
    public static class ProjectSetup
    {
        public const string MENU = "Window/Genesis for MicroVerse/Roads";

        public const string SETTINGS_DIRECTORY = Genesis.ProjectSetup.GENESIS_DATA_SETTINGS_DIRECTORY;
        public const string SETTINGS_FILENAME = "Roads Settings.asset";

        /// <summary>
        /// Shows options for development like eg logging of t position on a spline, bounds of a selection, etc
        /// </summary>
        public static bool DEVELOPER_MODE = false;
    }
}
#endif