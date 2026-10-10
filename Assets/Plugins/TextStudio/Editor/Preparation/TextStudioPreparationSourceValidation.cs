using System.Collections.Generic;
using UnityEditor;

namespace TextStudio.Editor
{
    [InitializeOnLoad]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class TextStudioPreparationSourceValidation
    {
        static readonly Dictionary<TextStudioFontAppearanceData, (uint revision, bool valid)> s_results = new Dictionary<TextStudioFontAppearanceData, (uint, bool)>();
        static TextStudioPreparationSourceValidation()
        {
            TextStudioFontAppearanceData.EditorSourceValidator = IsCurrent;
            EditorApplication.projectChanged += Invalidate;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.quitting += Shutdown;
        }
        internal static void Invalidate() => s_results.Clear();
        static bool IsCurrent(TextStudioFontAppearanceData data)
        {
            if (s_results.TryGetValue(data, out (uint revision, bool valid) result) && result.revision == data.Revision) return result.valid;
            bool valid = TextStudioFontAppearanceBuildValidation.ValidateAsset(data, out _);
            s_results[data] = (data.Revision, valid);
            return valid;
        }
        static void Shutdown() { TextStudioFontAppearanceData.EditorSourceValidator = null; s_results.Clear(); }
    }
}
