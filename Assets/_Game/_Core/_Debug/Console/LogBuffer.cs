using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aipocalypse.Core.Debugging
{
    public enum ConsoleEntryKind
    {
        Log,
        Warning,
        Error,
        // A line typed into the console.
        Input,
        // A command's result.
        Output,
    }

    public readonly struct ConsoleEntry
    {
        public readonly string Text;
        public readonly ConsoleEntryKind Kind;
        // Identical lines in a row are kept once with a count.
        public readonly int Count;

        public ConsoleEntry(string text, ConsoleEntryKind kind, int count = 1)
        {
            Text = text;
            Kind = kind;
            Count = count;
        }
    }

    // The console's scrollback in a fixed ring: Unity logs from any thread (moved in by Flush on the main thread) and the
    // console's own lines. A repeat of the newest line only raises its count.
    public sealed class LogBuffer : IDisposable
    {
        readonly struct PendingLog
        {
            public readonly string Text;
            public readonly string StackTrace;
            public readonly LogType Type;

            public PendingLog(string text, string stackTrace, LogType type)
            {
                Text = text;
                StackTrace = stackTrace;
                Type = type;
            }
        }

        readonly object _lock = new();
        readonly ConsoleEntry[] _entries;
        List<PendingLog> _pending = new();
        List<PendingLog> _flushing = new();
        int _first;
        int _count;

        public LogBuffer(int capacity)
        {
            _entries = new ConsoleEntry[Math.Max(1, capacity)];
            Application.logMessageReceivedThreaded += OnLog;
        }

        public int Count => _count;
        // Bumped by every change, so the view redraws only then.
        public int Version { get; private set; }
        // Of the last error, exception or assert logged by Unity.
        public string LastStackTrace { get; private set; }

        // 0 = oldest.
        public ConsoleEntry this[int index] => _entries[(_first + index) % _entries.Length];

        public void Dispose() => Application.logMessageReceivedThreaded -= OnLog;

        // Main thread: moves the Unity logs received since the last call into the buffer, handing each to added.
        public void Flush(Action<ConsoleEntry> added = null)
        {
            lock (_lock)
            {
                if (_pending.Count == 0)
                    return;
                (_pending, _flushing) = (_flushing, _pending);
            }
            foreach (PendingLog log in _flushing)
            {
                ConsoleEntryKind kind = log.Type switch
                {
                    LogType.Log => ConsoleEntryKind.Log,
                    LogType.Warning => ConsoleEntryKind.Warning,
                    _ => ConsoleEntryKind.Error,
                };
                if (kind == ConsoleEntryKind.Error && !string.IsNullOrEmpty(log.StackTrace))
                    LastStackTrace = log.Text + "\n" + log.StackTrace;
                Add(log.Text, kind);
                added?.Invoke(new ConsoleEntry(log.Text, kind));
            }
            _flushing.Clear();
        }

        // Main thread only.
        public void Add(string text, ConsoleEntryKind kind)
        {
            text ??= string.Empty;
            Version++;
            if (_count > 0)
            {
                int newest = (_first + _count - 1) % _entries.Length;
                ConsoleEntry last = _entries[newest];
                if (last.Kind == kind && last.Text == text)
                {
                    _entries[newest] = new ConsoleEntry(text, kind, last.Count + 1);
                    return;
                }
            }
            if (_count < _entries.Length)
            {
                _entries[(_first + _count) % _entries.Length] = new ConsoleEntry(text, kind);
                _count++;
                return;
            }
            _entries[_first] = new ConsoleEntry(text, kind);
            _first = (_first + 1) % _entries.Length;
        }

        public void Clear()
        {
            Array.Clear(_entries, 0, _entries.Length);
            _first = 0;
            _count = 0;
            Version++;
        }

        void OnLog(string text, string stackTrace, LogType type)
        {
            lock (_lock)
                _pending.Add(new PendingLog(text, stackTrace, type));
        }
    }
}
