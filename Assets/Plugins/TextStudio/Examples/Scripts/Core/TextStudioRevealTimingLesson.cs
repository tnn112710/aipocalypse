using TMPro;
using UnityEngine;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>Observes native reveal events without changing the text or its authoring settings.</summary>
    [ExecuteAlways, AddComponentMenu("")]
    public sealed class TextStudioRevealTimingLesson : MonoBehaviour
    {
        [SerializeField] TextStudioText _source;
        [SerializeField] TMP_Text _status;
        float _startedAt;
        public TextStudioText Subject => _source;
        public int ObservedGlyphCount { get; private set; }
        public float LastGlyphTime { get; private set; }
        public float CompletionTime { get; private set; }
        public bool Finished { get; private set; }
        public bool Hidden { get; private set; }

        public void Configure(TextStudioText source, TMP_Text status)
        {
            Unsubscribe(); _source = source; _status = status;
            if (isActiveAndEnabled) Subscribe();
        }

        void OnEnable() => Subscribe();
        void OnDisable() => Unsubscribe();
        void Subscribe()
        {
            if (_source == null) return;
            _source.OnTypewriterStarted += OnStarted;
            _source.OnGlyphRevealed += OnGlyph;
            _source.OnShowComplete += OnFinished;
            _source.OnDisappearComplete += OnHidden;
        }
        void Unsubscribe()
        {
            if (_source == null) return;
            _source.OnTypewriterStarted -= OnStarted;
            _source.OnGlyphRevealed -= OnGlyph;
            _source.OnShowComplete -= OnFinished;
            _source.OnDisappearComplete -= OnHidden;
        }
        void OnStarted()
        {
            _startedAt = Time.realtimeSinceStartup; ObservedGlyphCount = 0;
            LastGlyphTime = CompletionTime = 0; Finished = Hidden = false;
        }
        void OnGlyph(int index, char character)
        {
            ObservedGlyphCount++; LastGlyphTime = Time.realtimeSinceStartup - _startedAt;
        }
        void OnFinished() { Finished = true; CompletionTime = Time.realtimeSinceStartup - _startedAt; }
        void OnHidden() { Hidden = true; }
        void Update()
        {
            if (_source == null || _status == null) return;
            _status.SetSampleText(_source.IsDisappearing ? "Erasing" : Hidden ? "Hidden" : _source.IsRevealPaused ? "Paused" : Finished ? "Finished" : _source.RevealedGlyphCount == _source.TotalGlyphCount ? "Reading pause" : "Writing");
        }
    }
}
