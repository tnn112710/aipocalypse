using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio.Examples.Whisperwood
{
    public sealed class TextStudioWhisperwoodStory : ScriptableObject
    {
        [SerializeField] List<TextStudioWhisperwoodNpc> _npcs = new List<TextStudioWhisperwoodNpc>();

        public IReadOnlyList<TextStudioWhisperwoodNpc> Npcs => _npcs;

        public void Configure(IEnumerable<TextStudioWhisperwoodNpc> npcs)
        {
            _npcs.Clear();
            if (npcs != null)
                _npcs.AddRange(npcs);
        }
    }

    [Serializable]
    public sealed class TextStudioWhisperwoodNpc
    {
        [SerializeField] string _name;
        [SerializeField] string _role;
        [SerializeField, TextArea(2, 4)] string _greeting = "Hello, traveller.";
        [SerializeField] string _question = "Ask about the gate";
        [SerializeField] string _word;
        [SerializeField] Vector2 _position;
        [SerializeField] Color _color;
        [SerializeField, TextArea(2, 5)] string[] _lines = Array.Empty<string>();
        [SerializeField, TextArea(2, 4)] string _repeatLine;

        public string Name => _name;
        public string Role => _role;
        public string Greeting => _greeting;
        public string Question => _question;
        public string Word => _word;
        public Vector2 Position => _position;
        public Color Color => _color;
        public IReadOnlyList<string> Lines => _lines;
        public string RepeatLine => _repeatLine;

        public TextStudioWhisperwoodNpc(
            string name,
            string role,
            string word,
            Vector2 position,
            Color color,
            string[] lines,
            string repeatLine,
            string greeting = "Hello, traveller.",
            string question = "Ask about the gate")
        {
            _name = name;
            _role = role;
            _greeting = greeting;
            _question = question;
            _word = word;
            _position = position;
            _color = color;
            _lines = lines ?? Array.Empty<string>();
            _repeatLine = repeatLine;
        }
    }
}
