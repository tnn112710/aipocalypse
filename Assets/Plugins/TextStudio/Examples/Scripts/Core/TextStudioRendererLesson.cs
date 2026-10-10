using System;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>One editable message feeds two Canvas renderers and three native UI Toolkit source routes.</summary>
    [ExecuteAlways, DefaultExecutionOrder(100), UnityEngine.AddComponentMenu("")]
    public sealed class TextStudioRendererLesson : MonoBehaviour
    {
        sealed class MessageData : INotifyBindablePropertyChanged
        {
            string _message = "SIGNAL FOUND";
            public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;
            [CreateProperty]
            public string Message
            {
                get => _message;
                set
                {
                    if (_message == value) return;
                    _message = value;
                    propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(nameof(Message)));
                }
            }
        }
        [SerializeField] UIDocument _document;
        [SerializeField] TextStudioText _tmp, _legacy;
        readonly MessageData _data = new MessageData();
        TextStudioBinding _binding, _elementBinding, _dataBinding;
        TextField _input;
        public TextStudioBinding Binding => _binding;
        public TextStudioBinding ElementBinding => _elementBinding;
        public TextStudioBinding DataBinding => _dataBinding;
        public TextStudioText CanvasSubject => _tmp;
        public TextStudioText LegacySubject => _legacy;
        public TextField SourceInput => _input;
        public void Configure(UIDocument document, TextStudioText tmp, TextStudioText legacy)
        {
            _document = document; _tmp = tmp; _legacy = legacy;
            if (isActiveAndEnabled) Bind();
        }
        void Start() => Bind();
        void OnEnable() => Bind();
        void OnDisable()
        {
            if (_input != null) _input.UnregisterValueChangedCallback(InputChanged);
            if (_document != null && _document.rootVisualElement != null)
                _document.rootVisualElement.UnregisterCallback<GeometryChangedEvent>(LayoutChanged);
        }
        void Bind()
        {
            if (_document == null) return;
            VisualElement root = _document.rootVisualElement;
            if (root?.Q<TextField>("renderer-source") == null) return;
            _input = root.Q<TextField>("renderer-source");
            _binding = root.Q<Label>("renderer-target").GetTextStudioBinding();
            _elementBinding = root.Q<Label>("renderer-element-target").GetTextStudioBinding();
            _dataBinding = root.Q<Label>("renderer-data-target").GetTextStudioBinding();
            _dataBinding.dataSource = _data;
            _dataBinding.dataSourcePath = new PropertyPath("Message");
            _input.UnregisterValueChangedCallback(InputChanged);
            _input.RegisterValueChangedCallback(InputChanged);
            root.UnregisterCallback<GeometryChangedEvent>(LayoutChanged);
            root.RegisterCallback<GeometryChangedEvent>(LayoutChanged);
            ApplyLayout();
        }
        void LayoutChanged(GeometryChangedEvent evt) => ApplyLayout();
        void ApplyLayout()
        {
            VisualElement root = _document.rootVisualElement;
            root.EnableInClassList("narrow", root.resolvedStyle.width / Mathf.Max(1, root.resolvedStyle.height) < 1.2f);
        }
        void InputChanged(ChangeEvent<string> evt) => SetMessage(evt.newValue);
        void SetMessage(string message)
        {
            // SetText follows each specimen's start policy, matching the source-driven routes: Element binding observes
            // the TextField directly and Data binding the typed property. Changed Glyphs reveals only what was typed.
            _tmp.SetText(message); _legacy.SetText(message); _binding.SetText(message);
            _data.Message = message;
        }
        public void SetDataMessage(string message) => _data.Message = message;
        public void CompleteReveal()
        {
            _tmp.SkipReveal(); _legacy.SkipReveal();
            _binding.SkipReveal(); _elementBinding.SkipReveal(); _dataBinding.SkipReveal();
        }
    }
}
