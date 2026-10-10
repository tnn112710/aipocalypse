using System;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal sealed class TextStudioTagCardElement : VisualElement
    {
        readonly TextStudioTagBrowserItem _item;

        public TextStudioTagCardElement(TextStudioTagBrowserItem item, Action<TextStudioTagBrowserItem> choose)
        {
            _item = item;
            AddToClassList("ts-tag-card");

            bool guided = TextStudioGuidedTagDraft.CanConfigure(_item.Definition);
            Button selectButton = new Button(() => choose?.Invoke(_item))
            {
                name = "ts-tag-card-primary-action",
                tooltip = BuildTooltip(guided)
            };
            selectButton.AddToClassList("ts-tag-card-select");
            Add(selectButton);

            Label tagName = new Label("<" + _item.Definition.Id + ">");
            tagName.enableRichText = false;
            tagName.AddToClassList("ts-tag-card-name");
            selectButton.Add(tagName);

            VisualElement summary = new VisualElement();
            summary.AddToClassList("ts-tag-card-summary");
            selectButton.Add(summary);

            Label title = new Label(_item.Definition.DisplayName);
            title.AddToClassList("ts-tag-card-title");
            summary.Add(title);

            if (!string.IsNullOrEmpty(_item.Example))
            {
                Label example = new Label(_item.Example);
                example.enableRichText = false;
                example.AddToClassList("ts-tag-card-example");
                summary.Add(example);
            }

            Label action = new Label(guided ? "Configure  ›" : "Insert");
            action.AddToClassList("ts-tag-card-action");
            if (guided)
                action.AddToClassList("ts-tag-card-configure");
            selectButton.Add(action);
        }

        string BuildTooltip(bool guided)
        {
            string action = guided
                ? "Configure validated parameters and review the exact markup before inserting."
                : "Insert this tag using its recommended example.";
            if (string.IsNullOrEmpty(_item.Definition.Description))
                return action;

            if (string.IsNullOrEmpty(_item.Example))
                return _item.Definition.Description + "\n\n" + action;

            return _item.Definition.Description +
                   "\n\nExample: " +
                   _item.Example +
                   "\n\n" +
                   action;
        }
    }
}
