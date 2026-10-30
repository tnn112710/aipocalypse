using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace VoxelLabs.UltimatePreview.UIElements
{
    /// <summary>
    /// Source-only toolbar UI. Ship as raw scripts so Toolbar and ToolbarToggle bind
    /// to the current Editor's assemblies, rather than the Editor used to build Core.
    /// </summary>
    public static class PromotionToolbarUIElements
    {
        private const string ToolbarName = "ultimateThumbnailsPromotionToolbar";

        public static void Refresh(EditorWindow window, bool visible, Action onEnable)
        {
            if (window == null) return;
            var root = window.rootVisualElement;
            var existing = root.Q(ToolbarName);
            if (!visible)
            {
                existing?.RemoveFromHierarchy();
                return;
            }
            if (existing != null) return;

            var toolbar = new Toolbar { name = ToolbarName };
            toolbar.style.position = Position.Absolute;
            toolbar.style.bottom = 0;
            toolbar.style.height = 20.5f;
            toolbar.style.right = 73;
            toolbar.style.paddingRight = 5;
            toolbar.style.flexDirection = FlexDirection.Row;
            var background = EditorGUIUtility.isProSkin
                ? new Color(0.25f, 0.25f, 0.25f, 1)
                : new Color(0.811f, 0.811f, 0.811f, 1);
            toolbar.style.backgroundColor = background;
            toolbar.style.borderBottomColor = background;

            var toggle = new ToolbarToggle
            {
                name = "enableUltimateThumbnails",
                value = false,
                text = "",
                tooltip = "Enable Ultimate Thumbnails (requires Ultimate Thumbnails)."
            };
            toggle.style.width = 30;
            var iconContainer = new VisualElement { pickingMode = PickingMode.Ignore };
            iconContainer.style.position = Position.Absolute;
            iconContainer.style.left = 0;
            iconContainer.style.right = 0;
            iconContainer.style.top = 0;
            iconContainer.style.bottom = 0;
            iconContainer.style.alignItems = Align.Center;
            iconContainer.style.justifyContent = Justify.Center;
            var icon = new Image
            {
                name = "ultimateThumbnailsPromotionIcon",
                image = EditorGUIUtility.IconContent("animationvisibilitytoggleon").image,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            icon.style.width = 16;
            icon.style.height = 16;
            iconContainer.Add(icon);
            toggle.Add(iconContainer);
            toggle.RegisterValueChangedCallback(evt =>
            {
                toggle.SetValueWithoutNotify(false);
                onEnable?.Invoke();
            });
            toolbar.Add(toggle);
            root.Add(toolbar);
        }

    }
}
