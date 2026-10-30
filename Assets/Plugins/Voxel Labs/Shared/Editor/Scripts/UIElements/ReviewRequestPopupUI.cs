using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VoxelLabs.Shared.UIElements
{
    /// <summary>Source-only popup UI; eligibility, preferences, URLs and product logic remain in the DLL.</summary>
    public static class ReviewRequestPopupUI
    {
        public static void Build(EditorWindow window, string productTitle, bool remindLater,
            Action<bool> reminderChanged, Action writeReview, Action close)
        {
            var root = window.rootVisualElement;
            root.Clear();
            root.AddToClassList("missing-asset-popup");
            var sheet = Resources.Load<StyleSheet>("VoxelLabs.Shared/MissingAssetPopup");
            if (sheet != null) root.styleSheets.Add(sheet);

            var content = new ScrollView();
            content.AddToClassList("missing-asset-content");
            // Keep the text and actions close together without changing Missing Asset popup styling.
            content.style.paddingBottom = 0;
            root.Add(content);
            // Use the registered asset name in the shared review heading.
            var heading = new Label($"Would you like to review {productTitle}");
            heading.AddToClassList("missing-asset-heading");
            content.Add(heading);
            var description = new Label("We’d be grateful if you could take a moment to share your experience on the Unity Asset Store. Your feedback means a lot and helps guide future updates.");
            description.AddToClassList("missing-asset-description");
            content.Add(description);
            var reminder = new Toggle("Remind me later") { name = "reviewRemindLater" };
            reminder.SetValueWithoutNotify(remindLater);
            reminder.tooltip = "Close: remind in 10 days when enabled, or 60 days when disabled. Write a Review: remind in 3 months.";
            reminder.RegisterValueChangedCallback(evt => reminderChanged?.Invoke(evt.newValue));
            content.Add(reminder);
            var buttons = new VisualElement();
            buttons.AddToClassList("missing-asset-buttons");
            buttons.style.marginTop = 4;
            root.Add(buttons);
            buttons.Add(new Button(() => writeReview?.Invoke()) { name = "writeReview", text = "Write a Review" });
            buttons.Add(new Button(() => close?.Invoke()) { name = "closeReview", text = "Close" });
        }
    }
}
