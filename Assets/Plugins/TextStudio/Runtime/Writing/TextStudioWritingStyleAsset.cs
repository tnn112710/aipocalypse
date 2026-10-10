using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Reusable glyph drawing treatment evaluated by the Typewriter lifecycle.</summary>
    [CreateAssetMenu(
        fileName = "TextStudioWritingStyle",
        menuName = "Text Studio/Text Authoring/Writing Style", order = 107)]
#if UNITY_6000_7_OR_NEWER
    // Asset change subscriptions serve Edit Mode too; subscribers release their own leases.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public sealed partial class TextStudioWritingStyleAsset : ScriptableObject
    {
        public const int CurrentSchemaVersion = 4;

        [SerializeField] int _schemaVersion = CurrentSchemaVersion;
        [SerializeField] TextStudioWritingConfiguration _configuration =
            TextStudioWritingConfiguration.Default;
        [SerializeField] WritingFontAsset _fontAsset;
        [SerializeField] WritingPhraseAsset _phraseAsset;
        [SerializeField, Min(0.00001f)] float _flatteningTolerance = 0.0025f;
        [NonSerialized] int _revision;

        internal static event Action<TextStudioWritingStyleAsset> Changed;

#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
        static void ReleaseChangeSubscriptions() => Changed = null;
#endif

        public int SchemaVersion => _schemaVersion;
        public int Revision => _revision;
        public TextStudioWritingConfiguration Configuration =>
            _configuration.Sanitized();
        public WritingFontAsset FontAsset => _fontAsset;
        public WritingPhraseAsset PhraseAsset => _phraseAsset;
        public float FlatteningTolerance =>
            Mathf.Clamp(_flatteningTolerance, 0.00001f, 0.1f);

        /// <summary>Applies sanitized writing settings and source assets, clamps flattening tolerance, and advances the asset revision.</summary>
        public void SetConfiguration(
            TextStudioWritingConfiguration configuration,
            WritingFontAsset fontAsset = null,
            WritingPhraseAsset phraseAsset = null,
            float flatteningTolerance = 0.0025f)
        {
            _configuration = configuration.Sanitized();
            _fontAsset = fontAsset;
            _phraseAsset = phraseAsset;
            _flatteningTolerance = Mathf.Clamp(
                flatteningTolerance,
                0.00001f,
                0.1f);
            NotifyChanged();
        }

        void Reset()
        {
            _schemaVersion = CurrentSchemaVersion;
            _configuration = TextStudioWritingConfiguration.Default;
            _flatteningTolerance = 0.0025f;
            NotifyChanged();
        }

        void OnValidate()
        {
            _schemaVersion = CurrentSchemaVersion;
            _configuration = _configuration.Sanitized();
            _flatteningTolerance = Mathf.Clamp(
                _flatteningTolerance,
                0.00001f,
                0.1f);
            NotifyChanged();
        }

        void NotifyChanged()
        {
            unchecked
            {
                _revision++;
            }
            Changed?.Invoke(this);
        }
    }
}
