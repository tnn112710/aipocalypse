namespace TextStudio
{
    /// <summary>Immutable authored selector scopes inherited by a resolved effect binding.</summary>
    internal sealed class TextStudioEffectSelectorChain
    {
        internal readonly TextStudioApplicationSelector Selector;
        internal readonly TextStudioEffectSelectorChain Parent;

        internal TextStudioEffectSelectorChain(TextStudioApplicationSelector selector,
            TextStudioEffectSelectorChain parent)
        {
            Selector = selector;
            Parent = parent;
        }
    }
}
