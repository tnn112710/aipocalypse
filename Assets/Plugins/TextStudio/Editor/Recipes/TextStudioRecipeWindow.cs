using ImpossibleRobert.Common;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal sealed class TextStudioRecipeWindow : EditorWindow
    {
        [MenuItem(TextStudioMenuPaths.Recipes, false, TextStudioMenuPaths.RecipesPriority)]
        public static void ShowWindow()
        {
            TextStudioRecipeWindow window = GetWindow<TextStudioRecipeWindow>();
            window.titleContent = new GUIContent("Text Studio Recipes");
            window.minSize = new Vector2(500f, 360f);
            window.Show();
        }

        void CreateGUI()
        {
            rootVisualElement.Clear();
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-recipe-window-root");
            root.style.flexGrow = 1f;
            CommonUITK.ApplyWindowLayout(root);
            rootVisualElement.Add(root);
            root.Add(TextStudioInspectorElements.CreateHelpBox(
                "Create small, editable starting objects for common production workflows.",
                HelpBoxMessageType.Info,
                "ts-recipe-header"));

            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "ts-recipe-list"
            };
            scroll.style.flexGrow = 1f;
            root.Add(scroll);
            IReadOnlyList<TextStudioRecipe> recipes = TextStudioRecipeInstaller.Recipes;
            for (int i = 0; i < recipes.Count; i++)
                scroll.Add(CreateRecipe(recipes[i]));
        }

        static VisualElement CreateRecipe(TextStudioRecipe recipe)
        {
            VisualElement section = TextStudioInspectorElements.CreateSection(
                recipe.Title,
                "ts-recipe-card",
                TextStudioInspectorElements.CreateMutedText(recipe.Description));

            if (!recipe.IsAvailable && !string.IsNullOrEmpty(recipe.UnavailableMessage))
                section.Add(TextStudioInspectorElements.CreateHelpBox(
                    recipe.UnavailableMessage,
                    HelpBoxMessageType.Warning));

            Button create = TextStudioInspectorElements.CreateButton(
                recipe.ActionLabel,
                "Create this recipe in the current scene.",
                () => TextStudioRecipeInstaller.CreateRecipe(recipe),
                primary: true);
            create.SetEnabled(recipe.IsAvailable);
            section.Add(create);

            if (recipe.Id == "long-text")
            {
                Button apply = TextStudioInspectorElements.CreateButton(
                    "Apply to Selected Text",
                    "Configures the selected Text Studio object for long text pages without replacing it.",
                    TextStudioLongTextPreset.ApplyToSelection);
                section.Add(apply);
            }

            return section;
        }
    }
}
