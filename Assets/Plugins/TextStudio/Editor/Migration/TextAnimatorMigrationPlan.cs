using System;
using System.Collections.Generic;
using System.IO;
using ImpossibleRobert.Common;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    /// <summary>One-click conversions, in the order the wizard presents them.</summary>
    internal enum TextAnimatorMigrationStepKind
    {
        AddTextStudio,
        SoundsAndEvents,
        TypingSpeed,
        Playbacks,
        StyleAliases
    }

    /// <summary>Work the wizard explains but leaves to the user.</summary>
    internal enum TextAnimatorMigrationTopicKind
    {
        CustomTags,
        Scripts,
        EventListeners,
        UnsupportedPlaybacks,
        MarkupChecks
    }

    internal enum TextAnimatorMigrationItemState
    {
        Ready,
        OpenScene,
        Blocked,
        Done,
        NeedsYou,
        WorksAsIs
    }

    internal sealed class TextAnimatorMigrationPlanItem
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public TextAnimatorMigrationItemState State { get; set; }

        /// <summary>Number of texts, components or references the item stands for.</summary>
        public int Count { get; set; } = 1;

        public Object SelectTarget { get; set; }
        public string OpenPath { get; set; } = string.Empty;
        public int OpenLine { get; set; }

        public GameObject GameObject { get; set; }
        public TMP_Text Text { get; set; }
        public Component SoundWriter { get; set; }
        public Component Typewriter { get; set; }
        public bool HasEvents { get; set; }
        public int EventListenerCount { get; set; }
        public int ManualListenerCount { get; set; }
        public Object Source { get; set; }
        public string TagId { get; set; } = string.Empty;
        public string StyleSheetPath { get; set; } = string.Empty;
    }

    internal sealed class TextAnimatorMigrationPlanStep
    {
        readonly List<TextAnimatorMigrationPlanItem> _items = new List<TextAnimatorMigrationPlanItem>();

        public TextAnimatorMigrationPlanStep(TextAnimatorMigrationStepKind kind)
        {
            Kind = kind;
        }

        public TextAnimatorMigrationStepKind Kind { get; }
        public IReadOnlyList<TextAnimatorMigrationPlanItem> Items => _items;
        public int ReadyCount { get; private set; }
        public int DoneCount { get; private set; }
        public int BlockedCount { get; private set; }

        /// <summary>Objects waiting in scenes that are not open.</summary>
        public int ClosedSceneObjectCount { get; private set; }

        public int ClosedSceneCount { get; private set; }
        public bool IsApplicable => _items.Count > 0;
        public bool IsComplete => IsApplicable && ReadyCount == 0 && ClosedSceneCount == 0 && BlockedCount == 0;

        internal void Add(TextAnimatorMigrationPlanItem item)
        {
            _items.Add(item);
        }

        internal void Complete()
        {
            _items.Sort(CompareItems);
            ReadyCount = 0;
            DoneCount = 0;
            BlockedCount = 0;
            ClosedSceneObjectCount = 0;
            ClosedSceneCount = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                TextAnimatorMigrationPlanItem item = _items[i];
                switch (item.State)
                {
                    case TextAnimatorMigrationItemState.Ready:
                        ReadyCount++;
                        break;
                    case TextAnimatorMigrationItemState.Done:
                        DoneCount++;
                        break;
                    case TextAnimatorMigrationItemState.Blocked:
                        BlockedCount++;
                        break;
                    case TextAnimatorMigrationItemState.OpenScene:
                        ClosedSceneCount++;
                        ClosedSceneObjectCount += item.Count;
                        break;
                }
            }
        }

        internal static int CompareItems(TextAnimatorMigrationPlanItem first, TextAnimatorMigrationPlanItem second)
        {
            int state = first.State.CompareTo(second.State);
            if (state != 0)
                return state;

            int location = string.Compare(first.Location, second.Location, StringComparison.OrdinalIgnoreCase);
            return location != 0
                ? location
                : string.Compare(first.Title, second.Title, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class TextAnimatorMigrationPlanTopic
    {
        readonly List<TextAnimatorMigrationPlanItem> _items = new List<TextAnimatorMigrationPlanItem>();

        public TextAnimatorMigrationPlanTopic(TextAnimatorMigrationTopicKind kind)
        {
            Kind = kind;
        }

        public TextAnimatorMigrationTopicKind Kind { get; }
        public IReadOnlyList<TextAnimatorMigrationPlanItem> Items => _items;
        public bool IsApplicable => _items.Count > 0;

        internal void Add(TextAnimatorMigrationPlanItem item)
        {
            _items.Add(item);
        }

        internal void Complete()
        {
            _items.Sort(TextAnimatorMigrationPlanStep.CompareItems);
        }
    }

    /// <summary>
    /// Presentation model for the Text Animator migration wizard. It is rebuilt from a scan plus live project state,
    /// so completed steps appear as done without another project scan.
    /// </summary>
    internal sealed class TextAnimatorMigrationPlan
    {
        readonly List<TextAnimatorMigrationPlanStep> _steps = new List<TextAnimatorMigrationPlanStep>();
        readonly List<TextAnimatorMigrationPlanTopic> _topics = new List<TextAnimatorMigrationPlanTopic>();
        readonly List<TextAnimatorMigrationPlanItem> _workingTexts = new List<TextAnimatorMigrationPlanItem>();
        readonly List<TextAnimatorMigrationPlanItem> _workingAssets = new List<TextAnimatorMigrationPlanItem>();
        readonly List<Component> _sceneTextAnimatorComponents = new List<Component>();
        readonly HashSet<string> _textAnimatorLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        TextAnimatorMigrationPlan(TextAnimatorMigrationInventory inventory)
        {
            Inventory = inventory ?? TextAnimatorMigrationInventoryBuilder.Empty;
            foreach (TextAnimatorMigrationStepKind kind in Enum.GetValues(typeof(TextAnimatorMigrationStepKind)))
                _steps.Add(new TextAnimatorMigrationPlanStep(kind));
            foreach (TextAnimatorMigrationTopicKind kind in Enum.GetValues(typeof(TextAnimatorMigrationTopicKind)))
                _topics.Add(new TextAnimatorMigrationPlanTopic(kind));
        }

        public static TextAnimatorMigrationPlan Empty => new TextAnimatorMigrationPlan(null);

        public TextAnimatorMigrationInventory Inventory { get; }
        public IReadOnlyList<TextAnimatorMigrationPlanStep> Steps => _steps;
        public IReadOnlyList<TextAnimatorMigrationPlanTopic> Topics => _topics;

        /// <summary>Texts whose markup Text Studio reads without changes.</summary>
        public IReadOnlyList<TextAnimatorMigrationPlanItem> WorkingTexts => _workingTexts;

        /// <summary>Text Animator assets that need no conversion: built-in equivalents, unused custom assets and setup assets.</summary>
        public IReadOnlyList<TextAnimatorMigrationPlanItem> WorkingAssets => _workingAssets;

        /// <summary>Text Animator components in open scenes, for the final clean-up.</summary>
        public IReadOnlyList<Component> SceneTextAnimatorComponents => _sceneTextAnimatorComponents;

        public int TextCount { get; private set; }
        public int BuiltInEquivalentCount { get; private set; }
        public int UnusedAssetCount { get; private set; }
        public int SetupAssetCount { get; private set; }
        public int ExcludedSampleCount { get; private set; }

        /// <summary>Scenes and prefabs that still contain Text Animator components.</summary>
        public int TextAnimatorLocationCount => _textAnimatorLocations.Count;

        public int ReadyCount { get; private set; }
        public int DoneCount { get; private set; }
        public int ClosedSceneCount { get; private set; }
        public int BlockedCount { get; private set; }
        public int NeedsYouCount { get; private set; }

        /// <summary>True when scenes, prefabs, text or scripts use Text Animator; setup assets alone do not count.</summary>
        public bool HasWork => TextCount > 0 || ReadyCount + DoneCount + ClosedSceneCount + BlockedCount + NeedsYouCount > 0;

        public TextAnimatorMigrationPlanStep GetStep(TextAnimatorMigrationStepKind kind) => _steps[(int)kind];
        public TextAnimatorMigrationPlanTopic GetTopic(TextAnimatorMigrationTopicKind kind) => _topics[(int)kind];

        /// <summary>Builds a plan from a scan, leaving Text Animator's own samples out unless requested.</summary>
        public static TextAnimatorMigrationPlan Build(TextAnimatorMigrationScanResult scan, bool includeSamples)
        {
            if (scan == null)
                return Empty;

            string samplesFolder = TextAnimatorMigrationAvailability.GetSamplesFolder();
            int excluded = 0;
            List<TextAnimatorMigrationCandidate> candidates = new List<TextAnimatorMigrationCandidate>(scan.Candidates.Count);
            for (int i = 0; i < scan.Candidates.Count; i++)
            {
                TextAnimatorMigrationCandidate candidate = scan.Candidates[i];
                if (!includeSamples && candidate != null &&
                    TextAnimatorMigrationAvailability.IsUnderFolder(candidate.AssetPath, samplesFolder))
                {
                    excluded++;
                    continue;
                }

                candidates.Add(candidate);
            }

            List<TextAnimatorMigrationArtifactRecord> artifacts = new List<TextAnimatorMigrationArtifactRecord>(scan.Artifacts.Count);
            for (int i = 0; i < scan.Artifacts.Count; i++)
            {
                TextAnimatorMigrationArtifactRecord artifact = scan.Artifacts[i];
                if (!includeSamples && artifact != null &&
                    TextAnimatorMigrationAvailability.IsUnderFolder(artifact.AssetPath, samplesFolder))
                {
                    excluded++;
                    continue;
                }

                artifacts.Add(artifact);
            }

            TextAnimatorMigrationInventory inventory = TextAnimatorMigrationInventoryBuilder.Build(
                candidates,
                artifacts,
                TextAnimatorMigrationStyleSheet.Empty);
            return Build(inventory, scan.Scripts, excluded);
        }

        /// <summary>Builds a plan from an analyzed inventory and the current editor state.</summary>
        public static TextAnimatorMigrationPlan Build(
            TextAnimatorMigrationInventory inventory,
            IReadOnlyList<TextAnimatorMigrationScriptReference> scripts,
            int excludedSampleCount)
        {
            TextAnimatorMigrationPlan plan = new TextAnimatorMigrationPlan(inventory);
            plan.ExcludedSampleCount = Mathf.Max(0, excludedSampleCount);
            Builder builder = new Builder(plan);
            builder.AddArtifacts();
            builder.AddSources();
            builder.AddScripts(scripts);
            plan.Complete();
            return plan;
        }

        void Complete()
        {
            ReadyCount = 0;
            DoneCount = 0;
            ClosedSceneCount = 0;
            BlockedCount = 0;
            NeedsYouCount = 0;
            for (int i = 0; i < _steps.Count; i++)
            {
                TextAnimatorMigrationPlanStep step = _steps[i];
                step.Complete();
                ReadyCount += step.ReadyCount;
                DoneCount += step.DoneCount;
                ClosedSceneCount += step.ClosedSceneCount;
                BlockedCount += step.BlockedCount;
            }

            for (int i = 0; i < _topics.Count; i++)
            {
                _topics[i].Complete();
                NeedsYouCount += _topics[i].Items.Count;
            }

            _workingTexts.Sort(TextAnimatorMigrationPlanStep.CompareItems);
            _workingAssets.Sort(TextAnimatorMigrationPlanStep.CompareItems);
        }

        sealed class Builder
        {
            readonly TextAnimatorMigrationPlan _plan;
            readonly Dictionary<int, TextAnimatorMigrationPlanItem> _texts = new Dictionary<int, TextAnimatorMigrationPlanItem>();
            readonly Dictionary<int, TextAnimatorMigrationPlanItem> _companions = new Dictionary<int, TextAnimatorMigrationPlanItem>();
            readonly Dictionary<string, TextAnimatorMigrationPlanItem> _closedScenes = new Dictionary<string, TextAnimatorMigrationPlanItem>(StringComparer.OrdinalIgnoreCase);
            readonly Dictionary<string, TextAnimatorMigrationPlanItem> _tagItems = new Dictionary<string, TextAnimatorMigrationPlanItem>(StringComparer.OrdinalIgnoreCase);
            readonly Dictionary<string, TextAnimatorMigrationArtifactRecord> _stylesByTag = new Dictionary<string, TextAnimatorMigrationArtifactRecord>(StringComparer.OrdinalIgnoreCase);
            readonly Dictionary<string, TextAnimatorMigrationArtifactRecord> _customByTag = new Dictionary<string, TextAnimatorMigrationArtifactRecord>(StringComparer.OrdinalIgnoreCase);
            readonly HashSet<string> _playbackTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            readonly HashSet<string> _importedTimingNames;
            readonly string _prefabStagePath;

            public Builder(TextAnimatorMigrationPlan plan)
            {
                _plan = plan;
                _importedTimingNames = FindImportedTimingNames();
                PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
                _prefabStagePath = stage != null ? stage.assetPath : string.Empty;
            }

            TextAnimatorMigrationPlanStep Step(TextAnimatorMigrationStepKind kind) => _plan.GetStep(kind);
            TextAnimatorMigrationPlanTopic Topic(TextAnimatorMigrationTopicKind kind) => _plan.GetTopic(kind);

            public void AddArtifacts()
            {
                IReadOnlyList<TextAnimatorMigrationArtifactRecord> artifacts = _plan.Inventory.Artifacts;
                for (int i = 0; i < artifacts.Count; i++)
                {
                    TextAnimatorMigrationArtifactRecord artifact = artifacts[i];
                    if (artifact == null)
                        continue;

                    switch (artifact.Kind)
                    {
                        case TextAnimatorMigrationArtifactKind.TextAnimatorComponent:
                        case TextAnimatorMigrationArtifactKind.TypewriterComponent:
                            AddTextComponent(artifact);
                            break;
                        case TextAnimatorMigrationArtifactKind.SoundWriterComponent:
                        case TextAnimatorMigrationArtifactKind.TypewriterEventComponent:
                            AddCompanion(artifact);
                            break;
                        case TextAnimatorMigrationArtifactKind.TimingProfile:
                            AddTiming(artifact);
                            break;
                        case TextAnimatorMigrationArtifactKind.Playback:
                            AddPlayback(artifact);
                            break;
                        case TextAnimatorMigrationArtifactKind.Style:
                            AddStyle(artifact);
                            break;
                        case TextAnimatorMigrationArtifactKind.Effect:
                        case TextAnimatorMigrationArtifactKind.Action:
                        case TextAnimatorMigrationArtifactKind.Other:
                            AddTagProvider(artifact);
                            break;
                        default:
                            AddWorkingAsset(artifact, "Text Animator setup asset. Text Studio uses its own Project Settings, so nothing needs converting.");
                            _plan.SetupAssetCount++;
                            break;
                    }
                }
            }

            void AddTextComponent(TextAnimatorMigrationArtifactRecord artifact)
            {
                Component component = artifact.Target as Component;
                if (component == null)
                {
                    if (artifact.Kind == TextAnimatorMigrationArtifactKind.TextAnimatorComponent)
                        AddClosedScene(TextAnimatorMigrationStepKind.AddTextStudio, artifact, "text", "texts");
                    _plan._textAnimatorLocations.Add(artifact.AssetPath);
                    return;
                }

                TrackTextAnimatorComponent(component);
                Component source = ResolveSource(component);
                GameObject gameObject = source != null ? source.gameObject : null;
                TMP_Text text = gameObject != null ? gameObject.GetComponent<TMP_Text>() : null;
                if (text == null)
                    return;

                int key = gameObject.GetStableId();
                if (!_texts.TryGetValue(key, out TextAnimatorMigrationPlanItem item))
                {
                    item = CreateObjectItem(gameObject, "text");
                    item.Text = text;
                    _texts.Add(key, item);
                    Step(TextAnimatorMigrationStepKind.AddTextStudio).Add(item);
                }

                if (artifact.Kind == TextAnimatorMigrationArtifactKind.TypewriterComponent)
                    item.Typewriter = source;

                bool hasTextStudio = gameObject.GetComponent<TextStudioText>() != null;
                item.State = hasTextStudio ? TextAnimatorMigrationItemState.Done : GetObjectState(gameObject);
                item.Detail = hasTextStudio
                    ? "Text Studio added. Remove Text Animator from this object once you are happy with the result."
                    : item.State == TextAnimatorMigrationItemState.Blocked
                        ? BlockedReason(gameObject)
                        : item.Typewriter != null
                            ? "Uses Text Animator and its typewriter."
                            : "Uses Text Animator.";
            }

            void AddCompanion(TextAnimatorMigrationArtifactRecord artifact)
            {
                Component component = artifact.Target as Component;
                if (component == null)
                {
                    if (artifact.Kind == TextAnimatorMigrationArtifactKind.SoundWriterComponent)
                        AddClosedScene(TextAnimatorMigrationStepKind.SoundsAndEvents, artifact, "sound player", "sound players");
                    return;
                }

                TrackTextAnimatorComponent(component);
                Component source = ResolveSource(component);
                if (source == null)
                    return;

                GameObject gameObject = source.gameObject;
                int key = gameObject.GetStableId();
                if (!_companions.TryGetValue(key, out TextAnimatorMigrationPlanItem item))
                {
                    item = CreateObjectItem(gameObject, "sound-events");
                    _companions.Add(key, item);
                    Step(TextAnimatorMigrationStepKind.SoundsAndEvents).Add(item);
                }

                int typedListeners = 0;
                if (artifact.Kind == TextAnimatorMigrationArtifactKind.SoundWriterComponent)
                {
                    item.SoundWriter = source;
                }
                else
                {
                    item.Typewriter = source;
                    item.HasEvents = true;
                    SerializedObject serializedTypewriter = new SerializedObject(source);
                    item.EventListenerCount = TextAnimatorMigrationCompanionUtility.CountTypewriterEventListeners(serializedTypewriter);
                    typedListeners = TextAnimatorMigrationCompanionUtility.CountManualTypewriterListeners(serializedTypewriter);
                    item.ManualListenerCount = typedListeners;
                }

                bool soundDone = item.SoundWriter == null || gameObject.GetComponent<TextStudioAudioPlayer>() != null;
                bool eventsDone = !item.HasEvents || gameObject.GetComponent<TextStudioTypewriterEventRelay>() != null;
                item.State = soundDone && eventsDone ? TextAnimatorMigrationItemState.Done : GetObjectState(gameObject);
                item.Detail = item.State == TextAnimatorMigrationItemState.Blocked
                    ? BlockedReason(gameObject)
                    : DescribeCompanion(item, item.State == TextAnimatorMigrationItemState.Done);

                if (typedListeners > 0)
                {
                    TextAnimatorMigrationPlanItem listenerItem = CreateObjectItem(gameObject, "listeners");
                    listenerItem.State = TextAnimatorMigrationItemState.NeedsYou;
                    listenerItem.Count = typedListeners;
                    listenerItem.Detail = typedListeners == 1
                        ? "1 typewriter event listener receives Text Animator data. Reconnect it on the Typewriter Event Relay."
                        : typedListeners + " typewriter event listeners receive Text Animator data. Reconnect them on the Typewriter Event Relay.";
                    Topic(TextAnimatorMigrationTopicKind.EventListeners).Add(listenerItem);
                }
            }

            static string DescribeCompanion(TextAnimatorMigrationPlanItem item, bool done)
            {
                bool sounds = item.SoundWriter != null;
                if (done)
                {
                    return sounds && item.HasEvents
                        ? "Text Studio now plays the typewriter sounds and relays the typewriter events."
                        : sounds
                            ? "Text Studio now plays the typewriter sounds."
                            : "Text Studio now relays the typewriter events.";
                }

                string detail = sounds ? "Recreates the typewriter sounds" : string.Empty;
                if (item.HasEvents)
                {
                    int copied = Mathf.Max(0, item.EventListenerCount - item.ManualListenerCount);
                    string events = copied == 0
                        ? "adds a Typewriter Event Relay"
                        : copied == 1 ? "copies 1 event listener" : "copies " + copied + " event listeners";
                    detail = detail.Length > 0 ? detail + " and " + events : char.ToUpperInvariant(events[0]) + events.Substring(1);
                    if (item.ManualListenerCount > 0)
                    {
                        detail += item.ManualListenerCount == 1
                            ? "; 1 more listener is listed under Needs you"
                            : "; " + item.ManualListenerCount + " more listeners are listed under Needs you";
                    }
                }

                return detail + ".";
            }

            void AddTiming(TextAnimatorMigrationArtifactRecord artifact)
            {
                Object source = artifact.Target != null ? artifact.Target : LoadAsset(artifact.AssetPath);
                if (!TextAnimatorProjectDefaultsImporter.IsTimingSource(source))
                {
                    AddWorkingAsset(artifact, "Typing timing that is not converted. Text Studio's typewriter uses its own timing settings.");
                    _plan.SetupAssetCount++;
                    return;
                }

                string importedName = TextAnimatorProjectDefaultsImporter.GetImportedName(source);
                bool done = _importedTimingNames.Contains(importedName);
                TextAnimatorMigrationPlanItem item = CreateAssetItem(artifact, source, "timing");
                item.State = done ? TextAnimatorMigrationItemState.Done : TextAnimatorMigrationItemState.Ready;
                item.Detail = done
                    ? "Converted to the Character Timings asset \"" + importedName + "\"."
                    : "Character delays and punctuation pauses become a Text Studio Character Timings asset.";
                item.Source = source;
                Step(TextAnimatorMigrationStepKind.TypingSpeed).Add(item);
            }

            void AddPlayback(TextAnimatorMigrationArtifactRecord artifact)
            {
                string tag = artifact.TagId.Trim();
                if (string.IsNullOrEmpty(tag) || !_playbackTags.Add(tag))
                    return;

                if (TextAnimatorMigrationRules.IsProjectPlayback(tag))
                {
                    TextAnimatorMigrationPlanItem done = CreateAssetItem(artifact, artifact.Target, "playback");
                    done.Title = "<" + tag + ">";
                    done.State = TextAnimatorMigrationItemState.Done;
                    done.Detail = "Imported as a Text Studio Playback with the same name.";
                    Step(TextAnimatorMigrationStepKind.Playbacks).Add(done);
                    return;
                }

                if (!artifact.RequiresCustomReview)
                {
                    AddWorkingAsset(artifact, "Text Studio has a built-in playback for <" + tag + ">.");
                    _plan.BuiltInEquivalentCount++;
                    return;
                }

                if (artifact.UsedSourceCount <= 0)
                {
                    AddWorkingAsset(artifact, "Custom playback that no text uses.");
                    _plan.UnusedAssetCount++;
                    return;
                }

                Object source = artifact.Target != null ? artifact.Target : LoadAsset(artifact.AssetPath);
                TextAnimatorMigrationPlanItem item = CreateAssetItem(artifact, source, "playback");
                item.Title = "<" + tag + ">";
                item.Count = artifact.UsedSourceCount;
                string reason = string.Empty;
                if (source != null && TextAnimatorPlaybackImporter.TryReadDefinition(source, out _, out reason))
                {
                    item.State = TextAnimatorMigrationItemState.Ready;
                    item.Source = source;
                    item.Detail = "Used in " + Texts(artifact.UsedSourceCount) + ". Becomes an editable Text Studio Playback with the same name.";
                    Step(TextAnimatorMigrationStepKind.Playbacks).Add(item);
                }
                else
                {
                    item.State = TextAnimatorMigrationItemState.NeedsYou;
                    item.Detail = "Used in " + Texts(artifact.UsedSourceCount) + ". " +
                                  (string.IsNullOrEmpty(reason) ? "This playback type cannot be converted." : reason) +
                                  " Create a Text Studio Playback asset named \"" + tag + "\" with similar timing.";
                    Topic(TextAnimatorMigrationTopicKind.UnsupportedPlaybacks).Add(item);
                }
            }

            void AddStyle(TextAnimatorMigrationArtifactRecord artifact)
            {
                string tag = artifact.TagId.Trim();
                if (string.IsNullOrEmpty(tag) || _stylesByTag.ContainsKey(tag))
                    return;

                _stylesByTag.Add(tag, artifact);
                bool registered = TextAnimatorMigrationRules.TryGetRegisteredCoverage(tag, out TextAnimatorMigrationRuleKind kind) &&
                                  kind == TextAnimatorMigrationRuleKind.Style;
                if (!registered && artifact.UsedSourceCount <= 0)
                {
                    AddWorkingAsset(artifact, "Style that no text uses.");
                    _plan.UnusedAssetCount++;
                    return;
                }

                TextAnimatorMigrationPlanItem item = CreateAssetItem(artifact, artifact.Target, "style");
                item.Title = "<" + tag + ">";
                item.TagId = tag;
                item.StyleSheetPath = artifact.AssetPath;
                item.Count = Mathf.Max(1, artifact.UsedSourceCount);
                item.State = registered ? TextAnimatorMigrationItemState.Done : TextAnimatorMigrationItemState.Ready;
                item.Detail = registered
                    ? "Registered as a Text Studio tag alias, so the tag keeps working."
                    : "Used in " + Texts(artifact.UsedSourceCount) + ". Becomes a Text Studio tag alias, so the tag keeps working unchanged.";
                Step(TextAnimatorMigrationStepKind.StyleAliases).Add(item);
            }

            void AddTagProvider(TextAnimatorMigrationArtifactRecord artifact)
            {
                string tag = artifact.TagId.Trim();
                if (string.IsNullOrEmpty(tag))
                {
                    AddWorkingAsset(artifact, "Text Animator asset that no text refers to.");
                    _plan.SetupAssetCount++;
                    return;
                }

                if (!artifact.RequiresCustomReview)
                {
                    AddWorkingAsset(artifact, "Text Studio has a built-in equivalent for <" + tag + ">.");
                    _plan.BuiltInEquivalentCount++;
                    return;
                }

                if (artifact.UsedSourceCount <= 0)
                {
                    AddWorkingAsset(artifact, "Custom " + KindName(artifact.Kind) + " <" + tag + "> that no text uses.");
                    _plan.UnusedAssetCount++;
                    return;
                }

                if (_customByTag.ContainsKey(tag))
                    return;

                _customByTag.Add(tag, artifact);
                TextAnimatorMigrationPlanItem item = CreateAssetItem(artifact, artifact.Target, "custom");
                item.Title = "<" + tag + ">";
                item.TagId = tag;
                item.Count = artifact.UsedSourceCount;
                item.State = TextAnimatorMigrationItemState.NeedsYou;
                item.Detail = "Text Animator " + KindName(artifact.Kind) + " \"" + artifact.Label + "\" used in " +
                              Texts(artifact.UsedSourceCount) + ". " + RecreateAdvice(artifact.Kind, tag);
                _tagItems[tag] = item;
                Topic(TextAnimatorMigrationTopicKind.CustomTags).Add(item);
            }

            public void AddSources()
            {
                IReadOnlyList<TextAnimatorMigrationSourceRecord> sources = _plan.Inventory.Sources;
                for (int i = 0; i < sources.Count; i++)
                {
                    TextAnimatorMigrationSourceRecord source = sources[i];
                    if (source == null)
                        continue;

                    _plan.TextCount++;
                    bool working = true;
                    IReadOnlyList<TextAnimatorMigrationIssue> issues = source.Result.Issues;
                    HashSet<string> reportedTags = null;
                    for (int issueIndex = 0; issueIndex < issues.Count; issueIndex++)
                    {
                        TextAnimatorMigrationIssue issue = issues[issueIndex];
                        if (issue.Status != TextAnimatorMigrationStatus.Manual &&
                            issue.Status != TextAnimatorMigrationStatus.Unsupported)
                        {
                            continue;
                        }

                        string tag = GetTagName(issue);
                        bool isTagIssue = !string.IsNullOrEmpty(tag) &&
                                          (issue.RuleKind == TextAnimatorMigrationRuleKind.Unknown ||
                                           issue.RuleKind == TextAnimatorMigrationRuleKind.Appearance ||
                                           issue.RuleKind == TextAnimatorMigrationRuleKind.Disappearance);
                        if (isTagIssue && _stylesByTag.ContainsKey(tag))
                        {
                            // The style alias step converts this tag; the text works once that step is done.
                            working &= IsStyleRegistered(tag);
                            continue;
                        }

                        working = false;
                        if (isTagIssue)
                        {
                            reportedTags ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            if (reportedTags.Add(tag))
                                AddUnknownTagUse(tag, source);
                            continue;
                        }

                        AddMarkupCheck(source, issue);
                    }

                    if (working)
                        _plan._workingTexts.Add(CreateTextItem(source));
                }
            }

            bool IsStyleRegistered(string tag)
            {
                return TextAnimatorMigrationRules.TryGetRegisteredCoverage(tag, out TextAnimatorMigrationRuleKind kind) &&
                       kind == TextAnimatorMigrationRuleKind.Style;
            }

            void AddUnknownTagUse(string tag, TextAnimatorMigrationSourceRecord source)
            {
                if (_tagItems.TryGetValue(tag, out TextAnimatorMigrationPlanItem existing))
                {
                    if (!_customByTag.ContainsKey(tag))
                    {
                        existing.Count++;
                        existing.Detail = UnknownTagDetail(existing.Count, tag);
                    }

                    return;
                }

                TextAnimatorMigrationPlanItem item = CreateTextItem(source);
                item.Key = "tag:" + tag;
                item.Title = "<" + tag + ">";
                item.TagId = tag;
                item.State = TextAnimatorMigrationItemState.NeedsYou;
                item.Count = 1;
                item.Detail = UnknownTagDetail(1, tag);
                _tagItems.Add(tag, item);
                Topic(TextAnimatorMigrationTopicKind.CustomTags).Add(item);
            }

            static string UnknownTagDetail(int count, string tag)
            {
                return "Used in " + Texts(count) + ", but no Text Animator asset defines it here. " +
                       "Create a Text Studio effect or action named \"" + tag + "\", or replace the tag.";
            }

            void AddMarkupCheck(TextAnimatorMigrationSourceRecord source, TextAnimatorMigrationIssue issue)
            {
                TextAnimatorMigrationPlanItem item = CreateTextItem(source);
                item.Key = "markup:" + source.Index + ":" + issue.Start;
                item.State = TextAnimatorMigrationItemState.NeedsYou;
                item.Detail = issue.OriginalText + ": " + issue.Message +
                              (string.IsNullOrEmpty(issue.Action) ? string.Empty : " " + issue.Action);
                Topic(TextAnimatorMigrationTopicKind.MarkupChecks).Add(item);
            }

            public void AddScripts(IReadOnlyList<TextAnimatorMigrationScriptReference> scripts)
            {
                if (scripts == null)
                    return;

                for (int i = 0; i < scripts.Count; i++)
                {
                    TextAnimatorMigrationScriptReference script = scripts[i];
                    TextAnimatorMigrationPlanItem item = new TextAnimatorMigrationPlanItem
                    {
                        Key = "script:" + script.AssetPath,
                        Title = Path.GetFileName(script.AssetPath),
                        Location = script.AssetPath,
                        State = TextAnimatorMigrationItemState.NeedsYou,
                        Count = script.ReferenceCount,
                        OpenPath = script.AssetPath,
                        OpenLine = script.Line,
                        SelectTarget = AssetDatabase.LoadAssetAtPath<MonoScript>(script.AssetPath),
                        Detail = (script.ReferenceCount == 1 ? "1 Text Animator reference" : script.ReferenceCount + " Text Animator references") +
                                 ", first on line " + script.Line + ": " + script.Snippet
                    };
                    Topic(TextAnimatorMigrationTopicKind.Scripts).Add(item);
                }
            }

            void AddClosedScene(
                TextAnimatorMigrationStepKind stepKind,
                TextAnimatorMigrationArtifactRecord artifact,
                string singular,
                string plural)
            {
                string scenePath = artifact.AssetPath;
                if (string.IsNullOrEmpty(scenePath))
                    return;

                string key = stepKind + "|" + scenePath;
                if (_closedScenes.TryGetValue(key, out TextAnimatorMigrationPlanItem existing))
                {
                    existing.Count += artifact.OccurrenceCount;
                    existing.Detail = ClosedSceneDetail(existing.Count, singular, plural);
                    return;
                }

                TextAnimatorMigrationPlanItem item = new TextAnimatorMigrationPlanItem
                {
                    Key = "scene:" + key,
                    Title = Path.GetFileNameWithoutExtension(scenePath),
                    Location = scenePath,
                    State = TextAnimatorMigrationItemState.OpenScene,
                    Count = artifact.OccurrenceCount,
                    OpenPath = scenePath,
                    SelectTarget = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath)
                };
                item.Detail = ClosedSceneDetail(item.Count, singular, plural);
                _closedScenes.Add(key, item);
                Step(stepKind).Add(item);
            }

            static string ClosedSceneDetail(int count, string singular, string plural)
            {
                return (count == 1 ? "1 " + singular : count + " " + plural) + " in this closed scene. Open the scene to include them.";
            }

            void AddWorkingAsset(TextAnimatorMigrationArtifactRecord artifact, string detail)
            {
                TextAnimatorMigrationPlanItem item = CreateAssetItem(artifact, artifact.Target, "asset");
                item.State = TextAnimatorMigrationItemState.WorksAsIs;
                item.Detail = detail;
                _plan._workingAssets.Add(item);
            }

            void TrackTextAnimatorComponent(Component component)
            {
                GameObject gameObject = component.gameObject;
                if (EditorUtility.IsPersistent(gameObject))
                {
                    _plan._textAnimatorLocations.Add(AssetDatabase.GetAssetPath(gameObject));
                    return;
                }

                if (!_plan._sceneTextAnimatorComponents.Contains(component))
                    _plan._sceneTextAnimatorComponents.Add(component);
                _plan._textAnimatorLocations.Add(gameObject.scene.path);
            }

            TextAnimatorMigrationItemState GetObjectState(GameObject gameObject)
            {
                return IsBlocked(gameObject) ? TextAnimatorMigrationItemState.Blocked : TextAnimatorMigrationItemState.Ready;
            }

            bool IsBlocked(GameObject gameObject)
            {
                if (!EditorUtility.IsPersistent(gameObject))
                    return false;

                string path = AssetDatabase.GetAssetPath(gameObject);
                return path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(path, _prefabStagePath, StringComparison.OrdinalIgnoreCase);
            }

            string BlockedReason(GameObject gameObject)
            {
                string path = AssetDatabase.GetAssetPath(gameObject);
                return string.Equals(path, _prefabStagePath, StringComparison.OrdinalIgnoreCase)
                    ? "This prefab is open in Prefab Mode. Save and close it, then continue here."
                    : "This prefab is part of a read-only package and cannot be changed.";
            }

            static TextAnimatorMigrationPlanItem CreateObjectItem(GameObject gameObject, string keyPrefix)
            {
                bool persistent = EditorUtility.IsPersistent(gameObject);
                string location = persistent
                    ? AssetDatabase.GetAssetPath(gameObject)
                    : string.IsNullOrEmpty(gameObject.scene.path) ? gameObject.scene.name : gameObject.scene.path;
                return new TextAnimatorMigrationPlanItem
                {
                    Key = keyPrefix + ":" + gameObject.GetStableId(),
                    Title = BuildTransformPath(gameObject.transform),
                    Location = location,
                    GameObject = gameObject,
                    SelectTarget = gameObject,
                    OpenPath = persistent ? location : string.Empty
                };
            }

            static TextAnimatorMigrationPlanItem CreateAssetItem(TextAnimatorMigrationArtifactRecord artifact, Object target, string keyPrefix)
            {
                Object selectTarget = target != null ? target : LoadAsset(artifact.AssetPath);
                return new TextAnimatorMigrationPlanItem
                {
                    Key = keyPrefix + ":" + artifact.Kind + ":" + artifact.AssetPath + ":" + artifact.TagId + ":" + artifact.Label,
                    Title = artifact.Label,
                    Location = artifact.AssetPath,
                    SelectTarget = selectTarget,
                    TagId = artifact.TagId
                };
            }

            static TextAnimatorMigrationPlanItem CreateTextItem(TextAnimatorMigrationSourceRecord source)
            {
                TextAnimatorMigrationCandidate candidate = source.Candidate;
                Object target = candidate != null ? candidate.Target : null;
                Component component = target as Component;
                string title = !string.IsNullOrEmpty(source.ObjectPath)
                    ? source.ObjectPath
                    : candidate != null && !string.IsNullOrEmpty(candidate.Label) ? candidate.Label : "Text";
                return new TextAnimatorMigrationPlanItem
                {
                    Key = "text:" + source.Index,
                    Title = title,
                    Location = candidate != null ? candidate.AssetPath : string.Empty,
                    State = TextAnimatorMigrationItemState.WorksAsIs,
                    SelectTarget = component != null ? component.gameObject : target != null ? target : LoadAsset(candidate != null ? candidate.AssetPath : null),
                    Detail = "Text Studio reads this markup as it is."
                };
            }

            static Component ResolveSource(Component component)
            {
                if (component == null)
                    return null;

                // Prefab instances inherit the change from the prefab that defines the component.
                if (PrefabUtility.IsPartOfPrefabInstance(component) && !PrefabUtility.IsAddedComponentOverride(component))
                {
                    Component source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(component);
                    if (source != null)
                        return source;
                }

                return component;
            }

            static string GetTagName(TextAnimatorMigrationIssue issue)
            {
                IReadOnlyList<TextAnimatorMarkupToken> tokens = TextAnimatorMarkupScanner.Scan(issue.OriginalText);
                return tokens.Count > 0 ? tokens[0].Name.Trim() : string.Empty;
            }

            static HashSet<string> FindImportedTimingNames()
            {
                HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string[] guids = AssetDatabase.FindAssets("t:" + nameof(CharacterTimings));
                for (int i = 0; i < guids.Length; i++)
                    names.Add(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guids[i])));
                return names;
            }

            static Object LoadAsset(string path)
            {
                return string.IsNullOrEmpty(path) || path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : AssetDatabase.LoadMainAssetAtPath(path);
            }

            static string BuildTransformPath(Transform transform)
            {
                string path = transform.name;
                for (Transform parent = transform.parent; parent != null; parent = parent.parent)
                    path = parent.name + "/" + path;
                return path;
            }

            static string KindName(TextAnimatorMigrationArtifactKind kind)
            {
                switch (kind)
                {
                    case TextAnimatorMigrationArtifactKind.Effect:
                        return "effect";
                    case TextAnimatorMigrationArtifactKind.Action:
                        return "action";
                    default:
                        return "tag";
                }
            }

            static string RecreateAdvice(TextAnimatorMigrationArtifactKind kind, string tag)
            {
                return kind == TextAnimatorMigrationArtifactKind.Action
                    ? "Create a Text Studio action named \"" + tag + "\" and these texts work without changes."
                    : "Create a Composed Effect named \"" + tag + "\" (no code needed) and these texts work without changes.";
            }
        }

        internal static string Texts(int count)
        {
            return count == 1 ? "1 text" : count + " texts";
        }
    }
}
