using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.SleepSystem;
using MB13.Aipocalypse.Core.TimeSystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using MB13.Aipocalypse.Input;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization;
using UnityEngine.UI;
using VContainer;

namespace MB13.Aipocalypse.Core.UI
{
    // Shows a sleep (SleepService; hidden in scenes without one): the wake-up clock to choose an hour (earlier / later,
    // Sleep, Cancel; Esc cancels), the eyes closing, the hours running past while asleep, and a line when the bed refuses.
    // Its own canvas with a raycaster, and an EventSystem active only while choosing.
    [RequireComponent(typeof(Canvas))]
    public sealed class SleepScreen : MonoBehaviour
    {
        static readonly LocalizedString DetailOne = Loc.Hud("sleep.detail_one");
        static readonly LocalizedString DetailMany = Loc.Hud("sleep.detail_many");
        static readonly LocalizedString Limit = Loc.Hud("sleep.limit");
        static readonly LocalizedString AsleepDay = Loc.Hud("sleep.day");

        [SerializeField, Tooltip("EventSystem + InputSystemUIInputModule: active only while choosing")] GameObject _eventSystem;
        [SerializeField, Tooltip("The wake-up clock")] GameObject _chooser;
        [SerializeField, Tooltip("The chosen wake-up time, HH:MM")] TMP_Text _wakeTime;
        [SerializeField, Tooltip("Day and hours of sleep under it")] TMP_Text _detail;
        [SerializeField, Tooltip("The earliest and latest wake-up hours")] TMP_Text _limit;
        [SerializeField] Button _earlier;
        [SerializeField] Button _later;
        [SerializeField] Button _sleep;
        [SerializeField] Button _cancel;
        [SerializeField, Tooltip("Black, over the whole screen: the closed eyes")] Image _darkness;
        [SerializeField, Tooltip("The clock running while asleep")] TMP_Text _asleepTime;
        [SerializeField] TMP_Text _asleepDay;
        [SerializeField, Tooltip("Why the bed refused")] TMP_Text _message;
        [SerializeField, Min(0f), Tooltip("Seconds a refusal stays (real time)")] float _messageSeconds = 2.5f;
        [SerializeField] MenuSounds _sounds;

        InputService _input;
        TimeService _timeService;
        SleepService _service;
        Canvas _canvas;
        SleepStage _shownStage = SleepStage.None;
        double _shownWake = -1d;
        int _shownMinute = -1;
        int _shownRefusals;
        float _messageLeft;
        int _openedFrame;

        [Inject]
        void Construct(InputService input, TimeService time, IObjectResolver resolver)
        {
            _input = input;
            _timeService = time;
            resolver.TryResolve(out _service);
            if (_eventSystem != null && _eventSystem.TryGetComponent(out InputSystemUIInputModule module))
                _input.ConfigureUiModule(module);
            if (_service != null)
                _shownRefusals = _service.RefusalCount;
        }

        void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _earlier.onClick.AddListener(() => _service?.Shift(-1));
            _later.onClick.AddListener(() => _service?.Shift(1));
            _sleep.onClick.AddListener(() => _service?.Confirm());
            _cancel.onClick.AddListener(() => _service?.Cancel());
            Show(SleepStage.None);
            _message.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_eventSystem != null)
                _eventSystem.SetActive(false);
        }

        void Update()
        {
            if (_service == null)
                return;

            SleepStage stage = _service.Stage;
            if (stage != _shownStage)
                Show(stage);
            TickMessage();

            switch (stage)
            {
                case SleepStage.Choosing:
                    if (Time.frameCount != _openedFrame && _input.UiCancelPressed && (_timeService == null || !_timeService.IsPaused))
                    {
                        _sounds?.Back();
                        _service.Cancel();
                    }
                    else
                        ShowChoice();
                    break;
                case SleepStage.FallingAsleep:
                case SleepStage.Asleep:
                case SleepStage.Waking:
                    SetDarkness(_service.Darkness);
                    if (stage == SleepStage.Asleep)
                        ShowAsleepClock(_service.Now);
                    break;
            }
        }

        void Show(SleepStage stage)
        {
            _shownStage = stage;
            bool choosing = stage == SleepStage.Choosing;
            bool dark = stage is SleepStage.FallingAsleep or SleepStage.Asleep or SleepStage.Waking;
            if (_eventSystem != null)
                _eventSystem.SetActive(choosing);
            _chooser.SetActive(choosing);
            _darkness.gameObject.SetActive(dark);
            _asleepTime.gameObject.SetActive(stage == SleepStage.Asleep);
            _asleepDay.gameObject.SetActive(stage == SleepStage.Asleep);
            _canvas.enabled = choosing || dark || _messageLeft > 0f;
            if (choosing)
            {
                _openedFrame = Time.frameCount;
                _shownWake = -1d;
                MenuButton.SelectSilently(_sleep);
            }
            if (dark)
                SetDarkness(_service.Darkness);
            _shownMinute = -1;
        }

        void ShowChoice()
        {
            WorldTime wake = _service.Wake;
            SleepWindow window = _service.Window;
            _earlier.interactable = wake.TotalHours > window.Earliest.TotalHours + 0.001;
            _later.interactable = wake.TotalHours < window.Latest.TotalHours - 0.001;
            if (wake.TotalHours == _shownWake)
                return;
            _shownWake = wake.TotalHours;
            _wakeTime.text = $"{wake.WholeHour:00}:{wake.Minute:00}";
            int hours = _service.HoursAsleep;
            _detail.text = (hours == 1 ? DetailOne : DetailMany).Format(wake.Day, hours);
            _limit.text = Limit.Format(window.Earliest.WholeHour.ToString("00"), window.Latest.WholeHour.ToString("00"));
        }

        void ShowAsleepClock(WorldTime now)
        {
            int minute = now.WholeHour * 60 + now.Minute;
            if (minute == _shownMinute)
                return;
            _shownMinute = minute;
            _asleepTime.text = $"{now.WholeHour:00}:{now.Minute:00}";
            _asleepDay.text = AsleepDay.Format(now.Day);
        }

        void SetDarkness(float darkness)
        {
            Color color = _darkness.color;
            color.a = darkness;
            _darkness.color = color;
            float clock = Mathf.InverseLerp(0.85f, 1f, darkness);
            _asleepTime.alpha = clock;
            _asleepDay.alpha = clock * 0.8f;
        }

        void TickMessage()
        {
            if (_service.RefusalCount != _shownRefusals)
            {
                _shownRefusals = _service.RefusalCount;
                _message.text = (_service.Refusal ?? string.Empty).ToUpperInvariant();
                _message.gameObject.SetActive(true);
                _messageLeft = _messageSeconds;
                _canvas.enabled = true;
            }
            if (_messageLeft <= 0f)
                return;
            _messageLeft -= Time.unscaledDeltaTime;
            if (_messageLeft > 0f)
                return;
            _message.gameObject.SetActive(false);
            _canvas.enabled = _shownStage != SleepStage.None;
        }
    }
}
