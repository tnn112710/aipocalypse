using System;
using UnityEngine;

namespace TextStudio.Editor
{
    internal sealed class TextStudioRecipe
    {
        readonly Func<GameObject> _create;
        readonly Func<bool> _isAvailable;

        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        public string ActionLabel { get; }
        public string UnavailableMessage { get; }
        public bool IsAvailable => _isAvailable == null || _isAvailable();

        public TextStudioRecipe(
            string id,
            string title,
            string description,
            Func<GameObject> create,
            string actionLabel = "Create",
            Func<bool> isAvailable = null,
            string unavailableMessage = "")
        {
            Id = id ?? string.Empty;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            ActionLabel = string.IsNullOrWhiteSpace(actionLabel) ? "Create" : actionLabel;
            UnavailableMessage = unavailableMessage ?? string.Empty;
            _create = create;
            _isAvailable = isAvailable;
        }

        public GameObject Create()
        {
            return IsAvailable ? _create?.Invoke() : null;
        }
    }
}
