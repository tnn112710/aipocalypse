using MB13.Aipocalypse.Core.TimeSystem;
using MB13.Aipocalypse.Input;
using UnityEngine;
using UnityEngine.InputSystem.UI;

namespace MB13.Aipocalypse.Core.UI
{
    // Opening and closing a modal screen: its EventSystem active only while open, the UI input requested and, when asked,
    // the game paused, all under the screen's name. Closing releases first, so the game never stays paused whatever
    // happens to the screen's visuals.
    public sealed class ModalScreen
    {
        object _owner;
        GameObject _eventSystem;
        InputService _input;
        TimeService _time;
        int _openedFrame;

        public bool IsOpen { get; private set; }
        public bool Pausing { get; private set; }
        // The key that opened it may still read as pressed this frame.
        public bool OpenedThisFrame => IsOpen && Time.frameCount == _openedFrame;

        // `eventSystem`: EventSystem + InputSystemUIInputModule, pointed at the UI map here.
        public void Bind(object owner, GameObject eventSystem, InputService input, TimeService time)
        {
            _owner = owner;
            _eventSystem = eventSystem;
            _input = input;
            _time = time;
            if (_eventSystem != null && _eventSystem.TryGetComponent(out InputSystemUIInputModule module))
                _input.ConfigureUiModule(module);
        }

        // False when it was open already.
        public bool Open(bool pause)
        {
            if (IsOpen)
                return false;
            IsOpen = true;
            _openedFrame = Time.frameCount;
            Pausing = pause;
            if (pause)
                _time?.RequestPause(_owner);
            _input.RequestUI(_owner);
            if (_eventSystem != null)
                _eventSystem.SetActive(true);
            return true;
        }

        // False when it was closed already.
        public bool Close()
        {
            if (!IsOpen)
                return false;
            IsOpen = false;
            if (_eventSystem != null)
                _eventSystem.SetActive(false);
            _input?.ReleaseUI(_owner);
            if (Pausing)
                _time?.ReleasePause(_owner);
            Pausing = false;
            return true;
        }
    }
}
