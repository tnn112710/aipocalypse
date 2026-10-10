namespace TextStudio
{
    /// <summary>
    /// Marker interface for any component that programmatically drives the text
    /// on a <see cref="TextStudioText"/> hub (localization bridges, data bindings,
    /// scripted sequences, ...). Enabled components on the same GameObject, or
    /// components explicitly registered with the hub, flip it out of "mirror
    /// TMP.text" mode into "drive TMP.text" mode, so the programmatically-supplied
    /// string wins over whatever a user might have typed into TMP's own text field.
    ///
    /// Components that just observe or animate Text Studio do NOT implement this
    /// interface - only those that push text content into the hub.
    /// </summary>
    public interface ITextStudioExternalTextSource
    {
    }
}
