namespace TextStudio
{
    /// <summary>Supplies frame-based advance input to reveal actions without binding Text Studio to an input system.</summary>
    public interface ITextStudioInputProvider
    {
        /// <summary>Reports whether any configured advance action was pressed during the current frame.</summary>
        /// <returns><see langword="true"/> only for a newly pressed advance action.</returns>
        bool WasAnyAdvancePressed();
    }

    /// <summary>Extends advance input with named action keys used by keyed wait tags.</summary>
    public interface ITextStudioKeyedInputProvider : ITextStudioInputProvider
    {
        /// <summary>Reports whether a specific advance action was pressed during the current frame.</summary>
        /// <param name="inputKey">Provider-defined action key. Null or empty input may select the provider default.</param>
        /// <returns><see langword="true"/> only when the requested action was newly pressed.</returns>
        bool WasAdvancePressed(string inputKey);
    }
}
