using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.Debugging
{
    // Which Unity logs the ScreenLog mirrors.
    public enum ScreenLogLevel
    {
        Off,
        Error,
        Warning,
        All,
    }

    public readonly struct ScreenMessage
    {
        public readonly string Key;
        public readonly string Text;
        public readonly ConsoleEntryKind Kind;
        // Unset: the console style's colour for Kind.
        public readonly Color? Color;
        public readonly float Remaining;

        public ScreenMessage(string key, string text, ConsoleEntryKind kind, Color? color, float remaining)
        {
            Key = key;
            Text = text;
            Kind = kind;
            Color = color;
            Remaining = remaining;
        }

        public ScreenMessage After(float deltaTime) => new(Key, Text, Kind, Color, Remaining - deltaTime);
    }

    // Unreal-style on-screen debug messages, drawn by the dev console's overlay, newest on top. A keyed message (Show) replaces
    // its own line, so a value shown every frame stays one line; seconds 0 lasts one frame. Runs on real time.
    // Inject it anywhere: in release builds it does nothing and nothing draws it.
    public sealed class ScreenLog : ITickable
    {
        public const float DefaultSeconds = 3f;
        public const int MaxMessages = 24;

        readonly List<ScreenMessage> _messages = new(MaxMessages);
        readonly bool _enabled;

        [Inject]
        public ScreenLog() : this(Debug.isDebugBuild)
        {
        }

        public ScreenLog(bool enabled) => _enabled = enabled;

        public ScreenLogLevel MirrorLevel { get; set; } = ScreenLogLevel.Error;
        public int Count => _messages.Count;
        // Bumped by every change, so the view redraws only then.
        public int Version { get; private set; }

        // 0 = oldest.
        public ScreenMessage this[int index] => _messages[index];

        public void Print(string text, float seconds = DefaultSeconds) => Add(null, text, ConsoleEntryKind.Log, null, seconds);
        public void Print(string text, Color color, float seconds = DefaultSeconds) => Add(null, text, ConsoleEntryKind.Log, color, seconds);

        public void Show(string key, string text, float seconds = 0f) => Add(key, text, ConsoleEntryKind.Log, null, seconds);
        public void Show(string key, string text, Color color, float seconds = 0f) => Add(key, text, ConsoleEntryKind.Log, color, seconds);

        public bool Mirrors(ConsoleEntryKind kind) => kind switch
        {
            ConsoleEntryKind.Error => MirrorLevel >= ScreenLogLevel.Error,
            ConsoleEntryKind.Warning => MirrorLevel >= ScreenLogLevel.Warning,
            ConsoleEntryKind.Log => MirrorLevel >= ScreenLogLevel.All,
            _ => false,
        };

        // A Unity log, shown when MirrorLevel includes its kind. A log repeated every frame stays one line, its time restarted.
        public void Mirror(ConsoleEntryKind kind, string text, float seconds)
        {
            if (!Mirrors(kind))
                return;
            for (int i = 0; i < _messages.Count; i++)
            {
                ScreenMessage message = _messages[i];
                if (message.Key == null && message.Color == null && message.Kind == kind && message.Text == text)
                {
                    _messages[i] = new ScreenMessage(null, text, kind, null, Mathf.Max(0f, seconds));
                    return;
                }
            }
            Add(null, text, kind, null, seconds);
        }

        public void Remove(string key)
        {
            int index = IndexOf(key);
            if (index < 0)
                return;
            _messages.RemoveAt(index);
            Version++;
        }

        public void Clear()
        {
            if (_messages.Count == 0)
                return;
            _messages.Clear();
            Version++;
        }

        public void Tick() => Advance(Time.unscaledDeltaTime);

        public void Advance(float deltaTime)
        {
            for (int i = _messages.Count - 1; i >= 0; i--)
            {
                ScreenMessage message = _messages[i].After(deltaTime);
                if (message.Remaining < 0f)
                {
                    _messages.RemoveAt(i);
                    Version++;
                }
                else
                {
                    _messages[i] = message;
                }
            }
        }

        void Add(string key, string text, ConsoleEntryKind kind, Color? color, float seconds)
        {
            if (!_enabled)
                return;
            var message = new ScreenMessage(key, text ?? string.Empty, kind, color, Mathf.Max(0f, seconds));
            int index = IndexOf(key);
            if (index >= 0)
            {
                ScreenMessage old = _messages[index];
                _messages[index] = message;
                if (old.Text != message.Text || old.Color != message.Color)
                    Version++;
                return;
            }
            if (_messages.Count == MaxMessages)
                _messages.RemoveAt(0);
            _messages.Add(message);
            Version++;
        }

        int IndexOf(string key)
        {
            if (key == null)
                return -1;
            for (int i = 0; i < _messages.Count; i++)
                if (_messages[i].Key == key)
                    return i;
            return -1;
        }
    }
}
