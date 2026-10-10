using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TextStudio.Editor
{
#if UNITY_6000_7_OR_NEWER
    // Recipe definitions are immutable editor metadata and do not own play-session state.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioRecipeInstaller
    {
        const string LocalizationBridgeTypeName =
            "TextStudio.Integration.UnityLocalization.TextStudioLocalizeString, TextStudio.Integration.UnityLocalization";
        const string ClassicAudioSetGuid = "6956d3958c432a64fb2eccd40ebafff0";

        public static IReadOnlyList<TextStudioRecipe> Recipes => s_recipes;

        static readonly TextStudioRecipe[] s_recipes =
        {
            new TextStudioRecipe(
                "dialogue-basic",
                "Dialogue Text",
                "Animated TMP dialogue with a skip-ready dialogue source.",
                CreateDialogue),
            new TextStudioRecipe(
                "long-text",
                "Long Text Pages",
                "TMP page overflow with follow-reveal page navigation.",
                CreateLongText),
            new TextStudioRecipe(
                "audio-player",
                "Audio Player",
                "Per-character and marker audio cues with a prewarmed source pool.",
                CreateAudioPlayer),
            new TextStudioRecipe(
                "marker-events",
                "Marker Events",
                "Animated text with an inspector-friendly marker event router.",
                CreateMarkerEvents),
            new TextStudioRecipe(
                "localization",
                "Unity Localization",
                "Localized Text Studio markup when the optional Localization integration is installed.",
                CreateLocalization,
                isAvailable: HasLocalizationBridge,
                unavailableMessage: "Install Text Studio's Unity Localization integration package before creating this recipe."),
            new TextStudioRecipe(
                "uitk-uxml",
                "UI Toolkit UXML",
                "Copies a normal Label with a persisted native Text Studio binding.",
                CopyUIToolkitUxmlSnippet,
                "Copy Snippet"),
            new TextStudioRecipe(
                "uitk-label",
                "Programmatic UI Toolkit",
                "Copies code that attaches Text Studio to a normal Label.",
                CopyUIToolkitLabelSnippet,
                "Copy Snippet"),
            new TextStudioRecipe(
                "uitk-binding",
                "Direct Label Text",
                "Copies the direct Label text workflow with automatic playback.",
                CopyUIToolkitBindingSnippet,
                "Copy Snippet"),
            new TextStudioRecipe(
                "uitk-element-source",
                "TextField To Label",
                "Copies persisted Element mode UXML using TextField.value as the source for a separate Label.",
                CopyUIToolkitElementSnippet,
                "Copy Snippet"),
            new TextStudioRecipe(
                "uitk-data-source",
                "UI Toolkit Data Source",
                "Copies a property-notifying view model and native Data mode setup.",
                CopyUIToolkitDataSnippet,
                "Copy Snippet"),
            new TextStudioRecipe(
                "uitk-capabilities",
                "Wave And Rainbow",
                "Copies a capability-aware Wave and Rainbow example for Basic and Full UI Toolkit runtimes.",
                CopyUIToolkitCapabilitySnippet,
                "Copy Snippet"),
            new TextStudioRecipe(
                "uitk-hover-restart",
                "UI Toolkit Hover Restart",
                "Copies a concise pointer callback that restarts a Text Studio Label.",
                CopyUIToolkitHoverSnippet,
                "Copy Snippet"),
            new TextStudioRecipe(
                "uitk-focus-reveal",
                "UI Toolkit Focus Reveal",
                "Copies a concise focus callback that reveals a Text Studio Label.",
                CopyUIToolkitFocusSnippet,
                "Copy Snippet")
        };

        public static bool TryGetRecipe(string id, out TextStudioRecipe recipe)
        {
            for (int i = 0; i < s_recipes.Length; i++)
            {
                if (string.Equals(s_recipes[i].Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    recipe = s_recipes[i];
                    return true;
                }
            }

            recipe = null;
            return false;
        }

        public static GameObject CreateRecipe(string id)
        {
            return TryGetRecipe(id, out TextStudioRecipe recipe) ? CreateRecipe(recipe) : null;
        }

        public static GameObject CreateRecipe(TextStudioRecipe recipe)
        {
            if (recipe == null)
                return null;

            if (!recipe.IsAvailable)
            {
                EditorUtility.DisplayDialog("Text Studio Recipes", recipe.UnavailableMessage, "OK");
                return null;
            }

            GameObject created = recipe.Create();
            if (created == null)
                return null;

            Selection.activeGameObject = created;
            EditorGUIUtility.PingObject(created);
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(created.scene);
            return created;
        }

        static GameObject CreateDialogue()
        {
            GameObject go = CreateAnimatedText(
                "Text Studio Dialogue",
                "Hello <wave>operator</wave>.",
                RevealMode.ByGlyph);
            TextStudioDialogueSource source = EnsureComponent<TextStudioDialogueSource>(go);
            source.ShowLine("Hello <wave>operator</wave>.");
            return go;
        }

        static GameObject CreateLongText()
        {
            GameObject go = CreateAnimatedText(
                "Text Studio Long Text",
                "Page one line.<page>Page two line.<page><pulse>Page three line.</pulse>",
                RevealMode.ByGlyph);

            TextStudioText hub = go.GetComponent<TextStudioText>();
            TMP_Text tmp = go.GetComponent<TMP_Text>();
            Undo.RecordObject(tmp, "Configure Text Studio Long Text");
            tmp.overflowMode = TextOverflowModes.Page;
            tmp.rectTransform.sizeDelta = new Vector2(560f, 220f);
            EditorUtility.SetDirty(tmp);

            TextStudioPageNavigator navigator = EnsureComponent<TextStudioPageNavigator>(go);
            Undo.RecordObject(navigator, "Configure Text Studio Page Navigator");
            navigator.SetTargets(hub, tmp);
            navigator.FollowReveal = true;
            EditorUtility.SetDirty(navigator);
            return go;
        }

        static GameObject CreateAudioPlayer()
        {
            GameObject go = CreateAnimatedText(
                "Text Studio Audio Player",
                "Audio-ready <wave>dialogue</wave> line.",
                RevealMode.ByGlyph);
            TextStudioAudioPlayer player = EnsureComponent<TextStudioAudioPlayer>(go);
            Undo.RecordObject(player, "Configure Text Studio Audio Player");
            player.SourcePoolSize = 4;
            player.CooldownSeconds = 0.02f;
            TextStudioAudioSet audioSet = LoadAudioSet(ClassicAudioSetGuid);
            if (audioSet != null)
            {
                player.SettingsSource = TextStudioAudioPlayerSettingsSource.AudioSet;
                player.AudioSet = audioSet;
            }
            else
            {
                Debug.LogWarning(
                    "Text Studio Audio Player recipe created without sample audio because the AudioTypewriter examples are not present.");
            }
            player.PrewarmPool();
            EditorUtility.SetDirty(player);
            return go;
        }

        static GameObject CreateMarkerEvents()
        {
            GameObject go = CreateAnimatedText(
                "Text Studio Marker Events",
                "Choose <event=choiceA,speaker,line42><pulse>north gate</pulse>.",
                RevealMode.ByGlyph);
            TextStudioMarkerEventRouter router = EnsureComponent<TextStudioMarkerEventRouter>(go);
            Undo.RecordObject(router, "Configure Text Studio Marker Router");
            if (router.Bindings.Count == 0)
                router.AddBinding("choiceA", null);
            EditorUtility.SetDirty(router);
            return go;
        }

        static GameObject CreateLocalization()
        {
            Type bridgeType = Type.GetType(LocalizationBridgeTypeName);
            if (bridgeType == null)
                return null;

            GameObject go = CreateAnimatedText(
                "Text Studio Localized Line",
                "Localized <wave>markup</wave> line.",
                RevealMode.ByGlyph);
            Undo.AddComponent(go, bridgeType);
            return go;
        }

        static GameObject CopyUIToolkitUxmlSnippet()
        {
            return CopyUIToolkitSnippet(
                "<UXML xmlns=\"UnityEngine.UIElements\" xmlns:ts=\"TextStudio\" editor-extension-mode=\"False\">\n" +
                "    <Label name=\"animated-title\" text=\"Hello &lt;wave&gt;UI Toolkit&lt;/wave&gt;\">\n" +
                "        <Bindings>\n" +
                "            <ts:TextStudioBinding property=\"text\" source-mode=\"LabelText\"\n" +
                "                update-mode=\"Automatic\" play-on-attach=\"true\"\n" +
                "                reveal-mode=\"ByGlyph\" speed-multiplier=\"1\" loop-reveal=\"false\" />\n" +
                "        </Bindings>\n" +
                "    </Label>\n" +
                "</UXML>",
                "Copied a normal Label with a persisted Text Studio binding to the clipboard.");
        }

        static GameObject CopyUIToolkitLabelSnippet()
        {
            return CopyUIToolkitSnippet(
                "Label label = new Label(\"Hello <wave>UI Toolkit</wave>\");\n" +
                "TextStudioBinding binding = label.AttachTextStudio();\n" +
                "binding.RevealMode = RevealMode.ByGlyph;\n" +
                "binding.UpdateMode = TextStudioUITKUpdateMode.Automatic;\n" +
                "binding.PlayOnAttach = true;\n" +
                "uiDocument.rootVisualElement.Add(label);",
                "Copied a code-created Label and Text Studio binding to the clipboard.");
        }

        static GameObject CopyUIToolkitBindingSnippet()
        {
            return CopyUIToolkitSnippet(
                "Label label = uiDocument.rootVisualElement.Q<Label>(\"animated-score\");\n" +
                "TextStudioBinding animation = label.AttachTextStudio();\n" +
                "animation.UpdateMode = TextStudioUITKUpdateMode.Automatic;\n" +
                "animation.RevealMode = RevealMode.ByGlyph;\n" +
                "label.text = \"Score <pulse>1,250</pulse>\";\n" +
                "animation.StartReveal();",
                "Copied an ordinary Label binding example to the clipboard.");
        }

        static GameObject CopyUIToolkitElementSnippet()
        {
            return CopyUIToolkitSnippet(
                "<UXML xmlns=\"UnityEngine.UIElements\" xmlns:ts=\"TextStudio\" editor-extension-mode=\"False\">\n" +
                "    <TextField name=\"message-source\" value=\"Edit &lt;wave&gt;this text&lt;/wave&gt;\" />\n" +
                "    <Label name=\"animated-output\" text=\"Fallback\">\n" +
                "        <Bindings>\n" +
                "            <ts:TextStudioBinding property=\"text\" source-mode=\"Element\"\n" +
                "                source-element=\"message-source\" source-property-path=\"value\" />\n" +
                "        </Bindings>\n" +
                "    </Label>\n" +
                "</UXML>",
                "Copied a TextField-to-Label Element source example to the clipboard.");
        }

        static GameObject CopyUIToolkitDataSnippet()
        {
            return CopyUIToolkitSnippet(
                "[GeneratePropertyBag]\n" +
                "sealed class DialogueViewModel : INotifyBindablePropertyChanged\n" +
                "{\n" +
                "    string _line;\n" +
                "    public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;\n" +
                "    [CreateProperty] public string Line\n" +
                "    {\n" +
                "        get => _line;\n" +
                "        set { if (_line == value) return; _line = value; propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(nameof(Line))); }\n" +
                "    }\n" +
                "}\n\n" +
                "Label label = root.Q<Label>(\"animated-line\");\n" +
                "TextStudioBinding binding = label.GetTextStudioBinding();\n" +
                "binding.SourceMode = TextStudioBindingSourceMode.Data;\n" +
                "binding.dataSource = viewModel;\n" +
                "binding.dataSourcePath = new PropertyPath(nameof(DialogueViewModel.Line));\n" +
                "binding.MarkDirty();",
                "Copied a native Data source example to the clipboard.");
        }

        static GameObject CopyUIToolkitCapabilitySnippet()
        {
            return CopyUIToolkitSnippet(
                "Label label = root.Q<Label>(\"animated-title\");\n" +
                "TextStudioBinding binding = label.AttachTextStudio();\n" +
                "label.text = \"<wave><rainbow>Full UI Toolkit effects</rainbow></wave>\";\n" +
                "if (!TextStudioUITKCapabilities.FullParityAvailable)\n" +
                "    Debug.Log(\"Typewriter works here; Wave and Rainbow need Unity 6000.2+ on the target Label.\");",
                "Copied a capability-aware Wave and Rainbow example to the clipboard.");
        }

        static GameObject CopyUIToolkitHoverSnippet()
        {
            return CopyUIToolkitSnippet(
                "Label label = root.Q<Label>(\"hover-label\");\n" +
                "TextStudioBinding binding = label.GetTextStudioBinding();\n" +
                "label.RegisterCallback<PointerEnterEvent>(_ => binding.StartReveal());",
                "Copied a hover restart example to the clipboard.");
        }

        static GameObject CopyUIToolkitFocusSnippet()
        {
            return CopyUIToolkitSnippet(
                "Label label = root.Q<Label>(\"focus-label\");\n" +
                "TextStudioBinding binding = label.GetTextStudioBinding();\n" +
                "label.focusable = true;\n" +
                "label.RegisterCallback<FocusInEvent>(_ => binding.StartReveal());",
                "Copied a focus reveal example to the clipboard.");
        }

        static GameObject CopyUIToolkitSnippet(string snippet, string confirmation)
        {
            EditorGUIUtility.systemCopyBuffer = snippet;
            EditorUtility.DisplayDialog("Text Studio Recipes", confirmation, "OK");
            return null;
        }

        static bool HasLocalizationBridge()
        {
            return Type.GetType(LocalizationBridgeTypeName) != null;
        }

        static TextStudioAudioSet LoadAudioSet(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<TextStudioAudioSet>(path);
        }

        static GameObject CreateAnimatedText(string objectName, string markup, RevealMode revealMode)
        {
            TMP_Text tmp = TextGalleryActions.CreateTMPExample(objectName, markup, revealMode, PlacementMode.Linear);
            GameObject go = tmp.gameObject;
            Selection.activeGameObject = go;
            return go;
        }

        static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(go);
        }
    }
}
