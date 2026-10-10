using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Serializable glyph metrics and authored centerline and outline strokes for one font variation.</summary>
    [Serializable]
    public struct TextStudioWritingGlyphRecord
    {
        [SerializeField] TextStudioWritingGlyphKey _key;
        [SerializeField] Rect _designBounds;
        [SerializeField] float _advance;
        [SerializeField] TextStudioWritingStroke[] _centerlineStrokes;
        [SerializeField] TextStudioWritingStroke[] _outlineStrokes;

        public TextStudioWritingGlyphKey Key => _key;
        public Rect DesignBounds => _designBounds;
        public float Advance => _advance;
        public int CenterlineStrokeCount => _centerlineStrokes?.Length ?? 0;
        public int OutlineStrokeCount => _outlineStrokes?.Length ?? 0;

        /// <summary>Stores glyph metrics and independently clones both stroke collections so later input edits do not affect the record.</summary>
        public TextStudioWritingGlyphRecord(
            TextStudioWritingGlyphKey key,
            Rect designBounds,
            float advance,
            TextStudioWritingStroke[] centerlineStrokes,
            TextStudioWritingStroke[] outlineStrokes = null)
        {
            _key = key;
            _designBounds = designBounds;
            _advance = Mathf.Max(0f, advance);
            _centerlineStrokes = CloneStrokes(centerlineStrokes);
            _outlineStrokes = CloneStrokes(outlineStrokes);
        }

        /// <summary>Returns an independent copy of the authored centerline stroke at the specified index.</summary>
        public TextStudioWritingStroke GetCenterlineStroke(int index)
        {
            return GetStroke(_centerlineStrokes, index);
        }

        /// <summary>Returns an independent copy of the authored outline stroke at the specified index.</summary>
        public TextStudioWritingStroke GetOutlineStroke(int index)
        {
            return GetStroke(_outlineStrokes, index);
        }

        internal TextStudioWritingGlyphRecord Clone()
        {
            return new TextStudioWritingGlyphRecord(
                _key,
                _designBounds,
                _advance,
                _centerlineStrokes,
                _outlineStrokes);
        }

        internal void RepairStrokeIdentities(string identityNamespace)
        {
            RepairStrokeIdentities(_centerlineStrokes, identityNamespace + ".centerline");
            RepairStrokeIdentities(_outlineStrokes, identityNamespace + ".outline");
        }

        static TextStudioWritingStroke GetStroke(
            TextStudioWritingStroke[] strokes,
            int index)
        {
            if (strokes == null || index < 0 || index >= strokes.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return strokes[index].Clone();
        }

        internal static TextStudioWritingStroke[] CloneStrokes(
            TextStudioWritingStroke[] strokes)
        {
            if (strokes == null || strokes.Length == 0)
                return Array.Empty<TextStudioWritingStroke>();
            TextStudioWritingStroke[] clone =
                new TextStudioWritingStroke[strokes.Length];
            for (int i = 0; i < strokes.Length; i++)
                clone[i] = strokes[i].Clone();
            return clone;
        }

        internal static void RepairStrokeIdentities(
            TextStudioWritingStroke[] strokes,
            string identityNamespace)
        {
            if (strokes == null)
                return;
            HashSet<TextStudioStackItemId> identities =
                new HashSet<TextStudioStackItemId>();
            for (int i = 0; i < strokes.Length; i++)
            {
                TextStudioStackItemId identity = strokes[i].StrokeId;
                if (identity.IsValid && identities.Add(identity))
                    continue;
                do
                {
                    identity = TextStudioStackItemId.CreateUnique(identityNamespace);
                }
                while (!identities.Add(identity));
                strokes[i] = strokes[i].WithRepairedIdentity(identity);
            }
        }
    }

}
