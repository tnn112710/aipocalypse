using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio
{
    /// <summary>A scene-local world or Canvas target referenced by reaction layers through a key.</summary>
    [Serializable]
    public sealed class TextStudioReactionWorldTargetBinding
    {
        [SerializeField] string _key;
        [SerializeField] Transform _target;

        public string Key { get => _key ?? string.Empty; set => _key = value ?? string.Empty; }
        public Transform Target { get => _target; set => _target = value; }
    }

    /// <summary>A UI Toolkit target resolved within the binding's current panel.</summary>
    [Serializable, UxmlObject]
    public sealed partial class TextStudioReactionPanelTargetBinding
    {
        [SerializeField] string _key;
        [SerializeField] string _elementName;

        [UxmlAttribute("key")]
        public string Key { get => _key ?? string.Empty; set => _key = value ?? string.Empty; }
        [UxmlAttribute("element-name")]
        public string ElementName { get => _elementName ?? string.Empty; set => _elementName = value ?? string.Empty; }
    }
}
