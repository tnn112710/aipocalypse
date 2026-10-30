using System;
using UnityEngine;

namespace VoxelLabs.UltimateThumbnails.Lib
{
    [Serializable]
    public class ToolbarSettings
    {
        [Tooltip("Shows or hides the entire toolbar.")]
        public bool showToolbar = true;
        
        [Tooltip("Shows or hides the toggle button for enabling/disabling Ultimate Thumbnails' custom icons.")]
        public bool showPreviewToggle = true;

        [Tooltip("Shows or hides the lazy loading toggle on the toolbar. Hiding this button does not change whether lazy loading is enabled.")]
        public bool showLazyLoadingToggle = false;
        
        [Tooltip("Shows or hides the toggle button for enhancing visibility.")]
        public bool showVisibilityEnhancerToggle = true;
        
        [Tooltip("Shows or hides the background color picker tool on the toolbar.")]
        public bool showBgColorPicker = true;

        [Tooltip("Shows or hides the Ultimate Inspector Fields toggle on the toolbar.")]
        public bool showUltimateInspectorFieldsToggle = true;
    }
}
