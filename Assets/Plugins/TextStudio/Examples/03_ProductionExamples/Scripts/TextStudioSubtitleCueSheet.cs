using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio.Examples.Production
{
    public sealed class TextStudioSubtitleCueSheet : ScriptableObject
    {
        [SerializeField] List<TextStudioSubtitleCue> _cues = new List<TextStudioSubtitleCue>();

        public IReadOnlyList<TextStudioSubtitleCue> Cues => _cues;

        public void Configure(IEnumerable<TextStudioSubtitleCue> cues)
        {
            _cues.Clear();
            if (cues != null)
                _cues.AddRange(cues);
        }
    }

    [Serializable]
    public sealed class TextStudioSubtitleCue
    {
        [SerializeField] string _speaker;
        [SerializeField, TextArea(2, 5)] string _text;

        public string Speaker => _speaker;
        public string Text => _text;

        public TextStudioSubtitleCue(string speaker, string text)
        {
            _speaker = speaker;
            _text = text;
        }
    }
}
