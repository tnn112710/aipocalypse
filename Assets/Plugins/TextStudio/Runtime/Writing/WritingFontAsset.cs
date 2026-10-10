using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Font-specific authored centerlines and prepared contours keyed by glyph variation.</summary>
    [CreateAssetMenu(
        fileName = "TextStudioWritingFont",
        menuName = "Text Studio/Text Authoring/Writing Font", order = 106)]
#if UNITY_6000_7_OR_NEWER
    // Asset change subscriptions serve Edit Mode too; subscribers release their own leases.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public sealed partial class WritingFontAsset : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] int _schemaVersion = CurrentSchemaVersion;
        [SerializeField, Min(1f)] float _unitsPerEm = 1000f;
        [SerializeField] TextStudioWritingGlyphRecord[] _glyphs =
            Array.Empty<TextStudioWritingGlyphRecord>();
        [NonSerialized] int _revision;

        internal static event Action<WritingFontAsset> Changed;

#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
        static void ReleaseChangeSubscriptions() => Changed = null;
#endif

        public int SchemaVersion => _schemaVersion;
        public int Revision => _revision;
        public float UnitsPerEm => Mathf.Max(1f, _unitsPerEm);
        public int GlyphCount => _glyphs?.Length ?? 0;

        /// <summary>Returns an independent copy of a glyph record, including its authored stroke and knot data.</summary>
        public TextStudioWritingGlyphRecord GetGlyph(int index)
        {
            if (_glyphs == null || index < 0 || index >= _glyphs.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _glyphs[index].Clone();
        }

        /// <summary>Replaces glyph records with independent copies, clamps units per em to at least one, repairs stroke identities, and advances the asset revision.</summary>
        public void SetGlyphs(
            float unitsPerEm,
            TextStudioWritingGlyphRecord[] glyphs)
        {
            _unitsPerEm = Mathf.Max(1f, unitsPerEm);
            _glyphs = CloneGlyphs(glyphs);
            RepairStrokeIdentities();
            NotifyChanged();
        }

        void OnValidate()
        {
            _schemaVersion = CurrentSchemaVersion;
            _unitsPerEm = Mathf.Max(1f, _unitsPerEm);
            _glyphs ??= Array.Empty<TextStudioWritingGlyphRecord>();
            RepairStrokeIdentities();
            NotifyChanged();
        }

        void RepairStrokeIdentities()
        {
            if (_glyphs == null)
                return;
            for (int i = 0; i < _glyphs.Length; i++)
                _glyphs[i].RepairStrokeIdentities("writing.glyph");
        }

        void NotifyChanged()
        {
            unchecked
            {
                _revision++;
            }
            Changed?.Invoke(this);
        }

        static TextStudioWritingGlyphRecord[] CloneGlyphs(
            TextStudioWritingGlyphRecord[] glyphs)
        {
            if (glyphs == null || glyphs.Length == 0)
                return Array.Empty<TextStudioWritingGlyphRecord>();
            TextStudioWritingGlyphRecord[] clone =
                new TextStudioWritingGlyphRecord[glyphs.Length];
            for (int i = 0; i < glyphs.Length; i++)
                clone[i] = glyphs[i].Clone();
            return clone;
        }
    }
}
