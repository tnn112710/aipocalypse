using UnityEngine;

namespace TextStudio
{
    /// <summary>Serialized base for leaf and expansion-only effect definitions.</summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public abstract partial class TextStudioEffectDefinitionAsset : ScriptableObject,
        ITextStudioEffectDefinition,
        ITextStudioEffectCapabilitiesProvider
    {
        static uint s_ContentRevision;

        public abstract string Id { get; }
        public abstract TextStudioEffectAreaSettings DefaultArea { get; }
        public abstract EffectBlendMode BlendMode { get; }
        public virtual TextStudioEffectCapabilities Capabilities => new TextStudioEffectCapabilities(
            TextStudioRenderChannels.All,
            TextStudioPhaseMask.All);

        internal static uint ContentRevision => s_ContentRevision;

        protected virtual void OnValidate()
        {
            NotifyEffectContentChanged();
        }

        internal static void NotifyEffectContentChanged()
        {
            unchecked
            {
                s_ContentRevision++;
            }
            if (s_ContentRevision == 0)
                s_ContentRevision = 1;
        }
    }
}
