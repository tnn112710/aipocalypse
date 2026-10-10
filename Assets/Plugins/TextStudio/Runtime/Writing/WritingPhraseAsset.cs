using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Authored stroke data for one exact shaped phrase and layout fingerprint.</summary>
    [CreateAssetMenu(
        fileName = "TextStudioWritingPhrase",
        menuName = "Text Studio/Text Authoring/Writing Phrase", order = 108)]
#if UNITY_6000_7_OR_NEWER
    // Asset change subscriptions serve Edit Mode too; subscribers release their own leases.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public sealed partial class WritingPhraseAsset : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] int _schemaVersion = CurrentSchemaVersion;
        [SerializeField, TextArea(1, 5)] string _resolvedText = string.Empty;
        [SerializeField] ulong _shapingFingerprint;
        [SerializeField] Rect _designBounds = new Rect(0f, 0f, 1f, 1f);
        [SerializeField] TextStudioWritingStroke[] _centerlineStrokes =
            Array.Empty<TextStudioWritingStroke>();
        [SerializeField] TextStudioWritingStroke[] _outlineStrokes =
            Array.Empty<TextStudioWritingStroke>();
        [NonSerialized] int _revision;

        internal static event Action<WritingPhraseAsset> Changed;

#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
        static void ReleaseChangeSubscriptions() => Changed = null;
#endif

        public int SchemaVersion => _schemaVersion;
        public int Revision => _revision;
        public string ResolvedText => _resolvedText ?? string.Empty;
        public ulong ShapingFingerprint => _shapingFingerprint;
        public Rect DesignBounds => _designBounds;
        public int CenterlineStrokeCount => _centerlineStrokes?.Length ?? 0;
        public int OutlineStrokeCount => _outlineStrokes?.Length ?? 0;

        /// <summary>Returns an independent copy of the phrase centerline stroke at the specified index.</summary>
        public TextStudioWritingStroke GetCenterlineStroke(int index)
        {
            return GetStroke(_centerlineStrokes, index);
        }

        /// <summary>Returns an independent copy of the phrase outline stroke at the specified index.</summary>
        public TextStudioWritingStroke GetOutlineStroke(int index)
        {
            return GetStroke(_outlineStrokes, index);
        }

        /// <summary>Checks exact resolved text equality and an equal, nonzero shaping fingerprint before reusing the authored phrase paths.</summary>
        public bool Matches(string resolvedText, ulong shapingFingerprint)
        {
            return _shapingFingerprint != 0UL &&
                   _shapingFingerprint == shapingFingerprint &&
                   string.Equals(
                       ResolvedText,
                       resolvedText ?? string.Empty,
                       StringComparison.Ordinal);
        }

        /// <summary>Replaces phrase text, shaping identity, bounds, and cloned stroke data, repairs stroke identities, and advances the asset revision.</summary>
        public void SetPhrase(
            string resolvedText,
            ulong shapingFingerprint,
            Rect designBounds,
            TextStudioWritingStroke[] centerlineStrokes,
            TextStudioWritingStroke[] outlineStrokes = null)
        {
            _resolvedText = resolvedText ?? string.Empty;
            _shapingFingerprint = shapingFingerprint;
            _designBounds = designBounds;
            _centerlineStrokes =
                TextStudioWritingGlyphRecord.CloneStrokes(centerlineStrokes);
            _outlineStrokes =
                TextStudioWritingGlyphRecord.CloneStrokes(outlineStrokes);
            RepairStrokeIdentities();
            NotifyChanged();
        }

        void OnValidate()
        {
            _schemaVersion = CurrentSchemaVersion;
            _resolvedText ??= string.Empty;
            _centerlineStrokes ??= Array.Empty<TextStudioWritingStroke>();
            _outlineStrokes ??= Array.Empty<TextStudioWritingStroke>();
            RepairStrokeIdentities();
            NotifyChanged();
        }

        void RepairStrokeIdentities()
        {
            TextStudioWritingGlyphRecord.RepairStrokeIdentities(
                _centerlineStrokes,
                "writing.phrase.centerline");
            TextStudioWritingGlyphRecord.RepairStrokeIdentities(
                _outlineStrokes,
                "writing.phrase.outline");
        }

        void NotifyChanged()
        {
            unchecked
            {
                _revision++;
            }
            Changed?.Invoke(this);
        }

        static TextStudioWritingStroke GetStroke(
            TextStudioWritingStroke[] strokes,
            int index)
        {
            if (strokes == null || index < 0 || index >= strokes.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return strokes[index].Clone();
        }
    }
}
