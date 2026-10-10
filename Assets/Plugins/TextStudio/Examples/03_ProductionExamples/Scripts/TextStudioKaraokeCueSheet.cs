using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio.Examples.Production
{
    public sealed class TextStudioKaraokeCueSheet : ScriptableObject
    {
        [SerializeField] AudioClip _audioClip;
        [SerializeField] List<KaraokeLineCue> _lines = new List<KaraokeLineCue>();

        public AudioClip AudioClip => _audioClip;
        public IReadOnlyList<KaraokeLineCue> Lines => _lines;

        public float Duration
        {
            get
            {
                if (_audioClip != null)
                    return _audioClip.length;
                return _lines.Count > 0 ? _lines[_lines.Count - 1].EndTime : 0f;
            }
        }

        public void Configure(AudioClip audioClip, IEnumerable<KaraokeLineCue> lines)
        {
            _audioClip = audioClip;
            _lines.Clear();
            if (lines != null)
                _lines.AddRange(lines);
        }

        public bool Validate(out string message)
        {
            float previousLineEnd = 0f;
            for (int lineIndex = 0; lineIndex < _lines.Count; lineIndex++)
            {
                KaraokeLineCue line = _lines[lineIndex];
                if (line == null || string.IsNullOrWhiteSpace(line.Text))
                {
                    message = "Line " + (lineIndex + 1) + " has no lyric text.";
                    return false;
                }
                if (line.StartTime < previousLineEnd || line.EndTime <= line.StartTime)
                {
                    message = "Line " + (lineIndex + 1) + " has overlapping or invalid times.";
                    return false;
                }

                int expectedWordCount = CountWords(line.Text);
                if (line.Words == null || line.Words.Count != expectedWordCount)
                {
                    message = "Line " + (lineIndex + 1) + " has " +
                        (line.Words != null ? line.Words.Count : 0) + " word cues for " +
                        expectedWordCount + " lyric words.";
                    return false;
                }

                float previousWordEnd = line.StartTime;
                for (int wordIndex = 0; wordIndex < line.Words.Count; wordIndex++)
                {
                    KaraokeWordCue word = line.Words[wordIndex];
                    if (word.StartTime < previousWordEnd || word.EndTime <= word.StartTime || word.EndTime > line.EndTime)
                    {
                        message = "Line " + (lineIndex + 1) + ", word " + (wordIndex + 1) + " has invalid times.";
                        return false;
                    }
                    previousWordEnd = word.EndTime;
                }

                previousLineEnd = line.EndTime;
            }

            if (_audioClip != null && previousLineEnd > _audioClip.length + 0.02f)
            {
                message = "The final lyric cue extends beyond the audio clip.";
                return false;
            }

            message = _lines.Count == 0 ? "Add at least one lyric line." : "Cue sheet is valid.";
            return _lines.Count > 0;
        }

        static int CountWords(string text)
        {
            int count = 0;
            bool insideWord = false;
            for (int i = 0; i < text.Length; i++)
            {
                bool whitespace = char.IsWhiteSpace(text[i]);
                if (!whitespace && !insideWord)
                    count++;
                insideWord = !whitespace;
            }
            return count;
        }
    }

    [Serializable]
    public sealed class KaraokeLineCue
    {
        [SerializeField, TextArea(1, 3)] string _text;
        [SerializeField, Min(0f)] float _startTime;
        [SerializeField, Min(0f)] float _endTime;
        [SerializeField] List<KaraokeWordCue> _words = new List<KaraokeWordCue>();

        public string Text => _text;
        public float StartTime => _startTime;
        public float EndTime => _endTime;
        public IReadOnlyList<KaraokeWordCue> Words => _words;

        public KaraokeLineCue(string text, float startTime, float endTime, IEnumerable<KaraokeWordCue> words)
        {
            _text = text;
            _startTime = startTime;
            _endTime = endTime;
            if (words != null)
                _words.AddRange(words);
        }
    }

    [Serializable]
    public struct KaraokeWordCue
    {
        [SerializeField, Min(0f)] float _startTime;
        [SerializeField, Min(0f)] float _endTime;

        public float StartTime => _startTime;
        public float EndTime => _endTime;

        public KaraokeWordCue(float startTime, float endTime)
        {
            _startTime = startTime;
            _endTime = endTime;
        }
    }
}
