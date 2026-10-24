using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using ImpossibleRobert.Common.Timing;
using TMPro;
using TextStudio.BuiltIns;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    internal sealed class TextStudioCoreGalleryProvider : ITextStudioGalleryProvider
    {
        const float SmallFontSize = 34f;
        const float LargeFontSize = 48f;
        const float RuntimeCapturePlacementScale = 0.1f;
        const float WritingGalleryFontSize = 48f;
        static readonly Vector2 WritingGalleryBounds =
            new Vector2(560f, 180f);

        public int SortOrder => 0;

        public void AddCards(List<TextGalleryCard> cards)
        {
            cards.Add(CreateEffectCard("EffectWave", "Wave Effect", "Travelling sine motion for playful words.", TextGalleryActions.CreateWaveEffectExample,
                "Hello <wave>world</wave>"));
            cards.Add(CreateEffectCard("EffectAreaWords", "Effect Area Grouping", "Apply effects per word, line, paragraph, range, or grouped units instead of every glyph.", TextGalleryActions.CreateEffectAreaWordsExample,
                "<wave area=word group=2>Grouped words move together</wave>", RevealMode.Instant, false, 18f));
            cards.Add(CreateEffectCard("EffectShake", "Shake Effect", "Deterministic jitter for impact or instability.", TextGalleryActions.CreateShakeEffectExample,
                "Careful, <shake>unstable</shake>"));
            cards.Add(CreateEffectCard("EffectWiggle", "Wiggle Effect", "Small organic position and rotation wobble.", TextGalleryActions.CreateWiggleEffectExample,
                "<wiggle>Playful motion</wiggle>"));
            cards.Add(CreateEffectCard("EffectBounce", "Bounce Effect", "Per-glyph hop with a lively stagger.", TextGalleryActions.CreateBounceEffectExample,
                "<bounce>Bouncy text</bounce>"));
            cards.Add(CreateEffectCard("EffectFineTuning", "Fine-Tuned Parameters", "Inline aliases and parameters adjust amplitude, speed, stagger, playback, colors, and timing.", TextGalleryActions.CreateFineTunedEffectExample,
                "<bounce a=.28 s=1.8 stagger=.08>Fine-tuned bounce</bounce>", RevealMode.Instant, false, 18f));
            cards.Add(CreateEffectCard("EffectSwing", "Swing Effect", "Rotational swing around each glyph origin.", TextGalleryActions.CreateSwingEffectExample,
                "<swing>Swinging text</swing>"));
            cards.Add(CreateEffectCard("EffectPendulum", "Pendulum Motion", "Uniform slow swing across the range.", () => TextGalleryActions.CreateEffectExample("Pendulum Effect", "<pendulum>Soft swing</pendulum>", RevealMode.Instant),
                "<pendulum>Soft swing</pendulum>"));
            cards.Add(CreateEffectCard("EffectDangle", "Dangle Motion", "Offset phase rotation for hanging glyphs.", () => TextGalleryActions.CreateEffectExample("Dangle Effect", "<dangle>Dangling text</dangle>", RevealMode.Instant),
                "<dangle>Dangling text</dangle>"));
            cards.Add(CreateEffectCard("EffectPulse", "Pulse Effect", "Gentle scale and alpha pulse for prompts.", TextGalleryActions.CreatePulseEffectExample,
                "<pulse>Press any key</pulse>"));
            cards.Add(CreateEffectCard("EffectBlink", "Blink Effect", "On/off alert text for warnings and cursors.", TextGalleryActions.CreateBlinkEffectExample,
                "<blink>Warning</blink>"));
            cards.Add(CreateEffectCard("EffectShimmer", "Shimmer Effect", "Moving highlight band for premium labels.", TextGalleryActions.CreateShimmerEffectExample,
                "<shimmer>Legendary reward</shimmer>", RevealMode.Instant, false, 20f));
            cards.Add(CreateEffectCard("EffectGradientFlow", "Gradient Flow", "Animated two-color gradient across glyphs.", TextGalleryActions.CreateGradientFlowEffectExample,
                "<gradientFlow>Gradient flow</gradientFlow>"));
            cards.Add(CreateEffectCard("EffectColorPulse", "Color Pulse", "Pulses between two colors for important words.", TextGalleryActions.CreateColorPulseEffectExample,
                "<colorPulse>Rare item</colorPulse>"));
            cards.Add(CreateEffectCard("EffectFloat", "Float Effect", "Slow buoyant drift for magical text.", TextGalleryActions.CreateFloatEffectExample,
                "<float>Magic words</float>"));
            cards.Add(CreateEffectCard("EffectRainbow", "Rainbow Color", "Animated hue cycling across the text.", TextGalleryActions.CreateRainbowEffectExample,
                "<rainbow>Rainbow text</rainbow>"));
            cards.Add(CreateEffectCard("EffectFade", "Fade In", "Alpha reveal for soft appearing text.", TextGalleryActions.CreateFadeEffectExample,
                "<fade>Soft reveal</fade>", RevealMode.ByGlyph, true));
            cards.Add(CreateEffectCard("EffectTypewriter", "Typewriter Effect", "Classic paper-strike style reveal effect for typewriter-flavored text.", TextGalleryActions.CreateTypewriterEffectExample,
                "<typewriter>Paper strike text</typewriter>", RevealMode.ByGlyph, true, 20f));
            cards.Add(CreateEffectCard("EffectSize", "Size In", "Clear scale-in reveal with a gentle active pulse.", TextGalleryActions.CreateSizeEffectExample,
                TextGalleryActions.SizeMarkup, RevealMode.ByGlyph, true, 0f,
                label => label.RevealAppearanceDuration = 0.5f,
                hub => hub.SetRevealAppearanceDuration(0.5f)));
            cards.Add(CreateEffectCard("EffectPop", "Pop Effect", "Punchy overshoot entrance for rewards.", TextGalleryActions.CreatePopEffectExample,
                "<pop>Bonus!</pop>", RevealMode.ByGlyph, true));
            cards.Add(CreateEffectCard("EffectRotate", "Rotate In", "Spins glyphs into place during reveal.", TextGalleryActions.CreateRotateEffectExample,
                "<rotate>Spin in</rotate>", RevealMode.ByGlyph, true));
            cards.Add(CreateEffectCard("EffectSlide", "Slide In", "Horizontal slide-in for kinetic captions.", TextGalleryActions.CreateSlideEffectExample,
                "<slide>Slide in</slide>", RevealMode.ByGlyph, true));
            cards.Add(CreateRenderedEffectCard("EffectSlideVertical", "Vertical Slide", "Distinct rise and drop transitions using the slideV and slideY Text Animator-style aliases.", TextGalleryActions.CreateSlideVerticalEffectExample,
                TextGalleryActions.VerticalSlideMarkup).WithDisplayTitle("<slideV>"));
            cards.Add(CreateEffectCard("EffectShear", "Shear Effect", "Animated skew for Text Animator-compatible horizontal and vertical shear styles.", TextGalleryActions.CreateShearEffectExample,
                "<shear a=1.2 horizontal=3 vertical=0>Skewed signal</shear>", RevealMode.Instant, false, 18f));
            cards.Add(CreateEffectCard("EffectExpand", "Expand Effect", "Soft pop-in combining alpha and scale.", TextGalleryActions.CreateExpandEffectExample,
                "<expand>Expand</expand>", RevealMode.ByGlyph, true));
            cards.Add(CreateEffectCard("EffectScramble", "Scramble Text", "Presentation-layer decode effect that swaps visible glyphs without changing resolved text, markers, or layout.", TextGalleryActions.CreateScrambleTextExample,
                "<scramble once duration=.8 revealDuration=.35 charset=hex>ACCESS GRANTED</scramble>", RevealMode.ByGlyph, true, 18f));
            cards.Add(CreateEffectCard("EffectDecrypt", "Decrypt Effect", "Lock-in jitter during glyph appearance.", TextGalleryActions.CreateDecryptEffectExample,
                "<decrypt>ACCESS GRANTED</decrypt>", RevealMode.ByGlyph, true, 20f));
            cards.Add(CreateEffectCard("EffectGlitch", "Glitch Effect", "Occasional slicing jitter and red color shift.", TextGalleryActions.CreateGlitchEffectExample,
                "<glitch>SIGNAL LOST</glitch>", RevealMode.Instant, false, 20f));
            cards.Add(CreateEffectCard("EffectEmphasis", "Emphasis Effect", "Gentle pulse for highlighted words.", TextGalleryActions.CreateEmphasisEffectExample,
                "This is <emphasis>important</emphasis>", RevealMode.Instant, false, 20f));
            cards.Add(CreateEffectCard("EffectElastic", "Elastic", "Squash-and-stretch entrance for combat, rewards, buttons, and dialogue beats.", TextGalleryActions.CreateElasticEffectExample,
                "<elastic>CRITICAL!</elastic>", RevealMode.ByGlyph, true, 20f));
            cards.Add(CreateEffectCard("EffectScatter2D", "Scatter 2D", "Seeded directional or radial assembly and fly-away motion.", TextGalleryActions.CreateScatter2DEffectExample,
                "<scatter2D>ASSEMBLE</scatter2D>", RevealMode.ByGlyph, true, 20f));
            cards.Add(CreateEffectCard("EffectTracking", "Visual Tracking", "Cinematic character-spacing motion without text reflow.", TextGalleryActions.CreateTrackingEffectExample,
                "<tracking amount=.7>CINEMATIC</tracking>", RevealMode.ByGlyph, true, 18f));
            cards.Add(CreateEffectCard("EffectCornerWarp", "Corner Warp", "Flag, ripple, jelly, squeeze, and bulge deformation from one effect.", TextGalleryActions.CreateCornerWarpEffectExample,
                "<cornerWarp mode=flag>ORGANIC</cornerWarp>", RevealMode.Instant, false, 20f));
            cards.Add(CreateRenderedEffectCard("EffectFocus", "Focus", "Defocus-to-sharp material reveal.", TextGalleryActions.CreateFocusEffectExample,
                TextGalleryActions.FocusMarkup, true, TextStudioPreviewSegment.Active));
            cards.Add(CreateRenderedEffectCard("EffectDissolve", "Dissolve", "Soft threshold dissolve with continuous Text mapping or per-Glyph mapping.", TextGalleryActions.CreateDissolveEffectExample,
                TextGalleryActions.DissolveMarkup, true));
            cards.Add(CreateRenderedEffectCard("EffectBurn", "Burn", "Procedural hot and ember edges travel continuously across the complete text.", TextGalleryActions.CreateBurnEffectExample,
                TextGalleryActions.BurnMarkup, true));
            cards.Add(CreateRenderedEffectCard("EffectOutlineGlow", "Outline & Glow", "Animated outline and glow emphasis for selections and rewards.", TextGalleryActions.CreateOutlineGlowEffectExample,
                TextGalleryActions.OutlineGlowMarkup, true, TextStudioPreviewSegment.Active));
            cards.Add(CreateRenderedEffectCard("EffectSurfaceSweep", "Surface Sweep", "True material highlight for premium labels and rewards.", TextGalleryActions.CreateSurfaceSweepEffectExample,
                TextGalleryActions.SurfaceSweepMarkup, true, TextStudioPreviewSegment.Active));
            cards.Add(CreateRenderedEffectCard("EffectChromaticGlitch", "Chromatic Glitch", "Seeded RGB separation with restrained slicing.", TextGalleryActions.CreateChromaticGlitchEffectExample,
                TextGalleryActions.ChromaticGlitchMarkup, true, TextStudioPreviewSegment.Active));
            cards.Add(CreateRenderedEffectCard(
                "EffectPixelGrid",
                "Pixel Grid",
                "True SDF pixelation with screen-stable and glyph-anchored cells.",
                TextGalleryActions.CreatePixelGridEffectExample,
                TextGalleryActions.PixelGridMarkup,
                true));

            cards.Add(CreateRenderedEffectCard("EffectSquiggle", "Squiggle", "Stepped hand-drawn contours inside flat text glyphs.", TextGalleryActions.CreateSquiggleEffectExample,
                TextGalleryActions.SquiggleMarkup, true, TextStudioPreviewSegment.Active));

            AddVisualDesignCards(cards);

            cards.Add(CreateTextCard("PlaceLinear", "Linear Placement", "Default TextMeshPro layout.", TextGalleryCategory.Placement, TextGalleryActions.CreateLinearPlacementExample,
                "Linear Layout", RevealMode.Instant,
                label =>
                {
                    label.ConfigureLinearPlacement();
                    label.SetPlacementOrientation(
                        TextStudioPlacementOrientation.KeepOriginal,
                        new Vector3(0f, 0f, 5f));
                },
                hub =>
                {
                    hub.ConfigureLinearPlacement();
                    hub.SetPlacementOrientation(
                        TextStudioPlacementOrientation.KeepOriginal,
                        new Vector3(0f, 0f, 5f));
                },
                false));
            cards.Add(CreateTextCard("PlaceArc", "Arc Path", "Text arranged along a circular arc.", TextGalleryCategory.Placement, TextGalleryActions.CreateArcPlacementExample,
                "Arc Path", RevealMode.Instant,
                label =>
                {
                    label.ConfigureArcPlacement(32f, 150f, -75f);
                    label.SetPlacementOrientation(TextStudioPlacementOrientation.FollowPath);
                },
                hub =>
                {
                    hub.ConfigureArcPlacement(
                        32f * RuntimeCapturePlacementScale,
                        150f,
                        -75f);
                    hub.SetPlacementOrientation(TextStudioPlacementOrientation.FollowPath);
                },
                false,
                20f));
            cards.Add(CreateTextCard("PlaceWave", "Wave Path", "Text arranged along an animated wave path.", TextGalleryCategory.Placement, TextGalleryActions.CreateWavePlacementExample,
                "Wave Text Path", RevealMode.Instant,
                label =>
                {
                    label.ConfigureWavePlacement(
                        TextGalleryActions.WavePlacementAmplitude,
                        TextGalleryActions.WavePlacementFrequency,
                        TextGalleryActions.WavePlacementSpeed);
                    label.SetPlacementOrientation(TextStudioPlacementOrientation.FollowPath);
                },
                hub =>
                {
                    hub.ConfigureWavePlacement(
                        TextGalleryActions.WavePlacementAmplitude * RuntimeCapturePlacementScale,
                        TextGalleryActions.WavePlacementFrequency,
                        TextGalleryActions.WavePlacementSpeed);
                    hub.SetPlacementOrientation(TextStudioPlacementOrientation.FollowPath);
                },
                false));
            cards.Add(CreateTextCard("PlaceCircle", "Circle Path", "Text wrapped around a circle.", TextGalleryCategory.Placement, TextGalleryActions.CreateCirclePlacementExample,
                "Circle", RevealMode.Instant,
                label =>
                {
                    label.ConfigureCirclePlacement(30f, -24f);
                    label.SetPlacementOrientation(TextStudioPlacementOrientation.FollowPath);
                },
                hub =>
                {
                    hub.ConfigureCirclePlacement(
                        30f * RuntimeCapturePlacementScale,
                        -24f);
                    hub.SetPlacementOrientation(TextStudioPlacementOrientation.FollowPath);
                },
                false,
                20f));
            cards.Add(CreateTextCard("PlaceGrid", "Grid Placement", "Glyphs arranged in a compact grid.", TextGalleryCategory.Placement, TextGalleryActions.CreateGridPlacementExample,
                "GRIDTEXT", RevealMode.Instant,
                label => label.ConfigureGridPlacement(
                    TextGalleryActions.GridPlacementColumns,
                    GridCellSizing.Fixed,
                    new Vector2(20f, 22f)),
                hub => hub.ConfigureGridPlacement(
                    TextGalleryActions.GridPlacementColumns,
                    GridCellSizing.Fixed,
                    new Vector2(
                        20f * RuntimeCapturePlacementScale,
                        22f * RuntimeCapturePlacementScale)),
                false,
                20f));

            cards.Add(CreateTextCard("RevealGlyph", "Typewriter (Glyph)", "Per-character reveal with punctuation-aware timing.", TextGalleryCategory.Reveal, TextGalleryActions.CreateRevealGlyphExample,
                "Hello, World!", RevealMode.ByGlyph, null, null, true));
            cards.Add(CreateTextCard("RevealWord", "Typewriter (Word)", "Reveal text word by word for subtitle and dialogue pacing.", TextGalleryCategory.Reveal, TextGalleryActions.CreateRevealWordExample,
                "The quick brown fox jumps", RevealMode.ByWord, null, null, true));
            cards.Add(CreateTextCard("RevealLine", "Typewriter (Line)", "Reveal complete lines in sequence.", TextGalleryCategory.Reveal, TextGalleryActions.CreateRevealLineExample,
                "First line.\nSecond line.", RevealMode.ByLine, null, null, true));
            cards.Add(CreateTextCard("RevealInstant", "Instant Reveal", "Static text with Text Studio styling and effects.", TextGalleryCategory.Reveal, TextGalleryActions.CreateInstantExample,
                "Instant Text", RevealMode.Instant, null, null, false));
            cards.Add(CreateTextCard("RevealTypewriterPreset", "Typewriter Presets", "Curated reveal recipes with timing, reveal mode, motion, loops, and hide behavior.", TextGalleryCategory.Reveal, TextGalleryActions.CreateTypewriterPresetExample,
                "<fade><slideV a=.055>Dialogue typewriter preset", RevealMode.ByGlyph,
                label => label.BuiltInTimingsPreset = RevealTimingsPreset.Narrative,
                hub => ConfigureBuiltInTimings(hub, RevealTimingsPreset.Narrative),
                true,
                20f));
            cards.Add(CreateTextCard("RevealTypewriterEffects", "Reveal & Hide Effects", "Choose shared-library effects for the appearance and disappearance phases while keeping Typewriter timing independent.", TextGalleryCategory.Reveal, TextGalleryActions.CreateTypewriterEffectsExample,
                "<appear fade,size,slideV,slideH>Paper strike motion</appear>", RevealMode.ByGlyph,
                label => label.BuiltInTimingsPreset = RevealTimingsPreset.Typist,
                hub => ConfigureBuiltInTimings(hub, RevealTimingsPreset.Typist),
                true,
                20f));
            cards.Add(CreateTextCard("RevealActionFlow", "Waits And Speed Tags", "Inline wait, speed, and wait-for-input tags for dialogue flow and continue-to-skip behavior.", TextGalleryCategory.Reveal, TextGalleryActions.CreateActionFlowExample,
                TextGalleryActions.ActionFlowMarkup, RevealMode.ByGlyph, null, null, true, 18f));
            cards.Add(CreateTextCard("RevealMarkerEvents", "Marker Event Router", "Inspector-friendly UnityEvent routing for authored event tags and production dialogue cues.", TextGalleryCategory.Reveal, TextGalleryActions.CreateMarkerEventRouterExample,
                "Choose <event=choiceA,speaker,line42><pulse>north gate</pulse>.", RevealMode.ByGlyph, null, null, true, 18f));
            cards.Add(CreateTextCard("RevealLongTextPages", "Long Text Pages", "Page overflow and follow-reveal helpers for codex pages, credits, tutorials, and dialogue history.", TextGalleryCategory.Reveal, TextGalleryActions.CreateLongTextPagesExample,
                "<typewriter>Chapter One</typewriter>\nPage overflow follows reveal.", RevealMode.ByGlyph, null, null, true, 18f));
            cards.Add(CreateTextCard("RevealAudioPlayer", "Dialogue Audio Player", "Per-character audio cue component for dialogue and subtitles.", TextGalleryCategory.Reveal, TextGalleryActions.CreateDialogueAudioPlayerExample,
                "Audio-ready <wave>dialogue</wave> line.", RevealMode.ByGlyph, null, null, true, 20f));
            cards.Add(CreateTextCard("RevealCenterOut", "Center-Out Sequence", "Production sequencing for forward, reverse, center-out, edges-in, and seeded-random reveals.", TextGalleryCategory.Reveal, TextGalleryActions.CreateCenterOutRevealExample,
                "<elastic>CENTER OUT</elastic>", RevealMode.ByGlyph, null, null, true, 20f));
        }

        static void AddVisualDesignCards(List<TextGalleryCard> cards)
        {
            AddVisualDesignCards(
                cards,
                TextStudioVisualDesignCatalog.Appearance);
            AddVisualDesignCards(
                cards,
                TextStudioVisualDesignCatalog.Writing);
            AddVisualDesignCards(
                cards,
                TextStudioVisualDesignCatalog.CompleteDesigns);
            foreach (TextStudioStyleCatalogEntry entry in new TextStudio2DStyleCatalogProvider(Array.Empty<Object>()).GetEntries())
            {
                if (entry.BuiltIn) continue;
                cards.Add(TextStudioStyleGalleryCards.Create(entry, TextGalleryCategory.Design,
                    () => new TextStudio2DStyleCatalogProvider(CaptureStyleTargets()), () =>
                    {
                        TMP_Text text = TextGalleryActions.CreateTMPExample("TextStudio - " + entry.Title, entry.SampleText, RevealMode.Instant, PlacementMode.Linear);
                        if (text != null) new TextStudio2DStyleCatalogProvider(new Object[] { text.GetComponent<TextStudioText>() }).Apply(entry);
                    }));
            }
        }

        static Object[] CaptureStyleTargets()
        {
            List<Object> owners = new List<Object>();
            foreach (GameObject selected in Selection.gameObjects)
            {
                TextStudioText text = selected != null ? selected.GetComponent<TextStudioText>() : null;
                if (text != null && !owners.Contains(text)) owners.Add(text);
            }
            return owners.ToArray();
        }

        static void AddVisualDesignCards(
            List<TextGalleryCard> cards,
            IReadOnlyList<TextStudioVisualDesignEntry> entries)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                TextStudioVisualDesignEntry entry = entries[i];
                TextGalleryCard card = new TextGalleryCard(
                    entry.Id,
                    entry.Title,
                    entry.Description,
                    TextGalleryCategory.Design,
                    () => ExecuteVisualDesignAction(entry, false),
                    context => TextGalleryRenderedPreview.Create(
                        TextStudioPackagePaths.BasePackageName,
                        TextGalleryRenderedPreview.BuildPreviewPath(
                            entry.PreviewFileName),
                        entry.Title,
                        GetVisualDesignAccent(entry.Kind)),
                    TextGalleryPreviewQuality.RenderedStatic,
                    GetVisualDesignSearchKeywords(entry),
                    GetVisualDesignActionLabel(entry.Kind, false, false),
                    GetVisualDesignActionTooltip(
                        entry.Kind,
                        false,
                        false,
                        default),
                    () => ExecuteVisualDesignAction(entry, true),
                    "Create New",
                    "Create a separate editable Text Studio object without changing the selection.",
                    () => CreateVisualDesignActionState(entry),
                    additionalBadges: new[]
                    {
                        GetVisualDesignCollectionLabel(entry.Collection)
                    },
                    productionMetadata: entry.ProductionMetadata)
                    .WithCaptureDescriptor(new TextGalleryCaptureDescriptor(
                        entry.SampleText,
                        ResolveVisualDesignRevealMode(entry),
                        false,
                        40f,
                        hub => ConfigureVisualDesignCapture(entry, hub),
                        TextStudioPreviewSegment.All,
                        3.2f,
                        0.68f));
                cards.Add(card);
            }
        }

        static string[] GetVisualDesignSearchKeywords(
            in TextStudioVisualDesignEntry entry)
        {
            string[] keywords = new string[entry.SearchKeywords.Length + 1];
            Array.Copy(
                entry.SearchKeywords,
                keywords,
                entry.SearchKeywords.Length);
            keywords[keywords.Length - 1] =
                GetVisualDesignCollectionLabel(entry.Collection);
            return keywords;
        }

        static string GetVisualDesignCollectionLabel(
            TextStudioVisualDesignCollection collection)
        {
            return collection switch
            {
                TextStudioVisualDesignCollection.Featured =>
                    "Featured Appearance",
                TextStudioVisualDesignCollection.CoreAppearance =>
                    "Core Appearance",
                TextStudioVisualDesignCollection.Materials => "Materials",
                TextStudioVisualDesignCollection.EditorialGraphic =>
                    "Editorial Graphics",
                TextStudioVisualDesignCollection.AnimatedAppearance =>
                    "Animated Appearance",
                TextStudioVisualDesignCollection.GameTitles => "Game Titles",
                TextStudioVisualDesignCollection.CinematicTitles => "Cinematic Titles",
                TextStudioVisualDesignCollection.Writing => "Writing",
                TextStudioVisualDesignCollection.CompleteDesign =>
                    "Complete Design",
                _ => "Visual Design"
            };
        }

        static TextGalleryCardActionState CreateVisualDesignActionState(
            TextStudioVisualDesignEntry entry)
        {
            bool assetAvailable = IsVisualDesignAssetAvailable(entry);
            TextStudioAppearanceBackendSummary backendSummary =
                TextStudioAppearanceBackendSupport.EvaluateSelection();
            bool hasTargets = backendSummary.TargetCount > 0;
            bool appearanceInactive =
                backendSummary.HasUnsupportedTargets &&
                IncludesAppearance(entry);
            return new TextGalleryCardActionState(
                assetAvailable
                    ? () => ExecuteVisualDesignAction(entry, !hasTargets)
                    : null,
                GetVisualDesignActionLabel(
                    entry.Kind,
                    hasTargets,
                    appearanceInactive),
                assetAvailable
                    ? GetVisualDesignActionTooltip(
                        entry.Kind,
                        hasTargets,
                        appearanceInactive,
                        backendSummary)
                    : "The packaged preset asset is unavailable. Rebuild the Text Studio visual design assets from the Debug Samples menu.",
                assetAvailable && hasTargets
                    ? () => ExecuteVisualDesignAction(entry, true)
                    : null,
                hasTargets ? "Create New" : string.Empty,
                hasTargets
                    ? "Create a separate editable Text Studio object without changing the selection."
                    : string.Empty);
        }

        static void ExecuteVisualDesignAction(
            TextStudioVisualDesignEntry entry,
            bool createNew)
        {
            if (createNew || !HasSelectedTextStudioTargets())
            {
                CreateVisualDesignText(entry);
                return;
            }

            GameObject[] selection = Selection.gameObjects;
            List<TextStudioText> targets = new List<TextStudioText>(selection.Length);
            for (int i = 0; i < selection.Length; i++)
            {
                TextStudioText target = selection[i] != null
                    ? selection[i].GetComponent<TextStudioText>()
                    : null;
                if (target != null && !targets.Contains(target))
                    targets.Add(target);
            }
            if (targets.Count == 0)
                return;

            Object[] undoTargets = new Object[targets.Count];
            for (int i = 0; i < targets.Count; i++)
                undoTargets[i] = targets[i];
            Undo.RecordObjects(undoTargets, "Apply Text Studio Visual Design");
            for (int i = 0; i < targets.Count; i++)
            {
                ApplyVisualDesign(entry, targets[i]);
                EditorUtility.SetDirty(targets[i]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    targets[i]);
                if (!Application.isPlaying &&
                    targets[i].gameObject.scene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                        targets[i].gameObject.scene);
                }
            }
        }

        static void CreateVisualDesignText(TextStudioVisualDesignEntry entry)
        {
            if (!IsVisualDesignAssetAvailable(entry))
            {
                Debug.LogWarning(
                    "[Text Studio] The visual design preset asset is unavailable: " +
                    entry.Title);
                return;
            }

            RevealMode revealMode = ResolveVisualDesignRevealMode(entry);
            TMP_Text text = TextGalleryActions.CreateTMPExample(
                "TextStudio - " + entry.Title,
                entry.SampleText,
                revealMode,
                PlacementMode.Linear);
            TextStudioText hub = text != null
                ? text.GetComponent<TextStudioText>()
                : null;
            if (hub == null)
                return;

            Undo.RecordObjects(
                new Object[] { hub, text },
                "Apply Text Studio Visual Design");
            ConfigureWritingGalleryTypography(entry, text, hub);
            TextStudioVisualDesignCatalog.ConfigureAppearanceTypography(text, entry.Id, text.fontSize);
            ApplyVisualDesign(entry, hub);
            StartAppearanceGalleryPreview(entry, hub);
            StartWritingGalleryPreview(entry, hub);
            EditorUtility.SetDirty(hub);
            EditorUtility.SetDirty(text);
            PrefabUtility.RecordPrefabInstancePropertyModifications(hub);
            PrefabUtility.RecordPrefabInstancePropertyModifications(text);
        }

        static void ApplyVisualDesign(
            TextStudioVisualDesignEntry entry,
            TextStudioText target)
        {
            if (target == null)
                return;

            switch (entry.Kind)
            {
                case TextStudioVisualDesignAssetKind.Appearance:
                    TextStudioVisualDesignCatalog.ConfigureAppearanceResources(target, entry.Id);
                    TextStudio2DStyleSelection.Assign(target,
                        TextStudioVisualDesignCatalog.LoadAsset<TextStudio2DStyleAsset>(entry), TextStudioStyleSelectionMode.Catalog);
                    break;
                case TextStudioVisualDesignAssetKind.Writing:
                    TextStudioTypewriterSettings writing = TextStudioTypewriterSettings.Default;
                    writing.RevealMode = entry.Id == "WritingCaptionLineWipe" ? RevealMode.ByLine : RevealMode.ByLetter;
                    writing.TimingMode = TextStudioTypewriterTimingMode.FinishCurrentUnit;
                    writing.WritingEnabled = true;
                    writing.WritingStyle = TextStudioVisualDesignCatalog.LoadAsset<TextStudioWritingStyleAsset>(entry);
                    writing.EraseOnHide = entry.Id == "WritingReverseErase";
                    target.ApplyTypewriterSettings(writing);
                    break;
                case TextStudioVisualDesignAssetKind.CompleteDesign:
                    TextStudioDesignPresetAsset preset =
                        TextStudioVisualDesignCatalog.LoadAsset<
                            TextStudioDesignPresetAsset>(entry);
                    if (preset != null)
                    {
                        TextStudioStyleSelection selection = TextStudio2DStyleSelection.State(target);
                        selection.Remember();
                        preset.ApplyTo(target);
                        TextStudio2DStyleSelection.State(target).SetAssigned(TextStudioStyleSelectionMode.Catalog, target.AppearanceStyle);
                    }
                    break;
            }
        }

        static RevealMode ResolveVisualDesignRevealMode(
            in TextStudioVisualDesignEntry entry)
        {
            return entry.Kind == TextStudioVisualDesignAssetKind.Appearance
                ? RevealMode.Instant
                : entry.Id == "WritingCaptionLineWipe" ? RevealMode.ByLine : RevealMode.ByLetter;
        }

        static void ConfigureVisualDesignCapture(
            TextStudioVisualDesignEntry entry,
            TextStudioText target)
        {
            TMP_Text text = target != null
                ? target.GetComponent<TMP_Text>()
                : null;
            ConfigureWritingGalleryTypography(entry, text, target);
            if (text != null)
                TextStudioVisualDesignCatalog.ConfigureAppearanceTypography(text, entry.Id, text.fontSize);
            ApplyVisualDesign(entry, target);
            if (entry.Collection == TextStudioVisualDesignCollection.AnimatedAppearance && target != null)
            {
                target.Preview.SetSegments(TextStudioPreviewSegment.Active);
                target.Preview.Scrub(TextStudioVisualDesignCatalog.ResolveAppearancePreviewProgress(entry.Id));
            }
            if (entry.Id != "WritingReverseErase")
                return;

            target.Preview.SetSegments(TextStudioPreviewSegment.Hide);
            target.Preview.Scrub(0.42f);
        }

        internal static bool ConfigureWritingGalleryTypography(
            in TextStudioVisualDesignEntry entry,
            TMP_Text text,
            TextStudioText hub)
        {
            if (entry.Kind != TextStudioVisualDesignAssetKind.Writing)
                return true;
            if (text == null || hub == null)
                return false;

            TMP_FontAsset font =
                TextStudioVisualDesignCatalog.LoadWritingDisplayFont();
            if (font == null)
            {
                Debug.LogWarning(
                    "[Text Studio] The packaged Kalam Writing font is unavailable. " +
                    entry.Title +
                    " will use its deterministic fallback instead of the curated gallery typography.");
                return false;
            }

            text.font = font;
            text.fontSize = WritingGalleryFontSize;
            text.fontStyle = FontStyles.Normal;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.richText = true;
            text.enableAutoSizing = false;
            text.rectTransform.sizeDelta = WritingGalleryBounds;
            UnityUtils.SetTextMeshProWordWrapping(text, false);

            text.ForceMeshUpdate(true, true);
            hub.SetText(entry.SampleText);
            hub.DiscoverRenderersAndProviders();
            hub.ForceMeshRefresh(0f);
            return true;
        }

        static void StartAppearanceGalleryPreview(
            in TextStudioVisualDesignEntry entry,
            TextStudioText hub)
        {
            if (entry.Kind != TextStudioVisualDesignAssetKind.Appearance || hub == null)
                return;
            hub.DiscoverRenderersAndProviders();
            hub.ForceMeshRefresh(0f);
            if (entry.Collection != TextStudioVisualDesignCollection.AnimatedAppearance || Application.isPlaying)
                return;
            hub.Preview.SetSegments(TextStudioPreviewSegment.Active);
            hub.Preview.Repeat = true;
            hub.Preview.Restart();
        }

        static void StartWritingGalleryPreview(
            in TextStudioVisualDesignEntry entry,
            TextStudioText hub)
        {
            if (entry.Kind != TextStudioVisualDesignAssetKind.Writing ||
                hub == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                hub.StartReveal();
                return;
            }

            hub.Preview.SetSegments(entry.Id == "WritingReverseErase" ? TextStudioPreviewSegment.Reveal | TextStudioPreviewSegment.Hide : TextStudioPreviewSegment.Reveal);
            hub.Preview.Repeat = true;
            hub.Preview.Restart();
        }

        static bool IsVisualDesignAssetAvailable(
            TextStudioVisualDesignEntry entry)
        {
            return entry.Kind switch
            {
                TextStudioVisualDesignAssetKind.Appearance =>
                    TextStudioVisualDesignCatalog.LoadAsset<
                        TextStudio2DStyleAsset>(entry) != null,
                TextStudioVisualDesignAssetKind.Writing =>
                    TextStudioVisualDesignCatalog.LoadAsset<
                        TextStudioWritingStyleAsset>(entry) != null,
                TextStudioVisualDesignAssetKind.CompleteDesign =>
                    TextStudioVisualDesignCatalog.LoadAsset<
                        TextStudioDesignPresetAsset>(entry) != null,
                _ => false
            };
        }

        static bool HasSelectedTextStudioTargets()
        {
            return TextStudioAppearanceBackendSupport
                .EvaluateSelection()
                .TargetCount > 0;
        }

        static string GetVisualDesignActionLabel(
            TextStudioVisualDesignAssetKind kind,
            bool hasTargets,
            bool appearanceInactive)
        {
            if (!hasTargets)
                return "Create Styled Text";
            return kind switch
            {
                TextStudioVisualDesignAssetKind.Appearance => appearanceInactive
                    ? "Assign Appearance"
                    : "Apply Appearance",
                TextStudioVisualDesignAssetKind.Writing => "Apply Writing",
                _ => "Apply Design"
            };
        }

        static string GetVisualDesignActionTooltip(
            TextStudioVisualDesignAssetKind kind,
            bool hasTargets,
            bool appearanceInactive,
            TextStudioAppearanceBackendSummary backendSummary)
        {
            if (!hasTargets)
            {
                return "Create editable Text Studio text using this packaged visual design preset.";
            }
            if (appearanceInactive)
            {
                string affected = backendSummary.TargetCount > 1
                    ? backendSummary.UnsupportedCount + " of " +
                      backendSummary.TargetCount + " selected objects"
                    : "The selected object";
                string mixed = backendSummary.HasMixedSupport
                    ? " The Appearance still renders on the other selected objects."
                    : string.Empty;
                if (kind == TextStudioVisualDesignAssetKind.Appearance)
                {
                    return "Assign this reusable Appearance asset. " +
                           TextStudioAppearanceBackendSupport.GetAppearanceAssignmentNotice(backendSummary);
                }

                return "Apply the complete design. Writing and behavior still apply, while " +
                       affected.ToLowerInvariant() +
                       " will preserve the Appearance without rendering it while the active backend owns glyph styling." +
                       mixed;
            }
            return kind switch
            {
                TextStudioVisualDesignAssetKind.Appearance =>
                    "Assign this reusable Appearance asset without changing text, tags, Typewriter, Writing, or placement.",
                TextStudioVisualDesignAssetKind.Writing =>
                    "Assign this reusable Writing asset without changing text, tags, Appearance, Typewriter, or placement.",
                _ =>
                    "Assign this complete Appearance, Writing, and behavior recipe without changing text, tags, Tag Stack, placement, or renderer selection."
            };
        }

        static bool IncludesAppearance(
            TextStudioVisualDesignEntry entry)
        {
            if (entry.Kind == TextStudioVisualDesignAssetKind.Appearance)
                return true;
            if (entry.Kind != TextStudioVisualDesignAssetKind.CompleteDesign)
                return false;

            TextStudioDesignPresetAsset preset =
                TextStudioVisualDesignCatalog.LoadAsset<
                    TextStudioDesignPresetAsset>(entry);
            return preset != null && preset.AppearanceStyle != null;
        }

        static Color GetVisualDesignAccent(
            TextStudioVisualDesignAssetKind kind)
        {
            return kind switch
            {
                TextStudioVisualDesignAssetKind.Appearance =>
                    new Color(0.28f, 0.72f, 0.82f, 1f),
                TextStudioVisualDesignAssetKind.Writing =>
                    new Color(0.86f, 0.58f, 0.28f, 1f),
                _ => new Color(0.72f, 0.42f, 0.88f, 1f)
            };
        }

        static TextGalleryCard CreateCard(
            string id,
            string title,
            string description,
            TextGalleryCategory category,
            System.Action createAction,
            TextGalleryPreviewFactory previewFactory,
            string actionLabel = "Create Example",
            string actionTooltip = "Create a configured Text Studio example in the current scene.")
        {
            return new TextGalleryCard(
                id,
                title,
                description,
                category,
                createAction,
                previewFactory,
                TextGalleryPreviewQuality.NativeUITK,
                null,
                actionLabel,
                actionTooltip,
                productionMetadata: CreateCoreMetadata(category));
        }

        static TextGalleryCard CreateTextCard(
            string id,
            string title,
            string description,
            TextGalleryCategory category,
            System.Action createAction,
            string markup,
            RevealMode revealMode,
            System.Action<TextStudioBinding> configureLabel,
            System.Action<TextStudioText> configureRuntime,
            bool loopReveal,
            float fontSizeOverride = 0f,
            string actionLabel = "Create Example",
            string actionTooltip = "Create a configured Text Studio example in the current scene.")
        {
            return CreateCard(
                    id,
                    title,
                    description,
                    category,
                    createAction,
                    context => CreateTextPreview(
                        context,
                        markup,
                        revealMode,
                        configureLabel,
                        loopReveal,
                        fontSizeOverride),
                    actionLabel,
                    actionTooltip)
                .WithCaptureDescriptor(new TextGalleryCaptureDescriptor(
                    markup,
                    revealMode,
                    loopReveal,
                    fontSizeOverride,
                    configureRuntime));
        }

        static TextGalleryCard CreateEffectCard(
            string id,
            string title,
            string description,
            System.Action createAction,
            string markup,
            RevealMode revealMode = RevealMode.Instant,
            bool loopReveal = false,
            float fontSizeOverride = 0f,
            System.Action<TextStudioBinding> configure = null,
            System.Action<TextStudioText> configureRuntime = null)
        {
            return new TextGalleryCard(
                id,
                title,
                description,
                TextGalleryCategory.Effects,
                createAction,
                context => CreateTextPreview(context, markup, revealMode, configure, loopReveal, fontSizeOverride),
                TextGalleryPreviewQuality.NativeUITK,
                GetEffectSearchKeywords(markup),
                "Create Example",
                "Create editable Text Studio text configured with this effect.",
                () => EditorGUIUtility.systemCopyBuffer = markup ?? string.Empty,
                "Copy Markup",
                "Copy this example markup to the clipboard.",
                productionMetadata: CreateEffectMetadata(markup, false))
                .WithDisplayTitle("<" + ResolveTagNameFromMarkup(markup) + ">")
                .WithSecondaryActionIcon("TreeEditor.Duplicate")
                .WithCaptureDescriptor(new TextGalleryCaptureDescriptor(
                    markup,
                    revealMode,
                    loopReveal,
                    fontSizeOverride,
                    configureRuntime));
        }

        static TextGalleryCard CreateRenderedEffectCard(
            string id,
            string title,
            string description,
            System.Action createAction,
            string markup,
            bool requiresTmpMaterial = false,
            TextStudioPreviewSegment segments = TextStudioPreviewSegment.All)
        {
            return new TextGalleryCard(
                id,
                title,
                description,
                TextGalleryCategory.Effects,
                createAction,
                context => TextGalleryLiveRenderedPreview.Create(
                    markup,
                    context != null && context.IsLargePreview,
                    segments),
                requiresTmpMaterial
                    ? TextGalleryPreviewQuality.RenderedTMPMaterial
                    : TextGalleryPreviewQuality.RenderedLive,
                GetEffectSearchKeywords(markup),
                "Create Example",
                "Create editable Text Studio text configured with this effect.",
                () => EditorGUIUtility.systemCopyBuffer = markup ?? string.Empty,
                "Copy Markup",
                "Copy this example markup to the clipboard.",
                productionMetadata: CreateEffectMetadata(
                    markup,
                    requiresTmpMaterial))
                .WithDisplayTitle("<" + ResolveTagNameFromMarkup(markup) + ">")
                .WithSecondaryActionIcon("TreeEditor.Duplicate")
                .WithCaptureDescriptor(new TextGalleryCaptureDescriptor(
                    markup,
                    RevealMode.Instant,
                    false,
                    segments: segments,
                    useSnapshotRenderer: true));
        }

        static void ConfigureBuiltInTimings(TextStudioText hub, RevealTimingsPreset preset)
        {
            if (hub == null)
                return;

            SerializedObject serializedObject = new SerializedObject(hub);
            SerializedProperty timings = serializedObject.FindProperty("_builtInTimingsPreset");
            if (timings == null)
                return;

            timings.enumValueIndex = (int)preset;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        static TextGalleryProductionMetadata CreateCoreMetadata(
            TextGalleryCategory category)
        {
            TextGalleryWorkflow workflow = category switch
            {
                TextGalleryCategory.Setup => TextGalleryWorkflow.Setup,
                TextGalleryCategory.Reveal => TextGalleryWorkflow.Typewriter,
                TextGalleryCategory.Placement => TextGalleryWorkflow.Placement,
                _ => TextGalleryWorkflow.Unspecified
            };
            TextGalleryReducedMotionSupport reducedMotion = category ==
                TextGalleryCategory.Setup
                    ? TextGalleryReducedMotionSupport.MotionFree
                    : TextGalleryReducedMotionSupport.Reduced;
            return new TextGalleryProductionMetadata(
                workflow,
                TextGalleryRendererSupport.FlatText,
                TextGalleryRenderCost.InPlace,
                TextGalleryResourceRequirements.None,
                reducedMotion);
        }

        static TextGalleryProductionMetadata CreateEffectMetadata(
            string markup,
            bool requiresTmpMaterial)
        {
            TextGalleryRendererSupport rendererSupport = requiresTmpMaterial
                ? TextGalleryRendererSupport.TextMeshPro
                : TextGalleryRendererSupport.FlatText |
                  TextGalleryRendererSupport.Extruded3D;
            TextGalleryRenderCost renderCost =
                TextGalleryRenderCost.InPlace;
            TextGalleryReducedMotionSupport reducedMotion =
                TextGalleryReducedMotionSupport.Reduced;

            string effectId = ResolveEffectIdFromMarkup(markup);
            TextStudioRegistry.EnsureInitialized();
            if (!string.IsNullOrEmpty(effectId) &&
                TextStudioRegistry.Effects.TryGetQuiet(
                    effectId,
                    out ITextStudioEffectDefinition effect) &&
                effect is ITextStudioEffectCapabilitiesProvider provider)
            {
                TextStudioEffectCapabilities capabilities =
                    provider.Capabilities;
                rendererSupport = ConvertRendererSupport(
                    capabilities.SupportedRenderDomains,
                    requiresTmpMaterial);
                renderCost = ConvertRenderCost(
                    capabilities.PerformanceTier);
                reducedMotion = ConvertReducedMotion(
                    capabilities.ReducedMotionBehavior);
            }

            return new TextGalleryProductionMetadata(
                TextGalleryWorkflow.EffectTags,
                rendererSupport,
                renderCost,
                TextGalleryResourceRequirements.None,
                reducedMotion);
        }

        static TextGalleryRendererSupport ConvertRendererSupport(
            TextStudioRenderDomain domains,
            bool requiresTmpMaterial)
        {
            TextGalleryRendererSupport support =
                TextGalleryRendererSupport.None;
            if ((domains & TextStudioRenderDomain.FlatText) != 0)
            {
                support |= requiresTmpMaterial
                    ? TextGalleryRendererSupport.TextMeshPro
                    : TextGalleryRendererSupport.FlatText;
            }
            if (!requiresTmpMaterial &&
                (domains & TextStudioRenderDomain.Extruded3D) != 0)
            {
                support |= TextGalleryRendererSupport.Extruded3D;
            }
            return support == TextGalleryRendererSupport.None
                ? TextGalleryRendererSupport.TextMeshPro
                : support;
        }

        static TextGalleryRenderCost ConvertRenderCost(
            TextStudioEffectPerformanceTier performanceTier)
        {
            return performanceTier switch
            {
                TextStudioEffectPerformanceTier.Realtime =>
                    TextGalleryRenderCost.InPlace,
                TextStudioEffectPerformanceTier.VertexStream =>
                    TextGalleryRenderCost.InPlace,
                TextStudioEffectPerformanceTier.Material =>
                    TextGalleryRenderCost.InPlace,
                _ => TextGalleryRenderCost.RendererManaged
            };
        }

        static TextGalleryReducedMotionSupport ConvertReducedMotion(
            TextStudioReducedMotionBehavior behavior)
        {
            return behavior switch
            {
                TextStudioReducedMotionBehavior.Preserve =>
                    TextGalleryReducedMotionSupport.Preserved,
                TextStudioReducedMotionBehavior.Disable =>
                    TextGalleryReducedMotionSupport.Disabled,
                _ => TextGalleryReducedMotionSupport.Reduced
            };
        }

        static string[] GetEffectSearchKeywords(string markup)
        {
            string effectId = ResolveEffectIdFromMarkup(markup);
            if (string.IsNullOrEmpty(effectId))
                return System.Array.Empty<string>();

            TextStudioRegistry.EnsureInitialized();
            if (!TextStudioRegistry.Effects.TryGetQuiet(
                    effectId,
                    out ITextStudioEffectDefinition effect) || effect == null)
                return System.Array.Empty<string>();

            object[] attributes = effect.GetType().GetCustomAttributes(typeof(TextStudioEffectAliasAttribute), false);
            if (attributes == null || attributes.Length == 0)
                return System.Array.Empty<string>();

            List<string> keywords = new List<string>(attributes.Length * 2);
            for (int i = 0; i < attributes.Length; i++)
            {
                TextStudioEffectAliasAttribute alias = attributes[i] as TextStudioEffectAliasAttribute;
                if (alias == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(alias.Alias))
                    keywords.Add(alias.Alias);
                if (!string.IsNullOrWhiteSpace(alias.Note))
                    keywords.Add(alias.Note);
            }

            return keywords.Count == 0 ? System.Array.Empty<string>() : keywords.ToArray();
        }

        static string ResolveEffectIdFromMarkup(string markup)
        {
            string name = ResolveTagNameFromMarkup(markup);
            if (string.IsNullOrEmpty(name))
                return string.Empty;
            return BuiltIns.BuiltInStyles.TryResolve(name, out string effectId) ? effectId : name;
        }

        static string ResolveTagNameFromMarkup(string markup)
        {
            if (string.IsNullOrEmpty(markup))
                return string.Empty;

            int searchStart = 0;
            while (searchStart < markup.Length)
            {
                int tagStart = markup.IndexOf('<', searchStart);
                if (tagStart < 0 || tagStart + 1 >= markup.Length)
                    return string.Empty;

                if (markup[tagStart + 1] == '/')
                {
                    searchStart = tagStart + 2;
                    continue;
                }

                int tagEnd = markup.IndexOf('>', tagStart + 1);
                if (tagEnd < 0)
                    return string.Empty;

                string name = markup.Substring(tagStart + 1, tagEnd - tagStart - 1).Trim();
                int separator = IndexOfTagSeparator(name);
                if (separator >= 0)
                    name = name.Substring(0, separator);

                return name;
            }

            return string.Empty;
        }

        static int IndexOfTagSeparator(string name)
        {
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsWhiteSpace(c) || c == '=' || c == '/')
                    return i;
            }

            return -1;
        }

        static VisualElement CreateTextPreview(
            TextGalleryPreviewContext context,
            string markup,
            RevealMode revealMode,
            System.Action<TextStudioBinding> configure,
            bool loopReveal,
            float fontSizeOverride = 0f)
        {
            VisualElement root = new VisualElement();
            root.style.flexGrow = 1f;
            root.style.justifyContent = Justify.Center;
            root.style.alignItems = Align.Center;
            root.style.overflow = Overflow.Hidden;
            root.style.borderTopLeftRadius = 4f;
            root.style.borderTopRightRadius = 4f;
            root.style.borderBottomLeftRadius = 4f;
            root.style.borderBottomRightRadius = 4f;
            root.style.backgroundColor = TextGalleryPreviewPalette.Background;

            Label label = new Label(markup);
            label.style.flexGrow = 1f;
            label.style.width = Length.Percent(100f);
            label.style.height = Length.Percent(100f);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.fontSize = fontSizeOverride > 0f
                ? fontSizeOverride
                : context != null && context.IsLargePreview ? LargeFontSize : SmallFontSize;
            label.style.color = new Color(0.92f, 0.95f, 0.98f, 1f);
            TextStudioBinding binding = label.AttachTextStudio();
            binding.EnableInEditMode = true;
            binding.UpdateMode = TextStudioUITKUpdateMode.Manual;
            binding.RevealMode = revealMode;
            binding.SpeedMultiplier = loopReveal ? 1.2f : 1f;
            binding.SetText(markup);
            configure?.Invoke(binding);

            if (revealMode == RevealMode.Instant)
                binding.SkipReveal();
            else
                binding.StartReveal();

            root.Add(label);
            SchedulePreviewTicks(root, binding, revealMode, loopReveal);
            return root;
        }

        static void SchedulePreviewTicks(VisualElement root, TextStudioBinding binding, RevealMode revealMode, bool loopReveal)
        {
            IVisualElementScheduledItem scheduledItem = null;
            VisualElement viewport = null;
            double lastTime = 0d;
            float replayTime = 0f;
            const float ReplayInterval = 2.8f;

            root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                ScrollView scrollView = root.GetFirstAncestorOfType<ScrollView>();
                viewport = scrollView != null ? scrollView.contentViewport : root.panel?.visualTree;
                lastTime = HostTime.CurrentHostTime(ClockMode.Realtime);
                replayTime = 0f;
                scheduledItem = root.schedule.Execute(() =>
                {
                    double now = HostTime.CurrentHostTime(ClockMode.Realtime);
                    float deltaTime = HostTime.ClampDelta((float)(now - lastTime), HostTime.DefaultMaxDeltaTime);
                    lastTime = now;
                    if (viewport == null || !root.worldBound.Overlaps(viewport.worldBound))
                        return;

                    binding.UpdateFromHost(deltaTime);

                    if (!loopReveal || revealMode == RevealMode.Instant)
                        return;

                    replayTime += deltaTime;
                    if (replayTime < ReplayInterval)
                        return;

                    replayTime = 0f;
                    binding.StartReveal();
                }).Every(16);
            });

            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                scheduledItem?.Pause();
                scheduledItem = null;
                viewport = null;
            });
        }
    }
}
