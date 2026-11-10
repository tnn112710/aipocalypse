using MB13.Aipocalypse.Core.SaveSystem;
using MB13.Aipocalypse.Input;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using VContainer;

namespace MB13.Aipocalypse.Core.UI
{
    // The main menu scene: Continue (the latest save), Prototype (straight into the world scene, no slot to pick), Load Game
    // (the slot list), Settings (later), Quit. Holds the UI input while the scene is open.
    public sealed class MainMenuScreen : MonoBehaviour
    {
        const float DisabledLabelAlpha = 0.35f;

        [SerializeField, Tooltip("EventSystem + InputSystemUIInputModule")] GameObject _eventSystem;
        [SerializeField, Tooltip("The buttons page")] GameObject _page;
        [SerializeField] Button _continue;
        [SerializeField, Tooltip("Under Continue: which save it loads")] TMP_Text _continueInfo;
        [SerializeField] Button _prototype;
        [SerializeField] Button _load;
        [SerializeField] Button _settings;
        [SerializeField] Button _quit;
        [SerializeField] TMP_Text _status;
        [SerializeField, Tooltip("Bottom corner: the build")] TMP_Text _version;
        [SerializeField] SaveSlotsPanel _slots;
        [SerializeField] ConfirmDialog _confirm;

        InputService _input;
        GameSession _session;
        SaveService _saves;
        int _latest;

        [Inject]
        void Construct(InputService input, GameSession session, SaveService saves)
        {
            _input = input;
            _session = session;
            _saves = saves;
            _slots.Bind(session, saves);
            if (_eventSystem != null && _eventSystem.TryGetComponent(out InputSystemUIInputModule module))
                _input.ConfigureUiModule(module);
            _input.RequestUI(this);
        }

        void Awake()
        {
            _continue.onClick.AddListener(Continue);
            _prototype.onClick.AddListener(Prototype);
            _load.onClick.AddListener(OpenSlots);
            SetInteractable(_settings, false);
            _quit.onClick.AddListener(() => _confirm.Ask("Quit the game?", "Quit", () => _session.Quit()));
            _slots.Closed += ShowPage;
            _slots.gameObject.SetActive(false);
            _confirm.Close();
            if (_version != null)
                _version.text = $"Prototype build {Application.version}";
        }

        void Start() => ShowPage();

        void OnDestroy()
        {
            _input?.ReleaseUI(this);
            if (_slots != null)
                _slots.Closed -= ShowPage;
        }

        void Update()
        {
            if (_input == null || _session.IsLoading || !_input.UiCancelPressed)
                return;
            if (_confirm.IsOpen)
                _confirm.Close();
            else if (_slots.IsOpen)
                _slots.Cancel();
        }

        void ShowPage()
        {
            _page.SetActive(true);
            _latest = _saves != null ? _saves.LatestSlot() : 0;
            SetInteractable(_continue, _latest > 0);
            if (_continueInfo != null)
                _continueInfo.text = _latest > 0 ? $"{SaveFormat.SlotName(_latest)}  ·  {SaveFormat.Summary(_saves.Info(_latest))}" : "No saves yet";
            EventSystem.current?.SetSelectedGameObject(_latest > 0 ? _continue.gameObject : _prototype.gameObject);
        }

        void Continue()
        {
            if (_latest > 0 && !_session.Load(_latest, out string message))
                SetStatus(message);
        }

        void Prototype()
        {
            if (!_session.StartPrototype(out string message))
                SetStatus(message);
        }

        void OpenSlots()
        {
            _page.SetActive(false);
            _slots.Open(SlotsMode.Load);
        }

        void SetStatus(string message)
        {
            if (_status != null)
                _status.text = message;
        }

        // Text buttons: the tint only lights the highlight behind the label, so a disabled one dims its label here.
        static void SetInteractable(Button button, bool interactable)
        {
            button.interactable = interactable;
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.alpha = interactable ? 1f : DisabledLabelAlpha;
        }
    }
}
