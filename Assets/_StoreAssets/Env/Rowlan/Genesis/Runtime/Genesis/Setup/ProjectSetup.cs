namespace Rowlan.Genesis
{
    /// <summary>
    /// Setup of the project. Contains constants for menu, paths and folders.
    /// </summary>
    public static class ProjectSetup
    {
        public const string MENU = "Window/Genesis for MicroVerse/Genesis";
        public const int MENU_ORDER = 1;

        public const string WINDOW_TITLE = "Genesis";

        public const string GENESIS_DATA_DIRECTORY = "Assets/Rowlan/Genesis Data";
        public const string GENESIS_DATA_SETTINGS_DIRECTORY = GENESIS_DATA_DIRECTORY + "/Settings";

        public const string SETTINGS_DATA_FILENAME = "Genesis Settings.asset";

        public const string TERRAIN_DATA_DIRECTORY = GENESIS_DATA_DIRECTORY +"/Terrain";


        #region Genesis Settings
        public const string GenesisSettings_FileName = "Genesis Settings";
        public const string GenesisSettings_MenuName = "Genesis for MicroVerse/Settings";
        #endregion Genesis Settings

        #region Stamp Teamplate
        public const string Stamp_TemplateCollection_FileName = "Stamp Template Collection";
        public const string Stamp_TemplateCollection_MenuName = "Genesis for MicroVerse/Templates/Stamp/Collection";

        public const string Stamp_SettingsTemplate_FileName = "Stamp Settings";
        public const string Stamp_SettingsTemplate_MenuName = "Genesis for MicroVerse/Templates/Stamp/Settings";
        #endregion Stamp Teamplate

        #region Layer Teamplate
        public const string Layer_TemplateCollection_FileName = "Layer Template Collection";
        public const string Layer_TemplateCollection_MenuName = "Genesis for MicroVerse/Templates/Layer/Collection";

        public const string Layer_SettingsTemplate_FileName = "Layer Settings";
        public const string Layer_SettingsTemplate_MenuName = "Genesis for MicroVerse/Templates/Layer/Settings";
        #endregion Layer Teamplate

        #region Path Teamplate
        public const string Path_TemplateCollection_FileName = "Path Template Collection";
        public const string Path_TemplateCollection_MenuName = "Genesis for MicroVerse/Templates/Path/Collection";

        public const string Path_SettingsTemplate_FileName = "Path Settings";
        public const string Path_SettingsTemplate_MenuName = "Genesis for MicroVerse/Templates/Path/Settings";
        #endregion Path Teamplate

        #region Tree Teamplate
        public const string Tree_TemplateCollection_FileName = "Tree Template Collection";
        public const string Tree_TemplateCollection_MenuName = "Genesis for MicroVerse/Templates/Tree/Collection";

        public const string Tree_SettingsTemplate_FileName = "Tree Settings";
        public const string Tree_SettingsTemplate_MenuName = "Genesis for MicroVerse/Templates/Tree/Settings";
        #endregion Tree Teamplate
    }
}