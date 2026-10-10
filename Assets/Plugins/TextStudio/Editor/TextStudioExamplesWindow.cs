using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    internal sealed class TextStudioExamplesWindow : EditorWindow
    {
        const float ContentMaxWidth = 920f;

        static readonly Color Accent = new Color(0.25f, 0.78f, 0.88f);
        static readonly Color CardBackground = new Color(0.12f, 0.135f, 0.165f, 0.96f);
        static readonly Color MutedText = new Color(0.68f, 0.72f, 0.79f);

        ScrollView _scroll;
        VisualElement _learnSection;
        bool _focusLearnPath;

        [MenuItem(TextStudioMenuPaths.Examples, false, TextStudioMenuPaths.ExamplesPriority)]
        public static void ShowWindow()
        {
            OpenWindow(false);
        }

        internal static void ShowLearnPath()
        {
            OpenWindow(true);
        }

        static void OpenWindow(bool focusLearnPath)
        {
            TextStudioExamplesWindow window = GetWindow<TextStudioExamplesWindow>(false, "Text Studio Examples", true);
            window.minSize = new Vector2(520f, 560f);
            window.Show();
            window.Focus();
            if (focusLearnPath)
                window.RequestLearnPathFocus();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            _learnSection = null;
            _scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "ts-examples-scroll"
            };
            _scroll.style.flexGrow = 1f;
            rootVisualElement.Add(_scroll);

            VisualElement content = new VisualElement();
            content.style.width = Length.Percent(100f);
            content.style.maxWidth = ContentMaxWidth;
            content.style.alignSelf = Align.Center;
            content.style.paddingLeft = 22f;
            content.style.paddingRight = 22f;
            content.style.paddingTop = 22f;
            content.style.paddingBottom = 28f;
            _scroll.Add(content);

            Label title = CreateLabel("Learn by building, then see it in production.", 25f, Color.white, true);
            content.Add(title);
            Label subtitle = CreateLabel(
                "Follow the guided path once, jump to a real-world scenario when you know what you need, and use Whisperwood as the complete reference.",
                14f,
                MutedText,
                false);
            subtitle.style.marginTop = 6f;
            subtitle.style.marginBottom = 18f;
            content.Add(subtitle);

            IReadOnlyList<string> products = TextStudioExampleCatalog.Products;
            for (int i = 0; i < products.Count; i++)
                AddProduct(content, products[i]);
            ScheduleLearnPathFocus();
        }

        void AddProduct(VisualElement parent, string product)
        {
            VisualElement productSection = new VisualElement();
            productSection.style.marginBottom = 26f;
            parent.Add(productSection);

            Label productHeading = CreateLabel(product, 21f, Color.white, true);
            productHeading.style.marginBottom = 4f;
            productSection.Add(productHeading);

            VisualElement learnSection = AddCategory(productSection, product, TextStudioExampleCategory.Learn, "1  Learn", "Small, inspectable lessons in recommended order.");
            if (_learnSection == null && string.Equals(product, "Text Studio", StringComparison.Ordinal))
            {
                _learnSection = learnSection;
                if (_learnSection != null)
                {
                    _learnSection.name = "ts-examples-learn";
                    _learnSection.focusable = true;
                }
            }
            AddCategory(productSection, product, TextStudioExampleCategory.Production, "2  Production Examples", "Recognizable scenarios with copyable runtime patterns.");
            AddCategory(productSection, product, TextStudioExampleCategory.CompleteDemo, "3  Complete Demo", "A polished capstone that combines the everyday workflow.");
        }

        static VisualElement AddCategory(
            VisualElement parent,
            string product,
            TextStudioExampleCategory category,
            string title,
            string summary)
        {
            IReadOnlyList<TextStudioExampleEntry> entries = TextStudioExampleCatalog.Entries;
            bool hasEntries = false;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Category == category && entries[i].Product == product)
                {
                    hasEntries = true;
                    break;
                }
            }
            if (!hasEntries)
                return null;

            VisualElement section = new VisualElement();
            section.style.marginBottom = 18f;
            parent.Add(section);

            Label heading = CreateLabel(title, 18f, Color.white, true);
            section.Add(heading);
            Label description = CreateLabel(summary, 13f, MutedText, false);
            description.style.marginTop = 3f;
            description.style.marginBottom = 7f;
            section.Add(description);

            string previousChapter = string.Empty;
            for (int i = 0; i < entries.Count; i++)
            {
                TextStudioExampleEntry entry = entries[i];
                if (entry.Category != category || entry.Product != product)
                    continue;

                if (!string.IsNullOrEmpty(entry.Chapter) && entry.Chapter != previousChapter)
                {
                    Label chapter = CreateLabel(entry.Chapter, 12f, Accent, true);
                    chapter.style.marginTop = previousChapter.Length == 0 ? 5f : 12f;
                    chapter.style.marginBottom = 1f;
                    section.Add(chapter);
                    previousChapter = entry.Chapter;
                }
                section.Add(CreateCard(entry));
            }
            return section;
        }

        void RequestLearnPathFocus()
        {
            _focusLearnPath = true;
            ScheduleLearnPathFocus();
        }

        void ScheduleLearnPathFocus()
        {
            if (!_focusLearnPath || _scroll == null || _learnSection == null)
                return;

            _learnSection.schedule.Execute(() =>
            {
                if (!_focusLearnPath || _scroll == null || _learnSection == null)
                    return;
                _learnSection.Focus();
                _scroll.ScrollTo(_learnSection);
                _focusLearnPath = false;
            }).ExecuteLater(0);
        }

        static VisualElement CreateCard(TextStudioExampleEntry entry)
        {
            VisualElement card = new VisualElement();
            card.style.flexDirection = FlexDirection.Row;
            card.style.alignItems = Align.Center;
            card.style.marginTop = 5f;
            card.style.paddingLeft = 12f;
            card.style.paddingRight = 10f;
            card.style.paddingTop = 9f;
            card.style.paddingBottom = 9f;
            card.style.backgroundColor = CardBackground;
            card.style.borderTopLeftRadius = 6f;
            card.style.borderTopRightRadius = 6f;
            card.style.borderBottomLeftRadius = 6f;
            card.style.borderBottomRightRadius = 6f;

            VisualElement copy = new VisualElement();
            copy.style.flexGrow = 1f;
            copy.style.minWidth = 0f;
            card.Add(copy);

            Label title = CreateLabel(entry.Order.ToString("00") + "  " + entry.Title, 14f, Color.white, true);
            copy.Add(title);
            Label description = CreateLabel(entry.Description, 12f, MutedText, false);
            description.style.marginTop = 2f;
            copy.Add(description);
            string badgesText = entry.Level + "  •  " + entry.Backend;
            if (!string.IsNullOrEmpty(entry.RequiredPackages))
                badgesText += "  •  " + entry.RequiredPackages;
            Label badges = CreateLabel(badgesText, 11f, Accent, false);
            badges.style.marginTop = 3f;
            copy.Add(badges);

            if (entry.HasAsset)
            {
                Button assets = new Button(() => PingAsset(entry)) { text = "Assets" };
                assets.tooltip = "Locate the most useful asset to inspect for this example.";
                assets.style.width = 62f;
                assets.style.marginLeft = 8f;
                card.Add(assets);
            }
            Button ping = new Button(() => PingScene(entry)) { text = "Scene" };
            ping.tooltip = "Locate this scene in the Project window.";
            ping.style.width = 60f;
            ping.style.marginLeft = 5f;
            card.Add(ping);
            Button open = new Button(() => Open(entry)) { text = "Open" };
            open.tooltip = "Open this example scene.";
            open.style.width = 64f;
            open.style.marginLeft = 5f;
            card.Add(open);
            return card;
        }

        static Label CreateLabel(string text, float size, Color color, bool bold)
        {
            Label label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.flexShrink = 1f;
            label.style.minWidth = 0f;
            label.style.fontSize = size;
            label.style.color = color;
            if (bold)
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }

        static void PingScene(TextStudioExampleEntry entry)
        {
            string path = TextStudioExampleCatalog.ResolveScenePath(entry);
            Object scene = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadMainAssetAtPath(path);
            if (scene == null)
            {
                Debug.LogWarning("[Text Studio] Example scene was not found: " + entry.SceneName);
                return;
            }
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = scene;
            EditorGUIUtility.PingObject(scene);
        }

        static void PingAsset(TextStudioExampleEntry entry)
        {
            string path = TextStudioExampleCatalog.ResolveAssetPath(entry);
            Object asset = string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null)
            {
                Debug.LogWarning("[Text Studio] Example asset was not found: " + entry.AssetLabel);
                return;
            }
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        static void Open(TextStudioExampleEntry entry)
        {
            string path = TextStudioExampleCatalog.ResolveScenePath(entry);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[Text Studio] Example scene was not found: " + entry.SceneName);
                return;
            }
            EditorSceneManager.OpenScene(path);
        }
    }

    internal enum TextStudioExampleCategory
    {
        Learn,
        Production,
        CompleteDemo
    }

    internal readonly struct TextStudioExampleEntry
    {
        public readonly TextStudioExampleCategory Category;
        public readonly int Order;
        public readonly string Title;
        public readonly string Description;
        public readonly string SceneName;
        public readonly string SceneGuid;
        public readonly string Level;
        public readonly string Backend;
        public readonly string Product;
        public readonly string Chapter;
        public readonly string RequiredPackages;
        public readonly string AssetLabel;
        public readonly string AssetGuid;

        public bool HasAsset => !string.IsNullOrEmpty(AssetGuid);

        public TextStudioExampleEntry(
            TextStudioExampleCategory category,
            int order,
            string title,
            string description,
            string sceneName,
            string level,
            string backend,
            string product = "Text Studio",
            string chapter = "",
            string requiredPackages = "",
            string assetLabel = "",
            string assetGuid = "",
            string sceneGuid = "")
        {
            Category = category;
            Order = order;
            Title = title;
            Description = description;
            SceneName = sceneName;
            SceneGuid = sceneGuid;
            Level = level;
            Backend = backend;
            Product = product;
            Chapter = chapter;
            RequiredPackages = requiredPackages;
            AssetLabel = assetLabel;
            AssetGuid = assetGuid;
        }
    }

#if UNITY_6000_7_OR_NEWER
    // Add-on providers register once per code load and must survive Play Mode when domain reload is disabled.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioExampleCatalog
    {
        static readonly List<Func<IReadOnlyList<TextStudioExampleEntry>>> Providers =
            new List<Func<IReadOnlyList<TextStudioExampleEntry>>>();
        static readonly List<TextStudioExampleEntry> ResolvedEntries =
            new List<TextStudioExampleEntry>();
        static readonly List<string> ResolvedProducts = new List<string>();
        static bool _dirty = true;

        static readonly TextStudioExampleEntry[] Catalog =
        {
            Learn(0, "Text Studio Showcase", "Inspect effects, appearance and placement immediately in one authored composition.", "Text Studio Showcase", "Beginner", "TMP"),
            Learn(1, "Animated Surfaces", "Compare moving surface finishes on the same lettering.", "Animated Surfaces", "Beginner", "TMP"),
            Learn(2, "Outlines and Bevels", "Compare sharp and round joins, beveled edges and metallic treatments.", "Outlines and Bevels", "Beginner", "TMP"),
            Learn(3, "Inline Images", "Compare original, tinted and styled images inside text.", "Inline Images", "Beginner", "TMP"),
            Learn(4, "Layered Lettering", "Compare shadows, layered depth and poster styling.", "Layered Lettering", "Beginner", "TMP"),
            Learn(5, "Paints and Layers", "Compare paint mapping, connected backgrounds and overlapping layers.", "Paints and Layers", "Beginner", "TMP"),
            Learn(8, "Styles and Designs", "Compare native text, reusable styles, inline recipes and complete designs.", "Styles and Designs", "Beginner", "TMP"),
            Learn(9, "Selective Recolouring", "Compare source recoloring with changes to the composed result.", "Selective Recolouring", "Beginner", "TMP"),
            Learn(12, "Surface Materials", "Compare three texture treatments with shared mapping.", "Surface Materials", "Beginner", "TMP"),
            Learn(13, "Text Decorations", "Inspect measured underlines and glyph contour clearance.", "Text Decorations", "Beginner", "TMP"),
            Learn(14, "Writing and Erasing", "Watch ink, authored strokes, marker and chalk write and erase together.", "Writing and Erasing", "Beginner", "TMP"),
            Learn(15, "Accessible Captions", "Read speaker and sound captions with size and motion preferences.", "Accessible Captions", "Intermediate", "TMP"),
            Learn(16, "Character Voices", "Hear one deliberate typewriter voice at a time.", "Character Voices", "Intermediate", "TMP"),
            Learn(17, "Combat Feedback", "Trigger damage, critical and healing feedback in a game encounter.", "Combat Feedback", "Intermediate", "TMP"),
            Learn(18, "Composed Effects", "Compare curve shapes using native composed effects.", "Composed Effects", "Intermediate", "TMP"),
            Learn(19, "Custom Extensions", "Observe custom actions and effects in a gate encounter.", "Custom Extensions", "Intermediate", "TMP"),
            Learn(20, "Dialogue and Pages", "Advance a short branching dialogue and inspect long-form pages.", "Dialogue and Pages", "Intermediate", "TMP"),
            Learn(21, "Events and Sound", "Let native text markers change the gate, lantern and sound.", "Events and Sound", "Intermediate", "TMP"),
            Learn(22, "Expressive Motion", "Compare expressive effects on short phrases.", "Expressive Motion", "Intermediate", "TMP"),
            Learn(23, "Karaoke Styles", "Follow audio-clocked words and compare styling recipes.", "Karaoke Styles", "Intermediate", "TMP"),
            Learn(24, "Placement and Deformation", "Compare text paths and authored deformation.", "Placement and Deformation", "Intermediate", "TMP"),
            Learn(25, "Renderer Comparison", "Edit one message across TMP, legacy UI and three UI Toolkit source routes.", "Renderer Comparison", "Intermediate", "TMP + UI Toolkit + Unity UI"),
            Learn(26, "Responsive HUD", "Try live text input, changing values and contextual game feedback.", "Responsive HUD", "Intermediate", "TMP"),
            Learn(27, "Reveal Timing", "Compare glyph, word and line reveal together.", "Reveal Timing", "Intermediate", "TMP"),
            Learn(27, "Reactions", "Let finishing words, markers, taps and changing numbers trigger their own responses.", "Reactions", "Intermediate", "TMP"),
            Learn(28, "Spatial Fields", "Drag one target across independently authored influence fields.", "Spatial Fields", "Intermediate", "TMP"),
            Learn(29, "The Gate and the Lantern", "Follow one dialogue through a timed pause, input wait and visible consequence.", "The Gate and the Lantern", "Intermediate", "TMP"),
            Production(5, "Text Magnifier", "Drag a polished lens across live text while its Transform drives exact radial resampling.", "Text Magnifier", "Text Magnifier Effect", "27a0175851d8e644b9e6267d34551869"),
            new TextStudioExampleEntry(TextStudioExampleCategory.CompleteDemo, 1, "Whisperwood (Canvas)",
                "A complete fantasy vignette using the TextMeshPro Canvas backend.", "TextStudioWhisperwood_Canvas", "Complete", "TMP Canvas"),
            new TextStudioExampleEntry(TextStudioExampleCategory.CompleteDemo, 2, "Whisperwood (UI Toolkit)",
                "The same complete vignette using the UI Toolkit backend.", "TextStudioWhisperwood_UIToolkit", "Complete", "UI Toolkit")
        };

        public static IReadOnlyList<TextStudioExampleEntry> Entries
        {
            get
            {
                ResolveProviders();
                return ResolvedEntries;
            }
        }

        public static IReadOnlyList<string> Products
        {
            get
            {
                ResolveProviders();
                return ResolvedProducts;
            }
        }

        public static void RegisterProvider(Func<IReadOnlyList<TextStudioExampleEntry>> provider)
        {
            if (provider == null || Providers.Contains(provider))
                return;
            Providers.Add(provider);
            _dirty = true;
        }

        static void ResolveProviders()
        {
            if (!_dirty)
                return;

            ResolvedEntries.Clear();
            ResolvedEntries.AddRange(Catalog);
            for (int i = 0; i < Providers.Count; i++)
            {
                IReadOnlyList<TextStudioExampleEntry> contributed = Providers[i]?.Invoke();
                if (contributed == null)
                    continue;
                for (int j = 0; j < contributed.Count; j++)
                    ResolvedEntries.Add(contributed[j]);
            }

            ResolvedProducts.Clear();
            for (int i = 0; i < ResolvedEntries.Count; i++)
            {
                string product = ResolvedEntries[i].Product;
                if (!ResolvedProducts.Contains(product))
                    ResolvedProducts.Add(product);
            }
            _dirty = false;
        }

        public static string ResolveScenePath(TextStudioExampleEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.SceneGuid))
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(entry.SceneGuid);
                return AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null ? scenePath : string.Empty;
            }
            string[] guids = AssetDatabase.FindAssets(entry.SceneName + " t:Scene");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]).Replace('\\', '/');
                if (path.EndsWith("/" + entry.SceneName + ".unity", StringComparison.OrdinalIgnoreCase) &&
                    path.IndexOf("/Examples/", StringComparison.OrdinalIgnoreCase) >= 0)
                    return path;
            }
            return string.Empty;
        }

        public static string ResolveAssetPath(TextStudioExampleEntry entry)
        {
            if (!entry.HasAsset)
                return string.Empty;

            string path = AssetDatabase.GUIDToAssetPath(entry.AssetGuid);
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }

        static TextStudioExampleEntry Learn(int order, string title, string description, string sceneName, string level, string backend)
        {
            return new TextStudioExampleEntry(TextStudioExampleCategory.Learn, order, title, description, sceneName, level, backend);
        }

        static TextStudioExampleEntry Production(
            int order,
            string title,
            string description,
            string sceneName,
            string assetLabel = "",
            string assetGuid = "")
        {
            return new TextStudioExampleEntry(
                TextStudioExampleCategory.Production,
                order,
                title,
                description,
                sceneName,
                "Production",
                "TMP Canvas",
                assetLabel: assetLabel,
                assetGuid: assetGuid);
        }
    }
}
