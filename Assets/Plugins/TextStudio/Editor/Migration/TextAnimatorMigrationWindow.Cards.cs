using System;
using System.Collections.Generic;
using System.Text;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    internal sealed partial class TextAnimatorMigrationWindow
    {
        const int ItemRowLimit = 40;

        sealed class StatTile
        {
            public VisualElement Root;
            public Label Value;
            public Label Caption;
            public Label Detail;
        }

        sealed class StepCard
        {
            public TextAnimatorMigrationStepKind Kind;
            public VisualElement Root;
            public Label Number;
            public Label Pill;
            public Label Changes;
            public Button Primary;
            public Foldout Items;
            public string Signature;
        }

        sealed class TopicCard
        {
            public TextAnimatorMigrationTopicKind Kind;
            public VisualElement Root;
            public Label Pill;
            public Foldout Items;
            public string Signature;
        }

        sealed class WorksCard
        {
            public VisualElement Root;
            public VisualElement Lines;
            public Foldout Items;
            public string Signature;
        }

        sealed class FinishCard
        {
            public VisualElement Root;
            public Label RemoveDetail;
            public Button SelectButton;
            public Button ExportButton;
        }

        WorksCard _worksCard;
        FinishCard _finishCard;

        static StatTile CreateStatTile(string toneClass, string tooltip)
        {
            StatTile tile = new StatTile
            {
                Root = CommonUITK.CreateContainer("ts-migration-stat", toneClass),
                Value = CommonUITK.CreateLabel("0", "ts-migration-stat-value"),
                Caption = CommonUITK.CreateLabel(string.Empty, "ts-migration-stat-label"),
                Detail = Copy(string.Empty, "ts-migration-stat-detail")
            };
            tile.Root.tooltip = tooltip;
            tile.Root.Add(tile.Value);
            tile.Root.Add(tile.Caption);
            tile.Root.Add(tile.Detail);
            return tile;
        }

        static void SetStat(StatTile tile, int value, string caption, string detail, bool active)
        {
            tile.Value.text = value.ToString();
            tile.Caption.text = caption;
            tile.Detail.text = detail;
            tile.Root.EnableInClassList("ts-migration-stat--clear", !active);
        }

        StepCard CreateStepCard(TextAnimatorMigrationStepKind kind)
        {
            StepCard card = new StepCard
            {
                Kind = kind,
                Root = CommonUITK.CreateContainer("ts-migration-card", "ts-migration-step")
            };
            card.Root.name = "ts-migration-step-" + kind;

            VisualElement heading = CommonUITK.CreateContainer("ts-migration-card-heading");
            card.Number = CommonUITK.CreateLabel("1", "ts-migration-step-number");
            heading.Add(card.Number);
            heading.Add(CommonUITK.CreateLabel(StepTitle(kind), "ts-migration-card-title"));
            card.Pill = Pill(string.Empty, string.Empty, string.Empty);
            heading.Add(card.Pill);
            card.Root.Add(heading);
            card.Root.Add(Copy(StepDescription(kind)));

            VisualElement impact = CommonUITK.CreateContainer("ts-migration-impact");
            card.Changes = AddImpactRow(impact, "Changes", string.Empty, "What this step adds or updates in your project.");
            AddImpactRow(impact, "Stays the same", StepKeeps(kind), "What this step never touches.");
            AddImpactRow(impact, "Keep in mind", StepNote(kind), "Limits worth knowing before you run the step.");
            card.Root.Add(impact);

            VisualElement actions = CommonUITK.CreateContainer("ts-migration-card-actions");
            card.Primary = TextStudioInspectorElements.CreateButton(
                StepButtonText(kind, 0),
                StepButtonTooltip(kind),
                () => RunStep(kind, _plan.GetStep(kind).Items));
            card.Primary.name = "ts-migration-run-" + kind;
            card.Primary.AddToClassList("ts-migration-button");
            card.Primary.AddToClassList("ts-migration-step-button");
            actions.Add(card.Primary);
            card.Root.Add(actions);

            card.Items = CommonUITK.CreateFoldout(string.Empty, false, null, "Show the items in this step. Each one can be converted on its own.", "ts-migration-items");
            card.Items.viewDataKey = "ts-migration-step-items-" + kind;
            card.Root.Add(card.Items);
            return card;
        }

        void UpdateStepCard(StepCard card, TextAnimatorMigrationPlanStep step, int number, bool editable)
        {
            bool complete = step.IsComplete;
            card.Number.text = complete ? "✓" : number.ToString();
            card.Number.EnableInClassList("ts-migration-step-number--done", complete);
            card.Root.EnableInClassList("ts-migration-card--done", complete);
            if (step.ReadyCount > 0)
                SetPill(card.Pill, step.ReadyCount + " ready", "info", "Items this step converts when you run it.");
            else if (step.ClosedSceneCount > 0)
                SetPill(card.Pill, "Open " + Count(step.ClosedSceneCount, "scene", "scenes"), "info", "Open these scenes to convert their objects.");
            else if (step.BlockedCount > 0)
                SetPill(card.Pill, step.BlockedCount + " locked", "attention", "Items that can't be changed right now. Their rows explain why.");
            else
                SetPill(card.Pill, "Done", "ready", "Everything in this step is converted.");

            card.Changes.text = StepChanges(card.Kind, step, _outputFolder);
            card.Primary.text = step.ReadyCount > 0 ? StepButtonText(card.Kind, step.ReadyCount) : "Nothing left to convert";
            card.Primary.SetEnabled(editable && step.ReadyCount > 0);
            SetDisplay(card.Primary, step.ReadyCount > 0);

            card.Items.text = ItemsHeading(card.Kind, step);
            string signature = BuildSignature(step.Items) + editable;
            if (signature == card.Signature)
                return;

            card.Signature = signature;
            TextAnimatorMigrationStepKind kind = card.Kind;
            FillItems(
                card.Items,
                step.Items,
                item => item.State == TextAnimatorMigrationItemState.Ready ? ItemActionText(kind) : null,
                ItemActionTooltip(kind),
                item => RunStep(kind, new[] { item }),
                editable);
        }

        TopicCard CreateTopicCard(TextAnimatorMigrationTopicKind kind)
        {
            TopicCard card = new TopicCard
            {
                Kind = kind,
                Root = CommonUITK.CreateContainer("ts-migration-card", "ts-migration-card--manual")
            };
            card.Root.name = "ts-migration-topic-" + kind;
            VisualElement heading = CommonUITK.CreateContainer("ts-migration-card-heading");
            heading.Add(CommonUITK.CreateLabel(TopicTitle(kind), "ts-migration-card-title"));
            card.Pill = Pill(string.Empty, string.Empty, string.Empty);
            heading.Add(card.Pill);
            card.Root.Add(heading);
            card.Root.Add(Copy(TopicGuidance(kind), "ts-migration-guide-text"));
            if (kind == TextAnimatorMigrationTopicKind.Scripts)
                card.Root.Add(CreateApiMap());

            card.Items = CommonUITK.CreateFoldout(string.Empty, true, null, "The items to work through. Select opens the object or file.", "ts-migration-items");
            card.Items.viewDataKey = "ts-migration-topic-items-" + kind;
            card.Root.Add(card.Items);
            return card;
        }

        void UpdateTopicCard(TopicCard card, TextAnimatorMigrationPlanTopic topic)
        {
            SetPill(card.Pill, Count(topic.Items.Count, "item", "items"), "attention", "Items that need you.");
            card.Items.text = Count(topic.Items.Count, "item", "items");
            string signature = BuildSignature(topic.Items);
            if (signature == card.Signature)
                return;

            card.Signature = signature;
            FillItems(card.Items, topic.Items, null, string.Empty, null, true);
        }

        static VisualElement CreateApiMap()
        {
            VisualElement map = CommonUITK.CreateContainer("ts-migration-api-map");
            map.tooltip = "Text Animator calls on the left and the matching TextStudioText calls on the right.";
            AddApiRow(map, "typewriter.ShowText(text)", "textStudio.ShowText(text)");
            AddApiRow(map, "typewriter.StartShowingText()", "textStudio.StartReveal()");
            AddApiRow(map, "typewriter.SkipTypewriter()", "textStudio.SkipReveal()");
            AddApiRow(map, "typewriter.StartDisappearingText()", "textStudio.StartDisappear()");
            AddApiRow(map, "textAnimator.SetText(text)", "textStudio.SetText(text)");
            return map;
        }

        static void AddApiRow(VisualElement map, string from, string to)
        {
            VisualElement row = CommonUITK.CreateContainer("ts-migration-api-row");
            row.Add(Copy(from, "ts-migration-api-from"));
            row.Add(Copy("→ " + to, "ts-migration-api-to"));
            map.Add(row);
        }

        WorksCard CreateWorksCard()
        {
            WorksCard card = new WorksCard
            {
                Root = CommonUITK.CreateContainer("ts-migration-card", "ts-migration-card--done"),
                Lines = CommonUITK.CreateContainer("ts-migration-works")
            };
            card.Root.name = "ts-migration-works-card";
            VisualElement heading = CommonUITK.CreateContainer("ts-migration-card-heading");
            heading.Add(CommonUITK.CreateLabel("Nothing to do for these", "ts-migration-card-title"));
            card.Root.Add(heading);
            card.Root.Add(card.Lines);
            card.Items = CommonUITK.CreateFoldout("Show details", false, null, "List the texts and Text Animator assets that need no work.", "ts-migration-items");
            card.Items.viewDataKey = "ts-migration-works-items";
            card.Root.Add(card.Items);
            return card;
        }

        void UpdateWorks()
        {
            TextAnimatorMigrationPlan plan = _plan;
            WorksCard card = _worksCard;
            card.Lines.Clear();
            AddWorksLine(card.Lines, plan.WorkingTexts.Count, "text uses markup that Text Studio reads as it is.", "texts use markup that Text Studio reads as it is.");
            AddWorksLine(card.Lines, plan.BuiltInEquivalentCount, "Text Animator effect, action or playback has a built-in Text Studio equivalent.", "Text Animator effects, actions and playbacks have built-in Text Studio equivalents.");
            AddWorksLine(card.Lines, plan.UnusedAssetCount, "custom Text Animator asset is not used by any text, so it can stay behind.", "custom Text Animator assets are not used by any text, so they can stay behind.");
            AddWorksLine(card.Lines, plan.SetupAssetCount, "Text Animator setup asset, such as settings or a database, needs no conversion.", "Text Animator setup assets, such as settings and databases, need no conversion.");
            bool hasLines = card.Lines.childCount > 0;
            SetDisplay(_worksSection, hasLines);
            if (!hasLines)
                return;

            List<TextAnimatorMigrationPlanItem> items = new List<TextAnimatorMigrationPlanItem>(plan.WorkingTexts.Count + plan.WorkingAssets.Count);
            items.AddRange(plan.WorkingTexts);
            items.AddRange(plan.WorkingAssets);
            card.Items.text = "Show " + Count(items.Count, "item", "items");
            string signature = BuildSignature(items);
            if (signature == card.Signature)
                return;

            card.Signature = signature;
            FillItems(card.Items, items, null, string.Empty, null, true);
        }

        static void AddWorksLine(VisualElement parent, int count, string singular, string plural)
        {
            if (count <= 0)
                return;

            VisualElement line = CommonUITK.CreateContainer("ts-migration-works-line");
            line.Add(CommonUITK.CreateLabel("✓", "ts-migration-works-icon"));
            line.Add(Copy(count + " " + (count == 1 ? singular : plural)));
            parent.Add(line);
        }

        FinishCard CreateFinishCard()
        {
            FinishCard card = new FinishCard
            {
                Root = CommonUITK.CreateContainer("ts-migration-card")
            };
            card.Root.name = "ts-migration-finish-card";
            card.Root.Add(Copy("When the steps above are done, remove Text Animator in this order. Nothing here happens automatically."));
            AddChecklistRow(card.Root, 1, "Keep a way back", "Commit your project to version control before removing anything.");
            AddChecklistRow(card.Root, 2, "Check your texts", "Select a text and use the Text Studio preview in the Inspector. It works in Edit Mode, so you can compare before removing anything.");

            VisualElement remove = AddChecklistRow(
                card.Root,
                3,
                "Remove the Text Animator components",
                "Then test in Play Mode. Text Animator and Text Studio should not animate the same text at the same time.");
            card.RemoveDetail = Copy(string.Empty, "ts-migration-small");
            remove.Add(card.RemoveDetail);
            card.SelectButton = Button(
                "Select them in open scenes",
                "Select every object in the open scenes that still has a Text Animator component, so you can remove the components yourself.",
                SelectTextAnimatorObjects,
                "ts-migration-select-text-animator");
            remove.Add(card.SelectButton);

            VisualElement uninstall = AddChecklistRow(
                card.Root,
                4,
                "Remove Text Animator from the project",
                "Once your scripts no longer use it, remove the package in the Package Manager and delete its samples folder.");
            uninstall.Add(Button(
                "Open Package Manager",
                "Open the Package Manager with Text Animator selected.",
                () => UnityEditor.PackageManager.UI.Window.Open(TextAnimatorMigrationAvailability.TextAnimatorPackageName),
                "ts-migration-open-package-manager"));

            card.ExportButton = Button(
                "Export report...",
                "Save a Markdown summary of this migration for your team or for later.",
                ExportReport,
                "ts-migration-export-report");
            card.ExportButton.AddToClassList("ts-migration-export");
            card.Root.Add(card.ExportButton);
            return card;
        }

        static VisualElement AddChecklistRow(VisualElement parent, int number, string title, string text)
        {
            VisualElement row = CommonUITK.CreateContainer("ts-migration-checklist-row");
            row.Add(CommonUITK.CreateLabel(number.ToString(), "ts-migration-checklist-number"));
            VisualElement body = CommonUITK.CreateContainer("ts-migration-checklist-body");
            body.Add(CommonUITK.CreateLabel(title, "ts-migration-item-title"));
            body.Add(Copy(text));
            row.Add(body);
            parent.Add(row);
            return body;
        }

        void UpdateFinish(bool editable)
        {
            int components = 0;
            for (int i = 0; i < _plan.SceneTextAnimatorComponents.Count; i++)
            {
                if (_plan.SceneTextAnimatorComponents[i] != null)
                    components++;
            }

            int locations = _plan.TextAnimatorLocationCount;
            _finishCard.RemoveDetail.text = locations == 0
                ? "No scene or prefab in " + DescribeScope() + " uses Text Animator components anymore."
                : "Text Animator components are still used in " + Count(locations, "scene or prefab", "scenes and prefabs") + ".";
            _finishCard.SelectButton.text = components == 1
                ? "Select 1 object in open scenes"
                : "Select " + components + " objects in open scenes";
            SetDisplay(_finishCard.SelectButton, components > 0);
            _finishCard.SelectButton.SetEnabled(editable);
        }

        void SelectTextAnimatorObjects()
        {
            List<Object> objects = new List<Object>();
            HashSet<int> ids = new HashSet<int>();
            for (int i = 0; i < _plan.SceneTextAnimatorComponents.Count; i++)
            {
                Component component = _plan.SceneTextAnimatorComponents[i];
                if (component != null && ids.Add(component.gameObject.GetStableId()))
                    objects.Add(component.gameObject);
            }

            Selection.objects = objects.ToArray();
            if (objects.Count > 0)
                EditorGUIUtility.PingObject(objects[0]);
        }

        void FillItems(
            Foldout foldout,
            IReadOnlyList<TextAnimatorMigrationPlanItem> items,
            Func<TextAnimatorMigrationPlanItem, string> actionText,
            string actionTooltip,
            Action<TextAnimatorMigrationPlanItem> action,
            bool editable)
        {
            foldout.Clear();
            int shown = Mathf.Min(items.Count, ItemRowLimit);
            for (int i = 0; i < shown; i++)
            {
                TextAnimatorMigrationPlanItem item = items[i];
                string label = actionText != null ? actionText(item) : null;
                foldout.Add(CreateItemRow(item, label, actionTooltip, action, editable));
            }

            if (items.Count > shown)
            {
                VisualElement more = CommonUITK.CreateContainer("ts-migration-card-actions");
                more.Add(Copy("Showing " + shown + " of " + items.Count + ".", "ts-migration-more"));
                more.Add(Button("See all findings", "Open the full, searchable list.", () => SwitchTab(1)));
                foldout.Add(more);
            }
        }

        VisualElement CreateItemRow(
            TextAnimatorMigrationPlanItem item,
            string actionLabel,
            string actionTooltip,
            Action<TextAnimatorMigrationPlanItem> action,
            bool editable)
        {
            VisualElement row = CommonUITK.CreateContainer("ts-migration-item");
            VisualElement text = CommonUITK.CreateContainer("ts-migration-item-text");
            text.Add(CommonUITK.CreateLabel(item.Title, "ts-migration-item-title"));
            if (!string.IsNullOrEmpty(item.Detail))
                text.Add(Copy(item.Detail, "ts-migration-item-detail"));
            if (!string.IsNullOrEmpty(item.Location) && !string.Equals(item.Location, item.Title, StringComparison.Ordinal))
                text.Add(Copy(item.Location, "ts-migration-item-location"));
            row.Add(text);

            VisualElement side = CommonUITK.CreateContainer("ts-migration-item-side");
            side.Add(Pill(StateText(item.State), StateTone(item.State), StateTooltip(item.State)));
            if (item.SelectTarget != null)
            {
                side.Add(ItemButton(
                    "Select",
                    "Select this in the Hierarchy or Project window.",
                    () => SelectItem(item)));
            }

            if (CanOpen(item))
            {
                Button open = ItemButton(
                    "Open",
                    item.OpenPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                        ? "Open this scene. You are asked to save changes in the current scenes first."
                        : "Open this script at the first Text Animator reference.",
                    () => OpenItem(item));
                open.SetEnabled(editable || !item.OpenPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase));
                side.Add(open);
            }

            if (!string.IsNullOrEmpty(actionLabel) && action != null)
            {
                Button run = ItemButton(actionLabel, actionTooltip, () => action(item));
                run.SetEnabled(editable);
                side.Add(run);
            }

            row.Add(side);
            return row;
        }

        static Button ItemButton(string text, string tooltip, Action action)
        {
            Button button = TextStudioInspectorElements.CreateButton(text, tooltip, action);
            button.AddToClassList("ts-migration-item-button");
            return button;
        }

        static bool CanOpen(TextAnimatorMigrationPlanItem item)
        {
            return !string.IsNullOrEmpty(item.OpenPath) &&
                   (item.OpenPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                    item.OpenPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
        }

        static void SelectItem(TextAnimatorMigrationPlanItem item)
        {
            Object target = item.SelectTarget;
            if (target == null)
                return;

            GameObject gameObject = target as GameObject;
            if (gameObject != null && EditorUtility.IsPersistent(gameObject))
            {
                // Objects inside prefab assets are highlighted through their prefab in the Project window.
                GameObject root = gameObject.transform.root.gameObject;
                Selection.activeObject = root;
                EditorGUIUtility.PingObject(root);
                return;
            }

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
            if (gameObject != null && SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();
        }

        void OpenItem(TextAnimatorMigrationPlanItem item)
        {
            if (item.OpenPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                if (!CanEdit() || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;

                EditorSceneManager.OpenScene(item.OpenPath, OpenSceneMode.Single);
                return;
            }

            Object asset = AssetDatabase.LoadMainAssetAtPath(item.OpenPath);
            if (asset != null)
                AssetDatabase.OpenAsset(asset, item.OpenLine);
        }

        static string BuildSignature(IReadOnlyList<TextAnimatorMigrationPlanItem> items)
        {
            StringBuilder builder = new StringBuilder(items.Count * 48);
            for (int i = 0; i < items.Count; i++)
            {
                TextAnimatorMigrationPlanItem item = items[i];
                builder.Append(item.Key).Append('|').Append((int)item.State).Append('|').Append(item.Detail).Append('\n');
            }

            return builder.ToString();
        }

        static string ItemsHeading(TextAnimatorMigrationStepKind kind, TextAnimatorMigrationPlanStep step)
        {
            int objects = 0;
            for (int i = 0; i < step.Items.Count; i++)
            {
                TextAnimatorMigrationPlanItem item = step.Items[i];
                objects += item.State == TextAnimatorMigrationItemState.OpenScene ? item.Count : 1;
            }

            switch (kind)
            {
                case TextAnimatorMigrationStepKind.AddTextStudio:
                    return Count(objects, "text", "texts");
                case TextAnimatorMigrationStepKind.SoundsAndEvents:
                    return Count(objects, "object", "objects");
                case TextAnimatorMigrationStepKind.TypingSpeed:
                    return Count(objects, "timing asset", "timing assets");
                case TextAnimatorMigrationStepKind.Playbacks:
                    return Count(objects, "playback", "playbacks");
                default:
                    return Count(objects, "style", "styles");
            }
        }

        static string StateText(TextAnimatorMigrationItemState state)
        {
            switch (state)
            {
                case TextAnimatorMigrationItemState.Ready:
                    return "Ready";
                case TextAnimatorMigrationItemState.OpenScene:
                    return "Scene closed";
                case TextAnimatorMigrationItemState.Blocked:
                    return "Locked";
                case TextAnimatorMigrationItemState.Done:
                    return "Done";
                case TextAnimatorMigrationItemState.NeedsYou:
                    return "To do";
                default:
                    return "Works";
            }
        }

        static string StateTone(TextAnimatorMigrationItemState state)
        {
            switch (state)
            {
                case TextAnimatorMigrationItemState.Ready:
                    return "info";
                case TextAnimatorMigrationItemState.Blocked:
                case TextAnimatorMigrationItemState.NeedsYou:
                    return "attention";
                case TextAnimatorMigrationItemState.Done:
                case TextAnimatorMigrationItemState.WorksAsIs:
                    return "ready";
                default:
                    return string.Empty;
            }
        }

        static string StateTooltip(TextAnimatorMigrationItemState state)
        {
            switch (state)
            {
                case TextAnimatorMigrationItemState.Ready:
                    return "Converts in one click.";
                case TextAnimatorMigrationItemState.OpenScene:
                    return "This scene is closed. Open it to convert its objects.";
                case TextAnimatorMigrationItemState.Blocked:
                    return "Can't be changed right now. The description explains why.";
                case TextAnimatorMigrationItemState.Done:
                    return "Already converted.";
                case TextAnimatorMigrationItemState.NeedsYou:
                    return "Needs a manual change. The description says what to do.";
                default:
                    return "Works in Text Studio without changes.";
            }
        }

        static VisualElement CreateImpactRow(string key, string value, string tooltip)
        {
            VisualElement row = CommonUITK.CreateContainer("ts-migration-impact-row");
            row.tooltip = tooltip;
            row.Add(CommonUITK.CreateLabel(key, "ts-migration-impact-key"));
            row.Add(Copy(value, "ts-migration-impact-value"));
            return row;
        }

        static Label AddImpactRow(VisualElement parent, string key, string value, string tooltip)
        {
            VisualElement row = CreateImpactRow(key, value, tooltip);
            parent.Add(row);
            return row.Q<Label>(className: "ts-migration-impact-value");
        }

        static string StepTitle(TextAnimatorMigrationStepKind kind)
        {
            switch (kind)
            {
                case TextAnimatorMigrationStepKind.AddTextStudio:
                    return "Add Text Studio to your texts";
                case TextAnimatorMigrationStepKind.SoundsAndEvents:
                    return "Recreate typewriter sounds and events";
                case TextAnimatorMigrationStepKind.TypingSpeed:
                    return "Import typing speed";
                case TextAnimatorMigrationStepKind.Playbacks:
                    return "Import playbacks";
                default:
                    return "Import style aliases";
            }
        }

        static string StepShortName(TextAnimatorMigrationStepKind kind)
        {
            switch (kind)
            {
                case TextAnimatorMigrationStepKind.AddTextStudio:
                    return "texts";
                case TextAnimatorMigrationStepKind.SoundsAndEvents:
                    return "sounds and events";
                case TextAnimatorMigrationStepKind.TypingSpeed:
                    return "typing speed";
                case TextAnimatorMigrationStepKind.Playbacks:
                    return "playbacks";
                default:
                    return "style aliases";
            }
        }

        static string StepDescription(TextAnimatorMigrationStepKind kind)
        {
            switch (kind)
            {
                case TextAnimatorMigrationStepKind.AddTextStudio:
                    return "Text Studio reads the text that is already on each object, so your Text Animator markup keeps working.";
                case TextAnimatorMigrationStepKind.SoundsAndEvents:
                    return "Copies typing sounds to a Text Studio Audio Player and typewriter events to a Typewriter Event Relay.";
                case TextAnimatorMigrationStepKind.TypingSpeed:
                    return "Keeps the reveal pace you tuned in Text Animator, including punctuation pauses.";
                case TextAnimatorMigrationStepKind.Playbacks:
                    return "Keeps the custom playback names used in your markup working.";
                default:
                    return "Keeps style tags from your Text Animator style sheet, such as <style1>, working without rewriting any text.";
            }
        }

        static string StepChanges(TextAnimatorMigrationStepKind kind, TextAnimatorMigrationPlanStep step, string folder)
        {
            switch (kind)
            {
                case TextAnimatorMigrationStepKind.AddTextStudio:
                    return "Adds a Text Studio component next to Text Animator on each text. Prefab instances update with their prefab.";
                case TextAnimatorMigrationStepKind.SoundsAndEvents:
                    return "Adds Text Studio Audio Player and Typewriter Event Relay components, and copies clips, sound settings and event listeners without parameters.";
                case TextAnimatorMigrationStepKind.TypingSpeed:
                    return "Creates Character Timings assets in " + folder + " and sets Text Studio's default typing speed to match.";
                case TextAnimatorMigrationStepKind.Playbacks:
                    return "Creates editable Playback assets in " + folder + " and registers them in Text Studio's Project Settings.";
                default:
                    return "Adds the styles to one tag alias asset in " + folder + " and registers it in Text Studio's Project Settings.";
            }
        }

        static string StepKeeps(TextAnimatorMigrationStepKind kind)
        {
            switch (kind)
            {
                case TextAnimatorMigrationStepKind.AddTextStudio:
                    return "Your text, and the Text Animator components with all their settings.";
                case TextAnimatorMigrationStepKind.SoundsAndEvents:
                    return "The Text Animator components and their listeners. Audio clips are referenced, not copied.";
                case TextAnimatorMigrationStepKind.TypingSpeed:
                    return "Text Animator's timing assets.";
                case TextAnimatorMigrationStepKind.Playbacks:
                    return "The Text Animator playback assets.";
                default:
                    return "Your text and the Text Animator style sheet.";
            }
        }

        static string StepNote(TextAnimatorMigrationStepKind kind)
        {
            switch (kind)
            {
                case TextAnimatorMigrationStepKind.AddTextStudio:
                    return "Check each text with the Text Studio preview, then remove Text Animator before testing in Play Mode, since both would animate the same text.";
                case TextAnimatorMigrationStepKind.SoundsAndEvents:
                    return "Listeners that receive Text Animator data are listed under Needs you, because they have to be reconnected by hand.";
                case TextAnimatorMigrationStepKind.TypingSpeed:
                    return "Only per-character delays are converted. Check how each typewriter starts and skips when you test.";
                case TextAnimatorMigrationStepKind.Playbacks:
                    return "Playbacks driven by custom code can't be read. They are listed under Needs you.";
                default:
                    return "Styles are imported as simple opening and closing markup.";
            }
        }

        static string StepButtonText(TextAnimatorMigrationStepKind kind, int count)
        {
            switch (kind)
            {
                case TextAnimatorMigrationStepKind.AddTextStudio:
                    return "Add Text Studio to " + Count(count, "text", "texts");
                case TextAnimatorMigrationStepKind.SoundsAndEvents:
                    return "Recreate sounds and events on " + Count(count, "object", "objects");
                case TextAnimatorMigrationStepKind.TypingSpeed:
                    return "Import " + Count(count, "typing speed asset", "typing speed assets");
                case TextAnimatorMigrationStepKind.Playbacks:
                    return "Import " + Count(count, "playback", "playbacks");
                default:
                    return "Import " + Count(count, "style alias", "style aliases");
            }
        }

        static string StepButtonTooltip(TextAnimatorMigrationStepKind kind)
        {
            switch (kind)
            {
                case TextAnimatorMigrationStepKind.AddTextStudio:
                    return "Add Text Studio to every ready text. Prefabs are saved; save open scenes yourself. Supports Undo.";
                case TextAnimatorMigrationStepKind.SoundsAndEvents:
                    return "Add Text Studio audio and event components to every ready object. Supports Undo.";
                default:
                    return "Create or update the converted assets in the folder shown above. Running it again updates them instead of adding copies.";
            }
        }

        static string ItemActionText(TextAnimatorMigrationStepKind kind)
        {
            switch (kind)
            {
                case TextAnimatorMigrationStepKind.AddTextStudio:
                    return "Add";
                case TextAnimatorMigrationStepKind.SoundsAndEvents:
                    return "Recreate";
                default:
                    return "Import";
            }
        }

        static string ItemActionTooltip(TextAnimatorMigrationStepKind kind)
        {
            switch (kind)
            {
                case TextAnimatorMigrationStepKind.AddTextStudio:
                    return "Add Text Studio to this text only.";
                case TextAnimatorMigrationStepKind.SoundsAndEvents:
                    return "Recreate the sounds and events of this object only.";
                default:
                    return "Import only this item.";
            }
        }

        static string TopicTitle(TextAnimatorMigrationTopicKind kind)
        {
            switch (kind)
            {
                case TextAnimatorMigrationTopicKind.CustomTags:
                    return "Recreate effects and actions";
                case TextAnimatorMigrationTopicKind.Scripts:
                    return "Update scripts";
                case TextAnimatorMigrationTopicKind.EventListeners:
                    return "Reconnect event listeners";
                case TextAnimatorMigrationTopicKind.UnsupportedPlaybacks:
                    return "Recreate custom playbacks";
                default:
                    return "Check markup";
            }
        }

        static string TopicShortName(TextAnimatorMigrationTopicKind kind)
        {
            switch (kind)
            {
                case TextAnimatorMigrationTopicKind.CustomTags:
                    return "effects and actions";
                case TextAnimatorMigrationTopicKind.Scripts:
                    return "scripts";
                case TextAnimatorMigrationTopicKind.EventListeners:
                    return "event listeners";
                case TextAnimatorMigrationTopicKind.UnsupportedPlaybacks:
                    return "playbacks";
                default:
                    return "markup";
            }
        }

        static string TopicGuidance(TextAnimatorMigrationTopicKind kind)
        {
            switch (kind)
            {
                case TextAnimatorMigrationTopicKind.CustomTags:
                    return "These tags come from Text Animator effects or actions that Text Studio doesn't include. Create a Text Studio effect or action with the same name and every text that uses the tag works again, without editing any text. Effects need no code: use Assets > Create > Text Studio > Animation > Composed Effect.";
                case TextAnimatorMigrationTopicKind.Scripts:
                    return "These scripts use the Text Animator API and won't compile once Text Animator is removed. Switch them to TextStudioText, which offers matching calls:";
                case TextAnimatorMigrationTopicKind.EventListeners:
                    return "These listeners receive Text Animator data such as CharacterData, so they can't be copied. Reconnect them on the Typewriter Event Relay: OnCharacterVisible passes the character index and text, and OnMessage passes the event name and payload.";
                case TextAnimatorMigrationTopicKind.UnsupportedPlaybacks:
                    return "These playbacks run custom code that can't be read. Create a Playback asset with the same name and similar timing (Assets > Create > Text Studio > Animation > Playback) and add it to the playback catalog in Text Studio's Project Settings.";
                default:
                    return "Text Studio can't map these parameters automatically. Select each text and adjust the tag as described.";
            }
        }
    }
}
