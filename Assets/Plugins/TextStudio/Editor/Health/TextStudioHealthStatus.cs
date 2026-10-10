using System.Collections.Generic;
using UnityEngine;

namespace TextStudio.Editor
{
    internal enum TextStudioHealthSeverity
    {
        Info,
        Warning,
        Error
    }

    internal readonly struct TextStudioHealthItem
    {
        public readonly TextStudioHealthSeverity Severity;
        public readonly string Title;
        public readonly string Detail;
        public readonly string Action;
        public readonly Object Target;

        public TextStudioHealthItem(
            TextStudioHealthSeverity severity,
            string title,
            string detail,
            string action,
            Object target = null)
        {
            Severity = severity;
            Title = title ?? string.Empty;
            Detail = detail ?? string.Empty;
            Action = action ?? string.Empty;
            Target = target;
        }
    }

    internal sealed class TextStudioHealthStatus
    {
        readonly List<TextStudioHealthItem> _items = new List<TextStudioHealthItem>();

        public IReadOnlyList<TextStudioHealthItem> Items => _items;
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }

        public void Add(
            TextStudioHealthSeverity severity,
            string title,
            string detail,
            string action,
            Object target = null)
        {
            _items.Add(new TextStudioHealthItem(severity, title, detail, action, target));
            if (severity == TextStudioHealthSeverity.Error) ErrorCount++;
            if (severity == TextStudioHealthSeverity.Warning) WarningCount++;
        }
    }
}
