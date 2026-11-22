using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.TimeSystem;
using MB13.Aipocalypse.Input;
using MessagePipe;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.UI
{
    // A radial menu over the game (the dog's orders): while open, time runs slow and input goes to the UI; the cursor's
    // direction from the screen centre (or a stick) picks a slice, a click / Submit takes it, Esc / right click / E / B
    // close it. Every way it closes gives time and input back.
    public sealed class CommandWheelPresenter : ITickable, IDisposable
    {
        // Dead zone round the centre at 1080p, scaled with the screen height.
        const float DeadZone = 40f;
        const float StickDeadZone = 0.5f;

        readonly InputService _input;
        readonly TimeService _time;
        readonly IDisposable _subscriptions;
        readonly List<WheelSlice> _slices = new();
        CommandWheel _view;
        object _owner;
        int _highlighted = -1;
        int _openedFrame = -1;

        public bool IsOpen => _owner != null;
        public object Owner => _owner;

        public CommandWheelPresenter(InputService input, TimeService time, ISubscriber<PlayerDied> died, ISubscriber<RaidStarted> raid)
        {
            _input = input;
            _time = time;
            _subscriptions = DisposableBag.Create(died.Subscribe(_ => Close()), raid.Subscribe(_ => Close()));
        }

        public void Attach(CommandWheel view) => _view = view;

        public bool TryOpen(object owner, IReadOnlyList<WheelSlice> slices, float timeScale)
        {
            if (owner == null || IsOpen || _input.IsUIRequested || slices.Count == 0)
                return false;
            _owner = owner;
            _slices.Clear();
            for (int i = 0; i < slices.Count; i++)
                _slices.Add(slices[i]);
            _highlighted = -1;
            _openedFrame = Time.frameCount;
            _time.Request(this, timeScale);
            _input.RequestUI(this);
            Mouse.current?.WarpCursorPosition(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            if (_view != null)
            {
                _view.Show(_slices);
                _view.Highlight(-1);
            }
            return true;
        }

        // Takes slice `index` when it can be picked (and closes); false leaves the wheel open.
        public bool Pick(int index)
        {
            if (!IsOpen || index < 0 || index >= _slices.Count || !_slices[index].Enabled)
                return false;
            Action picked = _slices[index].Picked;
            Close();
            picked?.Invoke();
            return true;
        }

        public void Close()
        {
            if (!IsOpen)
                return;
            _owner = null;
            _slices.Clear();
            _time.Release(this);
            _input.ReleaseUI(this);
            _view?.Hide();
        }

        public void Tick()
        {
            if (!IsOpen || Time.frameCount == _openedFrame)
                return;
            if (_input.UiCancelPressed || _input.UiRightClickPressed || _input.UiInteractKeyPressed)
            {
                Close();
                return;
            }
            int slice = Aimed();
            if (slice != _highlighted)
            {
                _highlighted = slice;
                _view?.Highlight(slice);
            }
            if (_input.UiConfirmPressed)
                Pick(_highlighted);
        }

        int Aimed()
        {
            Vector2 stick = _input.UiNavigate;
            if (stick.sqrMagnitude >= StickDeadZone * StickDeadZone)
                return WheelMath.SliceAt(stick, _slices.Count, 0f);
            Vector2 fromCentre = _input.UiPoint - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            return WheelMath.SliceAt(fromCentre, _slices.Count, DeadZone * Screen.height / 1080f);
        }

        public void Dispose()
        {
            Close();
            _subscriptions.Dispose();
        }
    }
}
