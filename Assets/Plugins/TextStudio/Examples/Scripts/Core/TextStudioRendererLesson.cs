using System;
using ImpossibleRobert.Common;
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
        [SerializeField, InspectorName("UI Panel"), Tooltip("Assign a UIDocument or, on Unity 6.5+, a PanelRenderer. An unassigned field discovers the host on this GameObject.")] Component _uiPanel;
        [SerializeField] TextStudioText _tmp, _legacy;
        readonly MessageData _data = new MessageData();
        TextStudioBinding _binding, _elementBinding, _dataBinding;
        TextField _input;
        readonly UIToolkitPanelRootTracker _panelRoot = new UIToolkitPanelRootTracker();
        VisualElement _root;
        VisualElement _initializedRoot;
        Component _boundHost;
        Component _initializedHost;
        int _rootVersion;
        int _initializedVersion;
        string _message = "SIGNAL FOUND";
        public Component UIPanel
        {
            get => _uiPanel;
            set
            {
                UIToolkitPanelHost.EnsureSupported(value, nameof(value));
                _uiPanel = value;
                if (isActiveAndEnabled) Bind();
            }
        }
        void OnValidate() => _uiPanel = UIToolkitPanelHost.Normalize(_uiPanel);
        public TextStudioBinding Binding => _binding;
        public TextStudioBinding ElementBinding => _elementBinding;
        public TextStudioBinding DataBinding => _dataBinding;
        public TextStudioText CanvasSubject => _tmp;
        public TextStudioText LegacySubject => _legacy;
        public TextField SourceInput => _input;
        public void Configure(Component uiPanel, TextStudioText tmp, TextStudioText legacy)
        {
            UIToolkitPanelHost.EnsureSupported(uiPanel, nameof(uiPanel));
            _uiPanel = uiPanel; _tmp = tmp; _legacy = legacy;
            if (isActiveAndEnabled) Bind();
        }
        void Start() => Bind();
        void OnEnable() => Bind();
        void Update() => Bind();
        void OnDisable() { Unbind(); _panelRoot.Release(); }
        void OnDestroy() { Unbind(); _panelRoot.Release(); }
        void Unbind()
        {
            _input?.UnregisterValueChangedCallback(InputChanged);
            _root?.UnregisterCallback<GeometryChangedEvent>(LayoutChanged);
            if (_dataBinding != null && ReferenceEquals(_dataBinding.dataSource, _data)) _dataBinding.dataSource = null;
            _input = null;
            _root = null;
            _boundHost = null;
            _binding = _elementBinding = _dataBinding = null;
        }
        void Bind()
        {
            Component host = _uiPanel != null ? _uiPanel : UIToolkitPanelHost.Find(gameObject);
            _panelRoot.SetHost(host);
            VisualElement root = _panelRoot.Root;
            int version = _panelRoot.RootVersion;
            if (!UIToolkitPanelHost.IsActive(host)) root = null;
            if (_boundHost == host && _root == root && _rootVersion == version) return;
            Unbind();
            _boundHost = host;
            _rootVersion = version;
            _root = root;
            if (_root?.Q<TextField>("renderer-source") == null) return;
            _input = _root.Q<TextField>("renderer-source");
            _binding = _root.Q<Label>("renderer-target")?.GetTextStudioBinding();
            _elementBinding = _root.Q<Label>("renderer-element-target")?.GetTextStudioBinding();
            _dataBinding = _root.Q<Label>("renderer-data-target")?.GetTextStudioBinding();
            if (_dataBinding != null)
            {
                _dataBinding.dataSource = _data;
                _dataBinding.dataSourcePath = new PropertyPath("Message");
            }
            _input.SetValueWithoutNotify(_message);
            if (_initializedHost != host || _initializedRoot != root || _initializedVersion != version)
            {
                _binding?.SetText(_message);
                _initializedHost = host;
                _initializedRoot = root;
                _initializedVersion = version;
            }
            _input.RegisterValueChangedCallback(InputChanged);
            _root.RegisterCallback<GeometryChangedEvent>(LayoutChanged);
            ApplyLayout();
        }
        void LayoutChanged(GeometryChangedEvent evt) => ApplyLayout();
        void ApplyLayout()
        {
            VisualElement root = _root;
            if (root == null) return;
            root.EnableInClassList("narrow", root.resolvedStyle.width / Mathf.Max(1, root.resolvedStyle.height) < 1.2f);
        }
        void InputChanged(ChangeEvent<string> evt) => SetMessage(evt.newValue);
        void SetMessage(string message)
        {
            // SetText follows each specimen's start policy, matching the source-driven routes: Element binding observes
            // the TextField directly and Data binding the typed property. Changed Glyphs reveals only what was typed.
            _message = message;
            _tmp?.SetText(message); _legacy?.SetText(message); _binding?.SetText(message);
            _data.Message = message;
        }
        public void SetDataMessage(string message) => _data.Message = message;
        public void CompleteReveal()
        {
            _tmp?.SkipReveal(); _legacy?.SkipReveal();
            _binding?.SkipReveal(); _elementBinding?.SkipReveal(); _dataBinding?.SkipReveal();
        }
    }
}
