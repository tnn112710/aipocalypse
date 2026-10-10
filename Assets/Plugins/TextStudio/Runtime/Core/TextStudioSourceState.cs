using System;
using UnityEngine;

namespace TextStudio
{
    [Serializable]
    internal sealed class TextStudioSourceState
    {
        [SerializeField, TextArea(3, 12)] string _markup = string.Empty;
        [SerializeField] uint _version;

        public string Markup => _markup ?? string.Empty;
        public uint Version => _version;

        public bool SetMarkup(string markup)
        {
            markup = markup ?? string.Empty;
            if (string.Equals(_markup, markup, StringComparison.Ordinal))
                return false;

            _markup = markup;
            _version++;
            return true;
        }
    }
}
