using VoxelLabs.Shared.Editor;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VoxelLabs.UltimateThumbnails.Lib;
using Toolbar = UnityEditor.UIElements.Toolbar;

namespace VoxelLabs.UltimateThumbnails.UIElements
{
    public static class ToolbarBottom
    {
        public const string ToolbarName = "voxel-labs-ultimate-thumbnails-toolbar";

        private static List<ToolbarData> toolbarDataList = new List<ToolbarData>();

        public static ToolbarData BottomToolbarGUI(bool isEnable, bool isProVersion, bool isEnhanceVisibilityOn, Color backgroundColor,
            Texture2D enhancedVisibilityEnabledIcon, Texture2D enhancedVisibilityDisabledIcon, Texture2D buyProTexture, ToolbarSettings toolbarSettings,
            bool isUltimateInspectorFieldsAvailable, bool showUltimateInspectorFieldsThumbnails, Action<Color> OnColorFieldValueChanged,
            Action<bool> OnVisibilityEnhancerToggleChanged, Action<bool> OnEnableToggleChanged,
            Action<bool> OnUltimateInspectorFieldsToggleChanged, bool? showInspectorFieldsToggle = null,
            Func<bool> showReviewButton = null, Action onReview = null, Texture2D reviewIcon = null,
            bool enableLazyLoading = true, Action<bool> onLazyLoadingChanged = null)
        {
            var toolbar = new Toolbar { name = ToolbarName };
            var toolbarData = new ToolbarData();
            
            toolbar.style.position = Position.Absolute;
            toolbar.style.bottom = 0;  
            toolbar.style.height = 20.5f;                  
            toolbar.style.right = 73;
            toolbar.style.paddingRight = 5;
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.justifyContent = Justify.FlexStart;
            toolbar.style.borderBottomColor = EditorGUIUtility.isProSkin ? new Color(0.25f, 0.25f, 0.25f, 1) : new Color(0.811f, 0.811f, 0.811f, 1.0f);
            toolbar.style.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.25f, 0.25f, 0.25f, 1) : new Color(0.811f, 0.811f, 0.811f, 1.0f);
            
            var colorField = new ColorField()
            {
                value = backgroundColor,
                style =
                {
                    width = 50,
                    display = isEnhanceVisibilityOn ? DisplayStyle.None : DisplayStyle.Flex,
                },
                tooltip = "Change background color" + (!isProVersion ? " (Pro-Only)" : ""),
                // enabledSelf = isProVersion // enabledSelf is read-only in 2021 hence using SetEnabled()
            };
            
            colorField.SetEnabled(isProVersion);

            colorField.RegisterValueChangedCallback(evt =>
            {
                OnColorFieldValueChanged?.Invoke(evt.newValue);
            });
            
            var visibilityEnhancerImage = new Image
            {
                image = isEnhanceVisibilityOn ? enhancedVisibilityEnabledIcon : enhancedVisibilityDisabledIcon,
                tooltip = "Enhance icons visibility"
            };
            
            // ToolbarToggle is the UI Toolkit equivalent of GUILayout.Toggle with toolbarButton
            var toggleVisibilityEnhancer = new ToolbarToggle {
                value = isEnhanceVisibilityOn,
                style = { width = 26 }
            };
            
            visibilityEnhancerImage.style.width = 16;
            visibilityEnhancerImage.style.height = 16;
            var visibilityEnhancerIconContainer = new VisualElement { pickingMode = PickingMode.Ignore };
            visibilityEnhancerIconContainer.style.position = Position.Absolute;
            visibilityEnhancerIconContainer.style.left = 0;
            visibilityEnhancerIconContainer.style.right = 0;
            visibilityEnhancerIconContainer.style.top = 0;
            visibilityEnhancerIconContainer.style.bottom = 0;
            visibilityEnhancerIconContainer.style.alignItems = Align.Center;
            visibilityEnhancerIconContainer.style.justifyContent = Justify.Center;
            visibilityEnhancerImage.pickingMode = PickingMode.Ignore;
            visibilityEnhancerIconContainer.Add(visibilityEnhancerImage);
            toggleVisibilityEnhancer.Add(visibilityEnhancerIconContainer);

            toggleVisibilityEnhancer.RegisterValueChangedCallback(evt =>
            {
                OnVisibilityEnhancerToggleChanged(evt.newValue);
            });
            
            var toggleEnable = new ToolbarToggle
            {
                text = "",
                value = isEnable,
                tooltip = "Toggles Ultimate Thumbnails."
            };
            
            var enableIcon = new Image
            {
                image = EditorGUIUtility.IconContent("animationvisibilitytoggleon").image
            };
            
            toggleEnable.style.width = 26;
            toggleEnable.Add(enableIcon);

            toggleEnable.RegisterValueChangedCallback(evt =>
            {
                OnEnableToggleChanged(evt.newValue);
            });

            var toggleLazyLoading = new ToolbarToggle
            {
                name = "toggleLazyLoading",
                value = enableLazyLoading,
                tooltip = "Toggle lazy loading. When enabled, thumbnails load over multiple Editor frames to keep the Editor smoother."
            };
            toggleLazyLoading.style.width = 26;
            toggleLazyLoading.style.display = isEnable && toolbarSettings.showLazyLoadingToggle
                ? DisplayStyle.Flex : DisplayStyle.None;
            var lazyLoadingImage = new Image
            {
                image = EditorGUIUtility.IconContent("WaitSpin04").image,
                pickingMode = PickingMode.Ignore,
                scaleMode = ScaleMode.ScaleToFit
            };
            lazyLoadingImage.style.width = 12.5f;
            lazyLoadingImage.style.height = 12.5f;
            var lazyLoadingIconContainer = new VisualElement { pickingMode = PickingMode.Ignore };
            lazyLoadingIconContainer.style.position = Position.Absolute;
            lazyLoadingIconContainer.style.left = 0;
            lazyLoadingIconContainer.style.right = 0;
            lazyLoadingIconContainer.style.top = 0;
            lazyLoadingIconContainer.style.bottom = 0;
            lazyLoadingIconContainer.style.alignItems = Align.Center;
            lazyLoadingIconContainer.style.justifyContent = Justify.Center;
            lazyLoadingIconContainer.Add(lazyLoadingImage);
            toggleLazyLoading.Add(lazyLoadingIconContainer);
            toggleLazyLoading.RegisterValueChangedCallback(evt => onLazyLoadingChanged?.Invoke(evt.newValue));

            var ultimateInspectorFieldsToggle = new ToolbarToggle
            {
                text = "",
                value = showUltimateInspectorFieldsThumbnails,
                tooltip = isUltimateInspectorFieldsAvailable
                    ? "Toggle Ultimate Inspector Fields thumbnails."
                    : "Ultimate Inspector Fields is not installed. Click for more information.",
                style =
                {
                    display = (showInspectorFieldsToggle ?? toolbarSettings.showUltimateInspectorFieldsToggle)
                        ? DisplayStyle.Flex
                        : DisplayStyle.None
                }
            };

            var ultimateInspectorFieldsIcon = new Image
            {
                image = EditorGUIUtility.IconContent("UnityEditor.InspectorWindow").image
            };

            ultimateInspectorFieldsToggle.Add(ultimateInspectorFieldsIcon);
            ultimateInspectorFieldsToggle.RegisterValueChangedCallback(evt =>
            {
                OnUltimateInspectorFieldsToggleChanged?.Invoke(evt.newValue);
            });

            toolbar.Add(ultimateInspectorFieldsToggle);

            if (!isProVersion)
            {
                var buyProButton = new ToolbarButton
                {
                    text = "",
                    tooltip = "Upgrade to PRO version."
                };
            
                var buyProIcon = new Image
                {
                    image = buyProTexture
                };
            
                buyProButton.Add(buyProIcon);
            
                buyProButton.clicked += () =>
                {
                    Application.OpenURL(AssetStoreLinks.GetUrl(AssetStoreLinks.UltimateThumbnails, "toolbar"));
                };

                toolbar.Add(buyProButton);
                
                toolbarData.buyProButton = buyProButton;
            }
            
            if (showReviewButton != null)
            {
                // Eligibility stays in Core; the source-only toolbar owns control construction and scheduling.
                var review = new ToolbarButton(() => onReview?.Invoke())
                    { name = "reviewUltimateThumbnails", tooltip = "Enjoying Ultimate Thumbnails? Share your experience on the Asset Store." };
                review.style.width = 26;
                review.style.display = DisplayStyle.None;
                var star = new Image { image = reviewIcon, pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit };
                star.style.width = 16;
                star.style.height = 16;
                review.style.alignItems = Align.Center;
                review.style.justifyContent = Justify.Center;
                review.Add(star);
                toolbar.Add(review);
                var reviewTimer = toolbar.schedule.Execute(() =>
                {
                    // Claim an offer only when its toolbar is actually attached and displayed.
                    if (toolbar.panel == null || !toolbar.visible || toolbar.resolvedStyle.display == DisplayStyle.None
                        || toolbar.worldBound.width <= 0 || toolbar.worldBound.height <= 0) return;
                    review.style.display = showReviewButton() ? DisplayStyle.Flex : DisplayStyle.None;
                }).Every(1000);
                toolbar.RegisterCallback<DetachFromPanelEvent>(_ => reviewTimer.Pause());
                toolbar.RegisterCallback<AttachToPanelEvent>(_ => reviewTimer.Resume());
            }

            toolbar.Add(colorField);
            toolbar.Add(toggleVisibilityEnhancer);
            toolbar.Add(toggleLazyLoading);
            toolbar.Add(toggleEnable);

            toolbarData.toolbar = toolbar;
            toolbarData.colorField = colorField;
            toolbarData.toggleVisibilityEnhancer = toggleVisibilityEnhancer;
            toolbarData.visibilityEnhancerImage = visibilityEnhancerImage;
            toolbarData.toggleEnable = toggleEnable;
            toolbarData.ultimateInspectorFieldsToggle = ultimateInspectorFieldsToggle;
            toolbarData.ultimateInspectorFieldsImage = ultimateInspectorFieldsIcon;
            
            // Closed Project windows must not remain rooted by the static update list.
            toolbar.RegisterCallback<DetachFromPanelEvent>(_ => toolbarDataList.Remove(toolbarData));
            toolbar.RegisterCallback<AttachToPanelEvent>(_ => {
                if (!toolbarDataList.Contains(toolbarData)) toolbarDataList.Add(toolbarData);
            });
            toolbarDataList.Add(toolbarData);

            return toolbarData;
        }
        
        public static void UpdateToolbar(bool isEnable, bool isEnhanceVisibilityOn, Color backgroundColor, 
            Texture2D enhancedVisibilityEnabledIcon, Texture2D enhancedVisibilityDisabledIcon, ToolbarSettings toolbarSettings,
            bool isUltimateInspectorFieldsAvailable, bool showUltimateInspectorFieldsThumbnails, bool? showInspectorFieldsToggle = null,
            bool enableLazyLoading = true)
        {
            foreach (var toolbarData in toolbarDataList)
            {
                if (toolbarData == null)
                    continue;

                var toolbar = toolbarData.toolbar as Toolbar;
                var toggleEnable = toolbarData.toggleEnable as ToolbarToggle;
                var toggleLazyLoading = toolbar.Q<ToolbarToggle>("toggleLazyLoading");
                var toggleVisibilityEnhancer = toolbarData.toggleVisibilityEnhancer as ToolbarToggle;
                var colorField = toolbarData.colorField as ColorField;
                var buyProButton = toolbarData.buyProButton as ToolbarButton;
                var ultimateInspectorFieldsToggle = toolbarData.ultimateInspectorFieldsToggle as ToolbarToggle;
                var ultimateInspectorFieldsImage = toolbarData.ultimateInspectorFieldsImage as Image;
                var visibilityEnhancerImage = toolbarData.visibilityEnhancerImage as Image;
            
                toolbar.style.display = toolbarSettings.showToolbar ? DisplayStyle.Flex : DisplayStyle.None;
            
                toggleEnable.SetValueWithoutNotify(isEnable);
                if (toggleLazyLoading != null)
                {
                    toggleLazyLoading.SetValueWithoutNotify(enableLazyLoading);
                    toggleLazyLoading.style.display = isEnable && toolbarSettings.showLazyLoadingToggle
                        ? DisplayStyle.Flex : DisplayStyle.None;
                }
                colorField.SetValueWithoutNotify(backgroundColor);
                toggleVisibilityEnhancer.SetValueWithoutNotify(isEnhanceVisibilityOn);
                visibilityEnhancerImage.image = isEnhanceVisibilityOn ? enhancedVisibilityEnabledIcon : enhancedVisibilityDisabledIcon;
            
                toggleEnable.style.display = toolbarSettings.showPreviewToggle ? DisplayStyle.Flex : DisplayStyle.None;
                toggleVisibilityEnhancer.style.display = isEnable ? DisplayStyle.Flex : DisplayStyle.None;
                colorField.style.display = (!isEnable || (isEnable && isEnhanceVisibilityOn)) ? DisplayStyle.None : DisplayStyle.Flex;
                if (buyProButton != null) 
                    buyProButton.style.display = isEnable ? DisplayStyle.Flex : DisplayStyle.None;

                if (ultimateInspectorFieldsToggle != null)
                {
                    ultimateInspectorFieldsToggle.SetValueWithoutNotify(showUltimateInspectorFieldsThumbnails);
                    ultimateInspectorFieldsToggle.tooltip = isUltimateInspectorFieldsAvailable
                        ? "Toggle Ultimate Inspector Fields thumbnails."
                        : "Ultimate Inspector Fields is not installed. Click for more information.";
                    ultimateInspectorFieldsToggle.style.display = (showInspectorFieldsToggle ?? toolbarSettings.showUltimateInspectorFieldsToggle)
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;

                    if (ultimateInspectorFieldsImage != null)
                    {
                        ultimateInspectorFieldsImage.image =
                            EditorGUIUtility.IconContent("UnityEditor.InspectorWindow").image;
                    }
                }
                
                if (!toolbarSettings.showVisibilityEnhancerToggle)
                    toggleVisibilityEnhancer.style.display = DisplayStyle.None;
                
                if (!toolbarSettings.showBgColorPicker)
                    colorField.style.display = DisplayStyle.None;   
            }
        }
    }
}
