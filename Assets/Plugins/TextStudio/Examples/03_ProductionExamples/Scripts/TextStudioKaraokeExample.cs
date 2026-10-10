using System.Collections.Generic;
using ImpossibleRobert.Common.Timing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TextStudio.Examples.Production
{
    [ExecuteAlways]
    [DefaultExecutionOrder(100)]
    [AddComponentMenu("")]
    public sealed class TextStudioKaraokeExample : TextStudioProductionExampleBase, ITextStudioExternalEffectSource
    {
        const int StyleCount = 6;
        const float NarrowSelectorWidth = 1050f;

        [Header("Scenario Data")]
        [SerializeField] TextStudioKaraokeCueSheet _cueSheet;
        [SerializeField] TextStudioKaraokeStylePreset[] _styles = new TextStudioKaraokeStylePreset[StyleCount];
        [SerializeField, Tooltip("Style the current lyric starts with; style buttons change it at runtime.")] int _styleIndex;

        [Header("Authored Scene View")]
        [SerializeField] TextStudioText _currentLine;
        [SerializeField] TMP_Text _previousLine;
        [SerializeField] TMP_Text _nextLine;
        [SerializeField] TMP_Text _clockLabel;
        [SerializeField] TMP_Text _statusLabel;
        [SerializeField] TMP_Text _modeTitleLabel;
        [SerializeField] TMP_Text _modeDescriptionLabel;
        [SerializeField] TMP_Text _playedColorModeLabel;
        [SerializeField] Image _progressFill;
        [SerializeField] AudioSource _audioSource;
        [SerializeField] GridLayoutGroup _styleGrid;
        [SerializeField] Button[] _styleButtons = new Button[StyleCount];
        [SerializeField] TMP_Text[] _styleButtonLabels = new TMP_Text[StyleCount];

        [SerializeField] bool _playOnEnable;
        [SerializeField] TMP_Text _transportLabel;
        [SerializeField] Comparison[] _comparisons = System.Array.Empty<Comparison>();

        [System.Serializable]
        public struct Comparison
        {
            public TextStudioText Text;
            public TextStudioKaraokeStylePreset Style;
        }

        sealed class ComparisonSource : ITextStudioExternalEffectSource
        {
            readonly TextStudioKaraokeExample _owner;
            internal readonly Comparison Specimen;
            internal ComparisonSource(TextStudioKaraokeExample owner, Comparison specimen) { _owner = owner; Specimen = specimen; }
            public bool IsActive => ((ITextStudioExternalEffectSource)_owner).IsActive;
            public void Submit(in TextStudioExternalEffectSubmissionContext context) => _owner.ApplyWordHighlight(_owner.CurrentTime, Specimen.Text, Specimen.Style, Specimen.Style.Coverage == TextStudioKaraokeHighlightCoverage.Progressive);
        }
        readonly List<ComparisonSource> _comparisonSources = new List<ComparisonSource>();

        public IReadOnlyList<Comparison> Comparisons => _comparisons;

        public void ConfigureComparisons(Comparison[] comparisons)
        {
            UnregisterComparisons();
            _comparisons = comparisons ?? System.Array.Empty<Comparison>();
            RegisterComparisons();
            _lineIndex = -1;
            RefreshPresentation(CurrentTime);
        }

        float _fallbackTime;
        int _lineIndex = -1;
        int _activeWordIndex = -1;
        int _completedWordCount;
        int _lastGridColumns = -1;
        float _lastGridWidth = -1f;
        bool _playing;
        bool _reducedMotion;
        bool _keepPlayedColor;
        float _lastAudioTime;
        TextStudioText _registeredTarget;
        bool _hasEditPreview;

        bool ITextStudioExternalEffectSource.IsActive => isActiveAndEnabled && (Application.isPlaying || _hasEditPreview);

        void ITextStudioExternalEffectSource.Submit(in TextStudioExternalEffectSubmissionContext context)
        {
            ApplyWordHighlight(CurrentTime);
        }

        public TextStudioKaraokeCueSheet CueSheet => _cueSheet;
        public IReadOnlyList<TextStudioKaraokeStylePreset> Styles => _styles;
        public int SelectedStyleIndex => _styleIndex;
        public bool ReducedMotion => _reducedMotion;
        public bool KeepPlayedColor => _keepPlayedColor;
        public bool IsPlaying => _playing;
        public float PlaybackTime => CurrentTime;

        public void ConfigureCoursePlayback(TMP_Text transportLabel, bool playOnEnable)
        {
            _transportLabel = transportLabel;
            _playOnEnable = playOnEnable;
            ConfigureAudioSource();
            RefreshPresentation(CurrentTime);
        }

        public void SeekTo(float seconds)
        {
            SetTime(seconds);
            _hasEditPreview = !Application.isPlaying;
            RegisterEffectSource();
            RefreshPresentation(_fallbackTime);
            HostTime.RequestEditorFrame(this, repaintAllViews: false);
        }

        public void ConfigureScene(
            TextStudioKaraokeCueSheet cueSheet,
            TextStudioKaraokeStylePreset[] styles,
            TextStudioText currentLine,
            TMP_Text previousLine,
            TMP_Text nextLine,
            TMP_Text clockLabel,
            TMP_Text statusLabel,
            TMP_Text modeTitleLabel,
            TMP_Text modeDescriptionLabel,
            TMP_Text playedColorModeLabel,
            Image progressFill,
            AudioSource audioSource,
            GridLayoutGroup styleGrid,
            Button[] styleButtons,
            TMP_Text[] styleButtonLabels)
        {
            _cueSheet = cueSheet;
            _styles = styles ?? new TextStudioKaraokeStylePreset[StyleCount];
            _currentLine = currentLine;
            _previousLine = previousLine;
            _nextLine = nextLine;
            _clockLabel = clockLabel;
            _statusLabel = statusLabel;
            _modeTitleLabel = modeTitleLabel;
            _modeDescriptionLabel = modeDescriptionLabel;
            _playedColorModeLabel = playedColorModeLabel;
            _progressFill = progressFill;
            _audioSource = audioSource;
            _styleGrid = styleGrid;
            _styleButtons = styleButtons ?? new Button[StyleCount];
            _styleButtonLabels = styleButtonLabels ?? new TMP_Text[StyleCount];
            ConfigureAudioSource();
            ResetRuntimeState();
            RegisterEffectSource();
        }

        void OnEnable()
        {
            ConfigureAudioSource();
            ResetRuntimeState();
            RegisterEffectSource();
            if (Application.isPlaying && _playOnEnable) Restart();
        }

        void OnDisable()
        {
            UnregisterComparisons();
            if (_registeredTarget != null) _registeredTarget.UnregisterExternalEffectSource(this);
            _registeredTarget = null;
            _hasEditPreview = false;
        }

        void RegisterEffectSource()
        {
            RegisterComparisons();
            if (!isActiveAndEnabled || _registeredTarget == _currentLine) return;
            if (_registeredTarget != null) _registeredTarget.UnregisterExternalEffectSource(this);
            _registeredTarget = _currentLine;
            if (_registeredTarget != null) _registeredTarget.RegisterExternalEffectSource(this);
        }

        void RegisterComparisons()
        {
            if (!isActiveAndEnabled || _comparisonSources.Count > 0) return;
            foreach (Comparison comparison in _comparisons)
            {
                if (comparison.Text == null || comparison.Style == null) continue;
                ComparisonSource source = new ComparisonSource(this, comparison);
                _comparisonSources.Add(source);
                comparison.Text.RegisterExternalEffectSource(source);
            }
        }

        void UnregisterComparisons()
        {
            foreach (ComparisonSource source in _comparisonSources)
                if (source.Specimen.Text != null) source.Specimen.Text.UnregisterExternalEffectSource(source);
            _comparisonSources.Clear();
        }

        protected override void Update()
        {
            UpdateStyleSelectorLayout();
            if (!Application.isPlaying || _currentLine == null)
                return;

            if (_playing && (_audioSource == null || _audioSource.clip == null))
                _fallbackTime += Time.unscaledDeltaTime;

            float time = CurrentTime;
            float duration = Mathf.Max(0.01f, Duration);
            if (_playing && _audioSource != null && _audioSource.clip != null && !_audioSource.isPlaying &&
                _lastAudioTime > duration - 0.1f)
            {
                time = duration;
            }
            if (_playing && time >= duration)
            {
                _playing = false;
                _audioSource?.Stop();
                time = duration;
            }
            _lastAudioTime = time;

            RefreshPresentation(time);
        }

        void RefreshPresentation(float time)
        {
            ResolveCues(time);
            if (_progressFill != null)
                _progressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(time / Mathf.Max(.01f, Duration)), 1f);
            if (_clockLabel != null)
                _clockLabel.text = FormatTime(time) + " / " + FormatTime(Duration);
            UpdateStatus();
        }

        public void TogglePlay()
        {
            if (!Application.isPlaying || _cueSheet == null)
                return;
            _playing = !_playing;
            if (_audioSource != null && _audioSource.clip != null)
            {
                if (_playing)
                    _audioSource.Play();
                else
                    _audioSource.Pause();
            }
            UpdateStatus();
        }

        public void Restart()
        {
            if (!Application.isPlaying) return;
            SetTime(0f);
            _playing = true;
            if (_audioSource != null && _audioSource.clip != null)
                _audioSource.Play();
        }

        public void SeekBackward()
        {
            Seek(-2f);
        }

        public void SeekForward()
        {
            Seek(2f);
        }

        public void SelectCurrentWord()
        {
            SelectStyle(0);
        }

        public void SelectClassicProgressive()
        {
            SelectStyle(1);
        }

        public void SelectPaintOnSweep()
        {
            SelectStyle(2);
        }

        public void SelectBeatPop()
        {
            SelectStyle(3);
        }

        public void SelectNeonWave()
        {
            SelectStyle(4);
        }

        public void SelectBounceAlong()
        {
            SelectStyle(5);
        }

        public void ToggleReducedMotion()
        {
            _reducedMotion = !_reducedMotion;
            UpdateStylePresentation();
            UpdateStatus();
        }

        public void TogglePlayedColorMode()
        {
            _keepPlayedColor = !_keepPlayedColor;
            UpdateStylePresentation();
            UpdateStatus();
        }

        void SelectStyle(int index)
        {
            int available = _styles != null ? _styles.Length : 0;
            _styleIndex = available > 0 ? Mathf.Clamp(index, 0, available - 1) : 0;
            TextStudioKaraokeStylePreset style = ResolveStyle();
            _keepPlayedColor = style != null &&
                style.Coverage == TextStudioKaraokeHighlightCoverage.Progressive;
            UpdateStylePresentation();
            UpdateStatus();
        }

        float CurrentTime
        {
            get
            {
                if (_audioSource != null && _audioSource.clip != null)
                    return _audioSource.timeSamples / (float)_audioSource.clip.frequency;
                return _fallbackTime;
            }
        }

        float Duration => _cueSheet != null ? _cueSheet.Duration : 0f;

        void ConfigureAudioSource()
        {
            if (_audioSource == null)
                return;
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.clip = _cueSheet != null ? _cueSheet.AudioClip : null;
        }

        void ResetRuntimeState()
        {
            _audioSource?.Stop();
            _fallbackTime = 0f;
            _lineIndex = -1;
            _activeWordIndex = -1;
            _completedWordCount = 0;
            _styleIndex = Mathf.Clamp(_styleIndex, 0, Mathf.Max(0, (_styles?.Length ?? 1) - 1));
            _playing = false;
            _hasEditPreview = false;
            _reducedMotion = false;
            TextStudioKaraokeStylePreset style = ResolveStyle();
            _keepPlayedColor = style != null &&
                style.Coverage == TextStudioKaraokeHighlightCoverage.Progressive;
            _lastAudioTime = 0f;
            _lastGridColumns = -1;
            _lastGridWidth = -1f;
            SetTime(0f);
            if (_progressFill != null)
                _progressFill.rectTransform.anchorMax = new Vector2(0.001f, 1f);
            if (_clockLabel != null)
                _clockLabel.text = FormatTime(0f) + " / " + FormatTime(Duration);
            UpdateStyleSelectorLayout();
            UpdateStylePresentation();
            UpdateStatus();
        }

        void Seek(float seconds)
        {
            SetTime(CurrentTime + seconds);
        }

        void SetTime(float seconds)
        {
            float clamped = Mathf.Clamp(seconds, 0f, Mathf.Max(0f, Duration - 0.001f));
            _fallbackTime = clamped;
            _lastAudioTime = clamped;
            if (_audioSource != null && _audioSource.clip != null)
            {
                int sample = Mathf.Clamp(
                    Mathf.RoundToInt(clamped * _audioSource.clip.frequency),
                    0,
                    _audioSource.clip.samples - 1);
                _audioSource.timeSamples = sample;
            }
            _lineIndex = -1;
            _activeWordIndex = -1;
            _completedWordCount = 0;
            ResolveCues(clamped);
        }

        void ResolveCues(float time)
        {
            if (_cueSheet == null || _cueSheet.Lines.Count == 0 || _currentLine == null)
                return;

            int resolvedLine = FindLine(time);
            if (resolvedLine != _lineIndex)
            {
                _lineIndex = resolvedLine;
                _activeWordIndex = -1;
                _completedWordCount = 0;
                KaraokeLineCue line = _cueSheet.Lines[_lineIndex];
                _currentLine.SetText(line.Text);
                _currentLine.SkipReveal();
                foreach (Comparison comparison in _comparisons)
                {
                    if (comparison.Text == null) continue;
                    comparison.Text.SetText(line.Text);
                    comparison.Text.SkipReveal();
                }
                if (_previousLine != null)
                    _previousLine.text = _lineIndex > 0 ? _cueSheet.Lines[_lineIndex - 1].Text : string.Empty;
                if (_nextLine != null)
                    _nextLine.text = _lineIndex + 1 < _cueSheet.Lines.Count ? _cueSheet.Lines[_lineIndex + 1].Text : string.Empty;
            }

            KaraokeLineCue current = _cueSheet.Lines[_lineIndex];
            _activeWordIndex = FindActiveWord(current, time);
            _completedWordCount = CountCompletedWords(current, time);
        }

        int FindLine(float time)
        {
            int low = 0;
            int high = _cueSheet.Lines.Count - 1;
            while (low <= high)
            {
                int middle = (low + high) >> 1;
                KaraokeLineCue line = _cueSheet.Lines[middle];
                if (time < line.StartTime)
                    high = middle - 1;
                else if (time >= line.EndTime)
                    low = middle + 1;
                else
                    return middle;
            }
            return Mathf.Clamp(high < 0 ? 0 : high, 0, _cueSheet.Lines.Count - 1);
        }

        static int FindActiveWord(KaraokeLineCue line, float time)
        {
            int low = 0;
            int high = line.Words.Count - 1;
            while (low <= high)
            {
                int middle = (low + high) >> 1;
                KaraokeWordCue word = line.Words[middle];
                if (time < word.StartTime)
                    high = middle - 1;
                else if (time >= word.EndTime)
                    low = middle + 1;
                else
                    return middle;
            }
            return -1;
        }

        static int CountCompletedWords(KaraokeLineCue line, float time)
        {
            int low = 0;
            int high = line.Words.Count;
            while (low < high)
            {
                int middle = (low + high) >> 1;
                if (line.Words[middle].EndTime <= time)
                    low = middle + 1;
                else
                    high = middle;
            }
            return low;
        }

        void ApplyWordHighlight(float time)
        {
            ApplyWordHighlight(time, _currentLine, ResolveStyle(), _keepPlayedColor);
        }

        void ApplyWordHighlight(float time, TextStudioText target, TextStudioKaraokeStylePreset style, bool keepPlayedColor)
        {
            if (target == null || !target.Plan.IsCreated || style == null || _lineIndex < 0)
                return;

            KaraokeLineCue line = _cueSheet.Lines[_lineIndex];
            int wordCount = Mathf.Min(line.Words.Count, target.Plan.WordCount);
            if (keepPlayedColor)
            {
                TextStudioEffectDefinitionAsset completedEffect = style.ResolveCompletedEffect(_reducedMotion) ??
                    style.ResolveActiveEffect(_reducedMotion);
                int completedCount = Mathf.Min(_completedWordCount, wordCount);
                for (int wordIndex = 0; wordIndex < completedCount; wordIndex++)
                    ApplyEffectToWord(target, completedEffect, 1f, 1f, wordIndex);
            }

            int lastCompletedWord = Mathf.Min(_completedWordCount, wordCount) - 1;
            if (lastCompletedWord >= 0 && style.ReleaseDuration > 0f)
            {
                KaraokeWordCue completedCue = line.Words[lastCompletedWord];
                float releaseProgress = Mathf.InverseLerp(
                    completedCue.EndTime,
                    completedCue.EndTime + style.ReleaseDuration,
                    time);
                float releaseIntensity = 1f - Mathf.SmoothStep(0f, 1f, releaseProgress);
                if (releaseIntensity > 0f)
                {
                    ApplyEffectToWord(
                        target,
                        style.ResolveActiveEffect(_reducedMotion),
                        1f,
                        releaseIntensity,
                        lastCompletedWord);
                }
            }

            if (_activeWordIndex < 0 || _activeWordIndex >= wordCount)
                return;

            KaraokeWordCue cue = line.Words[_activeWordIndex];
            float progress = Mathf.InverseLerp(cue.StartTime, cue.EndTime, time);
            ApplyEffectToWord(target, style.ResolveActiveEffect(_reducedMotion), progress, 1f, _activeWordIndex);
        }

        static void ApplyEffectToWord(TextStudioText target, TextStudioEffectDefinitionAsset effect, float progress, float intensity, int wordIndex)
        {
            if (effect == null || target?.Plan == null ||
                wordIndex < 0 || wordIndex >= target.Plan.WordCount)
            {
                return;
            }

            WordRange range = target.Plan.WordRanges[wordIndex];
            target.ApplyExternalEffect(effect, progress, intensity,
                new TextStudioTextRange(range.glyphStart, range.GlyphEnd));
        }

        TextStudioKaraokeStylePreset ResolveStyle()
        {
            if (_styles == null || _styles.Length == 0)
                return null;
            int index = Mathf.Clamp(_styleIndex, 0, _styles.Length - 1);
            return _styles[index];
        }

        void UpdateStylePresentation()
        {
            TextStudioKaraokeStylePreset style = ResolveStyle();
            for (int i = 0; i < (_styleButtons?.Length ?? 0); i++)
            {
                Button button = _styleButtons[i];
                if (button != null)
                    button.interactable = i != _styleIndex;
                if (_styleButtonLabels != null && i < _styleButtonLabels.Length && _styleButtonLabels[i] != null)
                {
                    string label = _styles != null && i < _styles.Length && _styles[i] != null
                        ? _styles[i].ButtonLabel
                        : "Style " + (i + 1);
                    _styleButtonLabels[i].text = i == _styleIndex ? "•  " + label : label;
                }
            }

            if (_modeTitleLabel != null)
            {
                string coverage = _keepPlayedColor
                    ? "COMPLETED + CURRENT"
                    : "CURRENT WORD";
                _modeTitleLabel.text = _transportLabel != null
                    ? style != null ? style.DisplayName : "Choose a style"
                    : style != null ? style.DisplayName.ToUpperInvariant() + "  •  " + coverage : "NO STYLE";
            }
            if (_modeDescriptionLabel != null)
            {
                TextStudioEffectDefinitionAsset active = style?.ResolveActiveEffect(_reducedMotion);
                _modeDescriptionLabel.text = style == null
                    ? "Assign karaoke style presets to inspect this example."
                    : style.Description + "\nRecipe: " + style.Recipe + "  •  Inspect: " +
                      (active != null ? active.name : "Missing effect") +
                      "  •  Release: " + style.ReleaseDuration.ToString(
                          "0.00",
                          System.Globalization.CultureInfo.InvariantCulture) + "s" +
                      (_keepPlayedColor ? "  •  Played words keep color" : "  •  Played words restore") +
                      (_reducedMotion ? "  •  Reduced motion" : string.Empty);
            }
            if (_playedColorModeLabel != null)
            {
                _playedColorModeLabel.text = _keepPlayedColor
                    ? "Played: Keep Color"
                    : "Played: Restore";
            }
        }

        void UpdateStyleSelectorLayout()
        {
            if (_styleGrid == null)
                return;

            RectTransform rect = _styleGrid.transform as RectTransform;
            float width = rect != null ? rect.rect.width : 0f;
            if (width <= 1f)
                return;
            int columns = width < NarrowSelectorWidth ? 3 : 6;
            if (columns == _lastGridColumns && Mathf.Abs(width - _lastGridWidth) < 0.5f)
                return;

            _lastGridColumns = columns;
            _lastGridWidth = width;
            float spacing = columns == 6 ? 10f : 12f;
            float horizontalPadding = 0f;
            float cellWidth = Mathf.Max(80f, (width - horizontalPadding * 2f - spacing * (columns - 1)) / columns);
            int rows = columns == 6 ? 1 : 2;
            float cellHeight = columns == 6 ? 48f : 40f;
            float contentHeight = rows * cellHeight + (rows - 1) * 8f;
            float topPadding = Mathf.Max(0f, ((rect != null ? rect.rect.height : contentHeight) - contentHeight) * 0.5f);
            _styleGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _styleGrid.constraintCount = columns;
            _styleGrid.spacing = new Vector2(spacing, 8f);
            _styleGrid.cellSize = new Vector2(cellWidth, cellHeight);
            _styleGrid.padding.left = Mathf.RoundToInt(horizontalPadding);
            _styleGrid.padding.right = Mathf.RoundToInt(horizontalPadding);
            _styleGrid.padding.top = Mathf.RoundToInt(topPadding);
            _styleGrid.padding.bottom = Mathf.RoundToInt(topPadding);
            LayoutRebuilder.MarkLayoutForRebuild(rect);
        }

        void UpdateStatus()
        {
            if (_transportLabel != null) _transportLabel.text = _playing ? "Pause" : "Play";
            if (_statusLabel == null)
                return;
            if (_transportLabel != null)
            {
                _statusLabel.text = (_playing ? "Playing" : "Paused") + (_reducedMotion ? "  •  Reduced motion" : string.Empty) +
                    "  •  Line " + (_lineIndex + 1) + " of " + (_cueSheet != null ? _cueSheet.Lines.Count : 0);
                return;
            }
            string source = _audioSource != null && _audioSource.clip != null ? "AUDIO timeSamples" : "DEMO CLOCK";
            _statusLabel.text = (_playing ? "PLAYING" : "PAUSED") + "  •  " + source + "  •  " +
                (_reducedMotion ? "REDUCED MOTION" : "FULL MOTION") + "  •  LINE " + (_lineIndex + 1) +
                " / " + (_cueSheet != null ? _cueSheet.Lines.Count : 0);
        }

        static string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float remainder = Mathf.Max(0f, seconds - minutes * 60f);
            return minutes.ToString("00") + ":" + remainder.ToString("00.00");
        }
    }
}
