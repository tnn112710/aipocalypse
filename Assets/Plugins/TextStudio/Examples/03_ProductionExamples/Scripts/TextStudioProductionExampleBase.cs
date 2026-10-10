using UnityEngine;

namespace TextStudio.Examples.Production
{
    /// <summary>
    /// Marker base for the production examples. The shipped scenes own their complete hierarchy;
    /// editor-only builders in TextStudio.Debug are responsible for generating it.
    /// </summary>
    public abstract class TextStudioProductionExampleBase : MonoBehaviour
    {
        protected virtual void Update()
        {
        }
    }
}
