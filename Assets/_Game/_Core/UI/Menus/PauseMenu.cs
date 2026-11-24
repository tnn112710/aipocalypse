using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.SaveSystem;
using MB13.Aipocalypse.Core.TimeSystem;
using MB13.Aipocalypse.Input;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using VContainer;

namespace MB13.Aipocalypse.Core.UI
{
    // Esc in play: pauses the game (also during a raid) and offers Resume, Save, Load, Settings (later), Main Menu, Save & Quit
    // and Quit.
    // Its own canvas with a raycaster, and an EventSystem active only while it's open; Esc steps back (dialog, slots, menu).
    [RequireComponent(typeof(Canvas))]
    public sealed class PauseMenu : MonoBehaviour
    {
        static readonly LocalizedString ConfirmMainMenu = Loc.UI("confirm.main_menu");
        static readonly LocalizedString MainMenuAction = Loc.UI("action.main_menu");
        static readonly LocalizedString ConfirmQuit = Loc.UI("confirm.quit");
        static readonly LocalizedString QuitAction = Loc.UI("action.quit");
        static readonly LocalizedString HintSlot = Loc.UI("pause.hint_slot");
        static readonly LocalizedString HintNoSlot = Loc.UI("pause.hint_no_slot");
        static readonly LocalizedString NoMainMenu = Loc.UI("menu.no_main_menu");

        [SerializeField, Tooltip("EventSystem + InputSystemUIInputModule: active only while open")] GameObject _eventSystem;
        [SerializeField, Tooltip("The buttons page")] GameObject _page;
        [SerializeField] Button _resume;
        [SerializeField] Button _save;
        [SerializeField] Button _load;
        [SerializeField] Button _settings;
        [SerializeField] Button _mainMenu;
        [SerializeField, Tooltip("Saves to the game's slot, then quits")] Button _saveQuit;
        [SerializeField] Button _quit;
        [SerializeField, Tooltip("Why saving is off right now (a raid, down)")] TMP_Text _hint;
        [SerializeField] SaveSlotsPanel _slots;
        [SerializeField] ConfirmDialog _confirm;
        [SerializeField] MenuSounds _sounds;

        readonly ModalScreen _modal = new();
        InputService _input;
        GameSession _session;
        PlayerRegistry _players;
        Canvas _canvas;
        System.IDisposable _savedSubscription;
        System.IDisposable _languageSubscription;

        public bool IsOpen => _modal.IsOpen;

        [Inject]
        void Construct(InputService input, TimeService time, GameSession session, SaveService saves, PlayerRegistry players,
            ISubscriber<GameSaved> saved, ISubscriber<LanguageChanged> languageChanged)
        {
            _players = players;
            _input = input;
            _session = session;
            _slots.Bind(session, saves);
            _savedSubscription = saved.Subscribe(_slots.ShowSaved);
            _languageSubscription = languageChanged.Subscribe(_ => RefreshHint());
            _modal.Bind(this, _eventSystem, input, time);
        }

        void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _resume.onClick.AddListener(Close);
            _save.onClick.AddListener(() => OpenSlots(SlotsMode.Save));
            _load.onClick.AddListener(() => OpenSlots(SlotsMode.Load));
            _settings.interactable = false;
            _mainMenu.onClick.AddListener(() =>
                _confirm.Ask(ConfirmMainMenu.Text(), MainMenuAction.Text(), ToMainMenu));
            _saveQuit.onClick.AddListener(SaveAndQuit);
            _quit.onClick.AddListener(() =>
                _confirm.Ask(ConfirmQuit.Text(), QuitAction.Text(), () => _session.Quit()));
            _slots.Closed += ShowPage;
            SetVisible(false);
        }

        // Only the requests here: on a scene unload the menu's children may already be gone.
        void OnDestroy()
        {
            _savedSubscription?.Dispose();
            _languageSubscription?.Dispose();
            _modal.Close();
        }

        void Update()
        {
            if (_input == null || _session == null)
                return;
            if (!_modal.IsOpen)
            {
                // Not while down: the death screen's own click would land on this menu.
                if (_input.PausePressed && !_input.IsUIRequested && _session.InWorld && !_session.IsLoading && !PlayerDown)
                    Open();
                return;
            }
            if (_session.IsLoading || _modal.OpenedThisFrame || !_input.UiCancelPressed)
                return;
            _sounds?.Back();
            if (_confirm.IsOpen)
                _confirm.Close();
            else if (_slots.IsOpen)
                _slots.Cancel();
            else
                Close();
        }

        public void Open()
        {
            if (!_modal.Open(pause: true))
                return;
            SetVisible(true);
            ShowPage();
        }

        public void Close()
        {
            if (!_modal.Close())
                return;
            _confirm.Close();
            SetVisible(false);
        }

        bool PlayerDown => _players?.Current != null && _players.Current.IsDead;

        void ShowPage()
        {
            if (!_modal.IsOpen)
                return;
            _page.SetActive(true);
            RefreshHint();
            MenuButton.SelectSilently(_resume);
        }

        void RefreshHint()
        {
            if (!_modal.IsOpen)
                return;
            bool canSave = _session.CanSave(out string reason);
            _save.interactable = canSave;
            _saveQuit.interactable = canSave && _session.Slot > 0;
            if (_hint != null)
                _hint.text = !canSave ? reason
                    : _session.Slot > 0 ? HintSlot.Format(SaveFormat.SlotName(_session.Slot), SaveFormat.PlayTime(_session.PlayTime))
                    : HintNoSlot.Format(SaveFormat.PlayTime(_session.PlayTime));
        }

        void OpenSlots(SlotsMode mode)
        {
            _page.SetActive(false);
            _slots.Open(mode);
        }

        void SaveAndQuit()
        {
            if (!_session.SaveAndQuit(out string message) && _hint != null)
                _hint.text = message;
        }

        void ToMainMenu()
        {
            if (!_session.ReturnToMainMenu() && _hint != null)
                _hint.text = NoMainMenu.Text();
        }

        void SetVisible(bool visible)
        {
            if (_canvas != null)
                _canvas.enabled = visible;
            _page.SetActive(visible);
            if (!visible)
                _slots.gameObject.SetActive(false);
        }
    }
}
