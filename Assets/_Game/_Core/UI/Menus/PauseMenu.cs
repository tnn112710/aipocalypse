using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.SaveSystem;
using MB13.Aipocalypse.Core.TimeSystem;
using MB13.Aipocalypse.Input;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.UI;
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

        InputService _input;
        TimeService _time;
        GameSession _session;
        PlayerRegistry _players;
        Canvas _canvas;
        System.IDisposable _savedSubscription;
        bool _open;
        int _openedFrame;

        public bool IsOpen => _open;

        [Inject]
        void Construct(InputService input, TimeService time, GameSession session, SaveService saves, PlayerRegistry players,
            ISubscriber<GameSaved> saved)
        {
            _players = players;
            _input = input;
            _time = time;
            _session = session;
            _slots.Bind(session, saves);
            _savedSubscription = saved.Subscribe(_slots.ShowSaved);
            if (_eventSystem != null && _eventSystem.TryGetComponent(out InputSystemUIInputModule module))
                _input.ConfigureUiModule(module);
        }

        void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _resume.onClick.AddListener(Close);
            _save.onClick.AddListener(() => OpenSlots(SlotsMode.Save));
            _load.onClick.AddListener(() => OpenSlots(SlotsMode.Load));
            _settings.interactable = false;
            _mainMenu.onClick.AddListener(() =>
                _confirm.Ask("Return to the main menu?\nProgress since your last save will be lost.", "Main Menu", ToMainMenu));
            _saveQuit.onClick.AddListener(SaveAndQuit);
            _quit.onClick.AddListener(() =>
                _confirm.Ask("Quit the game?\nProgress since your last save will be lost.", "Quit", () => _session.Quit()));
            _slots.Closed += ShowPage;
            SetVisible(false);
        }

        // Only the requests here: on a scene unload the menu's children may already be gone.
        void OnDestroy()
        {
            _savedSubscription?.Dispose();
            if (_open)
                Release();
            _open = false;
        }

        void Update()
        {
            if (_input == null || _session == null)
                return;
            if (!_open)
            {
                // Not while down: the death screen's own click would land on this menu.
                if (_input.PausePressed && !_input.IsUIRequested && _session.InWorld && !_session.IsLoading && !PlayerDown)
                    Open();
                return;
            }
            if (_session.IsLoading || Time.frameCount == _openedFrame || !_input.UiCancelPressed)
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
            if (_open)
                return;
            _open = true;
            _openedFrame = Time.frameCount;
            _time?.RequestPause(this);
            _input.RequestUI(this);
            if (_eventSystem != null)
                _eventSystem.SetActive(true);
            SetVisible(true);
            ShowPage();
        }

        public void Close()
        {
            if (!_open)
                return;
            _open = false;
            // Released first, so the game never stays paused whatever happens to the visuals.
            Release();
            _confirm.Close();
            SetVisible(false);
        }

        bool PlayerDown => _players?.Current != null && _players.Current.IsDead;

        void Release()
        {
            if (_eventSystem != null)
                _eventSystem.SetActive(false);
            _input?.ReleaseUI(this);
            _time?.ReleasePause(this);
        }

        void ShowPage()
        {
            if (!_open)
                return;
            _page.SetActive(true);
            bool canSave = _session.CanSave(out string reason);
            _save.interactable = canSave;
            _saveQuit.interactable = canSave && _session.Slot > 0;
            if (_hint != null)
                _hint.text = !canSave ? reason
                    : _session.Slot > 0 ? $"Playing in {SaveFormat.SlotName(_session.Slot)}  ·  {SaveFormat.PlayTime(_session.PlayTime)}"
                    : $"Not saved to a slot yet  ·  {SaveFormat.PlayTime(_session.PlayTime)}";
            MenuButton.SelectSilently(_resume);
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
                _hint.text = "No main menu scene in the Build Settings";
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
