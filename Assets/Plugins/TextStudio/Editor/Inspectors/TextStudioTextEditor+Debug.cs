using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using TextStudio.BuiltIns;
using TMPro;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    internal partial class TextStudioTextEditor
    {
        VisualElement BuildUitkRenderersTab(TextStudioText hub)
        {
            VisualElement root = new VisualElement();
            root.AddToClassList("ts-renderers-tab");
            root.Add(CreateAlignedHint(
                "Every enabled renderer on this GameObject receives every frame. Toggle their Behaviour enabled state to show or hide output."));

            VisualElement list = CreateSection("Active Renderers");
            root.Add(list);
            Button rescan = new Button(() =>
            {
                hub.DiscoverRenderersAndProviders();
                BuildUitkActiveTab(hub);
            })
            {
                text = "Rescan Renderers"
            };
            list.Add(rescan);

            ITextStudioTextBackend activeBackend = hub.Backends.ActiveBackend;
            if (activeBackend != null)
            {
                TextStudioRenderChannels backendChannels =
                    activeBackend is ITextStudioTextBackendRenderChannelSupport channelSupport
                        ? channelSupport.SupportedRenderChannels
                        : TextStudioRenderChannels.All;
                AddRendererRow(
                    list,
                    activeBackend.DisplayName,
                    activeBackend.TargetObject,
                    activeBackend.IsAvailable &&
                    (activeBackend.TargetObject is not Behaviour targetBehaviour ||
                     targetBehaviour.isActiveAndEnabled),
                    false,
                    true,
                    ResolveBackendRendererCapabilities(
                        activeBackend.TargetObject,
                        backendChannels),
                    backendChannels);
            }
            else
            {
                RendererCapabilities tmpCapabilities =
                    (hub.TMPText is TextMeshProUGUI
                        ? RendererCapabilities.UIOverlay
                        : RendererCapabilities.WorldSpace) |
                    RendererCapabilities.SupportsPerGlyphTRS |
                    RendererCapabilities.SupportsPerGlyphColor |
                    RendererCapabilities.SupportsPerGlyphAlpha;
                AddRendererRow(
                    list,
                    "TextMeshPro (built-in)",
                    hub.TMPText,
                    hub.TMPText != null && hub.TMPText.enabled,
                    hub.TMPText == null,
                    true,
                    tmpCapabilities,
                    TextStudioRenderChannels.All);
            }
            IReadOnlyList<ITextStudioRenderer> renderers = hub.Renderers;
            for (int i = 0; i < renderers.Count; i++)
            {
                ITextStudioRenderer renderer = renderers[i];
                Behaviour behaviour = renderer as Behaviour;
                if (behaviour != null)
                    AddRendererRow(
                        list,
                        renderer.GetType().Name,
                        behaviour,
                        behaviour.isActiveAndEnabled,
                        false,
                        false,
                        renderer.Capabilities,
                        ResolveSupportedRenderChannels(renderer));
            }

            list.Add(CreateAlignedHint(
                "Effects stay authored once in Text Studio. Each renderer reports which shared channels it faithfully displays, so 2D and extension renderers remain part of the same workflow."));

            return root;
        }

        static RendererCapabilities ResolveBackendRendererCapabilities(
            Object target,
            TextStudioRenderChannels channels)
        {
            RendererCapabilities capabilities = target is UnityEngine.UI.Graphic
                ? RendererCapabilities.UIOverlay
                : RendererCapabilities.WorldSpace;
            if ((channels & TextStudioRenderChannels.Transform) != 0)
                capabilities |= RendererCapabilities.SupportsPerGlyphTRS;
            if ((channels & TextStudioRenderChannels.Color) != 0)
                capabilities |= RendererCapabilities.SupportsPerGlyphColor;
            if ((channels & TextStudioRenderChannels.Alpha) != 0)
                capabilities |= RendererCapabilities.SupportsPerGlyphAlpha;
            return capabilities;
        }

        VisualElement BuildUitkEventsTab(TextStudioText hub)
        {
            VisualElement root = new VisualElement();
            root.AddToClassList("ts-events-tab");

            VisualElement reactions = CreateSection("Reactions");
            TextStudioBehaviorProfile profile = hub.BehaviorProfile;
            TextStudioText[] hosts = GetSelectedTextHosts();
            if (TextStudioReactionInspectorContext.IsProfileBacked(hub))
            {
                reactions.Add(CreateAlignedHint(
                    "Reactions come from the assigned Behavior Profile. Editing it changes every host using that profile."));
                reactions.Add(CreateProfileEditButton(hub, "ts-profile-reactions-edit-button"));
                _profileReactionObject = new SerializedObject(profile);
                _profileReactionRecipeRoot = TextStudioReactionRecipeInspector.Create(
                    _profileReactionObject, "_reactions",
                    () => CreateProfileReactionAsset(profile), "ts-profile-reactions", hosts);
                _profileReactionRecipeRoot.SetEnabled(_isEditingAssignedProfile);
                reactions.Add(_profileReactionRecipeRoot);
            }
            else
            {
                reactions.Add(TextStudioReactionRecipeInspector.Create(
                    serializedObject, "_reactions", CreateReactionAsset, "ts-reactions", hosts));
            }
            SerializedProperty reactionTargets = serializedObject.FindProperty("_reactionTargetBindings");
            if (reactionTargets != null)
                reactions.Add(CreateReactionTargetObjects(reactionTargets, hosts));
            root.Add(reactions);

            VisualElement events = CreateSection("Events");
            if (_onSetText != null)
                _onSetText.isExpanded = true;
            events.Add(CreateBoundPropertyField(_onSetText, "On Set Text"));
            if (_onShowComplete != null)
                _onShowComplete.isExpanded = true;
            events.Add(CreateBoundPropertyField(_onShowComplete, "On Show Complete"));
            if (_onDisappearComplete != null)
                _onDisappearComplete.isExpanded = true;
            events.Add(CreateBoundPropertyField(_onDisappearComplete, "On Disappear Complete"));
            events.Add(CreateAlignedHint(
                "For character, wait, and inline message callbacks, add a Typewriter Event Relay component."));
            root.Add(events);
            return root;
        }

        TextStudioText[] GetSelectedTextHosts()
        {
            Object[] selected = targets;
            TextStudioText[] hosts = new TextStudioText[selected.Length];
            for (int i = 0; i < selected.Length; i++)
                hosts[i] = selected[i] as TextStudioText;
            return hosts;
        }

        /// <summary>
        /// Component-local scene objects for the recipe's named targets. Shown while a selected host's effective
        /// recipe names a target, including on a disabled layer, or while bindings exist.
        /// </summary>
        VisualElement CreateReactionTargetObjects(SerializedProperty reactionTargets, TextStudioText[] hosts)
        {
            VisualElement container = new VisualElement { name = "ts-reaction-target-objects" };
            PropertyField field = CreateBoundPropertyField(reactionTargets, "Target Objects");
            field.tooltip = "Scene objects used by reactions whose Target Binding names a key. Stored on this component, never in the recipe or Behavior Profile.";
            container.Add(field);
            container.Add(CreateAlignedHint(
                "Connect the recipe's named targets to objects in this scene. These assignments belong to this component."));

            void Refresh()
            {
                bool relevant = false;
                for (int i = 0; i < hosts.Length && !relevant; i++)
                    relevant = TextStudioReactionInspectorContext.NeedsTargetObjects(hosts[i]);
                container.style.display = relevant ? DisplayStyle.Flex : DisplayStyle.None;
            }

            Refresh();
            TextStudioInspectorElements.SchedulePeriodicRefresh(container, () =>
            {
                if (CanRunScheduledInspectorUpdate(container))
                    Refresh();
            }, 500);
            return container;
        }

        VisualElement BuildUitkDebugTab(TextStudioText hub)
        {
            VisualElement root = new VisualElement();
            root.AddToClassList("ts-debug-tab");
            VisualElement visibility = CreateSection("Visibility");
            Label visibilityLabel = new Label();
            visibilityLabel.AddToClassList("ts-debug-summary");
            visibility.Add(visibilityLabel);
            root.Add(visibility);

            VisualElement timing = CreateSection("Reveal Timing");
            timing.AddToClassList("ts-debug-timing-section");
            VisualElement timingValues = new VisualElement();
            timingValues.AddToClassList("ts-debug-timing-values");
            string[] timingLabels =
            {
                "State",
                "Progress",
                "Approx. duration",
                "Active actions",
                "Renderers",
                "Visibility overrides",
                "Effect families"
            };
            Label[] timingValueLabels = new Label[timingLabels.Length];
            for (int i = 0; i < timingLabels.Length; i++)
            {
                VisualElement row = TextStudioInspectorElements.CreateKeyValueRow(timingLabels[i], "-");
                timingValueLabels[i] = row.Q<Label>(className: "ts-key-value-value");
                timingValues.Add(row);
            }
            timing.Add(timingValues);
            root.Add(timing);

            VisualElement tags = CreateSection("Tags");
            Label tagSummary = new Label();
            tagSummary.AddToClassList("ts-debug-summary");
            VisualElement tagRows = new VisualElement();
            tagRows.AddToClassList("ts-debug-list");
            tags.Add(tagSummary);
            tags.Add(tagRows);
            root.Add(tags);

            root.Add(BuildUitkRenderingDiagnosticsSection(hub));
            root.Add(BuildUitkRenderersTab(hub));
            root.Add(BuildUitkAppearanceOutputSection(hub));
            root.Add(BuildUitkBackendWriteHandlingSection());
            root.Add(CreateReactionTestSection(hub));
            root.Add(BuildUitkMarkerTester(hub));

            TextStudioInspectorElements.SchedulePeriodicRefresh(_uitkRoot, () =>
            {
                if (!CanRunScheduledInspectorUpdate(root, hub))
                    return;

                TextStudioVisibilityReport visibilityReport = TextStudioVisibilityDiagnostics.Evaluate(hub);
                visibilityLabel.text = visibilityReport.Summary;
                TextStudioRevealDebugSnapshot snapshot = hub.GetRevealDebugSnapshot();
                TextStudioTimingDiagnostic timingDiagnostic = TextStudioTimingDiagnostics.FromSnapshot(snapshot);
                timingValueLabels[0].text = timingDiagnostic.State.ToString();
                timingValueLabels[1].text =
                    snapshot.RevealedGlyphCount + "/" + snapshot.TotalGlyphCount +
                    " (" + snapshot.RevealProgress.ToString("P1") + ")";
                timingValueLabels[2].text = snapshot.ApproximateDuration.ToString("0.###") + " s";
                timingValueLabels[3].text = snapshot.ActiveActionCount.ToString();
                timingValueLabels[4].text = snapshot.RendererCount.ToString();
                timingValueLabels[5].text = snapshot.HasVisibilityOverrides ? "Active" : "None";
                timingValueLabels[6].text = "Reveal " + FormatDebugSwitch(snapshot.RevealEffectsEnabled) +
                                            " · Active " + FormatDebugSwitch(snapshot.ActiveEffectsEnabled) +
                                            " · Hide " + FormatDebugSwitch(snapshot.HideEffectsEnabled);
                RefreshUitkDebugTagDiagnostics(hub, tagSummary, tagRows);
            }, 250);

            return root;
        }

        VisualElement BuildUitkRenderingDiagnosticsSection(TextStudioText hub)
        {
            VisualElement section = CreateSection("Rendering");
            section.name = "ts-debug-rendering-diagnostics";
            HelpBox warning = CreateHelpBox(string.Empty, HelpBoxMessageType.Warning);
            warning.name = "ts-debug-material-warning";
            Label qualityHint = TextStudioInspectorElements.CreateMutedText(
                string.Empty, "ts-debug-rendering-quality-hint");
            section.Add(warning);
            section.Add(qualityHint);
            Label capabilitySummary = TextStudioInspectorElements.CreateMutedText(string.Empty);
            section.Add(capabilitySummary);
            VisualElement capabilityRows = new VisualElement();
            section.Add(capabilityRows);
            Action refreshRoute = AddRenderRouteRows(section, hub);
            TextStudioCapabilityReport capabilities = new TextStudioCapabilityReport();
            List<HelpBox> capabilityBoxes = new List<HelpBox>();

            Action refresh = () =>
            {
                refreshRoute();
                string warningText = hub != null ? hub.MaterialCompatibilityWarning : string.Empty;
                string hintText = hub != null ? hub.AppearanceQualityHint : string.Empty;
                if (hub != null) hub.CollectCapabilities(capabilities);
                capabilitySummary.text = capabilities.BackendName + " · " + capabilities.EffectiveMotionPolicy + " Motion";
                int visibleRows = 0;
                for (int i = 0; i < capabilities.Diagnostics.Count; i++)
                {
                    TextStudioPreviewDiagnostic diagnostic = capabilities.Diagnostics[i];
                    if (diagnostic.Message == warningText || diagnostic.Message == hintText) continue;
                    if (visibleRows == capabilityBoxes.Count)
                    {
                        HelpBox box = CreateHelpBox(string.Empty, HelpBoxMessageType.Info);
                        capabilityBoxes.Add(box);
                        capabilityRows.Add(box);
                    }
                    HelpBox row = capabilityBoxes[visibleRows++];
                    row.style.display = DisplayStyle.Flex;
                    row.messageType = diagnostic.Severity == TextStudioPreviewDiagnosticSeverity.Error
                        ? HelpBoxMessageType.Error : diagnostic.Severity == TextStudioPreviewDiagnosticSeverity.Warning
                            ? HelpBoxMessageType.Warning : HelpBoxMessageType.Info;
                    string message = diagnostic.Message + (string.IsNullOrEmpty(diagnostic.Action) ? string.Empty : "\n" + diagnostic.Action);
                    if (row.text != message) row.text = message;
                }
                for (int i = visibleRows; i < capabilityBoxes.Count; i++)
                    capabilityBoxes[i].style.display = DisplayStyle.None;
                bool hasWarning = !string.IsNullOrEmpty(warningText);
                bool hasHint = !string.IsNullOrEmpty(hintText);
                section.style.display = DisplayStyle.Flex;
                warning.style.display = hasWarning ? DisplayStyle.Flex : DisplayStyle.None;
                qualityHint.style.display = hasHint ? DisplayStyle.Flex : DisplayStyle.None;
                if (warning.text != warningText)
                    warning.text = warningText;
                if (qualityHint.text != hintText)
                    qualityHint.text = hintText;
            };
            refresh();
            TextStudioInspectorElements.SchedulePeriodicRefresh(section, () =>
            {
                if (CanRunScheduledInspectorUpdate(section, hub))
                    refresh();
            }, 250);
            return section;
        }

        VisualElement BuildUitkAppearanceOutputSection(TextStudioText hub)
        {
            VisualElement section = CreateSection("Compiled Output");
            section.name = "ts-debug-appearance-output";
            Label status = AddAppearanceSummaryRow(section, "Status");
            Label path = AddAppearanceSummaryRow(section, "Minimum Path");
            Label operations = AddAppearanceSummaryRow(section, "Operations");
            Label buckets = AddAppearanceSummaryRow(section, "Render Buckets");
            Label bounds = AddAppearanceSummaryRow(section, "Bounds Expansion");
            Label samples = AddAppearanceSummaryRow(section, "Samples");
            Label geometry = AddAppearanceSummaryRow(section, "Geometry");
            Label surfaces = AddAppearanceSummaryRow(section, "Surfaces");
            section.Add(CreateAlignedHint(
                "Compatible fused layers share a universal table-driven shader and one render bucket. Extra buckets are created only when output blending or a higher-fidelity execution path requires them."));

            CompiledTextStudio2DStyle renderedStyle = null;
            TextStudioAppearanceSource renderedSource = (TextStudioAppearanceSource)(-1);
            Action refresh = () =>
            {
                TextStudioAppearanceSource source = hub != null
                    ? hub.AppearanceSource
                    : TextStudioAppearanceSource.Inline;
                CompiledTextStudio2DStyle compiled = hub != null
                    ? hub.CompiledAppearanceStyle
                    : null;
                if (ReferenceEquals(compiled, renderedStyle) && source == renderedSource)
                    return;

                renderedStyle = compiled;
                renderedSource = source;
                section.style.display = hub != null &&
                    (source == TextStudioAppearanceSource.Inline || hub.AppearanceStyle != null)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                RefreshAppearanceOutput(
                    compiled,
                    status,
                    path,
                    operations,
                    buckets,
                    bounds,
                    samples,
                    geometry,
                    surfaces);
            };
            refresh();
            TextStudioInspectorElements.SchedulePeriodicRefresh(section, () =>
            {
                if (CanRunScheduledInspectorUpdate(section, hub))
                    refresh();
            }, 250);
            return section;
        }

        static void RefreshAppearanceOutput(
            CompiledTextStudio2DStyle compiled,
            Label status,
            Label path,
            Label operations,
            Label buckets,
            Label bounds,
            Label samples,
            Label geometry,
            Label surfaces)
        {
            if (compiled == null)
            {
                status.text = "Waiting for compilation";
                path.text = "-";
                operations.text = "-";
                buckets.text = "-";
                bounds.text = "-";
                samples.text = "-";
                geometry.text = "-";
                surfaces.text = "-";
                return;
            }

            status.text = compiled.IsValid ? "Ready" : "Needs attention";
            path.text = compiled.IsPassThrough
                ? "Backend Native"
                : FormatExecutionPath(GetMinimumAppearancePath(compiled));
            operations.text = compiled.OperationCount.ToString();
            buckets.text = compiled.BucketCount.ToString();
            TextStudio2DBoundsExpansion expansion = compiled.BoundsExpansion;
            bounds.text = string.Format(
                "L {0:0.###}  B {1:0.###}  R {2:0.###}  T {3:0.###}",
                expansion.Left,
                expansion.Bottom,
                expansion.Right,
                expansion.Top);
            TextStudio2DCostEstimate cost = compiled.Cost;
            samples.text = string.Format(
                "{0} field, {1} texture",
                cost.FieldSamples,
                cost.TextureSamples);
            geometry.text = FormatCount(cost.GeometrySegments, "segment", "segments");
            surfaces.text = string.Format(
                "{0}, {1}",
                FormatCount(cost.OffscreenPasses, "pass", "passes"),
                FormatCount(cost.IntermediateSurfaces, "surface", "surfaces"));
        }

        static TextStudio2DExecutionPath GetMinimumAppearancePath(
            CompiledTextStudio2DStyle compiled)
        {
            TextStudio2DExecutionPath result = TextStudio2DExecutionPath.FusedField;
            for (int i = 0; i < compiled.OperationCount; i++)
            {
                TextStudio2DExecutionPath candidate = compiled.GetOperation(i).ExecutionPath;
                if (candidate > result)
                    result = candidate;
            }
            return result;
        }

        static string FormatExecutionPath(TextStudio2DExecutionPath path)
        {
            return path switch
            {
                TextStudio2DExecutionPath.FusedField => "Fused Field",
                TextStudio2DExecutionPath.RetainedGeometry => "Retained Geometry",
                TextStudio2DExecutionPath.HeroCompositor => "Hero Compositor",
                _ => "Automatic"
            };
        }

        static string FormatDebugSwitch(bool enabled)
        {
            return enabled ? "On" : "Off";
        }

        VisualElement BuildUitkBackendWriteHandlingSection()
        {
            VisualElement tmp = CreateSection("Direct Backend Writes");
            tmp.name = "ts-debug-tmp-write-section";
            tmp.Add(CreateBoundPropertyField(_backendWriteHandling, "Policy"));
            Label hint = new Label(
                "Raw mode always mirrors backend edits. While Text Studio is driving Live or runtime output, Warn and Ignore preserve canonical tags; Import As Source deliberately replaces them.");
            hint.AddToClassList("ts-inline-info-text");
            tmp.Add(hint);
            return tmp;
        }

        VisualElement BuildUitkMarkerTester(TextStudioText hub)
        {
            VisualElement marker = CreateSection("Event Marker Test");
            marker.name = "ts-event-marker-test";
            TextField route = new TextField("Route")
            {
                value = s_markerRoute
            };
            route.RegisterValueChangedCallback(evt => s_markerRoute = evt.newValue ?? string.Empty);
            marker.Add(route);

            TextField parameters = new TextField("Parameters")
            {
                value = s_markerParameters
            };
            parameters.RegisterValueChangedCallback(evt => s_markerParameters = evt.newValue ?? string.Empty);
            marker.Add(parameters);

            VisualElement row = new VisualElement();
            row.AddToClassList("ts-button-row");
            row.Add(new Button(() => InvokeMarkerTest(hub)) { text = "Invoke Event" });
            marker.Add(row);

            Label hint = new Label("Calls TextStudioMarkerEventRouter on this GameObject. For <event=route,arg>, set Route to event and Parameters to route,arg.");
            hint.AddToClassList("ts-muted-text");
            marker.Add(hint);
            return marker;
        }

        void OpenTagBrowser(
            TextStudioText hub,
            VisualElement anchorElement,
            TextStudioTagInsertionSnapshot insertion)
        {
            TextStudioTagAnalysisContext context =
                TextStudioTagInspectorContext.Build(hub, insertion.SourceText);
            Rect anchor = ResolveScreenRect(anchorElement);
            TextStudioTagBrowserWindow.Show(anchor, context, insertion, edit =>
            {
                ApplyMarkupEdit(hub, edit, _uitkSourceField);
                RefreshUitkDiagnostics(hub);
            });
        }

        static Rect ResolveScreenRect(VisualElement element)
        {
            if (element == null)
                return new Rect(0f, 0f, 0f, 0f);

            Rect bounds = element.worldBound;
            EditorWindow owner = EditorWindow.focusedWindow;
            if (owner == null)
                return bounds;

            return new Rect(
                owner.position.x + bounds.x,
                owner.position.y + bounds.y,
                bounds.width,
                bounds.height);
        }

        void ApplyMarkupEdit(
            TextStudioText hub,
            TextStudioTagSourceEditor.EditResult edit,
            TextField sourceField)
        {
            if (hub == null)
                return;

            serializedObject.Update();
            Undo.RecordObject(hub, "Insert Text Studio Tag");
            string next = edit.Text;
            _markup.stringValue = next;
            serializedObject.ApplyModifiedProperties();
            hub.SetText(next);
            EditorUtility.SetDirty(hub);

            if (sourceField != null)
            {
                sourceField.SetValueWithoutNotify(next);
                _uitkSourceHighlight?.Refresh(
                    next,
                    ResolveEffectiveMarkupSyntax(hub));
                sourceField.Focus();
                sourceField.schedule.Execute(() =>
                {
                    sourceField.cursorIndex = edit.SelectionEnd;
                    sourceField.selectIndex = edit.SelectionStart;
                });
            }

            RefreshUitkDiagnostics(hub);
        }

        void ApplySourceText(TextStudioText hub, string value)
        {
            serializedObject.Update();
            _markup.stringValue = value ?? string.Empty;
            serializedObject.ApplyModifiedProperties();
            hub.SetText(_markup.stringValue);
            EditorUtility.SetDirty(hub);
        }

        void RefreshUitkInlineTagHelp(TextStudioText hub, bool allowEndFallback = false)
        {
            _uitkInlineTagHelp?.Refresh(allowEndFallback);
        }

        void RefreshUitkInlineTagHelpIfCaretChanged(TextStudioText hub)
        {
            _uitkInlineTagHelp?.RefreshIfCaretChanged();
        }

        void RefreshUitkDiagnostics(TextStudioText hub)
        {
            if (_uitkDiagnostics == null)
                return;

            _uitkDiagnostics.Clear();
            string source = _markup.stringValue ?? string.Empty;
            string diagnosticSource = hub.CompiledPlan != null &&
                                      hub.CompiledPlan.CanonicalText != null
                ? hub.CompiledPlan.CanonicalText
                : source;
            TextStudioTagAnalysisContext context =
                TextStudioTagInspectorContext.Build(hub, diagnosticSource);
            TextStudioDiagnosticReport report =
                TextStudioAppearanceAuthoringMetadata.GetAnalysis(context, diagnosticSource);
            if (_uitkTagSummary != null)
            {
                string compatibility = hub.LastCompatibilityTranslationCount > 0
                    ? ", " + hub.LastCompatibilityTranslationCount + " compatibility tag(s)"
                    : string.Empty;
                _uitkTagSummary.text = BuildTagStatus(report) + compatibility;
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                _uitkDiagnostics.Add(TextStudioInspectorElements.CreateMutedText(
                    "Markup is empty. Type text or insert a tag to see Text Studio render."));
                return;
            }

            int shown = 0;
            IReadOnlyList<TextStudioCompatibilityDiagnostic> compatibilityDiagnostics =
                hub.CompatibilityDiagnostics;
            for (int i = 0; i < compatibilityDiagnostics.Count && shown < 4; i++)
            {
                TextStudioCompatibilityDiagnostic diagnostic = compatibilityDiagnostics[i];
                HelpBoxMessageType type =
                    diagnostic.Severity == TextStudioCompatibilityDiagnosticSeverity.Warning
                        ? HelpBoxMessageType.Warning
                        : HelpBoxMessageType.Info;
                _uitkDiagnostics.Add(CreateHelpBox(
                    "Compatibility: " + diagnostic.Message,
                    type));
                shown++;
            }
            for (int i = 0; i < report.Diagnostics.Length; i++)
            {
                TextStudioDiagnostic diagnostic = report.Diagnostics[i];
                if (diagnostic.Severity == TextStudioDiagnosticSeverity.Info)
                    continue;

                HelpBoxMessageType type = diagnostic.Severity == TextStudioDiagnosticSeverity.Error
                    ? HelpBoxMessageType.Error
                    : HelpBoxMessageType.Warning;
                string prefix = string.IsNullOrEmpty(diagnostic.TagName)
                    ? diagnostic.Code.ToString()
                    : diagnostic.Code + " <" + diagnostic.TagName + ">";
                _uitkDiagnostics.Add(CreateHelpBox(prefix + ": " + diagnostic.Message, type));
                shown++;
                if (shown >= 4)
                    break;
            }
        }

        static void RefreshUitkDebugTagDiagnostics(TextStudioText hub, Label summary, VisualElement rows)
        {
            if (hub == null || summary == null || rows == null)
                return;

            TextStudioTagAnalysisContext context = TextStudioTagInspectorContext.Build(hub, hub.Markup);
            TextStudioDiagnosticReport report = TextStudioAppearanceAuthoringMetadata.GetAnalysis(context, hub.Markup);
            summary.text = BuildTagStatus(report);
            rows.Clear();

            if (report.Tags.Length == 0)
            {
                Label empty = new Label("No tags in source.");
                empty.AddToClassList("ts-muted-text");
                rows.Add(empty);
                return;
            }

            int tagCount = Mathf.Min(report.Tags.Length, 12);
            for (int i = 0; i < tagCount; i++)
            {
                TextStudioTagDiagnostic tag = report.Tags[i];
                VisualElement row = new VisualElement();
                row.AddToClassList("ts-debug-tag-row");

                Label name = new Label("<" + (tag.IsClosing ? "/" : string.Empty) + tag.Name + ">");
                name.enableRichText = false;
                name.AddToClassList("ts-debug-tag-name");
                row.Add(name);

                string details = tag.Kind + "  " + tag.SourceStart + "-" + tag.SourceEnd;
                if (tag.HasParameter)
                    details += "  param";
                Label value = new Label(details);
                value.AddToClassList("ts-muted-text");
                row.Add(value);
                rows.Add(row);
            }

            if (report.Tags.Length > tagCount)
            {
                Label more = new Label((report.Tags.Length - tagCount) + " more tags.");
                more.AddToClassList("ts-muted-text");
                rows.Add(more);
            }

            int shown = 0;
            for (int i = 0; i < report.Diagnostics.Length; i++)
            {
                TextStudioDiagnostic diagnostic = report.Diagnostics[i];
                if (diagnostic.Severity == TextStudioDiagnosticSeverity.Info)
                    continue;

                HelpBoxMessageType type = diagnostic.Severity == TextStudioDiagnosticSeverity.Error
                    ? HelpBoxMessageType.Error
                    : HelpBoxMessageType.Warning;
                rows.Add(CreateHelpBox(diagnostic.Code + ": " + diagnostic.Message, type));
                shown++;
                if (shown >= 3)
                    break;
            }
        }

        static void AddRendererRow(
            VisualElement parent,
            string label,
            Object component,
            bool enabled,
            bool missing,
            bool activeProvider,
            RendererCapabilities capabilities,
            TextStudioRenderChannels supportedChannels)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("ts-renderer-row");

            VisualElement summary = new VisualElement();
            summary.AddToClassList("ts-renderer-summary");
            Label name = new Label((enabled ? "● " : "○ ") + label)
            {
                tooltip = label
            };
            name.AddToClassList("ts-renderer-name");
            summary.Add(name);

            VisualElement badges = new VisualElement();
            badges.AddToClassList("ts-renderer-badges");

            if (activeProvider)
            {
                Label badge = new Label("layout");
                badge.AddToClassList("ts-small-badge");
                badges.Add(badge);
            }

            AddCapabilityBadge(badges, capabilities, RendererCapabilities.WorldSpace, "world");
            AddCapabilityBadge(badges, capabilities, RendererCapabilities.UIOverlay, "UI");
            AddRenderChannelBadge(badges, supportedChannels);

            if (missing)
            {
                Label missingLabel = new Label("not attached");
                missingLabel.AddToClassList("ts-muted-text");
                badges.Add(missingLabel);
            }

            summary.Add(badges);
            row.Add(summary);

            if (!missing && component != null)
            {
                VisualElement actions = new VisualElement();
                actions.AddToClassList("ts-renderer-actions");
                actions.Add(new Button(() => EditorGUIUtility.PingObject(component))
                {
                    text = "Ping",
                    tooltip = "Highlight this renderer component in Unity."
                });
                actions.Add(new Button(() => Selection.activeObject = component)
                {
                    text = "Select",
                    tooltip = "Select this renderer component in the Inspector."
                });
                row.Add(actions);
            }

            parent.Add(row);
        }

        static TextStudioRenderChannels ResolveSupportedRenderChannels(ITextStudioRenderer renderer)
        {
            if (renderer is ITextStudioRendererChannelSupport channelSupport)
                return channelSupport.SupportedRenderChannels & TextStudioRenderChannels.All;

            RendererCapabilities capabilities = renderer != null
                ? renderer.Capabilities
                : RendererCapabilities.None;
            TextStudioRenderChannels channels = TextStudioRenderChannels.None;
            if ((capabilities & RendererCapabilities.SupportsPerGlyphTRS) != 0)
            {
                channels |= TextStudioRenderChannels.Position |
                    TextStudioRenderChannels.Rotation |
                    TextStudioRenderChannels.Scale;
            }
            if ((capabilities & RendererCapabilities.SupportsPerGlyphColor) != 0)
                channels |= TextStudioRenderChannels.Color;
            if ((capabilities & RendererCapabilities.SupportsPerGlyphAlpha) != 0)
                channels |= TextStudioRenderChannels.Alpha;
            return channels;
        }

        static void AddRenderChannelBadge(
            VisualElement row,
            TextStudioRenderChannels supportedChannels)
        {
            TextStudioRenderChannels channels = supportedChannels & TextStudioRenderChannels.All;
            string label = channels == TextStudioRenderChannels.All
                ? "all effects"
                : channels == TextStudioRenderChannels.None
                    ? "limited effects"
                    : CountRenderChannels(channels) + " effect channels";
            Label badge = new Label(label)
            {
                tooltip = BuildRenderChannelTooltip(channels)
            };
            badge.AddToClassList("ts-small-badge");
            row.Add(badge);
        }

        static int CountRenderChannels(TextStudioRenderChannels channels)
        {
            int count = 0;
            TextStudioRenderChannels bit = TextStudioRenderChannels.Position;
            while (bit <= TextStudioRenderChannels.UvOffset)
            {
                if ((channels & bit) != 0) count++;
                bit = (TextStudioRenderChannels)((int)bit << 1);
            }
            return count;
        }

        static string BuildRenderChannelTooltip(TextStudioRenderChannels channels)
        {
            if (channels == TextStudioRenderChannels.None)
                return "This renderer has not declared support for shared Text Studio effect channels.";

            List<string> names = new List<string>(9);
            AddRenderChannelName(names, channels, TextStudioRenderChannels.Position, "Position");
            AddRenderChannelName(names, channels, TextStudioRenderChannels.Rotation, "Rotation");
            AddRenderChannelName(names, channels, TextStudioRenderChannels.Scale, "Scale");
            AddRenderChannelName(names, channels, TextStudioRenderChannels.Alpha, "Alpha");
            AddRenderChannelName(names, channels, TextStudioRenderChannels.Color, "Color");
            AddRenderChannelName(names, channels, TextStudioRenderChannels.Skew, "Skew");
            AddRenderChannelName(names, channels, TextStudioRenderChannels.Pivot, "Pivot");
            AddRenderChannelName(names, channels, TextStudioRenderChannels.CornerOffsets, "Corner deformation");
            AddRenderChannelName(names, channels, TextStudioRenderChannels.UvOffset, "UV offset");
            return "Supported shared channels: " + string.Join(", ", names) + ".";
        }

        static void AddRenderChannelName(
            List<string> names,
            TextStudioRenderChannels channels,
            TextStudioRenderChannels channel,
            string label)
        {
            if ((channels & channel) != 0)
                names.Add(label);
        }

        static void AddCapabilityBadge(
            VisualElement row,
            RendererCapabilities capabilities,
            RendererCapabilities capability,
            string label)
        {
            if ((capabilities & capability) == 0)
                return;

            Label badge = new Label(label);
            badge.AddToClassList("ts-small-badge");
            row.Add(badge);
        }

        static void SetClass(VisualElement element, string className, bool enabled)
        {
            if (enabled)
                element.AddToClassList(className);
            else
                element.RemoveFromClassList(className);
        }
    }
}
