using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ImpossibleRobert.Common;
using ImpossibleRobert.Common.Editor.Packages;
using UnityEditor;
using UnityEngine;
using PackageManagerInfo = UnityEditor.PackageManager.PackageInfo;

namespace TextStudio.Editor
{
    public enum TextStudioIntegrationSupportLevel
    {
        NativeAdapter,
        AutomaticCompatibility,
        CuratedCommands,
        SampleRecipe
    }

    public enum TextStudioIntegrationValidationProvenance
    {
        ExactVersionValidated,
        SourceVerified,
        CompatibilityRecipe
    }

    public enum TextStudioIntegrationIssueSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class TextStudioIntegrationDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string CompanionDisplayName { get; }
        public string IntegrationDisplayName { get; }
        public string Category { get; }
        public string Description { get; }
        public string Ownership { get; }
        public TextStudioIntegrationSupportLevel SupportLevel { get; }
        public TextStudioIntegrationValidationProvenance Provenance { get; }
        public string IntegrationAssemblyName { get; }
        public string IntegrationAssetSearch { get; }
        public string VendorAssemblyName { get; }
        public string VendorPackageName { get; }
        public string VendorAssetSearch { get; }
        public string CompanionAssemblyName => VendorAssemblyName;
        public string CompanionPackageName => VendorPackageName;
        public string CompanionAssetSearch => VendorAssetSearch;
        public string RequiredIntegrationId { get; }
        public string NestedPackageFileName { get; }
        public string NestedPackageOwnerPackageName { get; }
        public string SampleAssetName { get; }
        public string SetupGuideGuid { get; }
        public string QuickStart { get; }
        public string DocumentationUrl { get; }

        public TextStudioIntegrationDefinition(
            string id,
            string displayName,
            string category,
            string description,
            string ownership,
            TextStudioIntegrationSupportLevel supportLevel,
            TextStudioIntegrationValidationProvenance provenance,
            string integrationAssemblyName,
            string integrationAssetSearch,
            string vendorAssemblyName,
            string vendorPackageName,
            string vendorAssetSearch,
            string nestedPackageFileName,
            string sampleAssetName,
            string documentationUrl)
            : this(
                id,
                displayName,
                category,
                description,
                ownership,
                supportLevel,
                provenance,
                integrationAssemblyName,
                integrationAssetSearch,
                vendorAssemblyName,
                vendorPackageName,
                vendorAssetSearch,
                nestedPackageFileName,
                sampleAssetName,
                string.Empty,
                string.Empty,
                string.Empty,
                documentationUrl)
        {
        }

        public TextStudioIntegrationDefinition(
            string id,
            string displayName,
            string category,
            string description,
            string ownership,
            TextStudioIntegrationSupportLevel supportLevel,
            TextStudioIntegrationValidationProvenance provenance,
            string integrationAssemblyName,
            string integrationAssetSearch,
            string vendorAssemblyName,
            string vendorPackageName,
            string vendorAssetSearch,
            string nestedPackageFileName,
            string sampleAssetName,
            string setupGuideGuid,
            string quickStart,
            string requiredIntegrationId,
            string documentationUrl)
            : this(
                id,
                displayName,
                displayName,
                "Text Studio integration for " + displayName,
                category,
                description,
                ownership,
                supportLevel,
                provenance,
                integrationAssemblyName,
                integrationAssetSearch,
                vendorAssemblyName,
                vendorPackageName,
                vendorAssetSearch,
                nestedPackageFileName,
                TextStudioPackagePaths.BasePackageName,
                sampleAssetName,
                setupGuideGuid,
                quickStart,
                requiredIntegrationId,
                documentationUrl)
        {
        }

        public TextStudioIntegrationDefinition(
            string id,
            string displayName,
            string companionDisplayName,
            string integrationDisplayName,
            string category,
            string description,
            string ownership,
            TextStudioIntegrationSupportLevel supportLevel,
            TextStudioIntegrationValidationProvenance provenance,
            string integrationAssemblyName,
            string integrationAssetSearch,
            string vendorAssemblyName,
            string vendorPackageName,
            string vendorAssetSearch,
            string nestedPackageFileName,
            string nestedPackageOwnerPackageName,
            string sampleAssetName,
            string setupGuideGuid,
            string quickStart,
            string requiredIntegrationId,
            string documentationUrl)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            CompanionDisplayName = companionDisplayName ?? string.Empty;
            IntegrationDisplayName = integrationDisplayName ?? string.Empty;
            Category = category ?? string.Empty;
            Description = description ?? string.Empty;
            Ownership = ownership ?? string.Empty;
            SupportLevel = supportLevel;
            Provenance = provenance;
            IntegrationAssemblyName = integrationAssemblyName ?? string.Empty;
            IntegrationAssetSearch = integrationAssetSearch ?? string.Empty;
            VendorAssemblyName = vendorAssemblyName ?? string.Empty;
            VendorPackageName = vendorPackageName ?? string.Empty;
            VendorAssetSearch = vendorAssetSearch ?? string.Empty;
            RequiredIntegrationId = requiredIntegrationId ?? string.Empty;
            NestedPackageFileName = nestedPackageFileName ?? string.Empty;
            NestedPackageOwnerPackageName = nestedPackageOwnerPackageName ?? string.Empty;
            SampleAssetName = sampleAssetName ?? string.Empty;
            SetupGuideGuid = setupGuideGuid ?? string.Empty;
            QuickStart = quickStart ?? string.Empty;
            DocumentationUrl = documentationUrl ?? string.Empty;
        }
    }

    public sealed class TextStudioIntegrationIssue
    {
        public TextStudioIntegrationIssueSeverity Severity { get; }
        public string Title { get; }
        public string Detail { get; }
        public string Action { get; }
        public bool BlocksPackageImport { get; }

        public TextStudioIntegrationIssue(
            TextStudioIntegrationIssueSeverity severity,
            string title,
            string detail,
            string action = "",
            bool blocksPackageImport = false)
        {
            Severity = severity;
            Title = title ?? string.Empty;
            Detail = detail ?? string.Empty;
            Action = action ?? string.Empty;
            BlocksPackageImport = blocksPackageImport;
        }
    }

    public sealed class TextStudioIntegrationValidationContext
    {
        public TextStudioIntegrationDefinition Definition { get; }
        public GameObject SelectedGameObject { get; }
        public UnityEngine.Object SelectedObject { get; }

        public TextStudioIntegrationValidationContext(
            TextStudioIntegrationDefinition definition,
            GameObject selectedGameObject)
            : this(definition, selectedGameObject, selectedGameObject)
        {
        }

        public TextStudioIntegrationValidationContext(
            TextStudioIntegrationDefinition definition,
            GameObject selectedGameObject,
            UnityEngine.Object selectedObject)
        {
            Definition = definition;
            SelectedGameObject = selectedGameObject;
            SelectedObject = selectedObject != null ? selectedObject : selectedGameObject;
        }
    }

    public interface ITextStudioIntegrationEditorProvider
    {
        string IntegrationId { get; }
        bool CanConfigure(TextStudioIntegrationValidationContext context);
        void Configure(TextStudioIntegrationValidationContext context);
        void Validate(
            TextStudioIntegrationValidationContext context,
            ICollection<TextStudioIntegrationIssue> issues);
    }

    public interface ITextStudioIntegrationCatalogContributor
    {
        void AddDefinitions(ICollection<TextStudioIntegrationDefinition> definitions);
    }

    internal sealed class TextStudioIntegrationState
    {
        readonly List<TextStudioIntegrationIssue> _issues = new List<TextStudioIntegrationIssue>();

        public TextStudioIntegrationDefinition Definition { get; }
        public bool VendorInstalled { get; internal set; }
        public string VendorVersion { get; internal set; }
        public bool CompanionInstalled
        {
            get => VendorInstalled;
            internal set => VendorInstalled = value;
        }
        public string CompanionVersion
        {
            get => VendorVersion;
            internal set => VendorVersion = value;
        }
        public bool IntegrationInstalled { get; internal set; }
        public bool IntegrationLoaded { get; internal set; }
        public bool DependencySatisfied { get; internal set; }
        public string NestedPackageAssetPath { get; internal set; }
        public TextStudioPackageSnapshot Package { get; internal set; }
        public string SampleAssetPath { get; internal set; }
        public string SetupGuideAssetPath { get; internal set; }
        public ITextStudioIntegrationEditorProvider Provider { get; internal set; }
        public IReadOnlyList<TextStudioIntegrationIssue> Issues => _issues;

        public bool IsAvailable =>
            CompanionInstalled &&
            IntegrationInstalled &&
            IntegrationLoaded &&
            DependencySatisfied;

        public bool IsReady =>
            IsAvailable &&
            !HasWarnings &&
            !HasErrors &&
            !HasPackageImportBlocker;

        public bool HasWarnings
        {
            get
            {
                for (int i = 0; i < _issues.Count; i++)
                {
                    if (_issues[i].Severity == TextStudioIntegrationIssueSeverity.Warning)
                        return true;
                }

                return false;
            }
        }

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < _issues.Count; i++)
                {
                    if (_issues[i].Severity == TextStudioIntegrationIssueSeverity.Error)
                        return true;
                }

                return false;
            }
        }

        public TextStudioIntegrationIssue CompatibilityNotice
        {
            get
            {
                foreach (TextStudioIntegrationIssue issue in _issues)
                {
                    if (issue.Severity == TextStudioIntegrationIssueSeverity.Info && issue.BlocksPackageImport)
                        return issue;
                }
                return null;
            }
        }

        public bool HasPackageImportBlocker
        {
            get
            {
                for (int i = 0; i < _issues.Count; i++)
                {
                    if (_issues[i].BlocksPackageImport)
                        return true;
                }

                return false;
            }
        }

        internal TextStudioIntegrationState(TextStudioIntegrationDefinition definition)
        {
            Definition = definition;
            VendorVersion = string.Empty;
            DependencySatisfied = true;
            NestedPackageAssetPath = string.Empty;
            SampleAssetPath = string.Empty;
            SetupGuideAssetPath = string.Empty;
        }

        internal void AddIssue(TextStudioIntegrationIssue issue)
        {
            if (issue != null)
                _issues.Add(issue);
        }
    }

#if UNITY_6000_7_OR_NEWER
    // Static-constructor state is code-load scoped; cleanup is handled explicitly where the type crosses Play Mode.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioIntegrationCatalog
    {
        const string BaseIntegrationGuideUrl =
            "https://www.wetzold.com/tools/text-studio/docs/connect-other-tools/";
        const string ThreeDIntegrationGuideUrl =
            "https://www.wetzold.com/tools/text-studio/3d/docs/learn-from-the-shipped-samples/";

        static readonly Dictionary<string, bool> AssetSearchCache =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        static readonly Dictionary<string, bool> AssemblyDefinitionCache =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        static readonly Dictionary<string, string> SampleAssetCache =
            new Dictionary<string, string>(StringComparer.Ordinal);
        static readonly Dictionary<string, string> SetupGuideAssetCache =
            new Dictionary<string, string>(StringComparer.Ordinal);
        static IReadOnlyList<TextStudioIntegrationDefinition> _definitions;
        static Dictionary<string, ITextStudioIntegrationEditorProvider> _providers;

        static readonly TextStudioIntegrationDefinition[] BuiltInDefinitions =
        {
            Define(
                "adventure-creator", "Adventure Creator", "Dialogue",
                "Dialogue presentation with either Text Studio reveal ownership or Adventure Creator scroll synchronization.",
                "Adventure Creator owns dialogue flow. Text Studio owns effects and visual completion.",
                TextStudioIntegrationSupportLevel.NativeAdapter,
                TextStudioIntegrationValidationProvenance.ExactVersionValidated,
                "TextStudio.Integration.AdventureCreator", "AC", "", "KickStarter t:Script",
                "TextStudio_AdventureCreator_Integration.unitypackage",
                "Adventure Creator Dialogue"),
            Define(
                "game-creator-2", "Game Creator 2", "Dialogue",
                "Native visual-scripting commands plus optional Dialogue module presentation.",
                "Game Creator owns story flow and variables. Text Studio owns reveal and effects.",
                TextStudioIntegrationSupportLevel.CuratedCommands,
                TextStudioIntegrationValidationProvenance.ExactVersionValidated,
                "TextStudio.Integration.GameCreator2", "GameCreator.Runtime.Core", "", "GameCreator.Runtime.Core t:AssemblyDefinitionAsset",
                "TextStudio_GameCreator2_Integration.unitypackage",
                "Game Creator Actions"),
            Define(
                "game-creator-2-dialogue", "Game Creator 2 Dialogue", "Dialogue",
                "Speech-skin adapter that preserves actors, portraits, choices, and Game Creator story timing.",
                "Game Creator owns dialogue timing and UI context. Text Studio owns visible reveal and effects.",
                TextStudioIntegrationSupportLevel.NativeAdapter,
                TextStudioIntegrationValidationProvenance.ExactVersionValidated,
                "TextStudio.Integration.GameCreator2.Dialogue", "GameCreator.Runtime.Dialogue", "", "GameCreator.Runtime.Dialogue t:AssemblyDefinitionAsset",
                "TextStudio_GameCreator2_Dialogue_Integration.unitypackage",
                ""),
            Define(
                "ink", "Ink", "Dialogue",
                "Ink story, tags, choices, and Text Studio line presentation.",
                "Ink owns story state. Text Studio owns visible text, reveal, and effects.",
                TextStudioIntegrationSupportLevel.NativeAdapter,
                TextStudioIntegrationValidationProvenance.ExactVersionValidated,
                "TextStudio.Integration.Ink", "Ink-Libraries", "com.inkle.ink-unity-integration", "InkLibrary t:Script",
                "TextStudio_Ink_Integration.unitypackage",
                "Ink Choices"),
            Define(
                "naninovel", "Naninovel", "Dialogue",
                "Naninovel printer integration through its revealable-text contract.",
                "Naninovel owns script and printer flow. Text Studio owns visible reveal and effects.",
                TextStudioIntegrationSupportLevel.NativeAdapter,
                TextStudioIntegrationValidationProvenance.SourceVerified,
                "TextStudio.Integration.Naninovel", "Elringus.Naninovel.Runtime", "com.elringus.naninovel", "Naninovel t:Script",
                "TextStudio_Naninovel_Integration.unitypackage",
                "Naninovel Dialogue"),
            Define(
                "pixel-crushers", "Pixel Crushers Dialogue System", "Dialogue",
                "Subtitle panel integration with skip-first continuation behavior.",
                "Dialogue System owns conversation flow. Text Studio owns subtitle reveal and effects.",
                TextStudioIntegrationSupportLevel.NativeAdapter,
                TextStudioIntegrationValidationProvenance.ExactVersionValidated,
                "", "", "", "DialogueSystemController t:Script",
                "TextStudio_PixelCrushers_Integration.unitypackage",
                "Dialogue System"),
            Define(
                "yarn-spinner", "Yarn Spinner", "Dialogue",
                "Yarn presenter/typewriter integration with action and replacement markup support.",
                "Yarn owns localized dialogue flow. Text Studio owns reveal and visual markup.",
                TextStudioIntegrationSupportLevel.NativeAdapter,
                TextStudioIntegrationValidationProvenance.SourceVerified,
                "TextStudio.Integration.YarnSpinner", "YarnSpinner.Unity", "dev.yarnspinner.unity", "DialogueRunner t:Script",
                "TextStudio_YarnSpinner_Integration.unitypackage",
                "Forest Gate"),
            Define(
                "playmaker-2", "PlayMaker 2", "Visual Scripting",
                "Curated actions, variables, waits, conditions, and lifecycle events.",
                "PlayMaker owns state-machine flow. Text Studio owns text state and rendering.",
                TextStudioIntegrationSupportLevel.CuratedCommands,
                TextStudioIntegrationValidationProvenance.ExactVersionValidated,
                "TextStudio.Integration.PlayMaker2", "HutongGames.PlayMaker", "com.hutonggames.playmaker", "PlayMakerFSM t:Script",
                "TextStudio_PlayMaker2_Integration.unitypackage",
                "PlayMaker Dialogue"),
            Define(
                "visual-scripting", "Unity Visual Scripting", "Visual Scripting",
                "Curated units and events for Text Studio control and observation.",
                "Visual Scripting owns graph flow. Text Studio owns text state and rendering.",
                TextStudioIntegrationSupportLevel.CuratedCommands,
                TextStudioIntegrationValidationProvenance.ExactVersionValidated,
                "TextStudio.Integration.UnityVisualScripting", "Unity.VisualScripting.Flow", "com.unity.visualscripting", "ScriptMachine t:Script",
                "TextStudio_UnityVisualScripting_Integration.unitypackage",
                "Visual Scripting"),
            Define(
                "led-studio", "LED Studio", "Rendering",
                "Text Studio glyph output rendered through LED Studio displays.",
                "Text Studio owns text and effects. LED Studio owns final LED rendering.",
                TextStudioIntegrationSupportLevel.NativeAdapter,
                TextStudioIntegrationValidationProvenance.ExactVersionValidated,
                "TextStudio.Integration.LEDStudio", "LEDStudio", "", "LEDStudio t:AssemblyDefinitionAsset",
                "TextStudio_LEDStudio_Integration.unitypackage",
                "Animated LED Boards"),
            Define(
                "ui-effect", "UIEffect", "Rendering",
                "Automatic UIEffect mesh synchronization for Text Studio UGUI output.",
                "Text Studio owns the animated mesh. UIEffect owns post-processing.",
                TextStudioIntegrationSupportLevel.AutomaticCompatibility,
                TextStudioIntegrationValidationProvenance.ExactVersionValidated,
                "TextStudio.Integration.UIEffect", "Coffee.UIEffect", "com.coffee.ui-effect", "UIEffect t:Script",
                "TextStudio_UIEffect_Integration.unitypackage",
                "UI Effects"),
            Define(
                "unitext", "UniText", "Rendering",
                "One Text Studio backend for open-source UniText 1.x and UniText Folio 3.x.",
                "UniText owns shaping and glyph geometry. Text Studio owns source text and animated presentation.",
                TextStudioIntegrationSupportLevel.NativeAdapter,
                TextStudioIntegrationValidationProvenance.ExactVersionValidated,
                "TextStudio.Integration.UniText", "LightSide.UniText", "media.lightside.unitext", "UniText t:AssemblyDefinitionAsset",
                "TextStudio_UniText_Integration.unitypackage",
                ""),
            Define(
                "unitext-free-sample", "UniText Open Source Sample", "Rendering",
                "Persisted Canvas showcase for Text Studio with open-source UniText 1.x.",
                "UniText owns shaping and Canvas geometry. Text Studio owns source text and animation.",
                TextStudioIntegrationSupportLevel.SampleRecipe,
                TextStudioIntegrationValidationProvenance.SourceVerified,
                "", "LightSide.UniText", "media.lightside.unitext", "UniText t:AssemblyDefinitionAsset",
                "TextStudio_UniText_OpenSource_Sample.unitypackage",
                "Multilingual Lettering"),
            Define(
                "unitext-folio-sample", "UniText Folio Sample", "Rendering",
                "Native lettering plus Document Reader and Interactive Journal examples. Reading scenes require UniText Folio 3.10 or newer.",
                "UniText Folio owns shaping and Canvas geometry. Text Studio owns source text and animation.",
                TextStudioIntegrationSupportLevel.SampleRecipe,
                TextStudioIntegrationValidationProvenance.ExactVersionValidated,
                "", "LightSide.UniText", "media.lightside.unitext", "UniText t:AssemblyDefinitionAsset",
                "TextStudio_UniText_Folio_Sample.unitypackage",
                "Multilingual Lettering"),
            Define(
                "unity-localization", "Unity Localization", "Content",
                "LocalizedString binding with Text Studio markup and runtime locale switching.",
                "Localization owns content resolution. Text Studio owns markup, reveal, and rendering.",
                TextStudioIntegrationSupportLevel.NativeAdapter,
                TextStudioIntegrationValidationProvenance.ExactVersionValidated,
                "TextStudio.Integration.UnityLocalization", "Unity.Localization", "com.unity.localization", "LocalizedString t:Script",
                "TextStudio_UnityLocalization_Integration.unitypackage",
                "Localized Captions"),
            Define(
                "unity-splines", "Unity Splines", "Layout",
                "Spline-based placement for animated Text Studio glyphs.",
                "Splines owns path geometry. Text Studio owns glyph placement and effects.",
                TextStudioIntegrationSupportLevel.NativeAdapter,
                TextStudioIntegrationValidationProvenance.ExactVersionValidated,
                "TextStudio.Splines", "Unity.Splines", "com.unity.splines", "SplineContainer t:Script",
                "TextStudio_UnitySplines_Integration.unitypackage",
                "Spline Wayfinding"),
            Define(
                "prime-tween", "PrimeTween", "Animation",
                "Composition recipes for tweening Text Studio public properties and commands.",
                "PrimeTween owns orchestration. Text Studio owns text animation and rendering.",
                TextStudioIntegrationSupportLevel.SampleRecipe,
                TextStudioIntegrationValidationProvenance.ExactVersionValidated,
                "TextStudio.Samples.PrimeTween", "PrimeTween.Runtime", "com.kyrylokuzyk.primetween", "PrimeTweenConfig t:ScriptableObject",
                "TextStudio_PrimeTween_Integration.unitypackage",
                "Notification Stack"),
            Define(
                "input-system", "Unity Input System", "Unity Packages",
                "Mapped wait and advance actions with safe action enable/restore ownership.",
                "Input System owns device input. Text Studio owns wait and reveal state.",
                TextStudioIntegrationSupportLevel.NativeAdapter,
                TextStudioIntegrationValidationProvenance.ExactVersionValidated,
                "TextStudio.InputSystem", "Unity.InputSystem", "com.unity.inputsystem", "InputActionAsset t:ScriptableObject",
                "", ""),
            Define(
                "timeline", "Unity Timeline", "Unity Packages",
                "Deterministic reveal/effect tracks and one-shot Text Studio control markers.",
                "Timeline owns sequencing. Text Studio owns text state, effects, and rendering.",
                TextStudioIntegrationSupportLevel.NativeAdapter,
                TextStudioIntegrationValidationProvenance.ExactVersionValidated,
                "TextStudio.Timeline", "Unity.Timeline", "com.unity.timeline", "TimelineAsset t:ScriptableObject",
                "TextStudio_UnityTimeline_Integration.unitypackage",
                "Relay Ridge"),
            Define(
                "3d-input-system", "Unity Input System for Text Studio 3D", "Text Studio 3D",
                "Connects Unity Input System actions and keyboard text to Text Studio 3D spatial controls.",
                "Unity Input System owns device events and action bindings. Text Studio 3D owns editing, interaction state, and geometry.",
                TextStudioIntegrationSupportLevel.NativeAdapter,
                TextStudioIntegrationValidationProvenance.ExactVersionValidated,
                "TextStudio.3D.Integration.InputSystem", "Unity.InputSystem", "com.unity.inputsystem", "InputActionAsset t:ScriptableObject",
                "TextStudio3D_InputSystem_Integration.unitypackage",
                "Spatial Controls"),
            Define(
                "3d-unitext-free-sample", "UniText Open Source for Text Studio 3D", "Text Studio 3D",
                "Persisted 3D shaping and extrusion showcase for open-source UniText 1.x.",
                "UniText owns shaping. Text Studio 3D owns geometry and rendering.",
                TextStudioIntegrationSupportLevel.SampleRecipe,
                TextStudioIntegrationValidationProvenance.SourceVerified,
                "", "LightSide.UniText", "media.lightside.unitext", "UniText t:AssemblyDefinitionAsset",
                "TextStudio3D_UniText_OpenSource_Sample.unitypackage",
                "Solid Shaped Text"),
            Define(
                "3d-unitext-folio-sample", "UniText Folio for Text Studio 3D", "Text Studio 3D",
                "Extended persisted 3D shaping and extrusion showcase for UniText Folio 3.x.",
                "UniText Folio owns shaping. Text Studio 3D owns geometry and rendering.",
                TextStudioIntegrationSupportLevel.SampleRecipe,
                TextStudioIntegrationValidationProvenance.ExactVersionValidated,
                "", "LightSide.UniText", "media.lightside.unitext", "UniText t:AssemblyDefinitionAsset",
                "TextStudio3D_UniText_Folio_Sample.unitypackage",
                "Folio Material Bench"),
            Define(
                "3d-unity-splines", "Unity Splines for Text Studio 3D", "Text Studio 3D",
                "Spline-driven extrusion and layout recipes for Text Studio 3D.",
                "Splines owns paths. Text Studio 3D owns generated geometry.",
                TextStudioIntegrationSupportLevel.SampleRecipe,
                TextStudioIntegrationValidationProvenance.ExactVersionValidated,
                "TextStudio.3D.Samples.UnitySplines", "Unity.Splines", "com.unity.splines", "SplineContainer t:Script",
                "TextStudio3D_UnitySplines_Integration.unitypackage",
                "Spline Extrusion"),
            Define(
                "3d-xr-interaction-toolkit", "XR Interaction Toolkit for Text Studio 3D", "Text Studio 3D",
                "Native typed ray, Near-Far, direct, poke, gaze, drag, cancellation, and haptic interaction.",
                "XRI owns interactors and device input. Text Studio 3D owns controls, feedback, menus, and geometry.",
                TextStudioIntegrationSupportLevel.NativeAdapter,
                TextStudioIntegrationValidationProvenance.SourceVerified,
                "TextStudio.3D.Integration.XRInteractionToolkit", "Unity.XR.Interaction.Toolkit", "com.unity.xr.interaction.toolkit", "XRBaseInteractable t:Script",
                "TextStudio3D_XRInteractionToolkit_Integration.unitypackage",
                ""),
        };

        static TextStudioIntegrationCatalog()
        {
            EditorApplication.projectChanged += ClearAssetCaches;
        }

        internal static IReadOnlyList<TextStudioIntegrationDefinition> GetDefinitions()
        {
            if (_definitions != null)
                return _definitions;

            List<TextStudioIntegrationDefinition> definitions =
                new List<TextStudioIntegrationDefinition>(BuiltInDefinitions);
            TypeCache.TypeCollection contributorTypes =
                TypeCache.GetTypesDerivedFrom<ITextStudioIntegrationCatalogContributor>();

            for (int i = 0; i < contributorTypes.Count; i++)
            {
                Type contributorType = contributorTypes[i];
                if (contributorType.IsAbstract || contributorType.IsInterface)
                    continue;

                try
                {
                    ITextStudioIntegrationCatalogContributor contributor =
                        Activator.CreateInstance(contributorType) as ITextStudioIntegrationCatalogContributor;
                    contributor?.AddDefinitions(definitions);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            List<TextStudioIntegrationDefinition> uniqueDefinitions =
                new List<TextStudioIntegrationDefinition>(definitions.Count);
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definitions.Count; i++)
            {
                TextStudioIntegrationDefinition definition = definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    Debug.LogError("[Text Studio] An integration catalog contributor supplied an entry without an ID.");
                    continue;
                }

                if (!ids.Add(definition.Id))
                {
                    Debug.LogError(
                        "[Text Studio] Duplicate integration catalog ID '" +
                        definition.Id + "' was ignored.");
                    continue;
                }

                uniqueDefinitions.Add(definition);
            }

            _definitions = uniqueDefinitions;
            return _definitions;
        }

        internal static IReadOnlyList<TextStudioIntegrationState> Evaluate(
            GameObject selectedGameObject = null,
            UnityEngine.Object selectedObject = null)
        {
            IReadOnlyList<TextStudioIntegrationDefinition> definitions = GetDefinitions();
            Dictionary<string, ITextStudioIntegrationEditorProvider> providers = FindProviders();
            List<TextStudioIntegrationState> states =
                new List<TextStudioIntegrationState>(definitions.Count);
            Dictionary<string, TextStudioIntegrationState> statesById =
                new Dictionary<string, TextStudioIntegrationState>(StringComparer.Ordinal);

            for (int i = 0; i < definitions.Count; i++)
            {
                TextStudioIntegrationDefinition definition = definitions[i];
                TextStudioIntegrationState state = Evaluate(definition);
                states.Add(state);
                statesById.Add(definition.Id, state);
            }

            for (int i = 0; i < states.Count; i++)
            {
                TextStudioIntegrationState state = states[i];
                TextStudioIntegrationDefinition definition = state.Definition;
                if (!string.IsNullOrEmpty(definition.RequiredIntegrationId))
                {
                    state.DependencySatisfied =
                        statesById.TryGetValue(
                            definition.RequiredIntegrationId,
                            out TextStudioIntegrationState requiredState) &&
                        requiredState.IsAvailable;
                    if (state.CompanionInstalled && !state.DependencySatisfied)
                    {
                        state.AddIssue(new TextStudioIntegrationIssue(
                            TextStudioIntegrationIssueSeverity.Warning,
                            "Required integration is not ready",
                            definition.DisplayName + " requires the " +
                            GetIntegrationDisplayName(statesById, definition.RequiredIntegrationId) +
                            " integration first.",
                            "Import and activate the required integration, then retry."));
                    }
                }

                if (state.CompanionInstalled &&
                    state.IntegrationInstalled &&
                    state.DependencySatisfied &&
                    !state.IntegrationLoaded &&
                    !string.IsNullOrEmpty(definition.IntegrationAssemblyName))
                {
                    state.AddIssue(new TextStudioIntegrationIssue(
                        TextStudioIntegrationIssueSeverity.Error,
                        "Integration assembly is not loaded",
                        definition.IntegrationAssemblyName + " is present but did not compile.",
                        "Check the Console for missing references or compiler errors."));
                }
            }

            for (int i = 0; i < states.Count; i++)
            {
                TextStudioIntegrationState state = states[i];
                TextStudioIntegrationDefinition definition = state.Definition;
                if (!providers.TryGetValue(
                        definition.Id,
                        out ITextStudioIntegrationEditorProvider provider))
                {
                    continue;
                }

                state.Provider = provider;
                TextStudioIntegrationValidationContext context =
                    new TextStudioIntegrationValidationContext(
                        definition,
                        selectedGameObject,
                        selectedObject);
                try
                {
                    provider.Validate(context, new IssueCollection(state));
                }
                catch (Exception exception)
                {
                    state.AddIssue(new TextStudioIntegrationIssue(
                        TextStudioIntegrationIssueSeverity.Error,
                        "Validation failed",
                        exception.Message,
                        "Check the Console for the complete exception."));
                    Debug.LogException(exception);
                }
            }

            return states;
        }

        static string GetIntegrationDisplayName(
            IReadOnlyDictionary<string, TextStudioIntegrationState> statesById,
            string integrationId)
        {
            return statesById.TryGetValue(
                    integrationId,
                    out TextStudioIntegrationState state)
                ? state.Definition.DisplayName
                : integrationId;
        }

        internal static bool IsAssemblyLoaded(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
                return false;

            IReadOnlyList<Assembly> assemblies = UnityUtils.GetLoadedAssemblies();
            for (int i = 0; i < assemblies.Count; i++)
            {
                if (string.Equals(
                        assemblies[i].GetName().Name,
                        assemblyName,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        internal static string FindSampleAssetPath(TextStudioIntegrationDefinition definition)
        {
            string guide = FindSetupGuideAssetPath(definition.SetupGuideGuid);
            if (!string.IsNullOrEmpty(definition.SetupGuideGuid) && string.IsNullOrEmpty(guide))
                return string.Empty;
            string root = string.IsNullOrEmpty(guide) ? string.Empty : Path.GetDirectoryName(guide)?.Replace('\\', '/');
            return FindSampleAssetPath(definition.SampleAssetName, root);
        }

        internal static string FindSampleAssetPath(string sampleAssetName, string root = "")
        {
            if (string.IsNullOrEmpty(sampleAssetName))
                return string.Empty;

            string cacheKey = root + ":" + sampleAssetName;
            if (SampleAssetCache.TryGetValue(cacheKey, out string cachedPath))
                return cachedPath;

            string[] guids = string.IsNullOrEmpty(root)
                ? AssetDatabase.FindAssets("t:Scene")
                : AssetDatabase.FindAssets("t:Scene", new[] { root });
            List<string> matchingPaths = new List<string>();
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.Equals(
                        Path.GetFileNameWithoutExtension(path),
                        sampleAssetName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    matchingPaths.Add(path);
                }
            }

            if (matchingPaths.Count == 0)
            {
                string[] assetPaths = AssetDatabase.GetAllAssetPaths();
                for (int i = 0; i < assetPaths.Length; i++)
                {
                    string path = assetPaths[i];
                    if (!string.IsNullOrEmpty(root) && !path.StartsWith(root + "/", StringComparison.Ordinal)) continue;
                    if (string.Equals(Path.GetExtension(path), ".unity", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(
                            Path.GetFileNameWithoutExtension(path),
                            sampleAssetName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        matchingPaths.Add(path);
                    }
                }
            }

            if (matchingPaths.Count > 0)
            {
                matchingPaths.Sort(StringComparer.Ordinal);
                string path = matchingPaths[0];
                SampleAssetCache[cacheKey] = path;
                return path;
            }

            SampleAssetCache[cacheKey] = string.Empty;
            return string.Empty;
        }

        internal static string FindSetupGuideAssetPath(string setupGuideGuid)
        {
            if (string.IsNullOrEmpty(setupGuideGuid))
                return string.Empty;

            if (SetupGuideAssetCache.TryGetValue(setupGuideGuid, out string cachedPath))
                return cachedPath;

            string path = AssetDatabase.GUIDToAssetPath(setupGuideGuid);
            if (string.IsNullOrEmpty(path) ||
                AssetDatabase.LoadAssetAtPath<TextAsset>(path) == null)
            {
                path = string.Empty;
            }

            SetupGuideAssetCache[setupGuideGuid] = path;
            return path;
        }

        static TextStudioIntegrationState Evaluate(TextStudioIntegrationDefinition definition)
        {
            TextStudioIntegrationState state = new TextStudioIntegrationState(definition);
            PackageManagerInfo companionPackage = string.IsNullOrEmpty(definition.CompanionPackageName)
                ? null
                : PackageManagerInfo.FindForPackageName(definition.CompanionPackageName);

            state.CompanionVersion = companionPackage != null ? companionPackage.version : string.Empty;
            if (definition.Id == "3d-visual-effect-graph")
            {
                if (!IsUnity63OrNewer())
                    state.AddIssue(new TextStudioIntegrationIssue(TextStudioIntegrationIssueSeverity.Error,
                        "Unity version is too old", "Visual Effect Graph integration requires Unity 6.3 or newer.",
                        "Open this package in Unity 6.3 or newer.", true));
                if (companionPackage != null &&
                    (!IntegrationPackageRevisionComparer.TryCompareVersions(companionPackage.version, "17.3.0", out int order) || order < 0))
                    state.AddIssue(new TextStudioIntegrationIssue(TextStudioIntegrationIssueSeverity.Error,
                        "Visual Effect Graph version is too old", "Install Visual Effect Graph 17.3 or newer before importing this bridge.",
                        "Update Visual Effect Graph in Package Manager.", true));
            }
            if (definition.Id.StartsWith("dynamics-", StringComparison.Ordinal) && !IsUnity63OrNewer())
                state.AddIssue(new TextStudioIntegrationIssue(TextStudioIntegrationIssueSeverity.Error,
                    "Unity version is too old", "Text Studio Dynamics requires Unity 6.3 or newer.",
                    "Open this package in Unity 6.3 or newer.", true));
            state.CompanionInstalled =
                companionPackage != null ||
                IsAssemblyLoaded(definition.CompanionAssemblyName) ||
                AssetSearchHasResult(definition.CompanionAssetSearch);
            bool hasIntegrationAssembly =
                !string.IsNullOrEmpty(definition.IntegrationAssemblyName);
            bool hasIntegrationAssetSearch =
                !string.IsNullOrEmpty(definition.IntegrationAssetSearch);
            bool integrationAssetFound =
                hasIntegrationAssetSearch &&
                (IsSamplePackage(definition)
                    ? !string.IsNullOrEmpty(FindSampleAssetPath(definition))
                    : AssetSearchHasResult(definition.IntegrationAssetSearch));
            state.IntegrationLoaded =
                hasIntegrationAssembly
                    ? IsAssemblyLoaded(definition.IntegrationAssemblyName)
                    : integrationAssetFound;
            state.IntegrationInstalled =
                (!hasIntegrationAssembly && !hasIntegrationAssetSearch) ||
                state.IntegrationLoaded ||
                AssemblyDefinitionExists(definition.IntegrationAssemblyName) ||
                integrationAssetFound;
            state.NestedPackageAssetPath = FindNestedPackageAssetPath(definition);
            state.Package = TextStudioIntegrationPackageTracker.Get(definition, state.NestedPackageAssetPath);
            state.SampleAssetPath = FindSampleAssetPath(definition);
            state.SetupGuideAssetPath = FindSetupGuideAssetPath(definition.SetupGuideGuid);

            if (state.CompanionInstalled && !state.IntegrationInstalled)
            {
                bool isSamplePackage = IsSamplePackage(definition);
                bool packageExpected = !string.IsNullOrEmpty(definition.NestedPackageFileName);
                bool packageFound = !string.IsNullOrEmpty(state.NestedPackageAssetPath);
                string ownerName = GetPackageDisplayName(definition.NestedPackageOwnerPackageName);
                state.AddIssue(new TextStudioIntegrationIssue(
                    packageExpected && !packageFound
                        ? TextStudioIntegrationIssueSeverity.Error
                        : TextStudioIntegrationIssueSeverity.Warning,
                    packageExpected && !packageFound
                        ? (isSamplePackage ? "Sample package not found" : "Integration package not found")
                        : (isSamplePackage ? "Sample package not imported" : "Integration not imported"),
                    packageExpected && !packageFound
                        ? definition.CompanionDisplayName + " is installed, but " +
                          definition.NestedPackageFileName + " was not found in the installed " +
                          ownerName + " package."
                        : definition.CompanionDisplayName + " is installed, but " +
                          definition.IntegrationDisplayName + " is not imported.",
                    packageExpected && !packageFound
                        ? "Reinstall or update " + ownerName + ", then reopen this window."
                        : packageFound
                            ? (isSamplePackage ? "Use Import Sample." : "Use Import Integration.")
                            : "Reinstall or update " + ownerName + ", then check the Integration Guide."));
            }

            return state;
        }

        static Dictionary<string, ITextStudioIntegrationEditorProvider> FindProviders()
        {
            if (_providers != null)
                return _providers;

            Dictionary<string, ITextStudioIntegrationEditorProvider> providers =
                new Dictionary<string, ITextStudioIntegrationEditorProvider>(StringComparer.Ordinal);
            TypeCache.TypeCollection providerTypes =
                TypeCache.GetTypesDerivedFrom<ITextStudioIntegrationEditorProvider>();

            for (int i = 0; i < providerTypes.Count; i++)
            {
                Type providerType = providerTypes[i];
                if (providerType.IsAbstract || providerType.IsInterface)
                    continue;

                try
                {
                    ITextStudioIntegrationEditorProvider provider =
                        Activator.CreateInstance(providerType) as ITextStudioIntegrationEditorProvider;
                    if (provider == null || string.IsNullOrEmpty(provider.IntegrationId))
                        continue;

                    if (providers.ContainsKey(provider.IntegrationId))
                    {
                        Debug.LogError(
                            "[Text Studio] Duplicate integration editor provider ID '" +
                            provider.IntegrationId + "' was ignored.");
                        continue;
                    }

                    providers.Add(provider.IntegrationId, provider);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            _providers = providers;
            return _providers;
        }

        static bool AssetSearchHasResult(string search)
        {
            if (string.IsNullOrEmpty(search))
                return false;

            if (AssetSearchCache.TryGetValue(search, out bool cached))
                return cached;

            bool found = AssetDatabase.FindAssets(search).Length > 0;
            AssetSearchCache[search] = found;
            return found;
        }

        static bool AssemblyDefinitionExists(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
                return false;

            if (AssemblyDefinitionCache.TryGetValue(assemblyName, out bool cached))
                return cached;

            string[] guids = AssetDatabase.FindAssets(assemblyName + " t:AssemblyDefinitionAsset");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.Equals(
                        Path.GetFileNameWithoutExtension(path),
                        assemblyName,
                        StringComparison.Ordinal))
                {
                    AssemblyDefinitionCache[assemblyName] = true;
                    return true;
                }
            }

            AssemblyDefinitionCache[assemblyName] = false;
            return false;
        }

        static void ClearAssetCaches()
        {
            AssetSearchCache.Clear();
            AssemblyDefinitionCache.Clear();
            SampleAssetCache.Clear();
            SetupGuideAssetCache.Clear();
        }

        internal static string FindNestedPackageAssetPath(TextStudioIntegrationDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.NestedPackageFileName))
                return string.Empty;

            string root = TextStudioPackagePaths.FindFullRoot(
                definition.NestedPackageOwnerPackageName);
            if (string.IsNullOrEmpty(root))
                return string.Empty;

            string fullPath = Path.Combine(
                root,
                "Integrations",
                definition.NestedPackageFileName);
            if (!File.Exists(fullPath))
                return string.Empty;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return string.Empty;

            string relative = fullPath.Replace('\\', '/');
            string normalizedRoot = projectRoot.Replace('\\', '/').TrimEnd('/') + "/";
            return relative.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                ? relative.Substring(normalizedRoot.Length)
                : string.Empty;
        }

        static bool IsUnity63OrNewer()
        {
            string[] parts = Application.unityVersion.Split('.');
            return parts.Length >= 2 && int.TryParse(parts[0], out int major) &&
                   int.TryParse(parts[1], out int minor) &&
                   (major > 6000 || major == 6000 && minor >= 3);
        }

        internal static bool IsSamplePackage(TextStudioIntegrationDefinition definition)
        {
            return definition != null &&
                   definition.SupportLevel == TextStudioIntegrationSupportLevel.SampleRecipe;
        }

        internal static string GetPackageDisplayName(string packageName)
        {
            if (string.Equals(packageName, "com.wetzold.text-studio.dynamics", StringComparison.Ordinal))
                return "Text Studio Dynamics";
            return string.Equals(
                    packageName,
                    TextStudioPackagePaths.ThreeDPackageName,
                    StringComparison.Ordinal)
                ? "Text Studio 3D"
                : "Text Studio";
        }

        static TextStudioIntegrationDefinition Define(
            string id,
            string displayName,
            string category,
            string description,
            string ownership,
            TextStudioIntegrationSupportLevel supportLevel,
            TextStudioIntegrationValidationProvenance provenance,
            string integrationAssemblyName,
            string vendorAssemblyName,
            string vendorPackageName,
            string vendorAssetSearch,
            string nestedPackageFileName,
            string sampleAssetName)
        {
            string integrationAssetSearch = id switch
            {
                "pixel-crushers" => "TextStudioPixelCrushersSubtitlePanel t:Script",
                "unitext-folio-sample" => "Multilingual Lettering t:Scene",
                "3d-unitext-folio-sample" => "Folio Material Bench t:Scene",
                _ => string.Empty
            };
            return new TextStudioIntegrationDefinition(
                id,
                displayName,
                GetCompanionDisplayName(id, displayName),
                GetIntegrationDisplayName(id, displayName),
                category,
                description,
                ownership,
                supportLevel,
                provenance,
                integrationAssemblyName,
                integrationAssetSearch,
                vendorAssemblyName,
                vendorPackageName,
                vendorAssetSearch,
                nestedPackageFileName,
                GetNestedPackageOwnerPackageName(id),
                sampleAssetName,
                GetSetupGuideGuid(id),
                GetQuickStart(id),
                GetRequiredIntegrationId(id),
                GetDocumentationUrl(id));
        }

        static string GetRequiredIntegrationId(string id)
        {
            return id switch
            {
                "game-creator-2-dialogue" => "game-creator-2",
                "unitext-free-sample" or
                "unitext-folio-sample" or
                "3d-unitext-free-sample" or
                "3d-unitext-folio-sample" => "unitext",
                _ => string.Empty
            };
        }

        static string GetCompanionDisplayName(string id, string displayName)
        {
            return id switch
            {
                "input-system" or "3d-input-system" => "Unity Input System",
                "timeline" => "Unity Timeline",
                "unitext" or
                "unitext-free-sample" or
                "unitext-folio-sample" or
                "3d-unitext-free-sample" or
                "3d-unitext-folio-sample" => "UniText",
                "unity-splines" or "3d-unity-splines" => "Unity Splines",
                "3d-xr-interaction-toolkit" => "XR Interaction Toolkit",
                _ => displayName
            };
        }

        static string GetIntegrationDisplayName(string id, string displayName)
        {
            return id switch
            {
                "adventure-creator" => "Text Studio Adventure Creator adapter",
                "game-creator-2" => "Text Studio Game Creator 2 command set",
                "game-creator-2-dialogue" => "Text Studio Game Creator 2 Dialogue adapter",
                "ink" => "Text Studio Ink bridge",
                "naninovel" => "Text Studio Naninovel adapter",
                "pixel-crushers" => "Text Studio Dialogue System adapter",
                "yarn-spinner" => "Text Studio Yarn Spinner presenter",
                "playmaker-2" => "Text Studio PlayMaker 2 action set",
                "visual-scripting" => "Text Studio Visual Scripting unit set",
                "led-studio" => "Text Studio LED Studio render adapter",
                "ui-effect" => "Text Studio UIEffect mesh adapter",
                "unitext" => "Text Studio UniText backend",
                "unitext-free-sample" => "Text Studio UniText open-source sample",
                "unitext-folio-sample" => "Text Studio UniText Folio sample",
                "unity-localization" => "Text Studio Localization adapter",
                "unity-splines" => "Text Studio Splines adapter",
                "prime-tween" => "Text Studio PrimeTween sample",
                "input-system" => "Text Studio Input System adapter",
                "timeline" => "Text Studio Timeline adapter",
                "3d-input-system" => "Text Studio 3D Input System adapter",
                "3d-unitext-free-sample" => "Text Studio 3D UniText open-source sample",
                "3d-unitext-folio-sample" => "Text Studio 3D UniText Folio sample",
                "3d-unity-splines" => "Text Studio 3D Unity Splines sample",
                "3d-xr-interaction-toolkit" => "Text Studio 3D XR adapter",
                _ => "Text Studio integration for " + displayName
            };
        }

        static string GetNestedPackageOwnerPackageName(string id)
        {
            return id.StartsWith("3d-", StringComparison.Ordinal)
                ? TextStudioPackagePaths.ThreeDPackageName
                : TextStudioPackagePaths.BasePackageName;
        }

        static string GetDocumentationUrl(string id)
        {
            return id switch
            {
                "adventure-creator" => BaseIntegrationGuideUrl + "#adventure-creator",
                "game-creator-2" or "game-creator-2-dialogue" => BaseIntegrationGuideUrl + "#game-creator-2",
                "ink" => BaseIntegrationGuideUrl + "#ink",
                "naninovel" => BaseIntegrationGuideUrl + "#naninovel",
                "pixel-crushers" => BaseIntegrationGuideUrl + "#pixel-crushers-dialogue-system",
                "yarn-spinner" => BaseIntegrationGuideUrl + "#yarn-spinner",
                "playmaker-2" => BaseIntegrationGuideUrl + "#playmaker-2",
                "visual-scripting" => BaseIntegrationGuideUrl + "#unity-visual-scripting",
                "led-studio" => BaseIntegrationGuideUrl + "#led-studio",
                "ui-effect" => BaseIntegrationGuideUrl + "#uieffect",
                "unitext" or
                "unitext-free-sample" or
                "unitext-folio-sample" => BaseIntegrationGuideUrl + "#unitext",
                "unity-localization" => BaseIntegrationGuideUrl + "#unity-localization",
                "unity-splines" => BaseIntegrationGuideUrl + "#unity-splines",
                "prime-tween" => BaseIntegrationGuideUrl + "#primetween",
                "input-system" => BaseIntegrationGuideUrl + "#input-system",
                "timeline" => BaseIntegrationGuideUrl + "#timeline",
                "3d-input-system" => ThreeDIntegrationGuideUrl + "#input-system",
                "3d-unitext-free-sample" or
                "3d-unitext-folio-sample" => ThreeDIntegrationGuideUrl + "#unitext",
                "3d-unity-splines" => ThreeDIntegrationGuideUrl + "#unity-splines",
                "3d-xr-interaction-toolkit" => ThreeDIntegrationGuideUrl + "#xr-interaction-toolkit",
                _ => BaseIntegrationGuideUrl
            };
        }

        static string GetSetupGuideGuid(string id)
        {
            return id switch
            {
                "adventure-creator" => "9078264e8faa037489fe5b1bece01d25",
                "game-creator-2" => "0e7d15c937d42544d99d3c533feda16f",
                "game-creator-2-dialogue" => "7f95da77c6014838a5b260d058310388",
                "ink" => "96728e899801b944d8056dbe23f8a038",
                "naninovel" => "fd658b5885053cf4ab1b7a3964996445",
                "pixel-crushers" => "4f237e5e1a714d88863f52d93753f0b0",
                "yarn-spinner" => "1da2fb7413b24c9bad9a488b97f6d34d",
                "playmaker-2" => "62fb31986d7c9f34cbd4cecfdfb4056e",
                "visual-scripting" => "1fdb86a0fe624d88b260dfcb72dd3478",
                "led-studio" => "912a0503ac2b41bc8f37a82d41e058a8",
                "ui-effect" => "8f4361593c7a4f20969e5d4dbb9b073c",
                "unitext-free-sample" => "18727552f50b4c029c26ca61a3242ef4",
                "unitext-folio-sample" => "0eb0826e9a31a47ba8af3506d26f5fa5",
                "unity-localization" => "d4ad4b4ce82f451c94d228de1a0f4db4",
                "unity-splines" => "90130eacc1e26864cb13b54d6393eaff",
                "timeline" => "2a7d8ccd0b104c9cbdbc2597e767ec6d",
                "prime-tween" => "32596e021b0f4d6459766a61ae888b12",
                "3d-input-system" => "3a64cd75f9fd4b7a814e91c06451cd4e",
                "3d-unitext-free-sample" => "c9fd44f4dd6d4e35a84cf0a14cd58a2f",
                "3d-unitext-folio-sample" => "49a12b18c3426460ebe98441a97f0991",
                "3d-unity-splines" => "1704e4ddb0ebc994db17b943bffdb0e6",
                "3d-xr-interaction-toolkit" => "2dd4772d2dc29492e9a578e70ae2193c",
                _ => string.Empty
            };
        }

        static string GetQuickStart(string id)
        {
            return id switch
            {
                "adventure-creator" =>
                    "Open the demo, or add the Speech Bridge to the AC subtitle UI and leave AC's Dialogue Line label unlinked.",
                "game-creator-2" =>
                    "Open the demo and use Text Studio instructions, conditions, and relays in a Game Creator graph.",
                "game-creator-2-dialogue" =>
                    "Select the Speech Skin asset used by the dialogue, choose Configure Selected, and repeat for any actor or expression overrides.",
                "ink" =>
                    "Open the demo, or assign a compiled Ink story to the Story Bridge and route choices through its events.",
                "naninovel" =>
                    "Replace stock reveal components with a plain TMP renderer, configure the text object, then assign the Text Studio adapter component to the printer's Revealable Text field. The standalone demo uses separate source wiring.",
                "pixel-crushers" =>
                    "Open the demo, or replace the standard subtitle panel with the Text Studio subtitle panel.",
                "yarn-spinner" =>
                    "Open the demo, or configure Text Studio as the Yarn line typewriter and markup processor.",
                "playmaker-2" =>
                    "Open the demo, or add the curated Text Studio actions and typed target variable to a PlayMaker 2 FSM.",
                "visual-scripting" =>
                    "Open the graph demo, or add the curated Text Studio units and events to a Script Graph.",
                "led-studio" =>
                    "Open the control-room sample, or use the Text Studio render backend on an LED text source.",
                "ui-effect" =>
                    "Place TextStudioText and UIEffect on the same TextMeshProUGUI object; mesh synchronization is automatic.",
                "unitext" =>
                    "Select a UniText object and use Configure Selected. The backend activates the best features exposed by the installed edition.",
                "unitext-free-sample" =>
                    "Import the sample and open Multilingual Lettering.",
                "unitext-folio-sample" =>
                    "Import the sample and open Multilingual Lettering, Document Reader, or Interactive Journal. The reading scenes require UniText Folio 3.10 or newer.",
                "unity-localization" =>
                    "Open Localized Captions for component and native Label binding recipes, or bind a LocalizedString through TextStudioLocalizeString. Native Label bindings require Unity 6 and Localization 1.5 or newer.",
                "unity-splines" =>
                    "Open the sample, or assign a Spline Placement Provider to animated Text Studio output.",
                "prime-tween" =>
                    "Open the sample and tween the parent UI while Text Studio owns label markup and reveal.",
                "input-system" =>
                    "Assign Input Actions to the Text Studio wait or advance action and choose who manages their lifetime.",
                "timeline" =>
                    "Open the sample, or add Text Studio reveal, effect, or control tracks and markers to a Timeline asset.",
                "3d-input-system" =>
                    "Select a 3D pointer, navigation group, or input field, choose Configure Selected, then assign the intended actions.",
                "3d-unitext-free-sample" =>
                    "Import the sample and open Solid Shaped Text.",
                "3d-unitext-folio-sample" =>
                    "Import the sample and open Folio Material Bench.",
                "3d-unity-splines" =>
                    "Open the sample and edit its Spline Containers to reshape the generated 3D lettering.",
                "3d-xr-interaction-toolkit" =>
                    "Create GameObject > Text Studio > 3D > Spatial UI > XRI Menu, or configure an existing Spatial UI hierarchy.",
                _ => string.Empty
            };
        }

        sealed class IssueCollection : ICollection<TextStudioIntegrationIssue>
        {
            readonly TextStudioIntegrationState _state;

            public int Count => _state.Issues.Count;
            public bool IsReadOnly => false;

            public IssueCollection(TextStudioIntegrationState state)
            {
                _state = state;
            }

            public void Add(TextStudioIntegrationIssue item)
            {
                _state.AddIssue(item);
            }

            public void Clear()
            {
                throw new NotSupportedException();
            }

            public bool Contains(TextStudioIntegrationIssue item)
            {
                return false;
            }

            public void CopyTo(TextStudioIntegrationIssue[] array, int arrayIndex)
            {
                for (int i = 0; i < _state.Issues.Count; i++)
                    array[arrayIndex + i] = _state.Issues[i];
            }

            public bool Remove(TextStudioIntegrationIssue item)
            {
                throw new NotSupportedException();
            }

            public IEnumerator<TextStudioIntegrationIssue> GetEnumerator()
            {
                return _state.Issues.GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
