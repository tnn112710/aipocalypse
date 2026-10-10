using System;
using System.Collections.Generic;
using System.Linq;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal sealed partial class TextStudioRuntimePreparationWindow
    {
        Toggle _editorAutomatic, _buildAutomatic;
        readonly List<(TextStudioRuntimeCharacterSet set, Toggle toggle)> _characterToggles = new List<(TextStudioRuntimeCharacterSet, Toggle)>();
        VisualElement _runtimeSection;
        Foldout _moreCharacters;
        Label _editingHelp, _buildHelp;
        DropdownField _scopeChoice, _embedding;
        TextField _corpus;
        ObjectField _expertTarget;
        VisualElement _expert;
        Foldout _addonSettings;
        TextStudioText _expertBuiltFor;
        TextStudioTextEditor _advancedEditor;
        bool _expandAddons;
        int _settingsState = -1;

        void BuildSettings(VisualElement parent)
        {
            TextStudioRuntimePreparationSettings settings = TextStudioRuntimePreparationSettings.instance;
            VisualElement automation = Section(parent, "Automation", "Text Studio keeps prepared data current for you. Turn these off only if you prefer to prepare manually.");
            _buildAutomatic = new Toggle { text = "Prepare before each build", name = "preparation-settings-builds",
                tooltip = "Prepare known text from the actual build content before each player build. Required failures stop the build with an explanation." };
            _buildAutomatic.RegisterValueChangedCallback(evt => { settings.automaticInBuilds = evt.newValue; SaveSettings(); });
            _buildHelp = Copy(string.Empty, "ts-preparation-setting-help");
            _editorAutomatic = new Toggle { text = "Update after edits", name = "preparation-settings-edits",
                tooltip = "Refresh affected text in open scenes after a short pause in editing. Turn off to prepare manually or when building." };
            _editorAutomatic.RegisterValueChangedCallback(evt => { settings.automaticInEditor = evt.newValue; SaveSettings(); });
            _editingHelp = Copy(string.Empty, "ts-preparation-setting-help");
            automation.Add(_buildAutomatic);
            automation.Add(_buildHelp);
            automation.Add(_editorAutomatic);
            automation.Add(_editingHelp);

            VisualElement scope = Section(parent, "Overview", null);
            _scopeChoice = new DropdownField("Check", new List<string> { "Whole project", "Open scenes only" }, _openScenesOnly ? 1 : 0)
            { name = "preparation-settings-scope", tooltip = "What the overview counts and Prepare includes. Builds always prepare their own content." };
            _scopeChoice.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            _scopeChoice.RegisterValueChangedCallback(_ =>
            {
                _openScenesOnly = _scopeChoice.index == 1;
                _checkRequested = !_openScenesOnly;
                _renderedSignature = 0;
                Tick();
            });
            scope.Add(_scopeChoice);
            scope.Add(Copy(ScopeTooltip + " Open scenes only skips reading saved build files, which is faster in very large projects.", "ts-preparation-setting-help", "ts-preparation-field-help"));

            VisualElement fonts = Section(parent, "Font outlines", null);
            _embedding = new DropdownField("Permission", new List<string> { "Ask when needed", "Allow in this project", "Use existing data only" }, 0)
            { name = "preparation-settings-fonts", tooltip = "Whether new font outlines may be saved with your project and included in builds. Existing valid data can always be reused." };
            _embedding.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            _embedding.RegisterValueChangedCallback(_ =>
            {
                settings.embeddingChoiceMade = _embedding.index > 0;
                settings.allowFontEmbedding = _embedding.index == 1;
                SaveSettings();
            });
            fonts.Add(_embedding);
            fonts.Add(Copy("Shape effects and 3D text need the outlines of your fonts in builds. Only allow saving them for fonts whose licenses permit embedding. This applies to all Text Studio add-ons.", "ts-preparation-setting-help", "ts-preparation-field-help"));

            _runtimeSection = Section(parent, "Characters shown at runtime", "Text in your scenes, localization tables, terminal sequences and mechanical displays is found automatically. Add the characters of text that changes while playing, such as scores, timers or player names.");
            _runtimeSection.name = "preparation-settings-characters";
            VisualElement common = CommonUITK.CreateContainer("ts-preparation-character-sets");
            VisualElement extended = CommonUITK.CreateContainer("ts-preparation-character-sets");
            _moreCharacters = new Foldout { text = "More character sets", value = false, tooltip = "Accented Latin, Greek and Cyrillic letters." };
            _moreCharacters.AddToClassList("ts-preparation-expert-foldout");
            _moreCharacters.Add(extended);
            _characterToggles.Clear();
            foreach (TextStudioRuntimeCharacters.Entry entry in TextStudioRuntimeCharacters.Entries)
            {
                TextStudioRuntimeCharacterSet set = entry.Set;
                Toggle toggle = new Toggle { text = entry.Label, tooltip = entry.Tooltip + " Characters a font does not contain are skipped." };
                toggle.AddToClassList("ts-preparation-character-set");
                toggle.RegisterValueChangedCallback(evt =>
                {
                    settings.runtimeCharacters = evt.newValue ? settings.runtimeCharacters | set : settings.runtimeCharacters & ~set;
                    SaveSettings();
                    RequestCoverageRecheck();
                });
                _characterToggles.Add((set, toggle));
                (entry.Common ? common : extended).Add(toggle);
            }
            _runtimeSection.Add(common);
            _runtimeSection.Add(_moreCharacters);
            _corpus = new TextField("Phrases and special characters") { multiline = true, value = settings.additionalText, name = "preparation-settings-text",
                tooltip = "Known phrases, names or symbols that code or a server shows, in addition to the sets above." };
            _corpus.AddToClassList("ts-preparation-corpus");
            _corpus.RegisterValueChangedCallback(evt => { settings.additionalText = evt.newValue; SaveSettings(); });
            _corpus.RegisterCallback<FocusOutEvent>(_ => RequestCoverageRecheck());
            _runtimeSection.Add(_corpus);
            _runtimeSection.Add(Copy("Each character is prepared for every font and 3D style that shows text, so more characters add more data. Prepared data lists the sizes. Missing characters are generated at runtime where the renderer supports it.", "ts-preparation-setting-help", "ts-preparation-field-help"));

            VisualElement expert = Section(parent, "One text object", "Expert controls for one text's appearance data and add-on preparation. They affect shared data and can increase build time and memory.");
            _expertTarget = new ObjectField("Text") { objectType = typeof(TextStudioText), allowSceneObjects = true, name = "preparation-settings-target",
                tooltip = "The text whose expert preparation settings you want to adjust." };
            _expertTarget.AddToClassList(BaseField<UnityEngine.Object>.alignedFieldUssClassName);
            _expertTarget.RegisterValueChangedCallback(evt => SetExpertTarget(evt.newValue as TextStudioText));
            expert.Add(_expertTarget);
            _expert = CommonUITK.CreateContainer("ts-preparation-expert");
            expert.Add(_expert);
            ApplyExpertTarget();
        }

        /// <summary>Coverage changes can make saved build content incomplete, so it is checked again at the next quiet moment.</summary>
        static void RequestCoverageRecheck() => s_recheckBuildContent = true;

        void ShowRuntimeCharacters()
        {
            SelectTab(SettingsTab);
            _settingsScroll.schedule.Execute(() =>
            {
                _settingsScroll.ScrollTo(_runtimeSection);
                if (_characterToggles.Count > 0) _characterToggles[0].toggle.Focus();
            }).ExecuteLater(60);
        }

        static VisualElement Section(VisualElement parent, string title, string intro)
        {
            VisualElement section = CommonUITK.CreateContainer("ts-preparation-settings-section");
            section.Add(CommonUITK.CreateLabel(title, "ts-preparation-section-title"));
            if (!string.IsNullOrEmpty(intro)) section.Add(Copy(intro, "ts-preparation-section-intro"));
            parent.Add(section);
            return section;
        }

        void SyncSettings(TextStudioRuntimePreparationSettings settings)
        {
            if (_buildAutomatic == null) return;
            int state = (settings.automaticInEditor ? 1 : 0) | (settings.automaticInBuilds ? 2 : 0) | (settings.embeddingChoiceMade ? 4 : 0) | (settings.allowFontEmbedding ? 8 : 0) | (_openScenesOnly ? 16 : 0);
            state = HashCode.Combine(state, settings.runtimeCharacters, settings.additionalText);
            if (state == _settingsState) return;
            _settingsState = state;
            foreach ((TextStudioRuntimeCharacterSet set, Toggle toggle) in _characterToggles) toggle.SetValueWithoutNotify((settings.runtimeCharacters & set) != 0);
            if (_characterToggles.Any(entry => entry.toggle.parent != null && entry.toggle.parent.parent == _moreCharacters.contentContainer && entry.toggle.value)) _moreCharacters.value = true;
            if (_corpus.panel?.focusController?.focusedElement != _corpus && _corpus.value != settings.additionalText) _corpus.SetValueWithoutNotify(settings.additionalText);
            _buildAutomatic.SetValueWithoutNotify(settings.automaticInBuilds);
            _editorAutomatic.SetValueWithoutNotify(settings.automaticInEditor);
            _scopeChoice.SetValueWithoutNotify(_scopeChoice.choices[_openScenesOnly ? 1 : 0]);
            _embedding.SetValueWithoutNotify(_embedding.choices[!settings.embeddingChoiceMade ? 0 : settings.allowFontEmbedding ? 1 : 2]);
            SetText(_buildHelp, settings.automaticInBuilds
                ? "Builds prepare their content automatically. Required data that cannot be prepared stops the build with an explanation."
                : "Builds use only data you prepare. Prepare required data before building; optional data is generated at runtime where supported.");
            SetText(_editingHelp, settings.automaticInEditor
                ? "Edited text in open scenes is updated after a short pause. Keep working as usual."
                : "Edited text is not updated automatically. Use Prepare in the overview, or let builds prepare their content.");
        }

        void ApplyExpertTarget()
        {
            if (_expertTarget == null) return;
            _expertTarget.SetValueWithoutNotify(_target);
            if (_expertBuiltFor != _target || _expert.childCount == 0) BuildExpertControls();
            if (!_expandAddons || _addonSettings == null) return;
            _expandAddons = false;
            _addonSettings.value = true;
            _settingsScroll.schedule.Execute(() => _settingsScroll.ScrollTo(_addonSettings)).ExecuteLater(60);
        }

        void SetExpertTarget(TextStudioText target)
        {
            _target = target;
            ApplyExpertTarget();
            _renderedSignature = 0;
        }

        void BuildExpertControls()
        {
            ReleaseExpertEditor();
            _expert.Clear();
            _addonSettings = null;
            _expertBuiltFor = _target;
            if (_target == null)
            {
                _expert.Add(Copy("Choose a text object to adjust its appearance data and add-on preparation settings.", "ts-preparation-setting-help", "ts-preparation-field-help"));
                return;
            }
            TextStudioText target = _target;
            Foldout appearance = new Foldout { text = "2D appearance data and inline images", value = false,
                tooltip = "Prepared font bindings, field quality, distance reach, image silhouettes and explicit font inventory." };
            appearance.AddToClassList("ts-preparation-expert-foldout");
            appearance.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != appearance || !evt.newValue || _advancedEditor != null || target == null) return;
                _advancedEditor = UnityEditor.Editor.CreateEditor(target) as TextStudioTextEditor;
                if (_advancedEditor == null) return;
                VisualElement content = _advancedEditor.BuildFontAppearanceAdvanced(target);
                content.Bind(_advancedEditor.serializedObject);
                appearance.Add(content);
            });
            _expert.Add(appearance);
            Foldout addons = new Foldout { text = "Add-on settings", value = false, tooltip = "Geometry policies, font sources, coverage and prepared libraries of installed add-ons for this text." };
            addons.AddToClassList("ts-preparation-expert-foldout");
            bool built = false;
            addons.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != addons || !evt.newValue || built || target == null) return;
                built = true;
                BuildAddonSettings?.Invoke(target, addons, SetExpertTarget);
                if (addons.contentContainer.childCount == 0) addons.Add(Copy("This text has no add-on preparation settings.", "ts-preparation-setting-help"));
            });
            _expert.Add(addons);
            _addonSettings = addons;
        }

        void ReleaseExpertEditor()
        {
            if (_advancedEditor != null) DestroyImmediate(_advancedEditor);
            _advancedEditor = null;
        }
    }
}
