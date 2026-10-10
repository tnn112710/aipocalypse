using System;
using UnityEngine;

namespace TextStudio
{
    public enum TextStudioAppearanceApplyMode { Replace = 0, Overlay = 1 }
    public enum TextStudioAppearanceActivationMode { Always = 0, Tagged = 1 }
    public enum TextStudioAppearanceDefinitionSource { Inline = 0, Asset = 1 }

    /// <summary>Activation metadata for a top-level appearance group. Inactive source data is retained.</summary>
    [Serializable]
    public struct TextStudioAppearanceActivation
    {
        [SerializeField] TextStudioAppearanceActivationMode _mode;
        [SerializeField] string _id;
        [SerializeField] TextStudioAppearanceDefinitionSource _source;
        [SerializeField] TextStudio2DStyleAsset _asset;
        [SerializeField] bool _overrideApplyMode;
        [SerializeField] TextStudioAppearanceApplyMode _applyMode;
        [SerializeField] TextStudioAppearanceExposure[] _exposures;

        public TextStudioAppearanceActivationMode Mode => _mode;
        public string Id => _id ?? string.Empty;
        public TextStudioAppearanceDefinitionSource Source => _source;
        public TextStudio2DStyleAsset Asset => _asset;
        public bool OverrideApplyMode => _overrideApplyMode;
        public TextStudioAppearanceApplyMode ApplyMode => _applyMode;
        public TextStudioAppearanceExposure[] Exposures => CloneExposures(_exposures);

        /// <summary>Creates a tagged inline appearance definition with the supplied identifier and application mode.</summary>
        public static TextStudioAppearanceActivation Inline(string id, TextStudioAppearanceApplyMode mode = TextStudioAppearanceApplyMode.Replace)
            => new TextStudioAppearanceActivation { _mode = TextStudioAppearanceActivationMode.Tagged, _id = id, _applyMode = mode };

        /// <summary>Creates a tagged appearance definition referencing a style asset, optionally overriding its default application mode.</summary>
        public static TextStudioAppearanceActivation Reference(string id, TextStudio2DStyleAsset asset, bool overrideMode = false, TextStudioAppearanceApplyMode mode = TextStudioAppearanceApplyMode.Replace)
            => new TextStudioAppearanceActivation { _mode = TextStudioAppearanceActivationMode.Tagged, _id = id, _source = TextStudioAppearanceDefinitionSource.Asset, _asset = asset, _overrideApplyMode = overrideMode, _applyMode = mode };

        /// <summary>Returns an activation copy with the supplied always-active or tag-activated mode.</summary>
        public TextStudioAppearanceActivation WithMode(TextStudioAppearanceActivationMode mode) { TextStudioAppearanceActivation copy = Clone(); copy._mode = mode; return copy; }
        /// <summary>Returns an activation copy with the supplied appearance tag identifier.</summary>
        public TextStudioAppearanceActivation WithId(string id) { TextStudioAppearanceActivation copy = Clone(); copy._id = id; return copy; }
        /// <summary>Returns an activation copy with the supplied inline or asset source and referenced asset.</summary>
        public TextStudioAppearanceActivation WithSource(TextStudioAppearanceDefinitionSource source, TextStudio2DStyleAsset asset) { TextStudioAppearanceActivation copy = Clone(); copy._source = source; copy._asset = asset; return copy; }
        /// <summary>Returns an activation copy with the supplied application mode and asset-mode override behavior.</summary>
        public TextStudioAppearanceActivation WithApplyMode(TextStudioAppearanceApplyMode mode, bool overrideMode = true) { TextStudioAppearanceActivation copy = Clone(); copy._applyMode = mode; copy._overrideApplyMode = overrideMode; return copy; }
        /// <summary>Returns an activation copy with an independently copied array of exposed parameter bindings.</summary>
        public TextStudioAppearanceActivation WithExposures(TextStudioAppearanceExposure[] exposures) { TextStudioAppearanceActivation copy = this; copy._exposures = CloneExposures(exposures); return copy; }
        /// <summary>Returns an activation copy with an independently copied exposure array.</summary>
        public TextStudioAppearanceActivation Clone() { TextStudioAppearanceActivation copy = this; copy._exposures = CloneExposures(_exposures); return copy; }
        internal static TextStudioAppearanceExposure[] CloneExposures(TextStudioAppearanceExposure[] source) => source == null || source.Length == 0 ? Array.Empty<TextStudioAppearanceExposure>() : (TextStudioAppearanceExposure[])source.Clone();

        /// <summary>Checks for an identifier beginning with a letter or underscore and continuing with letters, digits, underscores, hyphens, periods, or colons.</summary>
        public static bool IsValidId(string id)
        {
            if (string.IsNullOrEmpty(id) || !(char.IsLetter(id[0]) || id[0] == '_')) return false;
            for (int index = 1; index < id.Length; index++)
                if (!char.IsLetterOrDigit(id[index]) && id[index] != '_' && id[index] != '-' && id[index] != '.' && id[index] != ':') return false;
            return true;
        }
    }
}
