using ImpossibleRobert.Common;
using UnityEditor;

namespace TextStudio.Editor
{
    internal static class TextStudioAbout
    {
        [MenuItem(TextStudioMenuPaths.About, false, TextStudioMenuPaths.AboutPriority)]
        private static void ShowAbout()
        {
            AboutWindow.Show("TextStudio");
        }
    }
}
