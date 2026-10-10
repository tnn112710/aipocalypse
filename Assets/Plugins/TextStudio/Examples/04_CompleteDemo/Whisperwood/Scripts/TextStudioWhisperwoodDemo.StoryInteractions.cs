using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using UIButton = UnityEngine.UI.Button;
using UIImage = UnityEngine.UI.Image;
using UITKButton = UnityEngine.UIElements.Button;

namespace TextStudio.Examples.Whisperwood
{
    public sealed partial class TextStudioWhisperwoodDemo
    {
        [Header("Story Interactions")]
        [SerializeField] UIButton _canvasAskButton;
        [SerializeField] UIButton _canvasLeaveButton;
        [SerializeField] UIButton _canvasJournalButton;
        [SerializeField] UIButton _canvasJournalBackButton;
        [SerializeField] UIImage[] _canvasLanterns = Array.Empty<UIImage>();
        UITKButton _uitkAskButton, _uitkLeaveButton, _uitkJournalButton, _uitkJournalBackButton;
        VisualElement[] _uitkLanterns = Array.Empty<VisualElement>();
        bool _dialogueOverlayPauseOwned;
        VisualElement _uitkMapViewport;

        public int CollectedWordCount => _collectedWordCount;
        public bool GateIsOpen => _gateOpen;
        public bool IsChoosing => _mode == DemoMode.Choice;
        public bool IsJournalOpen => _mode == DemoMode.Journal;
        public bool HasWord(int npcIndex) => npcIndex >= 0 && npcIndex < _collectedWords.Length && _collectedWords[npcIndex];

        public void ConfigureStoryInteractions(UIButton ask, UIButton leave, UIButton journal,
            UIButton journalBack, UIImage[] lanterns)
        {
            _canvasAskButton = ask;
            _canvasLeaveButton = leave;
            _canvasJournalButton = journal;
            _canvasJournalBackButton = journalBack;
            _canvasLanterns = lanterns ?? Array.Empty<UIImage>();
            UpdateLanterns();
        }

        public void TalkToNpc(int npcIndex)
        {
            if (_mode != DemoMode.Exploring || npcIndex < 0 || npcIndex >= NpcCount) return;
            TextStudioWhisperwoodNpc npc = NpcAt(npcIndex);
            SetSpeakerVoice(npcIndex);
            _activeNpcIndex = npcIndex;
            _dialogueNpcIndex = npcIndex;
            _dialogueCompletion = DialogueCompletion.None;
            _mode = DemoMode.Choice;
            SetSpeaker(npc.Name);
            ShowDialogueLine(HasWord(npcIndex) ? npc.RepeatLine : npc.Greeting);
            SetDialogueVisible(true);
            SetChoiceVisible(true);
            SetChoiceLabel(HasWord(npcIndex) ? "Talk again" : npc.Question);
            // Like the other dialogue modes, choices hide the bottom prompt, which would sit on the dialogue frame.
            SetHudVisible(true);
        }

        public void AskForWord()
        {
            if (_mode != DemoMode.Choice || _dialogueNpcIndex < 0) return;
            StartNpcDialogue(_dialogueNpcIndex);
        }

        public void LeaveConversation()
        {
            if (_mode != DemoMode.Choice) return;
            _dialogueNpcIndex = -1;
            _activeNpcIndex = -1;
            _mode = DemoMode.Exploring;
            SetDialogueVisible(false);
            SetHudVisible(true);
            UpdateProximity();
        }

        public void OpenJournal()
        {
            if (_mode != DemoMode.Exploring && _mode != DemoMode.Dialogue && _mode != DemoMode.Choice && _mode != DemoMode.Finale && _mode != DemoMode.Clue) return;
            _modeBeforePause = _mode;
            PauseDialogueForOverlay();
            SetFinaleVisible(false);
            _mode = DemoMode.Journal;
            SetMenuVisible(false);
            SetDialogueVisible(false);
            SetHudVisible(false);
            SetWorldOverlaysVisible(false);
            SetTitleVisible(true);
            SetTitle("Journal");
            SetCreditsVisible(true);
            SetButtonVisible(_canvasJournalBackButton, _uitkJournalBackButton, true);
            StringBuilder text = new StringBuilder("Words for the Moon Gate\n\n");
            for (int i = 0; i < NpcCount; i++)
            {
                TextStudioWhisperwoodNpc npc = NpcAt(i);
                text.Append(npc.Name).Append(HasWord(i) ? " shared " + npc.Word + "." : " may know a word.").Append('\n');
            }
            text.Append(_gateOpen ? "\nAll three lanterns are lit. The gate is ready." : "\nEach word lights a lantern. Find all three before you visit the gate.");
            ShowInstant(_canvasCredits, _uitkCredits, text.ToString());
            SetPrompt("Back, J or Escape returns to your place.");
        }

        void PauseDialogueForOverlay()
        {
            PauseClueForOverlay();
            PauseInscription();
            if (_dialogueOverlayPauseOwned) return;
            _dialogueOverlayPauseOwned = _backend == Backend.Canvas
                ? _canvasDialogue != null && _canvasDialogue.IsRevealing
                : _uitkDialogue != null && _uitkDialogue.IsRevealing;
            if (!_dialogueOverlayPauseOwned) return;
            if (_backend == Backend.Canvas) _canvasDialogue.PauseReveal();
            else _uitkDialogue.PauseReveal();
        }

        void ResumeDialogueFromOverlay()
        {
            ResumeInscription();
            if (!_dialogueOverlayPauseOwned) return;
            _dialogueOverlayPauseOwned = false;
            if (_backend == Backend.Canvas) _canvasDialogue?.ResumeReveal();
            else _uitkDialogue?.ResumeReveal();
        }

        void SetChoiceVisible(bool visible)
        {
            SetClueButtonVisible(visible);
            SetButtonVisible(_canvasAskButton, _uitkAskButton, visible);
            SetButtonVisible(_canvasLeaveButton, _uitkLeaveButton, visible);
            SetButtonVisible(_canvasContinueButton, _uitkContinueButton, !visible && _mode == DemoMode.Dialogue);
            SetButtonVisible(_canvasSkipButton, _uitkSkipButton, !visible && _mode == DemoMode.Dialogue);
        }

        void SetChoiceLabel(string value)
        {
            if (_canvasAskButton != null) _canvasAskButton.GetComponentInChildren<TextStudioText>(true)?.ShowTextInstant(value);
            if (_uitkAskButton != null) _uitkAskButton.text = value;
        }

        void UpdateLanterns()
        {
            for (int i = 0; i < NpcCount; i++)
            {
                Color color = HasWord(i) ? new Color(1f, .72f, .24f) : new Color(.2f, .16f, .1f);
                if (i < _canvasLanterns.Length && _canvasLanterns[i] != null) _canvasLanterns[i].color = color;
                if (i < _uitkLanterns.Length && _uitkLanterns[i] != null) _uitkLanterns[i].style.backgroundColor = color;
            }
        }

        void BindStoryInteractions()
        {
            _uitkMapViewport = _uitkRoot.Q<VisualElement>("map-viewport");
            _uitkMapViewport?.RegisterCallback<GeometryChangedEvent>(FitMap);
            FitMap(null);
            _uitkAskButton = _uitkRoot.Q<UITKButton>("ask-button");
            _uitkLeaveButton = _uitkRoot.Q<UITKButton>("leave-button");
            _uitkJournalButton = _uitkRoot.Q<UITKButton>("journal-button");
            _uitkJournalBackButton = _uitkRoot.Q<UITKButton>("journal-back-button");
            if (_uitkAskButton != null) _uitkAskButton.clicked += AskForWord;
            if (_uitkLeaveButton != null) _uitkLeaveButton.clicked += LeaveConversation;
            if (_uitkJournalButton != null) _uitkJournalButton.clicked += OpenJournal;
            if (_uitkJournalBackButton != null) _uitkJournalBackButton.clicked += ResumeGame;
            _uitkDialoguePanel?.RegisterCallback<ClickEvent>(HandleDialogueClick);
            _uitkCreditsPanel?.RegisterCallback<ClickEvent>(HandleDialogueClick);
            _uitkLanterns = new VisualElement[NpcCount];
            for (int i = 0; i < NpcCount; i++)
            {
                _uitkLanterns[i] = _uitkRoot.Q<VisualElement>("lantern-" + i);
                SetUITKMarkerPosition(_uitkLanterns[i], NpcAt(i).Position + new Vector2(-.04f, .025f), 24f, 34f);
            }
            UpdateLanterns();
        }

        void FitMap(GeometryChangedEvent evt)
        {
            if (_uitkMapViewport == null || _uitkWorld == null) return;
            Rect available = _uitkMapViewport.contentRect;
            if (available.width <= 0 || available.height <= 0) return;
            Texture2D map = _uitkWorld.resolvedStyle.backgroundImage.texture;
            float aspect = map != null ? (float)map.width / map.height : 16f / 9f;
            float width = Mathf.Min(available.width, available.height * aspect);
            _uitkWorld.style.width = width;
            _uitkWorld.style.height = width / aspect;
        }

        void UnbindStoryInteractions()
        {
            _uitkMapViewport?.UnregisterCallback<GeometryChangedEvent>(FitMap);
            _uitkMapViewport = null;
            if (_uitkAskButton != null) _uitkAskButton.clicked -= AskForWord;
            if (_uitkLeaveButton != null) _uitkLeaveButton.clicked -= LeaveConversation;
            if (_uitkJournalButton != null) _uitkJournalButton.clicked -= OpenJournal;
            if (_uitkJournalBackButton != null) _uitkJournalBackButton.clicked -= ResumeGame;
            _uitkDialoguePanel?.UnregisterCallback<ClickEvent>(HandleDialogueClick);
            _uitkCreditsPanel?.UnregisterCallback<ClickEvent>(HandleDialogueClick);
            _uitkAskButton = _uitkLeaveButton = _uitkJournalButton = _uitkJournalBackButton = null;
        }

        void HandleDialogueClick(ClickEvent evt)
        {
            VisualElement target = evt.target as VisualElement;
            if (evt.button != 0 || target is UITKButton || target?.GetFirstAncestorOfType<UITKButton>() != null) return;
            if (_mode == DemoMode.Dialogue || _mode == DemoMode.Credits) Advance();
        }

        bool HasFocusedButton()
        {
            if (_backend == Backend.Canvas)
            {
                GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                return selected != null && selected.activeInHierarchy && selected.GetComponent<UIButton>() != null;
            }
            return _uitkRoot?.focusController?.focusedElement is UITKButton;
        }
    }
}
