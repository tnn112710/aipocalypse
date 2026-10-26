using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Aipocalypse.Core.TimeSystem;
using Aipocalypse.Input;
using UnityEngine;
using VContainer.Unity;

namespace Aipocalypse.Core.Debugging
{
    // The dev console (editor and development builds): ` opens and closes it, a typed line runs through the CommandRegistry
    // (';' chains statements). Unity logs show only at the ScreenLog level (log.screen.*, off by default), in the scrollback and
    // on screen. While open it holds InputService's text input, so the gameplay, UI and Debug maps are off and typing never
    // fires a cheat. ConsoleView draws it.
    public sealed class ConsoleService : IInitializable, ITickable, IDisposable
    {
        const string HistoryFile = "console_history.txt";

        readonly InputService _input;
        readonly CommandRegistry _commands;
        readonly LogBuffer _logs;
        readonly ScreenLog _screen;
        readonly TimeService _time;
        readonly ConsoleSettings _settings;
        readonly ConsoleLine _line = new();
        readonly ConsoleHistory _history;
        readonly List<string> _suggestions = new();
        readonly List<string> _statements = new();
        readonly List<string> _groups = new();
        readonly StringBuilder _typed = new();
        readonly KeyRepeat _backspace = new();
        readonly KeyRepeat _delete = new();
        readonly KeyRepeat _left = new();
        readonly KeyRepeat _right = new();
        readonly Func<ConsoleEntryKind, bool> _keep;
        readonly Action<ConsoleEntry> _mirror;
        string _historyPath;
        string _completionBase;
        bool _completing;

        public ConsoleService(InputService input, CommandRegistry commands, LogBuffer logs, ScreenLog screen, TimeService time, ConsoleSettings settings)
        {
            _input = input;
            _commands = commands;
            _logs = logs;
            _screen = screen;
            _time = time;
            _settings = settings;
            _history = new ConsoleHistory(settings.HistorySize);
            _keep = screen.Mirrors;
            _mirror = entry => _screen.Mirror(entry.Kind, entry.Text, _settings.ScreenLogSeconds);
        }

        public bool IsOpen { get; private set; }
        public ConsoleLine Line => _line;
        public LogBuffer Logs => _logs;
        public IReadOnlyList<string> Suggestions => _suggestions;
        // Index into Suggestions while Tab cycles them, else -1.
        public int SelectedSuggestion { get; private set; } = -1;
        // Usage and description of the command being typed, or null.
        public string Hint { get; private set; }
        // Text lines scrolled up from the newest.
        public int ScrollOffset { get; private set; }
        // Set by the view as it draws: lines it shows (PageUp / PageDown step) and how far up it can scroll.
        public int PageLines { get; set; } = 20;
        public int ScrollLimit { get; set; } = int.MaxValue;
        // Bumped by every change the view draws, except the log's own (LogBuffer.Version).
        public int Version { get; private set; }

        public void Initialize()
        {
            _screen.MirrorLevel = _settings.ScreenLogLevel;
            _historyPath = Path.Combine(Application.persistentDataPath, HistoryFile);
            try
            {
                if (File.Exists(_historyPath))
                    _history.Load(File.ReadAllLines(_historyPath));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Debug.LogWarning($"Console: couldn't read {_historyPath}: {e.Message}");
            }
        }

        public void Dispose() => Close();

        public void Tick()
        {
            _logs.Flush(_keep, _mirror);
            if (_input.ConsoleTogglePressed)
            {
                if (IsOpen)
                    Close();
                else
                    Open();
                return;
            }
            if (!IsOpen)
                return;
            if (_input.WasPressed(ConsoleButton.Close))
            {
                Close();
                return;
            }
            ReadTyping();
            ReadEditing(Time.unscaledDeltaTime);
            if (_input.WasPressed(ConsoleButton.Submit))
                Submit();
        }

        public void Open()
        {
            if (IsOpen)
                return;
            IsOpen = true;
            _input.RequestTextInput(this);
            if (_settings.PauseWhileOpen)
                _time.RequestPause(this);
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen)
                return;
            IsOpen = false;
            _input.ReleaseTextInput(this);
            _time.ReleasePause(this);
            Changed();
        }

        // Runs a line as if typed (echo, results and errors go to the scrollback); ';' separates statements.
        public void Run(string text)
        {
            _logs.Add(text, ConsoleEntryKind.Input);
            CommandParser.SplitStatements(text, _statements);
            foreach (string statement in _statements)
            {
                CommandResult result = _commands.Execute(statement);
                // Logs written by the command come before its result.
                _logs.Flush(_keep, _mirror);
                if (!string.IsNullOrEmpty(result.Message))
                    _logs.Add(result.Message, result.Succeeded ? ConsoleEntryKind.Output : ConsoleEntryKind.Error);
            }
        }

        void Submit()
        {
            string text = _line.Text.Trim();
            _line.Clear();
            ScrollOffset = 0;
            if (text.Length > 0)
            {
                _history.Add(text);
                SaveHistory();
                Run(text);
            }
            Refresh();
        }

        void ReadTyping()
        {
            _input.TakeTypedText(_typed);
            if (_typed.Length == 0)
                return;
            // The console key's own character (` or ~, or a layout's character on that key) never lands in the line.
            if (_input.ConsoleToggleHeld)
                _typed.Clear();
            for (int i = _typed.Length - 1; i >= 0; i--)
                if (_typed[i] is '`' or '~')
                    _typed.Remove(i, 1);
            if (_typed.Length > 0)
            {
                _line.Insert(_typed);
                Refresh();
            }
            _typed.Clear();
        }

        void ReadEditing(float deltaTime)
        {
            bool word = _input.ConsoleWordModifierHeld;
            int version = _line.Version;
            if (Repeats(_backspace, ConsoleButton.Backspace, deltaTime))
                _line.Backspace(word);
            if (Repeats(_delete, ConsoleButton.Delete, deltaTime))
                _line.Delete(word);
            if (_input.WasPressed(ConsoleButton.Paste))
                Paste();
            if (_line.Version != version)
                Refresh();

            version = _line.Version;
            if (Repeats(_left, ConsoleButton.CaretLeft, deltaTime))
                _line.MoveLeft(word);
            if (Repeats(_right, ConsoleButton.CaretRight, deltaTime))
                _line.MoveRight(word);
            if (_input.WasPressed(ConsoleButton.Home))
                _line.Home();
            if (_input.WasPressed(ConsoleButton.End))
                _line.End();
            if (_line.Version != version)
                Changed();

            if (_input.WasPressed(ConsoleButton.HistoryPrevious) && _history.TryPrevious(_line.Text, out string previous))
                SetLine(previous);
            if (_input.WasPressed(ConsoleButton.HistoryNext) && _history.TryNext(out string next))
                SetLine(next);
            if (_input.WasPressed(ConsoleButton.Complete))
                Complete();

            if (_input.WasPressed(ConsoleButton.PageUp))
                Scroll(PageLines - 1);
            if (_input.WasPressed(ConsoleButton.PageDown))
                Scroll(-(PageLines - 1));
            float wheel = _input.ConsoleScroll;
            if (wheel != 0f)
                Scroll(wheel > 0f ? _settings.WheelLines : -_settings.WheelLines);
        }

        bool Repeats(KeyRepeat repeat, ConsoleButton button, float deltaTime) =>
            repeat.Step(_input.WasPressed(button), _input.IsHeld(button), deltaTime, _settings.RepeatDelay, _settings.RepeatInterval);

        // The clipboard's first line.
        void Paste()
        {
            string clip = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(clip))
                return;
            int end = clip.IndexOfAny(new[] { '\r', '\n' });
            _line.Insert(end >= 0 ? clip.Substring(0, end) : clip);
        }

        // Tab: completes the token being typed; pressed again, cycles through the other suggestions.
        void Complete()
        {
            if (!_completing)
            {
                int start = _commands.Suggest(_line.Text, _suggestions);
                if (_suggestions.Count == 0)
                    return;
                _completionBase = _line.Text.Substring(0, start);
                SelectedSuggestion = 0;
                _completing = true;
            }
            else
            {
                SelectedSuggestion = (SelectedSuggestion + 1) % _suggestions.Count;
            }
            string completed = _completionBase + _suggestions[SelectedSuggestion];
            _line.Set(_suggestions.Count == 1 ? completed + " " : completed);
            UpdateHint();
            Changed();
        }

        void SetLine(string text)
        {
            _line.Set(text);
            Refresh();
        }

        void Scroll(int lines)
        {
            int offset = Mathf.Clamp(ScrollOffset + lines, 0, Mathf.Max(0, ScrollLimit));
            if (offset == ScrollOffset)
                return;
            ScrollOffset = offset;
            Changed();
        }

        // The line changed: new suggestions, Tab starts over.
        void Refresh()
        {
            _completing = false;
            SelectedSuggestion = -1;
            _commands.Suggest(_line.Text, _suggestions);
            UpdateHint();
            Changed();
        }

        void UpdateHint()
        {
            string statement = _line.Text.Substring(CommandParser.LastStatementStart(_line.Text)).TrimStart();
            if (statement.Length == 0)
            {
                // Nothing typed yet: what there is to type here (this scene's command groups).
                _commands.Groups(_groups);
                Hint = _groups.Count == 0 ? null : "Commands: " + string.Join("  ", _groups) + "   (help <group>, Tab completes)";
                return;
            }
            int space = statement.IndexOf(' ');
            string name = space >= 0 ? statement.Substring(0, space) : statement;
            if (space < 0 && SelectedSuggestion < 0 && _suggestions.Count > 0 && !_commands.TryGet(name, out _))
                name = _suggestions[0];
            Hint = _commands.Describe(name);
        }

        void SaveHistory()
        {
            if (_historyPath == null)
                return;
            try
            {
                File.WriteAllLines(_historyPath, _history.Lines);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Debug.LogWarning($"Console: couldn't write {_historyPath}: {e.Message}");
            }
        }

        void Changed() => Version++;
    }
}
