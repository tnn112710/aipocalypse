using UnityEditor;
using UnityEngine;

namespace TextStudio.Editor
{
    [FilePath("ProjectSettings/TextStudioRuntimePreparation.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class TextStudioRuntimePreparationSettings : ScriptableSingleton<TextStudioRuntimePreparationSettings>
    {
        [SerializeField] internal bool automaticInEditor = true;
        [SerializeField] internal bool automaticInBuilds = true;
        [SerializeField] internal bool embeddingChoiceMade;
        [SerializeField] internal bool allowFontEmbedding;
        [SerializeField] internal string additionalText = string.Empty;
        [SerializeField] internal TextStudioRuntimeCharacterSet runtimeCharacters;
        internal void Persist() => Save(true);
    }
}
