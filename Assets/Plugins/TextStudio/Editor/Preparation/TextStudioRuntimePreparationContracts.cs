using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;

namespace TextStudio.Editor
{
    public enum TextStudioPreparationState { Ready, Updating, RuntimeGenerated, NeedsAttention, Unused }

    /// <summary>Optional presentation metadata supplied by the owning add-on.</summary>
    public sealed class TextStudioPreparationPresentation
    {
        public string ProductId { get; }
        public string ProductName { get; }
        public string KindId { get; }
        public string KindName { get; }
        public string Benefit { get; }
        /// <summary>The prepared data follows the characters text can show, so runtime character coverage applies.</summary>
        internal bool UsesCharacters { get; set; }
        public TextStudioPreparationPresentation(string productId, string productName, string kindId, string kindName, string benefit)
        { ProductId = productId; ProductName = productName; KindId = kindId; KindName = kindName; Benefit = benefit; }
    }

    public interface ITextStudioPreparationPresentationProvider
    {
        TextStudioPreparationPresentation Presentation { get; }
    }

    /// <summary>Reference discovery does not determine whether saved data is current.</summary>
    public enum TextStudioPreparationReference { NotChecked, Found, NotFound }

    /// <summary>A provider's current readiness for one authored text. Required failures stop content builds.</summary>
    public sealed class TextStudioPreparationItem
    {
        public string Provider { get; }
        public TextStudioPreparationPresentation Presentation { get; set; }
        public TextStudioPreparationReference BuildReference { get; set; }
        public string Summary { get; set; }
        public string Target { get; }
        public TextStudioPreparationState State { get; set; }
        public string Message { get; set; }
        public bool Required { get; }
        /// <summary>True when the user must edit or record content; the shared Prepare action cannot resolve this item.</summary>
        public bool RequiresManualAction { get; set; }
        /// <summary>New font source data is needed and must respect the project's embedding choice.</summary>
        public bool RequiresFontEmbedding { get; set; }
        /// <summary>User-facing effect of leaving this item unchanged. Empty uses the shared required/optional guidance.</summary>
        public string Consequence { get; set; }
        internal string SourcePath { get; set; }
        internal TextStudioText SourceText { get; set; }
        /// <summary>Saved assets holding this item's prepared data. Navigation only; never used to select preparation work.</summary>
        internal IReadOnlyList<string> DataPaths { get; set; } = Array.Empty<string>();
        public TextStudioPreparationItem(string provider, string target, TextStudioPreparationState state, string message, bool required = true)
        { Provider = provider; Target = target; State = state; Message = message; Required = required; }
    }

    public sealed class TextStudioPreparationReport
    {
        internal bool IsAnalysis { get; set; }
        internal bool IsComplete { get; set; } = true;
        internal string OperationLabel { get; set; }
        readonly List<TextStudioPreparationItem> _items = new List<TextStudioPreparationItem>();
        public IReadOnlyList<TextStudioPreparationItem> Items => _items;
        public bool Succeeded => IsComplete && !_items.Exists(item => item.Required && item.State == TextStudioPreparationState.NeedsAttention);
        public void Add(TextStudioPreparationItem item) => _items.Add(item ?? throw new ArgumentNullException(nameof(item)));
    }

    /// <summary>Optional add-ons register an Editor provider without introducing a dependency into the base package.</summary>
    public interface ITextStudioRuntimePreparationProvider
    {
        string Id { get; }
        void Analyze(TextStudioText text, TextStudioPreparationReport report);
        /// <summary>Yield between bounded work batches. Dispose must release owned jobs on cancellation.</summary>
        IEnumerator Prepare(TextStudioText text, BuildTarget target, TextStudioPreparationReport report);
    }

    /// <summary>Optional integrations contribute known future text such as localization entries and finite display alphabets.</summary>
    public interface ITextStudioPreparationTextContributor
    {
        void Collect(TextStudioText text, ICollection<string> samples);
    }
}
