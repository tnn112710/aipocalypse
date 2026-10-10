using System.Collections.Generic;

namespace TextStudio.Editor
{
    /// <summary>
    /// Editor extension point that contributes discoverable creation or setup cards to the Text Gallery.
    /// Implementations must have a parameterless constructor.
    /// </summary>
    public interface ITextStudioGalleryProvider
    {
        /// <summary>Gets the provider order used before contributed cards are displayed.</summary>
        int SortOrder { get; }

        /// <summary>Adds the provider's cards to the supplied mutable collection.</summary>
        /// <param name="cards">Shared collection for the current gallery reload. Do not retain it.</param>
        void AddCards(List<TextGalleryCard> cards);
    }
}
