using System.Text;

namespace MB13.Aipocalypse.Core.Debugging
{
    // The console's input line: text and caret. With word set (Ctrl held), moves and deletes jump whole words.
    public sealed class ConsoleLine
    {
        readonly StringBuilder _text = new();
        string _cached;

        public int Caret { get; private set; }
        public int Length => _text.Length;
        public string Text => _cached ??= _text.ToString();
        // Bumped by every change, caret moves included.
        public int Version { get; private set; }

        public char this[int index] => _text[index];

        public void Insert(StringBuilder chars)
        {
            if (chars.Length == 0)
                return;
            _text.Insert(Caret, chars.ToString());
            Caret += chars.Length;
            Changed();
        }

        public void Insert(string chars)
        {
            if (string.IsNullOrEmpty(chars))
                return;
            _text.Insert(Caret, chars);
            Caret += chars.Length;
            Changed();
        }

        // Replaces the text; the caret goes to the end.
        public void Set(string text)
        {
            _text.Clear().Append(text);
            Caret = _text.Length;
            Changed();
        }

        public void Clear() => Set(string.Empty);

        public void Backspace(bool word)
        {
            int from = word ? WordStartBefore(Caret) : Caret - 1;
            if (from < 0 || from >= Caret)
                return;
            _text.Remove(from, Caret - from);
            Caret = from;
            Changed();
        }

        public void Delete(bool word)
        {
            int to = word ? WordEndAfter(Caret) : Caret + 1;
            if (to > _text.Length || to <= Caret)
                return;
            _text.Remove(Caret, to - Caret);
            Changed();
        }

        public void MoveLeft(bool word) => MoveTo(word ? WordStartBefore(Caret) : Caret - 1);
        public void MoveRight(bool word) => MoveTo(word ? WordEndAfter(Caret) : Caret + 1);
        public void Home() => MoveTo(0);
        public void End() => MoveTo(_text.Length);

        void MoveTo(int caret)
        {
            caret = caret < 0 ? 0 : caret > _text.Length ? _text.Length : caret;
            if (caret == Caret)
                return;
            Caret = caret;
            Version++;
        }

        // Words are letters, digits and '_', so "clock.set" is two words.
        bool IsWord(int index) => char.IsLetterOrDigit(_text[index]) || _text[index] == '_';

        int WordStartBefore(int index)
        {
            while (index > 0 && !IsWord(index - 1))
                index--;
            while (index > 0 && IsWord(index - 1))
                index--;
            return index;
        }

        int WordEndAfter(int index)
        {
            while (index < _text.Length && !IsWord(index))
                index++;
            while (index < _text.Length && IsWord(index))
                index++;
            return index;
        }

        void Changed()
        {
            _cached = null;
            Version++;
        }
    }
}
