using System;

namespace TextStudio.Editor
{
    internal sealed class TextStudioAuthoringSession
    {
        string _sourceText;
        int _selectionStart;
        int _selectionEnd;
        bool _isPlaying;
        float _previewProgress;

        public event Action SourceChanged;
        public event Action PreviewChanged;

        public TextStudioAuthoringSession(string sourceText)
        {
            _sourceText = sourceText ?? string.Empty;
            _selectionStart = _sourceText.Length;
            _selectionEnd = _selectionStart;
        }

        public string SourceText => _sourceText;
        public int SelectionStart => _selectionStart;
        public int SelectionEnd => _selectionEnd;
        public bool IsPlaying => _isPlaying;
        public float PreviewProgress => _previewProgress;

        public void SetSource(string sourceText, int selectionStart, int selectionEnd)
        {
            string next = sourceText ?? string.Empty;
            int start = Math.Max(0, Math.Min(selectionStart, next.Length));
            int end = Math.Max(0, Math.Min(selectionEnd, next.Length));
            bool changed = !string.Equals(_sourceText, next, StringComparison.Ordinal);
            _sourceText = next;
            _selectionStart = Math.Min(start, end);
            _selectionEnd = Math.Max(start, end);
            if (changed)
                SourceChanged?.Invoke();
        }

        public TextStudioTagSourceEditor.EditResult InsertSnippet(string snippet)
        {
            TextStudioTagSourceEditor.EditResult edit =
                TextStudioTagSourceEditor.ReplaceSelectionWithSnippet(
                    _sourceText,
                    _selectionStart,
                    _selectionEnd,
                    snippet);
            SetSource(edit.Text, edit.SelectionStart, edit.SelectionEnd);
            return edit;
        }

        public TextStudioDiagnosticReport Analyze(TextStudioTagAnalysisContext context)
        {
            return TextStudioAppearanceAuthoringMetadata.GetAnalysis(context, _sourceText);
        }

        public static string BuildTagStatus(TextStudioDiagnosticReport report)
        {
            string status = report.AuthoredTagCount == 1
                ? "1 tag found"
                : report.AuthoredTagCount + " tags found";
            int errors = CountDiagnostics(report, TextStudioDiagnosticSeverity.Error);
            if (errors > 0)
                return status + ", " + FormatCount(errors, "error");

            int warnings = CountDiagnostics(report, TextStudioDiagnosticSeverity.Warning);
            return warnings > 0
                ? status + ", " + FormatCount(warnings, "warning")
                : status;
        }

        static int CountDiagnostics(
            TextStudioDiagnosticReport report,
            TextStudioDiagnosticSeverity severity)
        {
            int count = 0;
            for (int i = 0; i < report.Diagnostics.Length; i++)
            {
                if (report.Diagnostics[i].Severity == severity)
                    count++;
            }
            return count;
        }

        static string FormatCount(int count, string noun)
        {
            return count + " " + noun + (count == 1 ? string.Empty : "s");
        }

        public void Play()
        {
            _isPlaying = true;
            PreviewChanged?.Invoke();
        }

        public void Pause()
        {
            _isPlaying = false;
            PreviewChanged?.Invoke();
        }

        public void Restart()
        {
            _previewProgress = 0f;
            _isPlaying = true;
            PreviewChanged?.Invoke();
        }

        public void Scrub(float progress)
        {
            _previewProgress = Math.Max(0f, Math.Min(1f, progress));
            _isPlaying = false;
            PreviewChanged?.Invoke();
        }
    }
}
