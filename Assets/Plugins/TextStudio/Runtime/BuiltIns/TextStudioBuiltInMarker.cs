namespace TextStudio.BuiltIns
{
    /// <summary>
    /// Implemented by code-defined built-ins so editor and runtime tooling can read common metadata.
    /// </summary>
    public interface ITextStudioBuiltIn
    {
        string Id { get; }
        string Category { get; }
        string DisplayName { get; }
        string Description { get; }
    }
}
