using System.Globalization;
using UnityEngine;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>Supplies ordinary input and score changes; native recipes own every response.</summary>
    [AddComponentMenu(""), DefaultExecutionOrder(100)]
    public sealed class TextStudioReactionsLesson : MonoBehaviour
    {
        [SerializeField] TextStudioText _completion;
        [SerializeField] TextStudioText _marker;
        [SerializeField] TextStudioText _signal;
        [SerializeField] TextStudioText _counter;
        [SerializeField] int _startingScore = 98;
        int _score;

        public TextStudioText Completion => _completion;
        public TextStudioText Marker => _marker;
        public TextStudioText Signal => _signal;
        public TextStudioText Counter => _counter;
        public int Score => _score;

        public void Configure(TextStudioText completion, TextStudioText marker,
            TextStudioText signal, TextStudioText counter)
        {
            _completion = completion;
            _marker = marker;
            _signal = signal;
            _counter = counter;
        }

        void Start()
        {
            _score = _startingScore;
            if (_counter != null) _counter.ShowTextInstant(_score.ToString(CultureInfo.InvariantCulture));
            ReplayCompletion();
            ReplayMarker();
        }

        public void ReplayCompletion()
        {
            if (Application.isPlaying && _completion != null)
                _completion.ShowText(_completion.Markup);
        }

        public void ReplayMarker()
        {
            if (Application.isPlaying && _marker != null)
                _marker.ShowText(_marker.Markup);
        }

        public void SendTap()
        {
            if (Application.isPlaying && _signal != null)
                _signal.PlayReaction("damage");
        }

        public void AddPoint()
        {
            if (!Application.isPlaying || _counter == null) return;
            _score++;
            _counter.ShowTextInstant(_score.ToString(CultureInfo.InvariantCulture));
        }
    }
}
