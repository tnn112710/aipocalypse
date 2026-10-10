#if UNITY_6000_3_OR_NEWER
using ImpossibleRobert.Common;
using UnityEditor.Toolbars;
using UnityEngine;

namespace TextStudio.Editor
{
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class ToolbarIntegration
    {
        private static Texture2D _icon32;
        private static Texture2D ToolbarIcon
        {
            get
            {
                if (_icon32 == null) _icon32 = CommonUIStyles.LoadTexture("text-studio-icon32");
                return _icon32;
            }
        }

        [MainToolbarElement("Text Studio/Text Gallery", defaultDockPosition = MainToolbarDockPosition.Left)]
        public static MainToolbarElement TextGalleryButton()
        {
            MainToolbarContent content = new MainToolbarContent(ToolbarIcon)
            {
                text = "Gallery",
                tooltip = "Open Text Studio Gallery"
            };

            return new MainToolbarButton(content, TextGalleryWindow.ShowWindow);
        }
    }
}
#endif
