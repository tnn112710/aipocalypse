using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>One independently owned inline shape or a link to a reusable curve profile.</summary>
    [Serializable]
    public sealed class TextStudioCurveReference : ITextStudioCurve
    {
        [SerializeField] TextStudioCurveDefinition _inline = new TextStudioCurveDefinition(CurveType.Arch);
        [SerializeField] TextStudioCurveProfile _profile;
        [NonSerialized] uint _localRevision;

        public bool IsLinked => _profile != null;
        public TextStudioCurveProfile Profile => _profile;
        public uint Revision => _profile != null ? _profile.Revision : _localRevision;
        public TextStudioCurveDefinition Definition => _profile != null ? _profile.Definition : _inline.Copy();

        /// <summary>Copies a curve definition into this reference, removes its profile link, and advances the local revision.</summary>
        public void SetInline(in TextStudioCurveDefinition definition)
        {
            _inline = definition.Copy();
            _profile = null;
            unchecked { _localRevision++; }
        }

        /// <summary>Links a non-null shared curve profile and advances the local revision.</summary>
        public void Link(TextStudioCurveProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            _profile = profile;
            unchecked { _localRevision++; }
        }

        /// <summary>Copies the linked profile's definition into local settings and removes the profile link.</summary>
        public void MakeLocal()
        {
            if (_profile == null) return;
            _inline = _profile.Definition;
            _profile = null;
            unchecked { _localRevision++; }
        }

        public float Evaluate(float t)
        {
            return _profile != null ? _profile.Evaluate(t) : _inline.Evaluate(t);
        }
    }
}
