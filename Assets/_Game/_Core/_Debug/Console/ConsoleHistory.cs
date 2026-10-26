using System.Collections.Generic;

namespace Aipocalypse.Core.Debugging
{
    // Lines run in the console, oldest first, browsed with Up / Down like a shell. The line being typed comes back after
    // the newest one; a repeat of the newest line isn't stored twice.
    public sealed class ConsoleHistory
    {
        readonly List<string> _lines = new();
        readonly int _capacity;
        int _cursor;
        string _draft;

        public ConsoleHistory(int capacity) => _capacity = capacity < 1 ? 1 : capacity;

        public IReadOnlyList<string> Lines => _lines;

        public void Add(string line)
        {
            line = line?.Trim();
            if (!string.IsNullOrEmpty(line) && (_lines.Count == 0 || _lines[^1] != line))
            {
                _lines.Add(line);
                if (_lines.Count > _capacity)
                    _lines.RemoveRange(0, _lines.Count - _capacity);
            }
            ResetBrowse();
        }

        public void Load(IEnumerable<string> lines)
        {
            _lines.Clear();
            foreach (string line in lines)
                Add(line);
        }

        // current is what's typed now, given back when browsing returns past the newest line.
        public bool TryPrevious(string current, out string line)
        {
            if (_cursor >= _lines.Count)
                _draft = current;
            if (_cursor == 0)
            {
                line = null;
                return false;
            }
            line = _lines[--_cursor];
            return true;
        }

        public bool TryNext(out string line)
        {
            if (_cursor >= _lines.Count)
            {
                line = null;
                return false;
            }
            _cursor++;
            line = _cursor == _lines.Count ? _draft ?? string.Empty : _lines[_cursor];
            return true;
        }

        public void ResetBrowse()
        {
            _cursor = _lines.Count;
            _draft = null;
        }
    }
}
