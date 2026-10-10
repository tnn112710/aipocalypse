using ImpossibleRobert.Common;

namespace TextStudio.Editor
{
    internal static class TextStudioMenuPaths
    {
        private const int MenuSeparatorPriorityGap = 11;

        public const string ToolsRoot = "Tools/Text Studio/";
        public const string SetupRoot = ToolsRoot + "Setup/";
        public const string ThreeDRoot = ToolsRoot + "3D/";
        public const string DebugRoot = ToolsRoot + "Debug/";
        public const string ThreeDDebugRoot = DebugRoot + "3D/";
        public const string DebugSamplesRoot = DebugRoot + "Samples/";
        public const string DebugIntegrationsRoot = DebugRoot + "Integrations/";
        public const string DebugPackagingRoot = DebugRoot + "Packaging/";

        public const string Welcome = ToolsRoot + "Welcome";
        public const string Examples = ToolsRoot + "Examples";
        public const string Integrations = ToolsRoot + "Integrations";
        public const string HealthCheck = SetupRoot + "Health Check";
        public const string TextGallery = ToolsRoot + "Text Gallery";
        public const string ThreeDGallery = ToolsRoot + "3D Gallery";
        public const string MechanicalDisplaysGallery = ToolsRoot + "Mechanical Gallery";
        public const string TerminalFXGallery = ToolsRoot + "Terminal Effects Gallery";
        public const string DynamicsGallery = ToolsRoot + "Dynamics Gallery";
        public const string RuntimePreparation = ToolsRoot + "Runtime Preparation";
        public const string Recipes = ToolsRoot + "Recipes";
        public const string Migration = SetupRoot + "Migration";
        public const string About = ToolsRoot + "About";
        public const string ValidateBasePackage = DebugPackagingRoot + "Validate Base Package";
        public const string ValidateThreeDPackage = DebugPackagingRoot + "Validate 3D Package";
        public const string ValidateMechanicalDisplaysPackage = DebugPackagingRoot + "Validate Mechanical Displays Package";
        public const string ValidateTerminalFXPackage = DebugPackagingRoot + "Validate Terminal FX Package";
        public const string ValidateAllPackages = DebugPackagingRoot + "Validate All Text Studio Packages";
        public const string BuildWebGLSmokePlayer = DebugPackagingRoot + "Build WebGL Smoke Player";
        public const string PerformanceLab = DebugRoot + "Open Performance Lab";
        public const string ThreeDRenderStudio = ThreeDRoot + "Render Studio";
        public const string ThreeDProjectTheme = ThreeDRoot + "Project Theme";
        public const string ThreeDBuildPreparation = ThreeDRoot + "Build Preparation";
        public const string ThreeDAdvancedGeometryPreparation = ThreeDRoot + "Advanced Geometry Preparation";
        public const string ThreeDAuthoringGlossary = ThreeDRoot + "Authoring Glossary";
        public const string ThreeDRunPerformanceBenchmarks = ThreeDDebugRoot + "Run Performance Benchmarks";
        public const string ThreeDManageFrozenData = ThreeDDebugRoot + "Manage Frozen Data";

        public const string MechanicalDisplaysRebuildPremiumSamples = DebugSamplesRoot + "Mechanical Displays/Rebuild Premium Samples";

        public const int TextGalleryPriority = WetzoldToolMenu.TextStudioRootPriority;
        public const int ThreeDGalleryPriority = TextGalleryPriority + 1;
        public const int MechanicalDisplaysGalleryPriority = TextGalleryPriority + 2;
        public const int TerminalFXGalleryPriority = TextGalleryPriority + 3;
        public const int DynamicsGalleryPriority = TextGalleryPriority + 4;
        public const int RecipesPriority = DynamicsGalleryPriority + MenuSeparatorPriorityGap;
        public const int ExamplesPriority = RecipesPriority + 1;
        public const int IntegrationsPriority = RecipesPriority + 2;
        public const int RuntimePreparationPriority = RecipesPriority + 3;
        public const int HealthCheckPriority = RecipesPriority + 4;
        public const int MigrationPriority = RecipesPriority + 5;
        public const int ThreeDPriority = RecipesPriority + 6;
        public const int ThreeDRenderStudioPriority = ThreeDPriority;
        public const int ThreeDProjectThemePriority = ThreeDPriority + 1;
        public const int ThreeDBuildPreparationPriority = ThreeDPriority + 2;
        public const int ThreeDAdvancedGeometryPreparationPriority = ThreeDPriority + 3;
        public const int ThreeDAuthoringGlossaryPriority = ThreeDPriority + 4;
        public const int WelcomePriority = WetzoldToolMenu.WelcomePriority;
        public const int AboutPriority = WetzoldToolMenu.AboutPriority;
        public const int DebugPriority = WetzoldToolMenu.DebugPriority;
        public const int DebugPerformancePriority = DebugPriority;
        public const int DebugPackagingPriority = DebugPriority + 10;
        public const int DebugSamplesPriority = DebugPriority + 11;
        public const int DebugIntegrationsPriority = DebugPriority + 12;
        public const int DebugAddOnPriority = DebugPriority + 13;
        public const int ThreeDManageFrozenDataPriority = DebugAddOnPriority + 1;
    }
}
