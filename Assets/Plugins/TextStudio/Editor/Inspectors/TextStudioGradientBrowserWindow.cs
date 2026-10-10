using ImpossibleRobert.Common;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
#if UNITY_6000_7_OR_NEWER
    // Immutable browser choices stay available throughout the editor session.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed class TextStudioGradientBrowserWindow : EditorWindow
    {
        const float DefaultWidth = 920f;
        const float DefaultHeight = 640f;

        static readonly List<string> UseCaseChoices = new List<string>
        {
            "All Uses",
            "Interface",
            "Combat & Energy",
            "Fantasy & Magic",
            "Sci-Fi & Neon",
            "Nature & Atmosphere",
            "Luxury & Material"
        };

        TextStudio2DPaintKind _kind;
        Action<TextStudioGradientRecipe> _onApply;
        TextField _search;
        DropdownField _useCase;
        Label _resultCount;
        VisualElement _cards;

        internal int FamilyRecipeCount => CountFamilyRecipes();
        internal int VisibleRecipeCount => _cards?.childCount ?? 0;
        internal TextStudio2DPaintKind SelectedKind => _kind;

        internal static TextStudioGradientBrowserWindow Open(
            TextStudio2DPaintKind kind,
            Action<TextStudioGradientRecipe> onApply)
        {
            if (!TextStudioGradientCatalog.IsGradient(kind))
                throw new ArgumentOutOfRangeException(nameof(kind));

            TextStudioGradientBrowserWindow window = CreateInstance<TextStudioGradientBrowserWindow>();
            window._kind = kind;
            window._onApply = onApply;
            window.titleContent = new GUIContent(
                TextStudioGradientCatalog.FamilyLabel(kind) + " Gradient Recipes");
            window.minSize = new Vector2(620f, 440f);
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            window.position = new Rect(
                main.center.x - DefaultWidth * 0.5f,
                main.center.y - DefaultHeight * 0.5f,
                DefaultWidth,
                DefaultHeight);
            window.ShowAuxWindow();
            return window;
        }

        void CreateGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot(
                "ts-gradient-browser-root");
            root.AddToClassList("ts-gradient-browser-root");
            CommonUITK.ApplyWindowLayout(root);
            rootVisualElement.Add(root);

            VisualElement header = new VisualElement();
            header.AddToClassList("ts-gradient-browser-header");
            Label eyebrow = new Label("TEXT STUDIO GRADIENT LIBRARY");
            eyebrow.AddToClassList("ts-gradient-browser-eyebrow");
            Label title = new Label(
                TextStudioGradientCatalog.FamilyLabel(_kind) + " Gradient Recipes");
            title.AddToClassList("ts-gradient-browser-title");
            Label description = new Label(
                "Choose from 18 professional " +
                TextStudioGradientCatalog.FamilyLabel(_kind).ToLowerInvariant() +
                " recipes. Applying one creates an editable custom gradient in the inspector.");
            description.AddToClassList("ts-gradient-browser-description");
            header.Add(eyebrow);
            header.Add(title);
            header.Add(description);
            root.Add(header);

            VisualElement toolbar = new VisualElement();
            toolbar.AddToClassList("ts-gradient-browser-toolbar");
            _search = new TextField
            {
                name = "ts-gradient-browser-search",
                tooltip = "Search recipe names, descriptions, and use cases."
            };
            _search.AddToClassList("ts-gradient-browser-search");
            _search.Q<TextElement>()?.AddToClassList("ts-gradient-browser-search-input");
            _search.RegisterValueChangedCallback(_ => RebuildCards());
            _useCase = new DropdownField(UseCaseChoices, 0)
            {
                name = "ts-gradient-browser-use-case",
                tooltip = "Filter the current set of 18 recipes by common game and text-output needs."
            };
            _useCase.AddToClassList("ts-gradient-browser-filter");
            _useCase.RegisterValueChangedCallback(_ => RebuildCards());
            _resultCount = new Label();
            _resultCount.AddToClassList("ts-gradient-browser-count");
            toolbar.Add(_search);
            toolbar.Add(_useCase);
            toolbar.Add(_resultCount);
            root.Add(toolbar);

            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("ts-gradient-browser-scroll");
            _cards = new VisualElement
            {
                name = "ts-gradient-browser-cards"
            };
            _cards.AddToClassList("ts-gradient-browser-cards");
            scroll.Add(_cards);
            root.Add(scroll);
            RebuildCards();
            _search.Focus();
        }

        void RebuildCards()
        {
            if (_cards == null)
                return;
            _cards.Clear();
            string query = (_search?.value ?? string.Empty).Trim();
            int useCaseIndex = _useCase?.index ?? 0;
            int count = 0;
            IReadOnlyList<TextStudioGradientRecipe> recipes = TextStudioGradientCatalog.ForKind(_kind);
            for (int i = 0; i < recipes.Count; i++)
            {
                TextStudioGradientRecipe recipe = recipes[i];
                if (!MatchesUseCase(recipe, useCaseIndex) || !MatchesSearch(recipe, query))
                {
                    continue;
                }
                _cards.Add(CreateCard(recipe));
                count++;
            }

            _resultCount.text = count + " of 18";
            if (count == 0)
            {
                VisualElement empty = new VisualElement();
                empty.AddToClassList("ts-gradient-browser-empty");
                Label title = new Label("No recipes match");
                title.AddToClassList("ts-gradient-browser-empty-title");
                Label help = new Label(
                    "Try a broader search or switch the use-case filter back to All Uses.");
                help.AddToClassList("ts-muted-text");
                Button clear = TextStudioInspectorElements.CreateButton(
                    "Clear Filters",
                    "Show all 18 recipes in this gradient family.",
                    () =>
                    {
                        _search.SetValueWithoutNotify(string.Empty);
                        _useCase.index = 0;
                        RebuildCards();
                    });
                empty.Add(title);
                empty.Add(help);
                empty.Add(clear);
                _cards.Add(empty);
            }
        }

        VisualElement CreateCard(TextStudioGradientRecipe recipe)
        {
            VisualElement card = new VisualElement
            {
                name = "ts-gradient-card-" + recipe.Id,
                focusable = true,
                tooltip = "Apply " + recipe.Name + " and continue editing it as a custom gradient."
            };
            card.AddToClassList("ts-gradient-card");
            TextStudioGradientPreviewElement preview = new TextStudioGradientPreviewElement();
            preview.AddToClassList("ts-gradient-card-preview");
            preview.ShowRecipe(recipe);
            card.Add(preview);

            VisualElement body = new VisualElement();
            body.AddToClassList("ts-gradient-card-body");
            VisualElement heading = new VisualElement();
            heading.AddToClassList("ts-gradient-card-heading");
            Label name = new Label(recipe.Name);
            name.AddToClassList("ts-gradient-card-title");
            Label useCase = new Label(recipe.UseCaseLabel);
            useCase.AddToClassList("ts-gradient-card-use-case");
            heading.Add(name);
            heading.Add(useCase);
            Label description = new Label(recipe.Description);
            description.AddToClassList("ts-gradient-card-description");
            body.Add(heading);
            body.Add(description);
            card.Add(body);

            void ApplyRecipe()
            {
                _onApply?.Invoke(recipe);
                Close();
            }

            card.AddManipulator(new Clickable(ApplyRecipe));
            card.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter &&
                    evt.keyCode != KeyCode.Space)
                {
                    return;
                }
                ApplyRecipe();
                evt.StopPropagation();
            });
            return card;
        }

        int CountFamilyRecipes()
        {
            return TextStudioGradientCatalog.ForKind(_kind).Count;
        }

        static bool MatchesSearch(TextStudioGradientRecipe recipe, string query)
        {
            return string.IsNullOrEmpty(query) ||
                   recipe.SearchText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool MatchesUseCase(TextStudioGradientRecipe recipe, int filterIndex)
        {
            if (filterIndex <= 0)
                return true;
            return (int)recipe.UseCase == filterIndex - 1;
        }
    }
}
