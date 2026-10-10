using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using TextStudio.BuiltIns;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    internal enum TextAnimatorMigrationArtifactKind
    {
        TextAnimatorComponent,
        TypewriterComponent,
        EffectDatabase,
        ActionDatabase,
        PlaybackDatabase,
        StyleSheet,
        Effect,
        Action,
        Playback,
        Style,
        AnimatorSettings,
        TypewriterSettings,
        GlobalSettings,
        TimingProfile,
        SoundWriterComponent,
        TypewriterEventComponent,
        OtherSettings,
        Other
    }

    internal enum TextAnimatorMigrationSourceImpact
    {
        Supported,
        Ready,
        NeedsReview
    }

    internal sealed class TextAnimatorMigrationSourceRecord
    {
        public TextAnimatorMigrationCandidate Candidate { get; }
        public TextAnimatorMigrationResult Result { get; }
        public int Index { get; }
        public string ScenePath { get; }
        public string ObjectPath { get; }
        public string ComponentType { get; }
        public TextAnimatorMigrationSourceImpact Impact { get; }
        public bool HasIssues => Result.ManualCount > 0 || Result.UnsupportedCount > 0;
        public bool HasChanges => Result.HasChanges;
        public bool CanApply => Candidate != null && Candidate.CanApply && HasChanges && !HasIssues;
        public string DisplayName => BuildDisplayName();

        public TextAnimatorMigrationSourceRecord(
            TextAnimatorMigrationCandidate candidate,
            TextAnimatorMigrationResult result,
            int index,
            string scenePath,
            string objectPath,
            string componentType)
        {
            Candidate = candidate;
            Result = result;
            Index = Mathf.Max(0, index);
            ScenePath = scenePath ?? string.Empty;
            ObjectPath = objectPath ?? string.Empty;
            ComponentType = componentType ?? string.Empty;

            if (HasIssues)
                Impact = TextAnimatorMigrationSourceImpact.NeedsReview;
            else if (result != null && result.HasChanges)
                Impact = TextAnimatorMigrationSourceImpact.Ready;
            else
                Impact = TextAnimatorMigrationSourceImpact.Supported;
        }

        string BuildDisplayName()
        {
            string label = Candidate != null && !string.IsNullOrEmpty(Candidate.Label)
                ? Candidate.Label
                : "Text Source";
            if (!string.IsNullOrEmpty(ObjectPath))
                return ObjectPath + " / " + label;
            return label;
        }
    }

    internal sealed class TextAnimatorMigrationIssueRecord
    {
        public TextAnimatorMigrationSourceRecord Source { get; }
        public TextAnimatorMigrationIssue Issue { get; }

        public TextAnimatorMigrationIssueRecord(TextAnimatorMigrationSourceRecord source, TextAnimatorMigrationIssue issue)
        {
            Source = source;
            Issue = issue;
        }
    }

    internal sealed class TextAnimatorMigrationArtifactRecord
    {
        public TextAnimatorMigrationArtifactKind Kind { get; }
        public string Label { get; }
        public string TagId { get; }
        public string AssetPath { get; }
        public TextAnimatorMigrationSourceKind SourceKind { get; }
        public Object Target { get; }
        public string Note { get; }
        public int UsedSourceCount { get; }
        public int TagUsageCount { get; }
        public bool RequiresCustomReview { get; }

        /// <summary>Number of serialized occurrences represented by this record, such as components in a closed scene.</summary>
        public int OccurrenceCount { get; }

        public TextAnimatorMigrationArtifactRecord(
            TextAnimatorMigrationArtifactKind kind,
            string label,
            string tagId,
            string assetPath,
            TextAnimatorMigrationSourceKind sourceKind,
            Object target,
            string note,
            int occurrenceCount = 1)
            : this(kind, label, tagId, assetPath, sourceKind, target, note, 0, 0, true, occurrenceCount)
        {
        }

        TextAnimatorMigrationArtifactRecord(
            TextAnimatorMigrationArtifactKind kind,
            string label,
            string tagId,
            string assetPath,
            TextAnimatorMigrationSourceKind sourceKind,
            Object target,
            string note,
            int usedSourceCount,
            int tagUsageCount,
            bool requiresCustomReview,
            int occurrenceCount)
        {
            Kind = kind;
            Label = label ?? string.Empty;
            TagId = tagId ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            SourceKind = sourceKind;
            Target = target;
            Note = note ?? string.Empty;
            UsedSourceCount = Mathf.Max(0, usedSourceCount);
            TagUsageCount = Mathf.Max(0, tagUsageCount);
            RequiresCustomReview = requiresCustomReview;
            OccurrenceCount = Mathf.Max(1, occurrenceCount);
        }

        public TextAnimatorMigrationArtifactRecord WithUsage(int usedSourceCount, int tagUsageCount)
        {
            return WithUsage(usedSourceCount, tagUsageCount, RequiresCustomReview);
        }

        public TextAnimatorMigrationArtifactRecord WithUsage(
            int usedSourceCount,
            int tagUsageCount,
            bool requiresCustomReview)
        {
            return new TextAnimatorMigrationArtifactRecord(
                Kind,
                Label,
                TagId,
                AssetPath,
                SourceKind,
                Target,
                Note,
                usedSourceCount,
                tagUsageCount,
                requiresCustomReview,
                OccurrenceCount);
        }
    }

    internal sealed class TextAnimatorMigrationInventory
    {
        readonly TextAnimatorMigrationSourceRecord[] _sources;
        readonly TextAnimatorMigrationIssueRecord[] _issues;
        readonly TextAnimatorMigrationArtifactRecord[] _artifacts;

        public IReadOnlyList<TextAnimatorMigrationSourceRecord> Sources => _sources;
        public IReadOnlyList<TextAnimatorMigrationIssueRecord> Issues => _issues;
        public IReadOnlyList<TextAnimatorMigrationArtifactRecord> Artifacts => _artifacts;
        public int SourceCount => _sources.Length;
        public int IssueCount => _issues.Length;
        public int ArtifactCount => _artifacts.Length;
        public int ReadySourceCount { get; }
        public int SupportedSourceCount { get; }
        public int IssueSourceCount { get; }
        public int AutomaticallyHandledSourceCount => ReadySourceCount + SupportedSourceCount;
        public int SceneCount { get; }
        public int PrefabCount { get; }
        public int ComponentCount { get; }
        public int TextAnimatorComponentCount { get; }
        public int ManualIssueCount { get; }
        public int UnsupportedIssueCount { get; }
        public int ConvertedTagCount { get; }
        public int CoveredTagCount { get; }
        public int CustomArtifactCount { get; }
        public int UsedCustomArtifactCount { get; }
        public int CompanionArtifactCount { get; }

        public TextAnimatorMigrationInventory(
            IReadOnlyList<TextAnimatorMigrationSourceRecord> sources,
            IReadOnlyList<TextAnimatorMigrationIssueRecord> issues,
            IReadOnlyList<TextAnimatorMigrationArtifactRecord> artifacts)
        {
            _sources = Copy(sources);
            _issues = Copy(issues);
            _artifacts = Copy(artifacts);

            HashSet<string> scenes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> prefabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<int> components = new HashSet<int>();
            HashSet<int> textAnimatorComponents = new HashSet<int>();

            for (int i = 0; i < _sources.Length; i++)
            {
                TextAnimatorMigrationSourceRecord source = _sources[i];
                if (source == null)
                    continue;

                switch (source.Impact)
                {
                    case TextAnimatorMigrationSourceImpact.Ready:
                        ReadySourceCount++;
                        break;
                    case TextAnimatorMigrationSourceImpact.Supported:
                        SupportedSourceCount++;
                        break;
                    case TextAnimatorMigrationSourceImpact.NeedsReview:
                        IssueSourceCount++;
                        break;
                }

                if (source.Result != null)
                {
                    ConvertedTagCount += source.Result.ConvertedCount;
                    CoveredTagCount += source.Result.CoveredCount;
                }

                string scenePath = !string.IsNullOrEmpty(source.ScenePath)
                    ? source.ScenePath
                    : GetScenePath(source.Candidate);
                if (!string.IsNullOrEmpty(scenePath))
                    scenes.Add(scenePath);

                string prefabPath = GetPrefabPath(source.Candidate);
                if (!string.IsNullOrEmpty(prefabPath))
                    prefabs.Add(prefabPath);

                Component component = source.Candidate != null ? source.Candidate.Target as Component : null;
                if (component != null)
                    components.Add(component.GetStableId());
            }

            for (int i = 0; i < _issues.Length; i++)
            {
                if (_issues[i].Issue.Status == TextAnimatorMigrationStatus.Manual)
                    ManualIssueCount++;
                else if (_issues[i].Issue.Status == TextAnimatorMigrationStatus.Unsupported)
                    UnsupportedIssueCount++;
            }

            for (int i = 0; i < _artifacts.Length; i++)
            {
                TextAnimatorMigrationArtifactRecord artifact = _artifacts[i];
                if (artifact == null)
                    continue;

                if (artifact.Kind == TextAnimatorMigrationArtifactKind.TextAnimatorComponent ||
                    artifact.Kind == TextAnimatorMigrationArtifactKind.TypewriterComponent ||
                    artifact.Kind == TextAnimatorMigrationArtifactKind.SoundWriterComponent ||
                    artifact.Kind == TextAnimatorMigrationArtifactKind.TypewriterEventComponent)
                {
                    if (artifact.Target != null)
                        textAnimatorComponents.Add(artifact.Target.GetStableId());
                    else
                        TextAnimatorComponentCount += artifact.OccurrenceCount;
                }

                if (TextAnimatorMigrationCompanionUtility.IsCompanionArtifact(artifact.Kind))
                    CompanionArtifactCount++;

                if (IsCustomArtifact(artifact))
                {
                    CustomArtifactCount++;
                    if (artifact.UsedSourceCount > 0)
                        UsedCustomArtifactCount++;
                }
            }

            SceneCount = scenes.Count;
            PrefabCount = prefabs.Count;
            ComponentCount = components.Count;
            TextAnimatorComponentCount += textAnimatorComponents.Count;
        }

        static string GetScenePath(TextAnimatorMigrationCandidate candidate)
        {
            if (candidate == null)
                return string.Empty;

            if (candidate.SourceKind == TextAnimatorMigrationSourceKind.Scene &&
                candidate.AssetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                return candidate.AssetPath;
            }

            return string.Empty;
        }

        static string GetPrefabPath(TextAnimatorMigrationCandidate candidate)
        {
            if (candidate == null)
                return string.Empty;

            if (candidate.SourceKind == TextAnimatorMigrationSourceKind.Prefab &&
                candidate.AssetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                return candidate.AssetPath;
            }

            return string.Empty;
        }

        static bool IsCustomArtifact(TextAnimatorMigrationArtifactRecord artifact)
        {
            return artifact != null &&
                   artifact.RequiresCustomReview &&
                   artifact.Kind != TextAnimatorMigrationArtifactKind.TextAnimatorComponent &&
                   artifact.Kind != TextAnimatorMigrationArtifactKind.TypewriterComponent &&
                   !TextAnimatorMigrationCompanionUtility.IsCompanionArtifact(artifact.Kind);
        }

        static TextAnimatorMigrationSourceRecord[] Copy(IReadOnlyList<TextAnimatorMigrationSourceRecord> sources)
        {
            if (sources == null || sources.Count == 0)
                return Array.Empty<TextAnimatorMigrationSourceRecord>();

            TextAnimatorMigrationSourceRecord[] copy = new TextAnimatorMigrationSourceRecord[sources.Count];
            for (int i = 0; i < sources.Count; i++)
                copy[i] = sources[i];
            return copy;
        }

        static TextAnimatorMigrationIssueRecord[] Copy(IReadOnlyList<TextAnimatorMigrationIssueRecord> issues)
        {
            if (issues == null || issues.Count == 0)
                return Array.Empty<TextAnimatorMigrationIssueRecord>();

            TextAnimatorMigrationIssueRecord[] copy = new TextAnimatorMigrationIssueRecord[issues.Count];
            for (int i = 0; i < issues.Count; i++)
                copy[i] = issues[i];
            return copy;
        }

        static TextAnimatorMigrationArtifactRecord[] Copy(IReadOnlyList<TextAnimatorMigrationArtifactRecord> artifacts)
        {
            if (artifacts == null || artifacts.Count == 0)
                return Array.Empty<TextAnimatorMigrationArtifactRecord>();

            TextAnimatorMigrationArtifactRecord[] copy = new TextAnimatorMigrationArtifactRecord[artifacts.Count];
            for (int i = 0; i < artifacts.Count; i++)
                copy[i] = artifacts[i];
            return copy;
        }
    }

#if UNITY_6000_7_OR_NEWER
    // The shared empty inventory is an immutable sentinel with no play-session state.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextAnimatorMigrationInventoryBuilder
    {
        sealed class TagUsage
        {
            readonly HashSet<int> _sourceIndexes = new HashSet<int>();

            public int SourceCount => _sourceIndexes.Count;
            public int TokenCount { get; private set; }

            public void Add(int sourceIndex)
            {
                _sourceIndexes.Add(sourceIndex);
                TokenCount++;
            }
        }

        public static TextAnimatorMigrationInventory Empty { get; } =
            new TextAnimatorMigrationInventory(
                Array.Empty<TextAnimatorMigrationSourceRecord>(),
                Array.Empty<TextAnimatorMigrationIssueRecord>(),
                Array.Empty<TextAnimatorMigrationArtifactRecord>());

        public static TextAnimatorMigrationInventory Build(
            IReadOnlyList<TextAnimatorMigrationCandidate> candidates,
            IReadOnlyList<TextAnimatorMigrationArtifactRecord> artifacts,
            TextAnimatorMigrationStyleSheet styleSheet)
        {
            styleSheet ??= TextAnimatorMigrationStyleSheet.Empty;
            List<TextAnimatorMigrationSourceRecord> sources = new List<TextAnimatorMigrationSourceRecord>();
            List<TextAnimatorMigrationIssueRecord> issues = new List<TextAnimatorMigrationIssueRecord>();
            Dictionary<string, TagUsage> usage = new Dictionary<string, TagUsage>(StringComparer.OrdinalIgnoreCase);

            if (candidates != null)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    TextAnimatorMigrationCandidate candidate = candidates[i];
                    TextAnimatorMigrationResult result = TextAnimatorMigrationAnalyzer.Analyze(
                        candidate != null ? candidate.SourceMarkup : string.Empty,
                        styleSheet);
                    TextAnimatorMigrationSourceRecord source = new TextAnimatorMigrationSourceRecord(
                        candidate,
                        result,
                        sources.Count,
                        ResolveScenePath(candidate),
                        ResolveObjectPath(candidate),
                        ResolveComponentType(candidate));
                    sources.Add(source);

                    for (int issueIndex = 0; issueIndex < result.Issues.Count; issueIndex++)
                    {
                        TextAnimatorMigrationIssue issue = result.Issues[issueIndex];
                        if (issue.Status == TextAnimatorMigrationStatus.Manual ||
                            issue.Status == TextAnimatorMigrationStatus.Unsupported)
                        {
                            issues.Add(new TextAnimatorMigrationIssueRecord(source, issue));
                        }
                    }

                    AddTagUsage(result.SourceMarkup, source.Index, usage);
                }
            }

            List<TextAnimatorMigrationArtifactRecord> resolvedArtifacts = new List<TextAnimatorMigrationArtifactRecord>();
            if (artifacts != null)
            {
                for (int i = 0; i < artifacts.Count; i++)
                {
                    TextAnimatorMigrationArtifactRecord artifact = artifacts[i];
                    if (artifact == null)
                        continue;

                    bool requiresCustomReview = RequiresCustomReview(artifact, styleSheet);
                    int usedSourceCount = 0;
                    int tokenUsageCount = 0;
                    if (requiresCustomReview &&
                        !string.IsNullOrWhiteSpace(artifact.TagId) &&
                        usage.TryGetValue(artifact.TagId.Trim(), out TagUsage tagUsage))
                    {
                        usedSourceCount = tagUsage.SourceCount;
                        tokenUsageCount = tagUsage.TokenCount;
                    }

                    resolvedArtifacts.Add(artifact.WithUsage(usedSourceCount, tokenUsageCount, requiresCustomReview));
                }
            }

            return new TextAnimatorMigrationInventory(sources, issues, resolvedArtifacts);
        }

        static bool RequiresCustomReview(
            TextAnimatorMigrationArtifactRecord artifact,
            TextAnimatorMigrationStyleSheet styleSheet)
        {
            if (artifact == null)
                return false;

            if (artifact.Kind == TextAnimatorMigrationArtifactKind.TextAnimatorComponent ||
                artifact.Kind == TextAnimatorMigrationArtifactKind.TypewriterComponent ||
                TextAnimatorMigrationCompanionUtility.IsCompanionArtifact(artifact.Kind))
            {
                return false;
            }

            string tagId = artifact.TagId;
            if (string.IsNullOrWhiteSpace(tagId))
                return true;

            string trimmedTagId = tagId.Trim();
            if (styleSheet != null && styleSheet.TryGet(trimmedTagId, out _))
                return false;

            if (TextAnimatorMigrationRules.TryGet(trimmedTagId, out TextAnimatorMigrationRule rule) &&
                rule.Status == TextAnimatorMigrationStatus.Covered)
            {
                return false;
            }

            if (BuiltInActions.TryGetCanonicalActionId(trimmedTagId, out _))
                return false;

            if (TextStudioPhaseTagUtility.TryGetPhase(trimmedTagId, out _))
                return false;

            // Imported Playbacks, registered style aliases and project actions already carry this tag in Text Studio.
            if (TextAnimatorMigrationRules.TryGetRegisteredCoverage(trimmedTagId, out _))
                return false;

            return true;
        }

        static void AddTagUsage(string sourceMarkup, int sourceIndex, Dictionary<string, TagUsage> usage)
        {
            IReadOnlyList<TextAnimatorMarkupToken> tokens = TextAnimatorMarkupScanner.Scan(sourceMarkup);
            for (int i = 0; i < tokens.Count; i++)
            {
                TextAnimatorMarkupToken token = tokens[i];
                AddTag(token.Name, sourceIndex, usage);
                AddParameterUsage(token.Name, token.RawParameter, sourceIndex, usage);
            }
        }

        static void AddParameterUsage(
            string tagName,
            string rawParameter,
            int sourceIndex,
            Dictionary<string, TagUsage> usage)
        {
            if (string.IsNullOrWhiteSpace(rawParameter))
                return;

            bool allowNumericModifiers = TextStudioPhaseTagUtility.TryResolveEffectId(tagName, out string effectId) &&
                                         TextStudioRegistry.Effects.TryGetQuiet(effectId, out ITextStudioEffectDefinition effect) &&
                                         effect is ITextStudioEffectFactory;
            TextStudioTagParameters parameters = TextStudioTagParameterParser.Parse(
                rawParameter,
                allowNumericModifiers);
            for (int i = 0; i < parameters.PositionalCount; i++)
                AddTag(parameters.GetString(i), sourceIndex, usage);

            for (int i = 0; i < parameters.NamedCount; i++)
            {
                TextStudioNamedTagParameter parameter = parameters.Named[i];
                if (parameter.Operation == TextStudioTagParameterOperation.Multiply)
                    continue;

                if (parameter.IsFlag)
                    AddTag(parameter.Name, sourceIndex, usage);
                else
                    AddTag(parameter.Value, sourceIndex, usage);
            }
        }

        static void AddTag(string tag, int sourceIndex, Dictionary<string, TagUsage> usage)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return;

            string normalized = tag.Trim();
            if (!usage.TryGetValue(normalized, out TagUsage tagUsage))
            {
                tagUsage = new TagUsage();
                usage.Add(normalized, tagUsage);
            }

            tagUsage.Add(sourceIndex);
        }

        static string ResolveScenePath(TextAnimatorMigrationCandidate candidate)
        {
            Component component = candidate != null ? candidate.Target as Component : null;
            if (component != null && component.gameObject.scene.IsValid())
                return component.gameObject.scene.path ?? string.Empty;

            if (candidate != null &&
                candidate.SourceKind == TextAnimatorMigrationSourceKind.Scene &&
                candidate.AssetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                return candidate.AssetPath;
            }

            return string.Empty;
        }

        static string ResolveObjectPath(TextAnimatorMigrationCandidate candidate)
        {
            Component component = candidate != null ? candidate.Target as Component : null;
            if (component == null)
                return string.Empty;

            return BuildTransformPath(component.transform);
        }

        static string ResolveComponentType(TextAnimatorMigrationCandidate candidate)
        {
            Object target = candidate != null ? candidate.Target : null;
            return target != null ? target.GetType().Name : string.Empty;
        }

        static string BuildTransformPath(Transform transform)
        {
            if (transform == null)
                return string.Empty;

            List<string> names = new List<string>();
            Transform cursor = transform;
            while (cursor != null)
            {
                names.Add(cursor.name);
                cursor = cursor.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }
    }
}
