using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TextStudio.Editor
{
    internal static class TextStudioHealthCheck
    {
        const string OptionalIntegrationAction = "Optional integrations are not required by the base Text Studio package.";

        internal static TextStudioHealthStatus Run()
        {
            TextStudioHealthStatus status = new TextStudioHealthStatus();
            string root = TextStudioPackagePaths.FindFullRoot(TextStudioPackagePaths.BasePackageName);
            if (string.IsNullOrEmpty(root))
            {
                status.Add(
                    TextStudioHealthSeverity.Error,
                    "Base package not found",
                    "Text Studio package root could not be resolved.",
                    "Reimport Text Studio or check the package manifest.");
                return status;
            }

            RequireFile(status, root, "package.json", "Base package manifest");
            RequireFile(status, root, "Documentation/Documentation.pdf", "Customer documentation PDF");
            ReportUnityVersion(status);
            ReportTextMeshPro(status);
            ReportRuntimeLoading(status);
            ReportOptionalIntegrations(status);
            ReportWorkflowContent(status);

            if (status.ErrorCount == 0)
            {
                status.Add(
                    TextStudioHealthSeverity.Info,
                    "Base package ready",
                    "No release-blocking Text Studio health issues were found.",
                    "Use the source-workspace Debug validation tools for release preparation.");
            }

            return status;
        }

        internal static TextStudioHealthStatus RunForSource(string source)
        {
            return RunForSource(source, null);
        }

        internal static TextStudioHealthStatus RunForSource(
            string source,
            TextStudioBehaviorProfile profile)
        {
            TextStudioHealthStatus status = new TextStudioHealthStatus();
            ReportSourceWorkflow(status, source, profile, "Source", profile);
            return status;
        }

        internal static TextStudioHealthStatus RunForText(TextStudioText text)
        {
            TextStudioHealthStatus status = new TextStudioHealthStatus();
            ReportTextWorkflow(status, text, "Text Studio Text");
            return status;
        }

        internal static TextStudioHealthStatus RunForBehaviorProfile(
            TextStudioBehaviorProfile profile)
        {
            TextStudioHealthStatus status = new TextStudioHealthStatus();
            ReportProfileWorkflow(status, profile, "Behavior Profile", profile);
            return status;
        }

        internal static TextStudioHealthStatus RunForComposedEffect(TextStudioComposedEffectAsset composed)
        {
            TextStudioHealthStatus status = new TextStudioHealthStatus();
            ReportComposedEffectWorkflow(status, composed, "Composed Effect", composed);
            return status;
        }

        public static void ValidateFromBatchmode()
        {
            TextStudioHealthStatus status = Run();
            for (int i = 0; i < status.Items.Count; i++)
                LogItem(status.Items[i]);

            EditorApplication.Exit(status.ErrorCount == 0 ? 0 : 1);
        }

        static void RequireFile(TextStudioHealthStatus status, string root, string relativePath, string title)
        {
            string fullPath = Path.Combine(root, relativePath);
            if (File.Exists(fullPath))
            {
                status.Add(TextStudioHealthSeverity.Info, title, relativePath + " exists.", string.Empty);
                return;
            }

            status.Add(
                TextStudioHealthSeverity.Error,
                title + " missing",
                relativePath + " was not found.",
                "Restore the file before exporting or shipping the package.");
        }

        static void ReportUnityVersion(TextStudioHealthStatus status)
        {
            status.Add(
                TextStudioHealthSeverity.Info,
                "Unity version",
                Application.unityVersion,
                "Validate clean imports in Unity 2023.2 and the target Unity 6 version.");
        }

        static void ReportTextMeshPro(TextStudioHealthStatus status)
        {
            bool hasTmp = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro") != null;
            status.Add(
                hasTmp ? TextStudioHealthSeverity.Info : TextStudioHealthSeverity.Error,
                "TextMeshPro",
                hasTmp ? "TextMeshPro is available." : "TextMeshPro is not available.",
                hasTmp ? string.Empty : "Install TextMeshPro before using Text Studio.");
        }

        static void ReportRuntimeLoading(TextStudioHealthStatus status)
        {
            List<TextStudioProjectSettings> settings =
                TextStudioProjectSettingsEditorUtility.FindAll();
            TextStudioProjectSettings canonical =
                TextStudioProjectSettingsEditorUtility.FindCanonical();
            string validationError =
                TextStudioRuntimeLoadingBuildValidator.ValidateRuntimeLoadingContract();

            if (!string.IsNullOrEmpty(validationError))
            {
                status.Add(
                    TextStudioHealthSeverity.Error,
                    "Runtime settings loading",
                    validationError,
                    "Keep at most one settings asset and move it to " +
                    TextStudioProjectSettings.CanonicalAssetPath + ".");
                return;
            }

            if (canonical == null)
            {
                status.Add(
                    TextStudioHealthSeverity.Info,
                    "Runtime settings loading",
                    "No project settings asset exists. Players use Text Studio's built-in defaults.",
                    string.Empty);
                return;
            }

            status.Add(
                TextStudioHealthSeverity.Info,
                "Runtime settings loading",
                "The canonical project settings asset and " +
                settings.Count + " settings asset total resolve through Resources key '" +
                TextStudioProjectSettings.ResourcePath + "'.",
                string.Empty);
        }

        static void ReportOptionalIntegrations(TextStudioHealthStatus status)
        {
            IReadOnlyList<TextStudioIntegrationState> integrations =
                TextStudioIntegrationCatalog.Evaluate();
            for (int i = 0; i < integrations.Count; i++)
            {
                TextStudioIntegrationState integration = integrations[i];
                string companionVersion = string.IsNullOrEmpty(integration.CompanionVersion)
                    ? string.Empty
                    : " " + integration.CompanionVersion;
                string companionState = integration.CompanionInstalled
                    ? integration.Definition.CompanionDisplayName + companionVersion + " is installed."
                    : integration.Definition.CompanionDisplayName + " is not installed.";
                string integrationState = integration.IntegrationLoaded
                    ? integration.Definition.IntegrationDisplayName + " is active."
                    : integration.IntegrationInstalled
                        ? integration.Definition.IntegrationDisplayName + " is imported but not loaded."
                        : integration.Definition.IntegrationDisplayName + " is not imported.";
                string detail = integration.CompatibilityNotice != null
                    ? integration.CompatibilityNotice.Detail
                    : companionState + " " + integrationState;
                string action = integration.CompatibilityNotice != null ? integration.CompatibilityNotice.Action : integration.IsReady
                    ? "This integration is ready."
                    : "Open Tools/Text Studio/Integrations for setup and validation.";
                TextStudioHealthSeverity severity = integration.HasErrors
                    ? TextStudioHealthSeverity.Warning
                    : TextStudioHealthSeverity.Info;
                status.Add(
                    severity,
                    "Optional integration: " + integration.Definition.DisplayName,
                    detail,
                    action);
                if (integration.Package != null &&
                    integration.Package.Status != TextStudioPackageStatus.NotApplicable &&
                    integration.Package.Status != TextStudioPackageStatus.Current &&
                    integration.Package.Status != TextStudioPackageStatus.Unavailable &&
                    integration.Package.Status != TextStudioPackageStatus.Checking)
                {
                    TextStudioHealthSeverity packageSeverity =
                        integration.Package.Status == TextStudioPackageStatus.InvalidBundle ||
                        integration.Package.Status == TextStudioPackageStatus.IncompleteImport
                            ? TextStudioHealthSeverity.Warning
                            : TextStudioHealthSeverity.Info;
                    status.Add(packageSeverity,
                        "Optional package: " + integration.Definition.DisplayName,
                        integration.Package.Detail,
                        "Open Tools/Text Studio/Integrations to review the bundled package and installed files.");
                }
            }
        }

        internal static TextStudioHealthItem CreateOptionalIntegrationItem(
            string integrationName,
            string assemblyName,
            string packageName,
            string packageVersion,
            bool assemblyLoaded)
        {
            string detail;
            if (!string.IsNullOrEmpty(packageVersion))
                detail = packageName + " " + packageVersion + " is installed.";
            else if (assemblyLoaded)
                detail = assemblyName + " is loaded in this project.";
            else
                detail = integrationName + " is not installed.";

            return new TextStudioHealthItem(
                TextStudioHealthSeverity.Info,
                "Optional integration: " + integrationName,
                detail,
                OptionalIntegrationAction);
        }

        static void ReportWorkflowContent(TextStudioHealthStatus status)
        {
            ReportLoadedTextStudioObjects(status);
            ReportProfileAssets(status);
            ReportComposedEffectAssets(status);
        }

        static void ReportLoadedTextStudioObjects(TextStudioHealthStatus status)
        {
            TextStudioText[] texts = Resources.FindObjectsOfTypeAll<TextStudioText>();
            for (int i = 0; i < texts.Length; i++)
            {
                TextStudioText text = texts[i];
                if (text == null)
                    continue;
                if (EditorUtility.IsPersistent(text))
                    continue;

                string scope = string.IsNullOrEmpty(text.name) ? "Text Studio Text" : text.name;
                ReportTextWorkflow(status, text, scope);
            }
        }

        static void ReportProfileAssets(TextStudioHealthStatus status)
        {
            string[] guids = AssetDatabase.FindAssets("t:TextStudioBehaviorProfile");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                TextStudioBehaviorProfile profile =
                    AssetDatabase.LoadAssetAtPath<TextStudioBehaviorProfile>(path);
                if (profile == null)
                    continue;

                ReportProfileWorkflow(status, profile, path, profile);
            }
        }

        static void ReportComposedEffectAssets(TextStudioHealthStatus status)
        {
            IReadOnlyDictionary<string, ITextStudioEffectDefinition> persistedDefinitions =
                CollectPersistedEffectDefinitions();
            string[] guids = AssetDatabase.FindAssets("t:TextStudioComposedEffectAsset");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                TextStudioComposedEffectAsset composed = AssetDatabase.LoadAssetAtPath<TextStudioComposedEffectAsset>(path);
                if (composed == null)
                    continue;

                ReportComposedEffectWorkflow(
                    status,
                    composed,
                    path,
                    composed,
                    persistedDefinitions,
                    IsExampleAssetPath(path)
                        ? TextStudioHealthSeverity.Warning
                        : TextStudioHealthSeverity.Error);
            }
        }

        static IReadOnlyDictionary<string, ITextStudioEffectDefinition>
            CollectPersistedEffectDefinitions()
        {
            Dictionary<string, ITextStudioEffectDefinition> definitions =
                new Dictionary<string, ITextStudioEffectDefinition>(StringComparer.OrdinalIgnoreCase);
            string[] guids = AssetDatabase.FindAssets("t:TextStudioEffectDefinitionAsset");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                TextStudioEffectDefinitionAsset definition =
                    AssetDatabase.LoadAssetAtPath<TextStudioEffectDefinitionAsset>(path);
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                    continue;

                definitions[NormalizeEffectId(definition.Id)] = definition;
            }

            return definitions;
        }

        static bool IsExampleAssetPath(string path)
        {
            string normalized = (path ?? string.Empty).Replace('\\', '/');
            return normalized.IndexOf("/Examples/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static void ReportTextWorkflow(TextStudioHealthStatus status, TextStudioText text, string scope)
        {
            if (text == null)
                return;

            ReportSourceWorkflow(status, text.Markup, text.BehaviorProfile, scope, text);
            if (text.BehaviorProfile != null &&
                text.BehaviorProfile.Provides(TextStudioBehaviorDomain.Typewriter))
            {
                return;
            }

            TextStudioTypewriterSettings settings = text.GetLocalBehaviorSettings().Typewriter.Resolve();
            ReportTypewriterEffectSettings(status, settings, scope, text);
        }

        static void ReportSourceWorkflow(
            TextStudioHealthStatus status,
            string source,
            TextStudioBehaviorProfile profile,
            string scope,
            UnityEngine.Object target)
        {
            ReportMarkupDiagnostics(status, source, scope, target);
            ReportProfileWorkflow(status, profile, scope, target);
            ReportConstantDefaultOverlap(status, source, profile, scope, target);
        }

        static void ReportProfileWorkflow(
            TextStudioHealthStatus status,
            TextStudioBehaviorProfile profile,
            string scope,
            UnityEngine.Object target)
        {
            if (profile == null)
                return;

            if (profile.Provides(TextStudioBehaviorDomain.DefaultEffects))
            {
                ValidateInvocationStyle(
                    status,
                    scope,
                    "Default Effects",
                    profile.DefaultEffects,
                    "Missing behavior profile effect reference",
                    "Invalid behavior profile effect invocation",
                    target);
                ValidateInvocationStyle(
                    status,
                    scope,
                    "Default Reveal Effects",
                    profile.DefaultRevealEffects,
                    "Missing behavior profile effect reference",
                    "Invalid behavior profile effect invocation",
                    target);
                ValidateInvocationStyle(
                    status,
                    scope,
                    "Default Active Effects",
                    profile.DefaultActiveEffects,
                    "Missing behavior profile effect reference",
                    "Invalid behavior profile effect invocation",
                    target);
                ValidateInvocationStyle(
                    status,
                    scope,
                    "Default Hide Effects",
                    profile.DefaultHideEffects,
                    "Missing behavior profile effect reference",
                    "Invalid behavior profile effect invocation",
                    target);
            }

            if (profile.Provides(TextStudioBehaviorDomain.Typewriter))
                ReportProfileTypewriterWorkflow(status, profile, scope, target);
        }

        static void ReportProfileTypewriterWorkflow(
            TextStudioHealthStatus status,
            TextStudioBehaviorProfile profile,
            string scope,
            UnityEngine.Object target)
        {
            TextStudioTypewriterRecipe recipe = profile.Typewriter;
            if (recipe.Source == TextStudioTypewriterSource.None)
                return;

            if (recipe.Source == TextStudioTypewriterSource.Custom && recipe.CustomPreset == null)
            {
                status.Add(
                    TextStudioHealthSeverity.Warning,
                    "Missing profile typewriter preset",
                    scope + " uses a custom typewriter source without a preset asset.",
                    "Assign a typewriter preset, switch to a built-in preset, use inline settings, or choose None.",
                    target);
                return;
            }

            TextStudioTypewriterSettings authoringSettings =
                recipe.Source == TextStudioTypewriterSource.Custom
                    ? recipe.CustomPreset.Settings
                    : recipe.Source == TextStudioTypewriterSource.Inline
                        ? recipe.InlineSettings
                        : profile.GetTypewriterSettings();
            ReportTypewriterEffectSettings(status, authoringSettings, scope, target);
        }

        static void ReportTypewriterEffectSettings(
            TextStudioHealthStatus status,
            TextStudioTypewriterSettings settings,
            string scope,
            UnityEngine.Object target)
        {
            ValidateInvocationStyle(
                status,
                scope,
                "Typewriter Reveal Effect",
                settings.RevealEffectExpression,
                "Missing Typewriter Reveal effect reference",
                "Invalid Typewriter Reveal effect invocation",
                target);
            ValidateInvocationStyle(
                status,
                scope,
                "Typewriter Hide Effect",
                settings.HideEffectExpression,
                "Missing Typewriter Hide effect reference",
                "Invalid Typewriter Hide effect invocation",
                target);

            ReportTypewriterEffectAsset(status, settings.RevealEffect, scope + " Typewriter Reveal Effect", target);
            ReportTypewriterEffectAsset(status, settings.HideEffect, scope + " Typewriter Hide Effect", target);
        }

        static void ReportTypewriterEffectAsset(
            TextStudioHealthStatus status,
            TextStudioEffectDefinitionAsset effect,
            string scope,
            UnityEngine.Object target)
        {
            if (!(effect is TextStudioComposedEffectAsset composed))
                return;

            ReportComposedEffectWorkflow(status, composed, scope, composed);
            ReportTypewriterComposedEffectPhaseCompatibility(status, composed, scope, composed);
        }

        static void ReportComposedEffectWorkflow(
            TextStudioHealthStatus status,
            TextStudioComposedEffectAsset composed,
            string scope,
            UnityEngine.Object target,
            IReadOnlyDictionary<string, ITextStudioEffectDefinition> persistedDefinitions = null,
            TextStudioHealthSeverity missingSeverity = TextStudioHealthSeverity.Error)
        {
            if (composed == null)
                return;

            List<string> invocations = new List<string>(8);
            if (!composed.TryGetAllEffectInvocations(invocations, out string error))
            {
                status.Add(
                    TextStudioHealthSeverity.Error,
                    "Invalid composed effect",
                    scope + ": " + error,
                    "Fix the Tag Stack or the highlighted effect layer.",
                    target);
                return;
            }

            ValidateInvocationList(
                status,
                scope,
                "Effect Sources",
                invocations,
                "Missing composed effect reference",
                "Invalid composed effect invocation",
                target,
                NormalizeEffectId(composed.Id),
                persistedDefinitions,
                missingSeverity);
        }

        static void ReportTypewriterComposedEffectPhaseCompatibility(
            TextStudioHealthStatus status,
            TextStudioComposedEffectAsset composed,
            string scope,
            UnityEngine.Object target)
        {
            if (composed == null)
                return;

            TextStudioPhaseMask typewriterPhases =
                TextStudioPhaseMask.Reveal | TextStudioPhaseMask.Hide;
            int ignoredCount = 0;
            string firstIgnored = string.Empty;
            IReadOnlyList<TextStudioCompositionLayer> layers = composed.Layers;
            for (int i = 0; i < layers.Count; i++)
            {
                TextStudioCompositionLayer layer = layers[i];
                if (layer == null || !layer.Enabled ||
                    (NormalizePhaseMask(layer.Phases) & typewriterPhases) != TextStudioPhaseMask.None)
                {
                    continue;
                }

                ignoredCount++;
                if (firstIgnored.Length == 0)
                    firstIgnored = "layer '" + layer.DisplayName + "'";
            }

            List<string> tagInvocations = new List<string>(4);
            if (composed.TryGetTagInvocations(tagInvocations, out _))
            {
                for (int i = 0; i < tagInvocations.Count; i++)
                {
                    if (!TextStudioEffectInvocationParser.TryParse(
                            tagInvocations[i],
                            out TextStudioEffectInvocation invocation,
                            out _) ||
                        (NormalizePhaseMask(invocation.PhaseMask) & typewriterPhases) !=
                        TextStudioPhaseMask.None)
                    {
                        continue;
                    }

                    ignoredCount++;
                    if (firstIgnored.Length == 0)
                        firstIgnored = "Tag Stack entry " + tagInvocations[i];
                }
            }

            if (ignoredCount == 0)
                return;

            status.Add(
                TextStudioHealthSeverity.Warning,
                "Typewriter composition has inactive entries",
                scope + " contains " + ignoredCount +
                " enabled " + (ignoredCount == 1 ? "entry" : "entries") +
                " without Reveal or Hide and ignores them for this assignment. First ignored: " +
                firstIgnored + ".",
                "Add Reveal or Hide to those entries, or invoke their Active treatment through source tags, Default Effects, or an ordinary Composed Effect tag.",
                target);
        }

        static void ReportMarkupDiagnostics(
            TextStudioHealthStatus status,
            string source,
            string scope,
            UnityEngine.Object target)
        {
            TextStudioTagAnalysisContext context = new TextStudioTagAnalysisContext(
                TextStudioTagCatalog.BuildDefault(),
                TextStudioTagBackend.TextMeshPro);
            TextStudioDiagnosticReport report = TextStudioMarkupDiagnostics.Analyze(source, context);
            for (int i = 0; i < report.Diagnostics.Length; i++)
            {
                TextStudioDiagnostic diagnostic = report.Diagnostics[i];
                switch (diagnostic.Code)
                {
                    case TextStudioDiagnosticCode.UnknownTag:
                    {
                        bool rangeLike = HasMatchingClosingTag(report, diagnostic.TagName);
                        status.Add(
                            TextStudioHealthSeverity.Warning,
                            rangeLike ? "Unknown effect tag" : "Unknown cue tag",
                            scope + " uses unknown tag <" + diagnostic.TagName + ">.",
                            rangeLike
                                ? "Register an effect with this tag id, fix the spelling, or remove the tag."
                                : "Register an action/cue with this tag id, fix the spelling, or remove the tag.",
                            target);
                        break;
                    }
                    case TextStudioDiagnosticCode.ComposedEffectCycle:
                        status.Add(
                            TextStudioHealthSeverity.Error,
                            "Composed effect cycle",
                            scope + ": " + diagnostic.Message,
                            "Remove the recursive composed effect reference.",
                            target);
                        break;
                    case TextStudioDiagnosticCode.InvalidEffectInvocation:
                        status.Add(
                            TextStudioHealthSeverity.Error,
                            "Invalid composed effect invocation",
                            scope + ": " + diagnostic.Message,
                            "Fix the Tag Stack or effect layer.",
                            target);
                        break;
                    case TextStudioDiagnosticCode.MissingReference:
                        status.Add(
                            TextStudioHealthSeverity.Warning,
                            "Unknown cue reference",
                            scope + ": " + diagnostic.Message,
                            "Create the referenced route/marker or update the tag parameter.",
                            target);
                        break;
                }
            }
        }

        static void ValidateInvocationStyle(
            TextStudioHealthStatus status,
            string scope,
            string styleName,
            string style,
            string missingTitle,
            string invalidTitle,
            UnityEngine.Object target,
            string rootEffectId = "")
        {
            if (string.IsNullOrWhiteSpace(style))
                return;

            List<string> invocations = new List<string>(4);
            if (!TextStudioTransitionStyleUtility.TrySplitInvocations(style, invocations, out string error))
            {
                status.Add(
                    TextStudioHealthSeverity.Error,
                    invalidTitle,
                    scope + " " + styleName + ": " + error,
                    "Use effect tags such as <wave><fade min=0.35 phase=active>.",
                    target);
                return;
            }

            ValidateInvocationList(
                status,
                scope,
                styleName,
                invocations,
                missingTitle,
                invalidTitle,
                target,
                rootEffectId);
        }

        static void ValidateInvocationList(
            TextStudioHealthStatus status,
            string scope,
            string listName,
            IReadOnlyList<string> invocations,
            string missingTitle,
            string invalidTitle,
            UnityEngine.Object target,
            string rootEffectId = "",
            IReadOnlyDictionary<string, ITextStudioEffectDefinition> persistedDefinitions = null,
            TextStudioHealthSeverity missingSeverity = TextStudioHealthSeverity.Error)
        {
            if (invocations == null)
                return;

            List<string> stack = new List<string>(8);
            if (!string.IsNullOrEmpty(rootEffectId))
                stack.Add(rootEffectId);

            for (int i = 0; i < invocations.Count; i++)
                ValidateInvocation(
                    status,
                    scope,
                    listName,
                    i,
                    invocations[i],
                    missingTitle,
                    invalidTitle,
                    target,
                    stack,
                    persistedDefinitions,
                    missingSeverity);
        }

        static void ValidateInvocation(
            TextStudioHealthStatus status,
            string scope,
            string listName,
            int index,
            string source,
            string missingTitle,
            string invalidTitle,
            UnityEngine.Object target,
            List<string> stack,
            IReadOnlyDictionary<string, ITextStudioEffectDefinition> persistedDefinitions,
            TextStudioHealthSeverity missingSeverity)
        {
            if (!TextStudioEffectInvocationParser.TryParse(
                    source,
                    out TextStudioEffectInvocation invocation,
                    out string error))
            {
                status.Add(
                    TextStudioHealthSeverity.Error,
                    invalidTitle,
                    scope + " " + listName + " item " + (index + 1) + ": " + error,
                    "Use a single effect invocation such as <wave> or <fade min=0.35 phase=active>.",
                    target);
                return;
            }

            ValidateEffectReference(
                status,
                scope,
                listName,
                invocation.EffectId,
                missingTitle,
                invalidTitle,
                target,
                stack,
                persistedDefinitions,
                missingSeverity);
        }

        static void ValidateEffectReference(
            TextStudioHealthStatus status,
            string scope,
            string listName,
            string effectId,
            string missingTitle,
            string invalidTitle,
            UnityEngine.Object target,
            List<string> stack,
            IReadOnlyDictionary<string, ITextStudioEffectDefinition> persistedDefinitions,
            TextStudioHealthSeverity missingSeverity)
        {
            string canonicalId = NormalizeEffectId(effectId);
            if (string.IsNullOrEmpty(canonicalId))
            {
                status.Add(
                    TextStudioHealthSeverity.Error,
                    missingTitle,
                    scope + " " + listName + " references an empty effect id.",
                    "Choose a registered effect tag.",
                    target);
                return;
            }

            if (ContainsEffectId(stack, canonicalId))
            {
                status.Add(
                    TextStudioHealthSeverity.Error,
                    "Composed effect cycle",
                    scope + " " + listName + " resolves back to <" + canonicalId + ">.",
                    "Remove the recursive composed effect reference.",
                    target);
                return;
            }

            ITextStudioEffectDefinition effectDefinition;
            if (!TextStudioRegistry.Effects.TryGetQuiet(canonicalId, out effectDefinition) &&
                (persistedDefinitions == null ||
                 !persistedDefinitions.TryGetValue(canonicalId, out effectDefinition)))
            {
                status.Add(
                    missingSeverity,
                    missingTitle,
                    scope + " " + listName + " references missing effect <" + canonicalId + ">.",
                    "Register the effect, add it to an effect database, or fix the tag id.",
                    target);
                return;
            }

            if (effectDefinition is not TextStudioComposedEffectAsset composed)
                return;

            stack.Add(canonicalId);
            List<string> invocations = new List<string>(8);
            if (!composed.TryGetAllEffectInvocations(invocations, out string error))
            {
                status.Add(
                    TextStudioHealthSeverity.Error,
                    invalidTitle,
                    scope + " composed <" + canonicalId + ">: " + error,
                    "Fix the Tag Stack or effect layer.",
                    target);
                stack.RemoveAt(stack.Count - 1);
                return;
            }
            for (int i = 0; i < invocations.Count; i++)
                ValidateInvocation(
                    status,
                    scope,
                    "composed <" + canonicalId + ">",
                    i,
                    invocations[i],
                    missingTitle,
                    invalidTitle,
                    target,
                    stack,
                    persistedDefinitions,
                    missingSeverity);
            stack.RemoveAt(stack.Count - 1);
        }

        static void ReportConstantDefaultOverlap(
            TextStudioHealthStatus status,
            string source,
            TextStudioBehaviorProfile profile,
            string scope,
            UnityEngine.Object target)
        {
            if (profile == null ||
                !profile.Provides(TextStudioBehaviorDomain.DefaultEffects) ||
                profile.DefaultEffectMode != TextStudioDefaultEffectMode.Constant ||
                string.IsNullOrEmpty(source))
            {
                return;
            }

            TextStudioCompiledPlan plan = BuildPlanWithoutProfile(source);
            if (plan == null || plan.VisibleGlyphCount <= 0)
                return;

            if (((HasInvocationForPhase(profile.DefaultEffects, TextStudioPhaseMask.Reveal) ||
                  HasInvocationForPhase(profile.DefaultRevealEffects, TextStudioPhaseMask.Reveal)) &&
                 ExplicitEffectsCoverAllGlyphs(plan, TextStudioPhaseMask.Reveal)) ||
                ((HasInvocationForPhase(profile.DefaultEffects, TextStudioPhaseMask.Active) ||
                  HasInvocationForPhase(profile.DefaultActiveEffects, TextStudioPhaseMask.Active)) &&
                 ExplicitEffectsCoverAllGlyphs(plan, TextStudioPhaseMask.Active)) ||
                ((HasInvocationForPhase(profile.DefaultEffects, TextStudioPhaseMask.Hide) ||
                  HasInvocationForPhase(profile.DefaultHideEffects, TextStudioPhaseMask.Hide)) &&
                 ExplicitEffectsCoverAllGlyphs(plan, TextStudioPhaseMask.Hide)))
            {
                status.Add(
                    TextStudioHealthSeverity.Info,
                    "Constant default effects overlap explicit tags",
                    scope + " uses Constant profile defaults while explicit effect tags already cover the same text.",
                    "This is valid, but Fallback defaults may be easier to reason about for large text sets.",
                    target);
            }
        }

        static TextStudioCompiledPlan BuildPlanWithoutProfile(string source)
        {
            StringTextSource textSource = new StringTextSource(source ?? string.Empty);
            TextStudioPipeline pipeline = new TextStudioPipeline();
            pipeline.Process(textSource);
            return pipeline.Plan;
        }

        static bool ExplicitEffectsCoverAllGlyphs(TextStudioCompiledPlan plan, TextStudioPhaseMask phaseMask)
        {
            if (plan == null || plan.VisibleGlyphCount <= 0 || plan.EffectBindingCount <= 0)
                return false;

            bool[] covered = new bool[plan.VisibleGlyphCount];
            for (int i = 0; i < plan.EffectBindingCount; i++)
            {
                EffectBinding binding = plan.EffectBindings[i];
                TextStudioPhaseMask bindingMask = binding.phaseMask == TextStudioPhaseMask.None
                    ? TextStudioPhaseMask.All
                    : binding.phaseMask;
                if ((bindingMask & phaseMask) == TextStudioPhaseMask.None)
                    continue;

                int start = Mathf.Clamp(binding.glyphStart, 0, plan.VisibleGlyphCount);
                int end = Mathf.Clamp(binding.glyphStart + binding.glyphCount, 0, plan.VisibleGlyphCount);
                for (int glyph = start; glyph < end; glyph++)
                    covered[glyph] = true;
            }

            for (int i = 0; i < covered.Length; i++)
            {
                if (!covered[i])
                    return false;
            }

            return true;
        }

        static bool HasInvocationForPhase(string style, TextStudioPhaseMask phaseMask)
        {
            if (string.IsNullOrWhiteSpace(style))
                return false;

            List<string> invocations = new List<string>(4);
            if (!TextStudioTransitionStyleUtility.TrySplitInvocations(style, invocations, out _))
                return false;

            for (int i = 0; i < invocations.Count; i++)
            {
                if (!TextStudioEffectInvocationParser.TryParse(
                        invocations[i],
                        out TextStudioEffectInvocation invocation,
                        out _))
                {
                    continue;
                }

                if ((NormalizePhaseMask(invocation.PhaseMask) & phaseMask) != 0)
                    return true;
            }

            return false;
        }

        static TextStudioPhaseMask NormalizePhaseMask(TextStudioPhaseMask phaseMask)
        {
            return phaseMask == TextStudioPhaseMask.None ? TextStudioPhaseMask.All : phaseMask;
        }

        static bool HasMatchingClosingTag(TextStudioDiagnosticReport report, string tagName)
        {
            if (report == null || string.IsNullOrEmpty(tagName))
                return false;

            for (int i = 0; i < report.Tags.Length; i++)
            {
                TextStudioTagDiagnostic tag = report.Tags[i];
                if (tag.IsClosing && string.Equals(tag.Name, tagName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        static string NormalizeEffectId(string effectId)
        {
            if (string.IsNullOrWhiteSpace(effectId))
                return string.Empty;

            string trimmed = effectId.Trim();
            return TextStudioPhaseTagUtility.TryResolveEffectId(trimmed, out string canonicalId)
                ? canonicalId
                : trimmed;
        }

        static bool ContainsEffectId(IReadOnlyList<string> stack, string effectId)
        {
            if (stack == null || string.IsNullOrEmpty(effectId))
                return false;

            for (int i = 0; i < stack.Count; i++)
            {
                if (string.Equals(stack[i], effectId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        static void LogItem(TextStudioHealthItem item)
        {
            string message = "[Text Studio Health] " + item.Title + ": " + item.Detail;
            switch (item.Severity)
            {
                case TextStudioHealthSeverity.Error:
                    Debug.LogError(message);
                    break;
                case TextStudioHealthSeverity.Warning:
                    Debug.LogWarning(message);
                    break;
                case TextStudioHealthSeverity.Info:
                default:
                    Debug.Log(message);
                    break;
            }
        }

    }
}
