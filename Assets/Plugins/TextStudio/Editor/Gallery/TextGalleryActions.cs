using ImpossibleRobert.Common;
using TextStudio.BuiltIns;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TextStudio.Editor
{
    internal static class TextGalleryActions
    {
        internal const string ActionFlowMarkup =
            "First beat<wait seconds=.35> waits.\n" +
            "<speed=2>Then speed shifts.";
        internal const float WavePlacementAmplitude = 18f;
        internal const float WavePlacementFrequency = 1.15f;
        internal const float WavePlacementSpeed = 0.75f;
        internal const int GridPlacementColumns = 4;
        internal const string VerticalSlideMarkup =
            "<appear slideV a=.32 area=range charOffset=0 wordOffset=0 lineOffset=0>" +
            "<disappear slideV a=.32 area=range charOffset=0 wordOffset=0 lineOffset=0>" +
            "RISE / DROP</disappear></appear>";
        internal const string FocusMarkup = "<focus amount=1 area=range>FOCUS</focus>";
        internal const string DissolveMarkup =
            "<dissolve softness=.22 space=text>DISSOLVE</dissolve>";
        internal const string BurnMarkup =
            "<burn softness=.16 space=text>BURN</burn>";
        internal const string OutlineGlowMarkup =
            "<outlineGlow outline=.4 glow=1 speed=.65>SELECTED</outlineGlow>";
        internal const string SurfaceSweepMarkup =
            "<surfaceSweep angle=20 width=.15 brightness=1.25 area=range><color=#C4872F>LEGENDARY</color></surfaceSweep>";
        internal const string ChromaticGlitchMarkup =
            "<chromaticGlitch strength=.16 frequency=5>SIGNAL</chromaticGlitch>";
        internal const string PixelGridMarkup =
            "<pixelGrid size=8 space=screen quality=preserve>PIXEL GRID</pixelGrid>";
        internal const string SquiggleMarkup = "<squiggle strength=.035 detail=4 rate=6>SQUIGGLE</squiggle>";
        internal const string SizeMarkup =
            "<size start=.3 pulse=.16 rate=.65>Grow in</size>";

        public static void CreateRevealGlyphExample() => CreateTMPExample("TextStudio - Typewriter Glyph", "Hello, World!", RevealMode.ByGlyph, PlacementMode.Linear);
        public static void CreateRevealWordExample() => CreateTMPExample("TextStudio - Typewriter Word", "The quick brown fox jumps", RevealMode.ByWord, PlacementMode.Linear);
        public static void CreateRevealLineExample() => CreateTMPExample("TextStudio - Typewriter Line", "First line.\nSecond line.", RevealMode.ByLine, PlacementMode.Linear);
        public static void CreateInstantExample() => CreateTMPExample("TextStudio - Instant", "Instant Text", RevealMode.Instant, PlacementMode.Linear);
        public static void CreateTypewriterPresetExample()
        {
            TextStudioBuiltInTypewriterPreset preset = BuiltInTypewriterPresets.SmoothDialogue;
            TMP_Text tmp = CreateTMPExample("TextStudio - Dialogue Typewriter Preset", preset.SampleText, preset.Mode, PlacementMode.Linear);
            TextStudioText hub = tmp.GetComponent<TextStudioText>();
            if (hub == null)
                return;

            Undo.RecordObject(hub, "Configure Text Studio Typewriter Preset");
            hub.SetBuiltInTypewriterPreset(preset.Id);
            EditorUtility.SetDirty(hub);
        }

        public static void CreateTypewriterEffectsExample()
        {
            TMP_Text tmp = CreateTMPExample("TextStudio - Typewriter Effects", "Every key leaves a mark", RevealMode.ByGlyph, PlacementMode.Linear);
            TextStudioText hub = tmp.GetComponent<TextStudioText>();
            if (hub == null)
                return;

            Undo.RecordObject(hub, "Configure Text Studio Reveal and Hide Effects");
            TextStudioTypewriterSettings settings = TextStudioTypewriterSettings.Default;
            settings.RevealMode = RevealMode.ByGlyph;
            settings.BuiltInTimingsPreset = RevealTimingsPreset.Typist;
            settings.RevealAppearanceDuration = 0.22f;
            settings.RevealEffectExpression = "<fade><size scale=1.12><slideVertical a=.045><slideHorizontal a=.012>";
            settings.HideEffectExpression = "<fade>";
            hub.ApplyTypewriterSettings(settings);
            EditorUtility.SetDirty(hub);
        }

        public static void CreateActionFlowExample() => CreateTMPExample("TextStudio - Waits And Speed Tags", ActionFlowMarkup, RevealMode.ByGlyph, PlacementMode.Linear);
        public static void CreateMarkerEventRouterExample() => TextStudioRecipeInstaller.CreateRecipe("marker-events");
        public static void CreateLongTextPagesExample() => TextStudioRecipeInstaller.CreateRecipe("long-text");
        public static void CreateDialogueAudioPlayerExample()
        {
            TMP_Text tmp = CreateTMPExample("TextStudio - Dialogue Audio Player", "Audio-ready <wave>dialogue</wave> line.", RevealMode.ByGlyph, PlacementMode.Linear);
            tmp.gameObject.AddComponent<TextStudioAudioPlayer>();
            Selection.activeGameObject = tmp.gameObject;
        }

        public static void CreateWaveEffectExample() => CreateEffectExample("Wave Effect", "Hello <wave>world</wave>", RevealMode.Instant);
        public static void CreateShakeEffectExample() => CreateEffectExample("Shake Effect", "Careful, <shake>unstable</shake> text", RevealMode.Instant);
        public static void CreateWiggleEffectExample() => CreateEffectExample("Wiggle Effect", "<wiggle>Playful motion</wiggle>", RevealMode.Instant);
        public static void CreateBounceEffectExample() => CreateEffectExample("Bounce Effect", "<bounce>Bouncy text</bounce>", RevealMode.Instant);
        public static void CreateSwingEffectExample() => CreateEffectExample("Swing Effect", "<swing>Swinging text</swing>", RevealMode.Instant);
        public static void CreatePulseEffectExample() => CreateEffectExample("Pulse Effect", "<pulse>Press any key</pulse>", RevealMode.Instant);
        public static void CreateBlinkEffectExample() => CreateEffectExample("Blink Effect", "<blink>Warning</blink>", RevealMode.Instant);
        public static void CreateShimmerEffectExample() => CreateEffectExample("Shimmer Effect", "<shimmer>Legendary reward</shimmer>", RevealMode.Instant);
        public static void CreateGradientFlowEffectExample() => CreateEffectExample("Gradient Flow Effect", "<gradientFlow>Gradient flow</gradientFlow>", RevealMode.Instant);
        public static void CreateColorPulseEffectExample() => CreateEffectExample("Color Pulse Effect", "<colorPulse>Rare item</colorPulse>", RevealMode.Instant);
        public static void CreateFloatEffectExample() => CreateEffectExample("Float Effect", "<float>Magic words</float>", RevealMode.Instant);
        public static void CreateRainbowEffectExample() => CreateEffectExample("Rainbow Effect", "<rainbow>Rainbow text</rainbow>", RevealMode.Instant);
        public static void CreateFadeEffectExample() => CreateEffectExample("Fade Effect", "<fade>Soft reveal</fade>", RevealMode.ByGlyph);
        public static void CreateTypewriterEffectExample() => CreateEffectExample("Typewriter Effect", "<typewriter>Paper strike text</typewriter>", RevealMode.ByGlyph);
        public static void CreateSizeEffectExample() => CreateEffectExample("Size Effect", SizeMarkup, RevealMode.ByGlyph);
        public static void CreatePopEffectExample() => CreateEffectExample("Pop Effect", "<pop>Bonus!</pop>", RevealMode.ByGlyph);
        public static void CreateRotateEffectExample() => CreateEffectExample("Rotate Effect", "<rotate>Spin in</rotate>", RevealMode.ByGlyph);
        public static void CreateSlideEffectExample() => CreateEffectExample("Slide Effect", "<slide>Slide in</slide>", RevealMode.ByGlyph);
        public static void CreateSlideVerticalEffectExample() => CreateEffectExample("Vertical Slide Effect", VerticalSlideMarkup, RevealMode.ByGlyph);
        public static void CreateShearEffectExample() => CreateEffectExample("Shear Effect", "<shear a=1.2 horizontal=3 vertical=0>Skewed signal</shear>", RevealMode.Instant);
        public static void CreateExpandEffectExample() => CreateEffectExample("Expand Effect", "<expand>Expand</expand>", RevealMode.ByGlyph);
        public static void CreateFineTunedEffectExample() => CreateEffectExample("Fine-Tuned Effect", "<bounce a=.28 s=1.8 stagger=.08>Fine-tuned bounce</bounce>", RevealMode.Instant);
        public static void CreateEffectAreaWordsExample() => CreateEffectExample("Effect Area Words", "<wave area=word group=2>Grouped words move together</wave>", RevealMode.Instant);
        public static void CreateScrambleTextExample() => CreateEffectExample("Scramble Text", "<scramble once duration=.8 revealDuration=.35 charset=hex>ACCESS GRANTED</scramble>", RevealMode.ByGlyph);
        public static void CreateDecryptEffectExample() => CreateEffectExample("Decrypt Effect", "<decrypt>ACCESS GRANTED</decrypt>", RevealMode.ByGlyph);
        public static void CreateGlitchEffectExample() => CreateEffectExample("Glitch Effect", "<glitch>SIGNAL LOST</glitch>", RevealMode.Instant);
        public static void CreateEmphasisEffectExample() => CreateEffectExample("Emphasis Effect", "This is <emphasis>important</emphasis>", RevealMode.Instant);
        public static void CreateElasticEffectExample() => CreateEffectExample("Elastic Effect", "<elastic>CRITICAL!</elastic>", RevealMode.ByGlyph);
        public static void CreateScatter2DEffectExample() => CreateEffectExample("Scatter 2D Effect", "<scatter2D>ASSEMBLE</scatter2D>", RevealMode.ByGlyph);
        public static void CreateTrackingEffectExample() => CreateEffectExample("Visual Tracking Effect", "<tracking amount=.7>CINEMATIC</tracking>", RevealMode.ByGlyph);
        public static void CreateCornerWarpEffectExample() => CreateEffectExample("Corner Warp Effect", "<cornerWarp mode=flag>ORGANIC</cornerWarp>", RevealMode.Instant);
        public static void CreateFocusEffectExample() => CreateEffectExample("Focus Effect", FocusMarkup, RevealMode.ByGlyph);
        public static void CreateDissolveEffectExample() => CreateEffectExample("Dissolve Effect", DissolveMarkup, RevealMode.ByGlyph);
        public static void CreateBurnEffectExample() => CreateEffectExample("Burn Effect", BurnMarkup, RevealMode.ByGlyph);
        public static void CreateOutlineGlowEffectExample() => CreateEffectExample("Outline & Glow Effect", OutlineGlowMarkup, RevealMode.Instant);
        public static void CreateSurfaceSweepEffectExample() => CreateEffectExample("Surface Sweep Effect", SurfaceSweepMarkup, RevealMode.Instant);
        public static void CreateChromaticGlitchEffectExample() => CreateEffectExample("Chromatic Glitch Effect", ChromaticGlitchMarkup, RevealMode.Instant);
        public static void CreatePixelGridEffectExample() => CreateEffectExample("Pixel Grid Effect", PixelGridMarkup, RevealMode.ByGlyph);
        public static void CreateSquiggleEffectExample() => CreateEffectExample("Squiggle Effect", SquiggleMarkup, RevealMode.Instant);
        public static void CreateCenterOutRevealExample()
        {
            TMP_Text tmp = CreateTMPExample("TextStudio - Center-Out Reveal", "CENTER OUT", RevealMode.ByGlyph, PlacementMode.Linear);
            TextStudioText hub = tmp.GetComponent<TextStudioText>();
            if (hub == null)
                return;
            TextStudioTypewriterSettings settings = TextStudioTypewriterSettings.Default;
            settings.RevealSequence = new TextStudioSequenceSettings
            {
                Order = TextStudioSequenceOrder.CenterOut,
                Spread = 1f,
                Seed = 1729
            };
            settings.RevealEffectExpression = "<fade><slideVertical a=.055>";
            settings.HideEffectExpression = "<fade><slideVertical a=.045>";
            hub.ApplyTypewriterSettings(settings);
            EditorUtility.SetDirty(hub);
        }
        public static void CreateLinearPlacementExample() => CreateTMPExample("TextStudio - Linear", "Linear Layout", RevealMode.Instant, PlacementMode.Linear);
        public static void CreateArcPlacementExample() => CreateTMPExample("TextStudio - Arc Path", "Arc Text Path", RevealMode.Instant, PlacementMode.Arc);
        public static void CreateWavePlacementExample()
        {
            TMP_Text tmp = CreateTMPExample("TextStudio - Wave Path", "Wave Text Path", RevealMode.Instant, PlacementMode.Wave);
            TextStudioText hub = tmp.GetComponent<TextStudioText>();
            if (hub == null)
                return;

            Undo.RecordObject(hub, "Configure Text Studio Wave Path");
            hub.ConfigureWavePlacement(
                WavePlacementAmplitude,
                WavePlacementFrequency,
                WavePlacementSpeed);
            hub.SetPlacementOrientation(TextStudioPlacementOrientation.FollowPath);
            EditorUtility.SetDirty(hub);
        }
        public static void CreateCirclePlacementExample() => CreateTMPExample("TextStudio - Circle Path", "Circle", RevealMode.Instant, PlacementMode.Circle);
        public static void CreateGridPlacementExample()
        {
            TMP_Text tmp = CreateTMPExample("TextStudio - Grid", "GRIDTEXT", RevealMode.Instant, PlacementMode.Grid);
            TextStudioText hub = tmp.GetComponent<TextStudioText>();
            if (hub == null)
                return;

            Undo.RecordObject(hub, "Configure Text Studio Grid");
            hub.ConfigureGridPlacement(TextGalleryActions.GridPlacementColumns);
            EditorUtility.SetDirty(hub);
        }

        public static TMP_Text CreateEffectExample(string displayName, string text, RevealMode revealMode)
        {
            return CreateTMPExample("TextStudio - " + displayName, text, revealMode, PlacementMode.Linear);
        }

        public static TMP_Text CreateTMPExample(string objectName, string text, RevealMode revealMode, PlacementMode placementMode)
        {
            Canvas canvas = FindCanvasForExample();
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("Canvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");
            }

            GameObject go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(560f, 180f);

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 48f;
            tmp.alignment = TextAlignmentOptions.Center;
            UnityUtils.SetTextMeshProWordWrapping(tmp, revealMode == RevealMode.ByLine);

            TextStudioText hub = go.AddComponent<TextStudioText>();
            SerializedObject so = new SerializedObject(hub);
            so.FindProperty("_markup").stringValue = text;
            so.FindProperty("_revealMode").enumValueIndex = (int)revealMode;
            so.FindProperty("_placementMode").enumValueIndex = (int)placementMode;
            so.ApplyModifiedPropertiesWithoutUndo();
            hub.TypewriterSource = revealMode == RevealMode.Instant
                ? TextStudioTypewriterSource.None
                : TextStudioTypewriterSource.Inline;
            hub.SetText(text);
            hub.SetAuthoringMode(TextStudioAuthoringMode.Preview);

            Undo.RegisterCreatedObjectUndo(go, "Create " + objectName);
            Selection.activeGameObject = go;
            return tmp;
        }

        static Canvas FindCanvasForExample()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != null)
            {
                Canvas selectedCanvas = selected.GetComponentInParent<Canvas>();
                if (selectedCanvas != null && selectedCanvas.isActiveAndEnabled)
                    return selectedCanvas;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            Canvas[] canvases = UnityUtils.FindAll<Canvas>();
            Canvas activeSceneCanvas = null;
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas candidate = canvases[i];
                if (candidate == null ||
                    candidate.gameObject.scene != activeScene)
                {
                    continue;
                }

                if (activeSceneCanvas == null ||
                    candidate.GetStableId() <
                    activeSceneCanvas.GetStableId())
                {
                    activeSceneCanvas = candidate;
                }
            }

            return activeSceneCanvas;
        }
    }
}
