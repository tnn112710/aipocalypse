using TMPro;
using UnityEngine;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>Connects phrase input to native writing and displays its prepared-path readiness.</summary>
    [ExecuteAlways, DefaultExecutionOrder(100), AddComponentMenu("")]
    public sealed class TextStudioStrokeLesson : MonoBehaviour
    {
        [SerializeField] TextStudioText _subject;
        [SerializeField] TextStudioStrokePathGraphic _paths;
        [SerializeField] TMP_Text _status;
        bool _refreshReadiness;
        public TextStudioText Subject => _subject;

        public void Configure(TextStudioText subject, TextStudioStrokePathGraphic paths, TMP_Text status)
        {
            _subject = subject; _paths = paths; _status = status;
            _refreshReadiness = true;
        }

        void OnEnable() { _refreshReadiness = true; }
        void OnDisable() { if (_subject != null) _subject.Preview.Pause(); }
        void Update() { if (_refreshReadiness && _subject != null) RefreshReadiness(); }

        public void EditPhrase(string value)
        {
            _subject.SetText(string.IsNullOrWhiteSpace(value) ? "Luma" : value);
            _subject.Preview.SetSegments(TextStudioPreviewSegment.Reveal);
            _subject.Preview.Repeat = false;
            _subject.Preview.Restart();
            _refreshReadiness = true;
        }

        void RefreshReadiness()
        {
            if (_subject.CompiledWritingStyle == null || !_subject.TryCaptureWritingPhraseIdentity(out string phrase, out ulong fingerprint, out _))
                return;
            _refreshReadiness = false;
            var resolution = TextStudioWritingResolver.Resolve(_subject.CompiledWritingStyle,
                new TextStudioWritingResolveRequest(phrase, fingerprint, false));
            bool prepared = resolution.SourceKind == TextStudioWritingSourceKind.PhrasePath && resolution.HasPreparedPath;
            _paths.gameObject.SetActive(prepared);
            _paths.Configure(prepared ? _subject.WritingStyle.PhraseAsset : null, false);
            _status.text = prepared ? "Prepared paths match" : "No matching paths: native fallback";
        }
    }
}
