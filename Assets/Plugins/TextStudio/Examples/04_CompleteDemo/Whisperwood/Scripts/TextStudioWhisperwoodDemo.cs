using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UIButton = UnityEngine.UI.Button;
using UIImage = UnityEngine.UI.Image;
using UITKButton = UnityEngine.UIElements.Button;

namespace TextStudio.Examples.Whisperwood
{
    [ExecuteAlways]
    [AddComponentMenu("")]
    public sealed partial class TextStudioWhisperwoodDemo : MonoBehaviour
    {
        public enum Backend
        {
            Canvas,
            UIToolkit
        }

        enum DemoMode
        {
            Menu,
            Exploring,
            Dialogue,
            Pause,
            Credits,
            Choice,
            Journal,
            Finale,
            Clue
        }

        enum DialogueCompletion
        {
            None,
            NpcReward,
            GateBlocked,
            GateFinale
        }

        const string TitleMarkup = "<shimmer>Whisperwood Letters</shimmer>";
        const string StartPromptMarkup = "<pulse>Press Start, Space, or Enter.</pulse>";
        const float PlayerMoveSpeed = 0.28f;
        const float NpcInteractDistance = 0.13f;
        const float GateInteractDistance = 0.12f;

        static readonly Vector2 GatePosition = new Vector2(0.5f, 0.86f);

        [Header("Scenario")]
        [SerializeField] Backend _backend;
        [SerializeField] TextStudioWhisperwoodStory _story;

        [Header("Canvas View")]
        [SerializeField] RectTransform _canvasWorldRect;
        [SerializeField] RectTransform _canvasMenuPanel;
        [SerializeField] RectTransform _canvasMenuButtonRow;
        [SerializeField] RectTransform _canvasDialoguePanel;
        [SerializeField] RectTransform _canvasCreditsPanel;
        [SerializeField] RectTransform _canvasPlayerMarker;
        [SerializeField] RectTransform _canvasGateMarker;
        [SerializeField] UIImage _canvasGateImage;
        [SerializeField] RectTransform[] _canvasNpcMarkers = Array.Empty<RectTransform>();
        [SerializeField] TextStudioText _canvasTitle;
        [SerializeField] TextStudioText _canvasMenuBody;
        [SerializeField] TextStudioText _canvasHud;
        [SerializeField] TextStudioText _canvasQuest;
        [SerializeField] TextStudioText _canvasPrompt;
        [SerializeField] TextStudioText _canvasNearby;
        [SerializeField] TextStudioText _canvasGateSign;
        [SerializeField] TextStudioText _canvasSpeaker;
        [SerializeField] TextStudioText _canvasDialogue;
        [SerializeField] TextStudioText _canvasCredits;
        [SerializeField] TextStudioDialogueSource _canvasDialogueSource;
        [SerializeField] UIButton _canvasStartButton;
        [SerializeField] UIButton _canvasCreditsButton;
        [SerializeField] UIButton _canvasResumeButton;
        [SerializeField] UIButton _canvasRestartButton;
        [SerializeField] UIButton _canvasContinueButton;
        [SerializeField] UIButton _canvasSkipButton;

        [Header("UI Toolkit View")]
        [SerializeField] UIDocument _uiDocument;

        readonly List<TextStudioBinding> _uitkBindings = new List<TextStudioBinding>(20);
        VisualElement[] _uitkNpcMarkers = Array.Empty<VisualElement>();
        bool[] _collectedWords = Array.Empty<bool>();

        DemoMode _mode;
        DemoMode _modeBeforePause;
        DialogueCompletion _dialogueCompletion;
        string[] _activeDialogueLines = Array.Empty<string>();
        int _activeNpcIndex = -1;
        int _dialogueNpcIndex = -1;
        int _dialogueLineIndex;
        int _collectedWordCount;
        int _nearNpcIndex = -1;
        bool _nearGate;
        bool _gateOpen;
        bool _initialized;
        bool _needsUIToolkitBinding;
        float _lastRealtime;
        Vector2 _playerPosition = new Vector2(0.5f, 0.28f);

        VisualElement _uitkRoot;
        VisualElement _uitkWorld;
        VisualElement _uitkPlayerMarker;
        VisualElement _uitkGateMarker;
        VisualElement _uitkMenuPanel;
        VisualElement _uitkMenuButtonRow;
        VisualElement _uitkDialoguePanel;
        VisualElement _uitkCreditsPanel;
        TextStudioBinding _uitkTitle;
        TextStudioBinding _uitkMenuBody;
        TextStudioBinding _uitkHud;
        TextStudioBinding _uitkQuest;
        TextStudioBinding _uitkPrompt;
        TextStudioBinding _uitkNearby;
        TextStudioBinding _uitkGateSign;
        TextStudioBinding _uitkSpeaker;
        TextStudioBinding _uitkDialogue;
        TextStudioBinding _uitkCredits;
        UITKButton _uitkStartButton;
        UITKButton _uitkCreditsButton;
        UITKButton _uitkResumeButton;
        UITKButton _uitkRestartButton;
        UITKButton _uitkContinueButton;
        UITKButton _uitkSkipButton;

        public Backend ActiveBackend => _backend;
        public TextStudioWhisperwoodStory Story => _story;

        public void ConfigureForScene(Backend backend, TextStudioWhisperwoodStory story, UIDocument uiDocument = null)
        {
            _backend = backend;
            _story = story;
            _uiDocument = uiDocument;
        }

        public void ConfigureCanvasView(
            RectTransform worldRect,
            RectTransform menuPanel,
            RectTransform menuButtonRow,
            RectTransform dialoguePanel,
            RectTransform creditsPanel,
            RectTransform playerMarker,
            RectTransform gateMarker,
            UIImage gateImage,
            RectTransform[] npcMarkers,
            TextStudioText title,
            TextStudioText menuBody,
            TextStudioText hud,
            TextStudioText quest,
            TextStudioText prompt,
            TextStudioText nearby,
            TextStudioText gateSign,
            TextStudioText speaker,
            TextStudioText dialogue,
            TextStudioText credits,
            TextStudioDialogueSource dialogueSource,
            UIButton startButton,
            UIButton creditsButton,
            UIButton resumeButton,
            UIButton restartButton,
            UIButton continueButton,
            UIButton skipButton)
        {
            _canvasWorldRect = worldRect;
            _canvasMenuPanel = menuPanel;
            _canvasMenuButtonRow = menuButtonRow;
            _canvasDialoguePanel = dialoguePanel;
            _canvasCreditsPanel = creditsPanel;
            _canvasPlayerMarker = playerMarker;
            _canvasGateMarker = gateMarker;
            _canvasGateImage = gateImage;
            _canvasNpcMarkers = npcMarkers ?? Array.Empty<RectTransform>();
            _canvasTitle = title;
            _canvasMenuBody = menuBody;
            _canvasHud = hud;
            _canvasQuest = quest;
            _canvasPrompt = prompt;
            _canvasNearby = nearby;
            _canvasGateSign = gateSign;
            _canvasSpeaker = speaker;
            _canvasDialogue = dialogue;
            _canvasCredits = credits;
            _canvasDialogueSource = dialogueSource;
            _canvasStartButton = startButton;
            _canvasCreditsButton = creditsButton;
            _canvasResumeButton = resumeButton;
            _canvasRestartButton = restartButton;
            _canvasContinueButton = continueButton;
            _canvasSkipButton = skipButton;
        }

        void OnEnable()
        {
            InitializeBackend();
        }

        void OnDisable()
        {
            ResumeDialogueFromOverlay();
            StopCluePlayback();
            UnsubscribeDialogueEvents();
            UnbindUIToolkitButtons();
            _initialized = false;
            if (_voice != null) _voice.StopPlayback();
        }

        void Update()
        {
            if (_needsUIToolkitBinding && TryBindUIToolkit())
                FinishInitialization();

            if (!_initialized)
                return;

            float deltaTime = GetDeltaTime();
            if (Application.isPlaying) UpdateClue();
            UpdateUITKLabels(deltaTime);
            if (!Application.isPlaying)
                return;

            UpdateGateFinale();
            HandleKeyboardInput();
            if (_mode == DemoMode.Exploring)
                UpdateMovement(deltaTime);
        }

        public void StartGame()
        {
            ResetRun();
            SetFinaleVisible(false);
            _mode = DemoMode.Exploring;
            SetMenuVisible(false);
            SetCreditsVisible(false);
            SetDialogueVisible(false);
            SetHudVisible(true);
            SetTitleVisible(false);
            SetWorldOverlaysVisible(true);
            SetPrompt("<pulse>Move with WASD or arrows. Press E near a villager.</pulse>");
            UpdateQuestText(false);
            UpdatePlayerView();
            UpdateProximity();
        }

        public void ResumeGame()
        {
            _mode = _modeBeforePause == DemoMode.Dialogue || _modeBeforePause == DemoMode.Choice || _modeBeforePause == DemoMode.Finale || _modeBeforePause == DemoMode.Clue ? _modeBeforePause : DemoMode.Exploring;
            ResumeDialogueFromOverlay();
            ResumeClueFromOverlay();
            SetFinaleVisible(_mode == DemoMode.Finale);
            SetMenuVisible(false);
            SetCreditsVisible(false);
            SetTitleVisible(false);
            SetDialogueVisible(_mode == DemoMode.Dialogue || _mode == DemoMode.Choice || _mode == DemoMode.Clue);
            SetHudVisible(true);
            SetWorldOverlaysVisible(_mode != DemoMode.Finale);
            UpdateProximity();
        }

        public void OpenCredits()
        {
            ShowCredits();
        }

        public void Advance()
        {
            if (_mode == DemoMode.Menu)
                StartGame();
            else if (_mode == DemoMode.Dialogue)
                AdvanceDialogueSequence();
            else if (_mode == DemoMode.Finale)
                ContinueFinale();
            else if (_mode == DemoMode.Clue)
                ReturnFromClue();
            else if (_mode == DemoMode.Credits)
                ShowMenu();
            else if (_mode == DemoMode.Pause)
                ResumeGame();
        }

        public void SkipConversation()
        {
            if (_mode == DemoMode.Dialogue)
                CompleteDialogue(true);
        }

        void InitializeBackend()
        {
            StopCluePlayback();
            UnsubscribeDialogueEvents();
            UnbindUIToolkitButtons();
            _initialized = false;
            _needsUIToolkitBinding = false;
            int npcCount = NpcCount;
            _collectedWords = new bool[npcCount];
            _uitkNpcMarkers = new VisualElement[npcCount];

            if (_backend == Backend.Canvas)
            {
                if (_canvasDialogue != null)
                {
                    _canvasDialogue.OnMarkerReached += HandleDialogueMarker;
                    if (Application.isPlaying && _voiceReactions != null)
                        _canvasDialogue.Reactions = TextStudioReactionRecipe.Custom(_voiceReactions);
                    if (Application.isPlaying)
                        _canvasDialogue.OnReactionAudioCue += HandleCanvasDialogueVoice;
                }
                FinishInitialization();
            }
            else if (TryBindUIToolkit())
            {
                FinishInitialization();
            }
            else
            {
                _needsUIToolkitBinding = true;
            }
        }

        void FinishInitialization()
        {
            _needsUIToolkitBinding = false;
            _initialized = true;
            ApplySoundPreference();
            ShowMenu();
        }

        bool TryBindUIToolkit()
        {
            if (_uiDocument == null || _uiDocument.rootVisualElement == null)
                return false;
            VisualElement documentRoot = _uiDocument.rootVisualElement;
            _uitkRoot = documentRoot.Q<VisualElement>("whisperwood-root");
            if (_uitkRoot == null)
                return false;

            _uitkWorld = _uitkRoot.Q<VisualElement>("world");
            _uitkPlayerMarker = _uitkRoot.Q<VisualElement>("player-marker");
            _uitkGateMarker = _uitkRoot.Q<VisualElement>("moon-gate-marker");
            _uitkMenuPanel = _uitkRoot.Q<VisualElement>("menu-panel");
            _uitkMenuButtonRow = _uitkRoot.Q<VisualElement>("menu-button-row");
            _uitkDialoguePanel = _uitkRoot.Q<VisualElement>("dialogue-panel");
            _uitkCreditsPanel = _uitkRoot.Q<VisualElement>("credits-panel");
            _uitkTitle = QueryBinding("title", RevealMode.ByGlyph);
            _uitkMenuBody = QueryBinding("menu-body", RevealMode.ByWord);
            _uitkHud = QueryBinding("controls", RevealMode.Instant);
            _uitkQuest = QueryBinding("quest", RevealMode.Instant);
            _uitkPrompt = QueryBinding("prompt", RevealMode.Instant);
            _uitkNearby = QueryBinding("nearby", RevealMode.Instant);
            _uitkGateSign = QueryBinding("gate-sign", RevealMode.Instant);
            _uitkSpeaker = QueryBinding("speaker", RevealMode.Instant);
            _uitkDialogue = QueryBinding("dialogue", RevealMode.ByWord);
            if (_uitkDialogue != null)
            {
                _uitkDialogue.TriggerRemainingMarkersOnSkip = true;
                if (Application.isPlaying && _voiceReactions != null)
                    _uitkDialogue.Reactions = TextStudioReactionRecipe.Custom(_voiceReactions);
            }
            _uitkCredits = QueryBinding("credits", RevealMode.ByWord);
            _uitkStartButton = _uitkRoot.Q<UITKButton>("start-button");
            _uitkCreditsButton = _uitkRoot.Q<UITKButton>("credits-button");
            _uitkResumeButton = _uitkRoot.Q<UITKButton>("resume-button");
            _uitkRestartButton = _uitkRoot.Q<UITKButton>("restart-button");
            _uitkContinueButton = _uitkRoot.Q<UITKButton>("continue-button");
            _uitkSkipButton = _uitkRoot.Q<UITKButton>("skip-button");

            for (int i = 0; i < NpcCount; i++)
            {
                _uitkNpcMarkers[i] = _uitkRoot.Q<VisualElement>("npc-marker-" + i);
                SetUITKMarkerPosition(_uitkNpcMarkers[i], NpcAt(i).Position, 34f, 34f);
                TextStudioBinding npcLabel = QueryBinding("npc-label-" + i, RevealMode.Instant);
                npcLabel?.ShowTextInstant(NpcAt(i).Name);
            }
            SetUITKMarkerPosition(_uitkGateMarker, GatePosition, 54f, 54f);
            SetUITKMarkerPosition(_uitkPlayerMarker, _playerPosition, 28f, 28f);

            BindUIToolkitButtons();
            if (_uitkDialogue != null)
            {
                _uitkDialogue.OnMarkerReached += HandleDialogueMarker;
                if (Application.isPlaying)
                    _uitkDialogue.OnReactionAudioCue += HandleUIToolkitDialogueVoice;
            }
            _uitkHud?.ShowTextInstant("WASD / Arrows move   E talks   Esc pauses");
            return _uitkDialogue != null;
        }

        TextStudioBinding QueryBinding(string name, RevealMode revealMode)
        {
            Label label = _uitkRoot?.Q<Label>(name);
            TextStudioBinding binding = label?.GetTextStudioBinding();
            if (binding == null)
                return null;

            // A scene UIDocument uses a player-context panel outside Play Mode, so Unity can
            // leave a serialized binding inactive even though the visual tree is attached.
            // Attach that existing binding directly so the ExecuteAlways sample remains readable.
            if (binding.Element == null)
                binding.Attach(label);

            binding.EnableInEditMode = true;
            binding.UpdateMode = TextStudioUITKUpdateMode.Manual;
            binding.RevealMode = revealMode;
            binding.TextChangeScope = TextStudioTextChangeScope.WholeText;
            binding.TextGeneratorPreference = TextStudioUITKTextGeneratorPreference.ProjectDefault;
            binding.SetRevealLoop(false, 1f);
            if (!_uitkBindings.Contains(binding))
                _uitkBindings.Add(binding);
            return binding;
        }

        void BindUIToolkitButtons()
        {
            BindStoryInteractions();
            BindSoundButton();
            BindFinale();
            BindClueButton();
            if (_uitkStartButton != null)
                _uitkStartButton.clicked += StartGame;
            if (_uitkCreditsButton != null)
                _uitkCreditsButton.clicked += OpenCredits;
            if (_uitkResumeButton != null)
                _uitkResumeButton.clicked += ResumeGame;
            if (_uitkRestartButton != null)
                _uitkRestartButton.clicked += StartGame;
            if (_uitkContinueButton != null)
                _uitkContinueButton.clicked += Advance;
            if (_uitkSkipButton != null)
                _uitkSkipButton.clicked += SkipConversation;
        }

        void UnbindUIToolkitButtons()
        {
            UnbindStoryInteractions();
            UnbindSoundButton();
            UnbindFinale();
            UnbindClueButton();
            if (_uitkStartButton != null)
                _uitkStartButton.clicked -= StartGame;
            if (_uitkCreditsButton != null)
                _uitkCreditsButton.clicked -= OpenCredits;
            if (_uitkResumeButton != null)
                _uitkResumeButton.clicked -= ResumeGame;
            if (_uitkRestartButton != null)
                _uitkRestartButton.clicked -= StartGame;
            if (_uitkContinueButton != null)
                _uitkContinueButton.clicked -= Advance;
            if (_uitkSkipButton != null)
                _uitkSkipButton.clicked -= SkipConversation;
            _uitkStartButton = null;
            _uitkCreditsButton = null;
            _uitkResumeButton = null;
            _uitkRestartButton = null;
            _uitkContinueButton = null;
            _uitkSkipButton = null;
        }

        void UnsubscribeDialogueEvents()
        {
            if (_canvasDialogue != null)
            {
                _canvasDialogue.OnMarkerReached -= HandleDialogueMarker;
                _canvasDialogue.OnReactionAudioCue -= HandleCanvasDialogueVoice;
            }
            if (_uitkDialogue != null)
            {
                _uitkDialogue.OnMarkerReached -= HandleDialogueMarker;
                _uitkDialogue.OnReactionAudioCue -= HandleUIToolkitDialogueVoice;
            }
            _uitkBindings.Clear();
        }

        float GetDeltaTime()
        {
            if (Application.isPlaying)
                return Time.deltaTime;
            float now = Time.realtimeSinceStartup;
            float deltaTime = _lastRealtime > 0f ? now - _lastRealtime : 0.016f;
            _lastRealtime = now;
            return Mathf.Clamp(deltaTime, 0f, 0.05f);
        }

        void HandleKeyboardInput()
        {
            TextStudioWhisperwoodKeyboardState keyboard = TextStudioWhisperwoodInput.ReadKeyboard();

            if (keyboard.CancelPressed)
            {
                if (_mode == DemoMode.Menu)
                    return;
                if (_mode == DemoMode.Pause || _mode == DemoMode.Journal)
                    ResumeGame();
                else if (_mode == DemoMode.Credits)
                    ShowMenu();
                else
                    ShowPauseMenu();
                return;
            }

            // Enter submits focused buttons; Space advances the story in both renderers.
            bool keyboardAdvance = (keyboard.EnterPressed && !HasFocusedButton()) || keyboard.SpacePressed;
            if (keyboardAdvance) Advance();
            if (keyboard.JournalPressed)
            {
                if (_mode == DemoMode.Journal) ResumeGame();
                else OpenJournal();
            }
            if (_mode == DemoMode.Exploring && keyboard.InteractPressed)
                Interact();
        }

        void UpdateMovement(float deltaTime)
        {
            Vector2 direction = TextStudioWhisperwoodInput.ReadKeyboard().Movement;
            if (direction.sqrMagnitude < 0.001f)
                return;

            _playerPosition += direction.normalized * PlayerMoveSpeed * deltaTime;
            _playerPosition.x = Mathf.Clamp(_playerPosition.x, 0.09f, 0.91f);
            _playerPosition.y = Mathf.Clamp(_playerPosition.y, 0.12f, 0.88f);
            UpdatePlayerView();
            UpdateProximity();
        }

        void ShowMenu()
        {
            ResumeDialogueFromOverlay();
            StopCluePlayback();
            SetFinaleVisible(false);
            _mode = DemoMode.Menu;
            SetMenuVisible(true);
            SetCreditsVisible(false);
            SetDialogueVisible(false);
            SetHudVisible(false);
            SetTitleVisible(true);
            SetWorldOverlaysVisible(false);
            SetTitle(TitleMarkup);
            SetMenuBody("The Moon Gate has fallen quiet.\nAsk Mara, Luma and Rowan for the words that will wake it.");
            SetPrompt(StartPromptMarkup);
            SetButtonVisible(_canvasResumeButton, _uitkResumeButton, false);
            SetButtonVisible(_canvasRestartButton, _uitkRestartButton, false);
            SetButtonVisible(_canvasStartButton, _uitkStartButton, true);
            SetButtonVisible(_canvasCreditsButton, _uitkCreditsButton, true);
            ApplyMenuButtonLayout(false);
        }

        void ShowPauseMenu()
        {
            _modeBeforePause = _mode;
            PauseDialogueForOverlay();
            SetFinaleVisible(false);
            _mode = DemoMode.Pause;
            SetMenuVisible(true);
            SetDialogueVisible(false);
            SetHudVisible(false);
            SetTitleVisible(true);
            SetWorldOverlaysVisible(false);
            SetTitle("Paused");
            SetMenuBody("Whisperwood waits quietly.");
            SetPrompt("<pulse>Resume, restart, or view credits.</pulse>");
            SetButtonVisible(_canvasStartButton, _uitkStartButton, false);
            SetButtonVisible(_canvasResumeButton, _uitkResumeButton, true);
            SetButtonVisible(_canvasRestartButton, _uitkRestartButton, true);
            SetButtonVisible(_canvasCreditsButton, _uitkCreditsButton, true);
            ApplyMenuButtonLayout(true);
        }

        void ShowCredits()
        {
            StopCluePlayback();
            SetFinaleVisible(false);
            _mode = DemoMode.Credits;
            SetMenuVisible(false);
            SetDialogueVisible(false);
            SetHudVisible(false);
            SetCreditsVisible(true);
            SetTitleVisible(false);
            SetWorldOverlaysVisible(false);
            SetPrompt("<pulse>Space / click returns to the menu.</pulse>");
            SetCredits(
                "<b>Whisperwood Letters</b>\n\n" +
                "A small adventure by\n<pop>Wetzold Studios</pop>\n\n" +
                "Made with Text Studio\nfor Canvas and UI Toolkit.\n\n" +
                "Thanks for taking the moonlit path.");
        }

        void ResetRun()
        {
            ResumeDialogueFromOverlay();
            StopCluePlayback();
            _playerPosition = new Vector2(0.5f, 0.28f);
            _collectedWordCount = 0;
            _gateOpen = false;
            _finaleCompleted = false;
            _nearGate = false;
            _nearNpcIndex = -1;
            _activeNpcIndex = -1;
            _dialogueNpcIndex = -1;
            _dialogueLineIndex = 0;
            _dialogueCompletion = DialogueCompletion.None;
            for (int i = 0; i < _collectedWords.Length; i++)
                _collectedWords[i] = false;
            UpdateLanterns();
            UpdateGateVisual();
        }

        void Interact()
        {
            UpdateProximity();
            if (_nearNpcIndex >= 0)
                TalkToNpc(_nearNpcIndex);
            else if (_nearGate)
                StartGateDialogue();
        }

        void StartNpcDialogue(int npcIndex)
        {
            TextStudioWhisperwoodNpc npc = NpcAt(npcIndex);
            SetSpeakerVoice(npcIndex);
            _mode = DemoMode.Dialogue;
            _activeNpcIndex = npcIndex;
            _dialogueNpcIndex = npcIndex;
            _dialogueLineIndex = 0;
            _dialogueCompletion = _collectedWords[npcIndex] ? DialogueCompletion.None : DialogueCompletion.NpcReward;
            _activeDialogueLines = _collectedWords[npcIndex]
                ? new[] { "<typewriter>" + npc.RepeatLine + "</typewriter><waitForInput>" }
                : CopyLines(npc.Lines);
            SetDialogueVisible(true);
            SetHudVisible(true);
            SetWorldOverlaysVisible(true);
            SetSpeaker("<pop>" + npc.Name + " the " + npc.Role + "</pop>");
            ShowDialogueLine(_activeDialogueLines[0]);
            SetPrompt("<pulse>Space / click continues. Skip Conversation closes this talk.</pulse>");
        }

        void StartGateDialogue()
        {
            _mode = DemoMode.Dialogue;
            _activeNpcIndex = -1;
            _dialogueNpcIndex = -1;
            _dialogueLineIndex = 0;
            if (_gateOpen)
            {
                _dialogueCompletion = DialogueCompletion.GateFinale;
                _activeDialogueLines = new[]
                {
                    "<typewriter>The three words rise into the moonstone.</typewriter><wait=0.25>",
                    "<shimmer><gradientFlow>The path opens.</gradientFlow></shimmer> <fade>Whisperwood remembers your letters.</fade><waitForInput>"
                };
            }
            else
            {
                _dialogueCompletion = DialogueCompletion.GateBlocked;
                _activeDialogueLines = new[]
                {
                    "<typewriter>The Moon Gate is quiet.</typewriter> It needs three village words before it opens.<waitForInput>"
                };
            }
            SetSpeaker("<pop>Moon Gate</pop>");
            SetDialogueVisible(true);
            SetHudVisible(true);
            SetWorldOverlaysVisible(true);
            ShowDialogueLine(_activeDialogueLines[0]);
            SetPrompt("<pulse>Space / click continues.</pulse>");
        }

        void AdvanceDialogueSequence()
        {
            if (!AdvanceDialogue())
                return;
            _dialogueLineIndex++;
            if (_dialogueLineIndex >= _activeDialogueLines.Length)
            {
                CompleteDialogue();
                return;
            }
            ShowDialogueLine(_activeDialogueLines[_dialogueLineIndex]);
        }

        void CompleteDialogue(bool skipped = false)
        {
            DialogueCompletion completion = _dialogueCompletion;
            if (skipped && completion == DialogueCompletion.NpcReward && _dialogueNpcIndex >= 0)
                CollectWord(_dialogueNpcIndex);
            _dialogueCompletion = DialogueCompletion.None;
            _activeDialogueLines = Array.Empty<string>();
            _dialogueLineIndex = 0;
            _dialogueNpcIndex = -1;
            SetDialogueVisible(false);
            if (completion == DialogueCompletion.GateFinale)
            {
                BeginGateFinale();
                return;
            }
            _mode = DemoMode.Exploring;
            SetHudVisible(true);
            UpdateProximity();
        }

        bool AdvanceDialogue()
        {
            if (_backend == Backend.Canvas)
                return _canvasDialogueSource == null || _canvasDialogueSource.AdvanceOrSkip();
            return _uitkDialogue == null || _uitkDialogue.AdvanceOrSkip();
        }

        void ShowDialogueLine(string markup)
        {
            if (_backend == Backend.Canvas)
                _canvasDialogueSource?.ShowLine(markup);
            else
                _uitkDialogue?.ShowText(markup);
        }

        void CollectWord(int npcIndex)
        {
            if (_collectedWords[npcIndex])
                return;
            _collectedWords[npcIndex] = true;
            _collectedWordCount++;
            _gateOpen = _collectedWordCount == NpcCount;
            UpdateQuestText(true);
            UpdateLanterns();
            UpdateGateVisual();
            TextStudioWhisperwoodNpc npc = NpcAt(npcIndex);
            SetPrompt(_gateOpen
                ? "<pop><colorPulse>The Moon Gate is open.</colorPulse></pop>"
                : "<pop>Word found: <colorPulse>" + npc.Word + "</colorPulse></pop>");
        }

        void UpdateProximity()
        {
            if (_mode != DemoMode.Exploring)
                return;
            _nearNpcIndex = -1;
            float bestNpcDistance = NpcInteractDistance;
            for (int i = 0; i < NpcCount; i++)
            {
                float distance = Vector2.Distance(_playerPosition, NpcAt(i).Position);
                if (distance < bestNpcDistance)
                {
                    bestNpcDistance = distance;
                    _nearNpcIndex = i;
                }
            }
            _nearGate = Vector2.Distance(_playerPosition, GatePosition) <= GateInteractDistance;
            if (_nearNpcIndex >= 0)
            {
                TextStudioWhisperwoodNpc npc = NpcAt(_nearNpcIndex);
                SetNearby("<pop>E: Talk to " + npc.Name + "</pop>");
                SetPrompt(_collectedWords[_nearNpcIndex]
                    ? "<pulse>" + npc.Name + " has already shared " + npc.Word + ".</pulse>"
                    : "<pulse>Press E to talk with " + npc.Name + ".</pulse>");
            }
            else if (_nearGate)
            {
                SetNearby(_gateOpen ? "<pop>E: Open Moon Gate</pop>" : "<pop>E: Inspect Moon Gate</pop>");
                SetPrompt(_gateOpen
                    ? "<pulse>Press E to open the Moon Gate.</pulse>"
                    : "<pulse>The gate needs all three words.</pulse>");
            }
            else
            {
                SetNearby(string.Empty);
                SetPrompt("<pulse>Move with WASD or arrows. Press E near villagers or the Moon Gate.</pulse>");
            }
        }

        void UpdatePlayerView()
        {
            SetCanvasMarkerPosition(_canvasPlayerMarker, _playerPosition);
            SetUITKMarkerPosition(_uitkPlayerMarker, _playerPosition, 28f, 28f);
        }

        void UpdateGateVisual()
        {
            Color gateColor = _gateOpen
                ? new Color(0.68f, 0.9f, 1f, 0.92f)
                : new Color(0.42f, 0.48f, 0.58f, 0.82f);
            if (_canvasGateImage != null)
                _canvasGateImage.color = gateColor;
            if (_uitkGateMarker != null)
                _uitkGateMarker.style.backgroundColor = gateColor;
            SetGateSign(_gateOpen ? "<shimmer><gradientFlow>Moon Gate Open</gradientFlow></shimmer>" : "Moon Gate");
        }

        void UpdateQuestText(bool animate)
        {
            StringBuilder builder = new StringBuilder(96);
            builder.Append("<pop>Words found ").Append(_collectedWordCount).Append('/').Append(NpcCount).Append("</pop>\n");
            for (int i = 0; i < NpcCount; i++)
            {
                if (i > 0)
                    builder.Append("  ");
                string word = NpcAt(i).Word;
                if (_collectedWords[i])
                    builder.Append("<colorPulse>").Append(word).Append("</colorPulse>");
                else
                    builder.Append(word.ToLowerInvariant());
            }
            string markup = builder.ToString();
            if (animate)
                ShowOnce(_canvasQuest, _uitkQuest, markup);
            else
                ShowInstant(_canvasQuest, _uitkQuest, markup);
        }

        void HandleDialogueMarker(string markerId, string[] parameters)
        {
            if (markerId != TextStudioBuiltInActionIds.Event || parameters == null || parameters.Length < 2)
                return;
            if (!string.Equals(parameters[0], "word", StringComparison.OrdinalIgnoreCase) ||
                _mode != DemoMode.Dialogue || _dialogueNpcIndex < 0) return;
            if (!string.Equals(NpcAt(_dialogueNpcIndex).Word, parameters[1], StringComparison.OrdinalIgnoreCase)) return;
            CollectWord(_dialogueNpcIndex);
            SetNearby(NpcAt(_dialogueNpcIndex).Name + "'s lantern is lit.");
        }

        void UpdateUITKLabels(float deltaTime)
        {
            if (_backend != Backend.UIToolkit)
                return;
            for (int i = 0; i < _uitkBindings.Count; i++)
                _uitkBindings[i]?.UpdateFromHost(deltaTime);
        }

        void SetTitle(string markup) => ShowInstant(_canvasTitle, _uitkTitle, markup);
        void SetMenuBody(string markup) => ShowInstant(_canvasMenuBody, _uitkMenuBody, markup);
        void SetPrompt(string markup) => ShowInstant(_canvasPrompt, _uitkPrompt, markup);
        void SetNearby(string markup) => ShowInstant(_canvasNearby, _uitkNearby, markup);
        void SetGateSign(string markup) => ShowInstant(_canvasGateSign, _uitkGateSign, markup);
        void SetSpeaker(string markup) => ShowInstant(_canvasSpeaker, _uitkSpeaker, markup);
        void SetCredits(string markup) => ShowOnce(_canvasCredits, _uitkCredits, markup);

        void ShowOnce(TextStudioText canvasText, TextStudioBinding uitkText, string markup)
        {
            if (_backend == Backend.Canvas)
                canvasText?.ShowText(markup);
            else
                uitkText?.ShowText(markup);
        }

        void ShowInstant(TextStudioText canvasText, TextStudioBinding uitkText, string markup)
        {
            if (_backend == Backend.Canvas)
                canvasText?.ShowTextInstant(markup);
            else
                uitkText?.ShowTextInstant(markup);
        }

        void SetMenuVisible(bool visible)
        {
            if (_backend == Backend.Canvas)
            {
                SetCanvasVisible(_canvasMenuPanel, visible);
                SetCanvasVisible(_canvasMenuButtonRow, visible);
                SetButtonVisible(_canvasStartButton, null, visible && _mode == DemoMode.Menu);
                SetButtonVisible(_canvasResumeButton, null, visible && _mode == DemoMode.Pause);
                SetButtonVisible(_canvasRestartButton, null, visible && _mode == DemoMode.Pause);
                SetButtonVisible(_canvasCreditsButton, null, visible);
                if (_canvasMenuBody != null)
                    _canvasMenuBody.gameObject.SetActive(visible);
            }
            else
            {
                SetDisplay(_uitkMenuPanel, visible);
                SetDisplay(_uitkMenuButtonRow, visible);
                SetDisplay(_uitkStartButton, visible && _mode == DemoMode.Menu);
                SetDisplay(_uitkResumeButton, visible && _mode == DemoMode.Pause);
                SetDisplay(_uitkRestartButton, visible && _mode == DemoMode.Pause);
                SetDisplay(_uitkCreditsButton, visible);
            }
        }

        void SetTitleVisible(bool visible)
        {
            if (_backend == Backend.Canvas)
            {
                if (_canvasTitle != null)
                    _canvasTitle.gameObject.SetActive(visible);
            }
            else
            {
                SetDisplay(_uitkTitle, visible);
            }
        }

        void SetHudVisible(bool visible)
        {
            SetButtonVisible(_canvasSoundButton, _uitkSoundButton, visible);
            SetButtonVisible(_canvasJournalButton, _uitkJournalButton, visible);
            bool promptVisible = _mode == DemoMode.Exploring || _mode == DemoMode.Menu ||
                                 _mode == DemoMode.Pause || _mode == DemoMode.Credits || _mode == DemoMode.Finale || _mode == DemoMode.Clue;
            if (_backend == Backend.Canvas)
            {
                SetTextVisible(_canvasHud, visible);
                SetTextVisible(_canvasQuest, visible);
                SetTextVisible(_canvasNearby, visible);
                SetTextVisible(_canvasPrompt, promptVisible);
            }
            else
            {
                SetDisplay(_uitkHud, visible);
                SetDisplay(_uitkQuest, visible);
                SetDisplay(_uitkNearby, visible);
                SetDisplay(_uitkPrompt, promptVisible);
            }
        }

        void ApplyMenuButtonLayout(bool pauseLayout)
        {
            if (_backend == Backend.Canvas)
            {
                if (pauseLayout)
                {
                    SetButtonAnchors(_canvasResumeButton, 0.03f, 0.28f);
                    SetButtonAnchors(_canvasRestartButton, 0.375f, 0.625f);
                    SetButtonAnchors(_canvasCreditsButton, 0.72f, 0.97f);
                }
                else
                {
                    SetButtonAnchors(_canvasStartButton, 0.14f, 0.42f);
                    SetButtonAnchors(_canvasCreditsButton, 0.58f, 0.86f);
                }
            }
            else if (pauseLayout)
            {
                SetUITKButtonLayout(_uitkResumeButton, 3f, 25f);
                SetUITKButtonLayout(_uitkRestartButton, 37.5f, 25f);
                SetUITKButtonLayout(_uitkCreditsButton, 72f, 25f);
            }
            else
            {
                SetUITKButtonLayout(_uitkStartButton, 14f, 28f);
                SetUITKButtonLayout(_uitkCreditsButton, 58f, 28f);
            }
        }

        void SetWorldOverlaysVisible(bool visible)
        {
            foreach (UIImage lantern in _canvasLanterns)
                if (lantern != null) lantern.transform.parent.gameObject.SetActive(visible);
            foreach (VisualElement lantern in _uitkLanterns) SetDisplay(lantern, visible);
            if (_backend == Backend.Canvas)
            {
                SetCanvasVisible(_canvasPlayerMarker, visible);
                SetCanvasVisible(_canvasGateMarker, visible);
                SetTextVisible(_canvasGateSign, visible);
                for (int i = 0; i < _canvasNpcMarkers.Length; i++)
                    SetCanvasVisible(_canvasNpcMarkers[i], visible);
            }
            else
            {
                SetDisplay(_uitkPlayerMarker, visible);
                SetDisplay(_uitkGateMarker, visible);
                SetDisplay(_uitkGateSign, visible);
                for (int i = 0; i < _uitkNpcMarkers.Length; i++)
                    SetDisplay(_uitkNpcMarkers[i], visible);
            }
        }

        void SetDialogueVisible(bool visible)
        {
            if (_backend == Backend.Canvas)
            {
                SetCanvasVisible(_canvasDialoguePanel, visible);
                SetButtonVisible(_canvasContinueButton, null, visible);
                SetButtonVisible(_canvasSkipButton, null, visible);
            }
            else
            {
                SetDisplay(_uitkDialoguePanel, visible);
                SetDisplay(_uitkContinueButton, visible);
                SetDisplay(_uitkSkipButton, visible);
            }
            SetChoiceVisible(visible && _mode == DemoMode.Choice);
        }

        void SetCreditsVisible(bool visible)
        {
            SetButtonVisible(_canvasJournalBackButton, _uitkJournalBackButton, visible && _mode == DemoMode.Journal);
            if (_backend == Backend.Canvas)
                SetCanvasVisible(_canvasCreditsPanel, visible);
            else
                SetDisplay(_uitkCreditsPanel, visible);
        }

        void SetButtonVisible(UIButton canvasButton, UITKButton uitkButton, bool visible)
        {
            if (_backend == Backend.Canvas)
            {
                if (canvasButton != null)
                    canvasButton.gameObject.SetActive(visible);
            }
            else
            {
                SetDisplay(uitkButton, visible);
            }
        }

        int NpcCount => _story != null ? _story.Npcs.Count : 0;

        TextStudioWhisperwoodNpc NpcAt(int index)
        {
            return _story.Npcs[index];
        }

        static string[] CopyLines(IReadOnlyList<string> lines)
        {
            string[] copy = new string[lines.Count];
            for (int i = 0; i < lines.Count; i++)
                copy[i] = lines[i];
            return copy;
        }

        static void SetCanvasMarkerPosition(RectTransform marker, Vector2 normalizedPosition)
        {
            if (marker == null)
                return;
            marker.anchorMin = normalizedPosition;
            marker.anchorMax = normalizedPosition;
            marker.anchoredPosition = Vector2.zero;
        }

        static void SetUITKMarkerPosition(VisualElement marker, Vector2 normalizedPosition, float width, float height)
        {
            if (marker == null)
                return;
            marker.style.left = Length.Percent(normalizedPosition.x * 100f);
            marker.style.top = Length.Percent((1f - normalizedPosition.y) * 100f);
            marker.style.marginLeft = -width * 0.5f;
            marker.style.marginTop = -height * 0.5f;
        }

        static void SetDisplay(VisualElement element, bool visible)
        {
            if (element != null)
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static void SetDisplay(TextStudioBinding binding, bool visible)
        {
            SetDisplay(binding?.Element, visible);
        }

        static void SetUITKButtonLayout(UITKButton button, float leftPercent, float widthPercent)
        {
            if (button == null)
                return;
            button.style.left = Length.Percent(leftPercent);
            button.style.width = Length.Percent(widthPercent);
        }

        static void SetButtonAnchors(UIButton button, float minX, float maxX)
        {
            if (button == null)
                return;
            RectTransform rect = button.transform as RectTransform;
            if (rect == null)
                return;
            rect.anchorMin = new Vector2(minX, 0.12f);
            rect.anchorMax = new Vector2(maxX, 0.88f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void SetCanvasVisible(RectTransform rect, bool visible)
        {
            if (rect != null)
                rect.gameObject.SetActive(visible);
        }

        static void SetTextVisible(TextStudioText text, bool visible)
        {
            if (text != null)
                text.gameObject.SetActive(visible);
        }
    }
}
