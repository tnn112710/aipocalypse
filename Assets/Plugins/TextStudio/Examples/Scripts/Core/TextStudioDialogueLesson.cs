using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>Owns a short conversation while native dialogue and page components own text playback.</summary>
    [UnityEngine.AddComponentMenu("")]
    public sealed class TextStudioDialogueLesson : MonoBehaviour, ITextStudioMarkupPreprocessor
    {
        const string Opening = "Welcome, {traveler}. Which path will you take?";
        const string Journal = "<b>MOON GATE / FIELD NOTES</b>\nMira keeps watch where the two paths meet.<page><b>THE RIVER PATH</b>\nFollow the silver reeds. The bridge is quiet at dawn.<page><b>THE RIDGE PATH</b>\nCarry a lantern. The last relay still answers after sunset.";
        [SerializeField] TextStudioDialogueSource _source;
        [SerializeField] TextStudioPageNavigator _pages;
        [SerializeField] TMP_Text _status, _speaker, _sourceText, _processedText, _pageLabel;
        [SerializeField] Button _river, _ridge;
        [SerializeField] GameObject _pageControls;
        int _completed, _requested;
        bool _journal, _answered, _choosing;
        public TextStudioDialogueSource Source => _source;
        public TextStudioPageNavigator Pages => _pages;
        public int CompletionCount => _completed;
        public int AdvanceRequestCount => _requested;
        public bool Choosing => _choosing;

        public void Configure(TextStudioDialogueSource source, TextStudioPageNavigator pages, TMP_Text status, TMP_Text speaker,
            TMP_Text sourceText, TMP_Text processedText, TMP_Text pageLabel, Button river, Button ridge, GameObject pageControls)
        {
            _source = source; _pages = pages; _status = status; _speaker = speaker; _sourceText = sourceText;
            _processedText = processedText; _pageLabel = pageLabel; _river = river; _ridge = ridge; _pageControls = pageControls;
            _source.RegisterMarkupPreprocessor(this);
            ResetLesson();
        }
        void OnEnable()
        {
            if (_source == null) return;
            _source.RegisterMarkupPreprocessor(this);
            _source.OnLineComplete.AddListener(LineComplete);
            _source.OnAdvanceRequested.AddListener(AdvanceRequested);
            _pages.OnPageChanged.AddListener(PageChanged);
            ResetLesson();
        }
        void OnDisable()
        {
            if (_source == null) return;
            _source.UnregisterMarkupPreprocessor(this);
            _source.OnLineComplete.RemoveListener(LineComplete);
            _source.OnAdvanceRequested.RemoveListener(AdvanceRequested);
            _pages.OnPageChanged.RemoveListener(PageChanged);
        }
        public string PreprocessMarkup(string markup, in TextStudioMarkupPreprocessContext context)
        {
            return markup.Replace("{traveler}", "<color=#A54B28>Mira</color>");
        }
        public void ResetLesson()
        {
            _journal = _answered = _choosing = false; _completed = _requested = 0;
            Show(Opening);
        }
        public void Replay()
        {
            _completed = _requested = 0; _choosing = false;
            Show(_source.SourceLine);
        }
        public void Advance() { _source.AdvanceOrSkip(); }
        public void ChooseRiver() { Choose(false); }
        public void ChooseRidge() { Choose(true); }
        void Choose(bool ridge)
        {
            if (!_choosing) return;
            _answered = true; _choosing = false;
            Show(ridge ? "Take the ridge, {traveler}. Keep your lantern close." : "Take the river, {traveler}. Follow the silver reeds.");
        }
        public void ToggleJournal()
        {
            _journal = !_journal; _answered = _choosing = false;
            Show(_journal ? Journal : Opening);
        }
        public void RevealAll() { _source.Target.SkipReveal(); }
        public void PreviousPage() { _pages.PreviousPage(); }
        public void NextPage() { _pages.NextPage(); }
        public void InspectSource() { TextStudioLessonActions.Inspect(_source); }
        void Show(string canonical)
        {
            _pageControls.SetActive(_journal); _speaker.SetSampleText(_journal ? "MIRA'S FIELD JOURNAL" : "ROWAN / AT THE MOON GATE");
            _source.SetLine(canonical, Application.isPlaying);
            if (!Application.isPlaying) _source.Target.ShowTextInstant(_source.CurrentLine);
            _pages.FirstPage(); _pages.Refresh();
            _sourceText.SetSampleText(_source.SourceLine); _processedText.SetSampleText(_source.CurrentLine);
            Refresh();
        }
        void LineComplete()
        {
            _completed++;
            // The finished question offers the decision; the conversation, not the text, owns what happens next.
            if (!_journal && !_answered) _choosing = true;
            Refresh();
        }
        void AdvanceRequested()
        {
            _requested++;
            // After an answer, the next beat returns to the question so both routes stay explorable.
            if (!_journal && _answered) { _answered = false; Show(Opening); return; }
            Refresh();
        }
        void PageChanged(int page, int count) { Refresh(); }
        void Refresh()
        {
            _river.SetSampleInteractable(_choosing); _ridge.SetSampleInteractable(_choosing);
            _pageLabel.SetSampleText("Page " + _pages.CurrentPage + " / " + _pages.PageCount);
            _status.SetSampleText(_choosing ? "Choose your route. The conversation owns this decision." :
                _journal ? "Use the page controls to browse. Reveal all finishes the same native text." :
                _answered ? "Your route is chosen. Continue asks the conversation for the next beat." : "Continue finishes the line. The finished question offers the routes.");
            _status.AppendSampleText("\nCompleted: " + _completed + " / Advance requests: " + _requested);
        }
    }
}
