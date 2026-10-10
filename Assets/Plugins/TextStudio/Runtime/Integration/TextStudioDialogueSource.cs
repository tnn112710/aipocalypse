using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TextStudio
{
    /// <summary>
    /// Generic bridge for narrative, dialogue, subtitle, and speech-bubble systems.
    /// Third-party tools can call this component without taking a package dependency
    /// on their APIs; Text Studio remains responsible for reveal, tags, effects, and
    /// renderer output.
    /// </summary>
    [AddComponentMenu("Text Studio/Integrations/Dialogue Source")]
    [HelpURL("https://www.wetzold.com/tools/text-studio")]
    /// <summary>
    /// Scene text source that preprocesses dialogue markup, controls reveal, and exposes advance-or-skip behavior.
    /// </summary>
    public sealed class TextStudioDialogueSource : MonoBehaviour, ITextStudioExternalTextSource
    {
        [SerializeField] TextStudioText _target;
        [SerializeField] bool _restartRevealOnLine = true;
        [SerializeField] bool _clearTextOnDisable;
        [SerializeField] List<MonoBehaviour> _markupPreprocessors = new List<MonoBehaviour>();
        [SerializeField] UnityEvent<string> _onLineShown = new UnityEvent<string>();
        [SerializeField] UnityEvent _onLineComplete = new UnityEvent();
        [SerializeField] UnityEvent _onAdvanceRequested = new UnityEvent();

        readonly List<ITextStudioMarkupPreprocessor> _registeredMarkupPreprocessors =
            new List<ITextStudioMarkupPreprocessor>();
        readonly List<ITextStudioMarkupPreprocessor> _preprocessorScratch =
            new List<ITextStudioMarkupPreprocessor>();

        string _sourceLine = string.Empty;
        string _currentLine = string.Empty;
        TextStudioText _subscribedTarget;

        public TextStudioText Target
        {
            get => ResolveTarget();
            set => SetTarget(value);
        }

        public TextStudioText ConfiguredTarget => _target;

        public string SourceLine => _sourceLine;
        public string CurrentLine => _currentLine;
        public bool RestartRevealOnLine
        {
            get => _restartRevealOnLine;
            set => _restartRevealOnLine = value;
        }

        public UnityEvent<string> OnLineShown => _onLineShown;
        public UnityEvent OnLineComplete => _onLineComplete;
        public UnityEvent OnAdvanceRequested => _onAdvanceRequested;

        /// <summary>Registers a dialogue-local markup transform in invocation order.</summary>
        public void RegisterMarkupPreprocessor(ITextStudioMarkupPreprocessor preprocessor)
        {
            if (TextStudioMarkupPreprocessorChain.IsNull(preprocessor) ||
                _registeredMarkupPreprocessors.Contains(preprocessor))
            {
                return;
            }

            _registeredMarkupPreprocessors.Add(preprocessor);
        }

        /// <summary>Stops applying a previously registered dialogue-local markup transform.</summary>
        /// <returns><see langword="true"/> when the processor was registered.</returns>
        public bool UnregisterMarkupPreprocessor(ITextStudioMarkupPreprocessor preprocessor)
        {
            if (TextStudioMarkupPreprocessorChain.IsNull(preprocessor))
                return false;

            return _registeredMarkupPreprocessors.Remove(preprocessor);
        }

        void Reset()
        {
            if (_target == null)
                _target = GetComponent<TextStudioText>();
        }

        void OnEnable()
        {
            TextStudioText target = ResolveTarget();
            if (target == null) return;

            SubscribeTarget(target);
        }

        void OnDisable()
        {
            TextStudioText target = _subscribedTarget;
            UnsubscribeTarget();

            if (_clearTextOnDisable)
                target?.ClearText();
        }

        void OnValidate()
        {
            if (!isActiveAndEnabled || _subscribedTarget == _target)
                return;

            UnsubscribeTarget();
            if (_target != null)
                SubscribeTarget(_target);
        }

        /// <summary>Replaces the current line, preprocesses it, and starts reveal playback.</summary>
        public void ShowLine(string markup)
        {
            SetLine(markup, _restartRevealOnLine);
        }

        /// <summary>Replaces and preprocesses the current line with explicit reveal-restart policy.</summary>
        public void SetLine(string markup, bool restartReveal)
        {
            _sourceLine = markup ?? string.Empty;
            TextStudioText target = ResolveTarget();
            _currentLine = ApplyMarkupPreprocessors(_sourceLine, target, false);
            if (target == null) return;

            target.SetText(_currentLine);
            target.DiscoverRenderersAndProviders();
            if (restartReveal)
                target.StartReveal();

            _onLineShown.Invoke(_currentLine);
        }

        /// <summary>Replaces and preprocesses the line, revealing changed glyphs while retaining matched text and the target's authored settings.</summary>
        public void ShowChangedLine(string markup)
        {
            _sourceLine = markup ?? string.Empty;
            TextStudioText target = ResolveTarget();
            _currentLine = ApplyMarkupPreprocessors(_sourceLine, target, false);
            if (target == null) return;

            target.ShowText(_currentLine, TextStudioTextChangeScope.ChangedGlyphs);
            target.DiscoverRenderersAndProviders();
            _onLineShown.Invoke(_currentLine);
        }

        /// <summary>Preprocesses and appends markup while preserving the existing prefix's reveal state.</summary>
        public void AppendToLine(string markup)
        {
            if (string.IsNullOrEmpty(markup)) return;
            _sourceLine += markup;

            TextStudioText target = ResolveTarget();
            if (HasMarkupPreprocessors())
                _currentLine = ApplyMarkupPreprocessors(_sourceLine, target, true);
            else
                _currentLine += markup;
            if (target == null) return;

            if (HasMarkupPreprocessors())
            {
                string previousMarkup = target.Markup ?? string.Empty;
                if (_currentLine.StartsWith(previousMarkup, StringComparison.Ordinal))
                    target.AppendText(_currentLine.Substring(previousMarkup.Length));
                else
                    target.SetText(_currentLine, TextStudioTextChangeScope.ChangedGlyphs);
            }
            else
                target.AppendText(markup);
            target.DiscoverRenderersAndProviders();
        }

        /// <summary>Clears source, processed line, and target output.</summary>
        public void ClearLine()
        {
            _sourceLine = string.Empty;
            _currentLine = string.Empty;
            TextStudioText target = ResolveTarget();
            if (target == null) return;

            target.ClearText();
            target.DiscoverRenderersAndProviders();
        }

        /// <summary>
        /// Production dialogue "continue" behavior: first call completes the current
        /// reveal/wait, second call asks the owning dialogue system for the next line.
        /// Returns true only when the caller should advance externally.
        /// </summary>
        public bool AdvanceOrSkip()
        {
            TextStudioText target = ResolveTarget();
            if (target != null && !target.AdvanceOrSkip())
                return false;

            _onAdvanceRequested.Invoke();
            return true;
        }

        TextStudioText ResolveTarget()
        {
            if (_target != null)
                return _target;

            SetTarget(GetComponent<TextStudioText>());
            return _target;
        }

        void SetTarget(TextStudioText target)
        {
            if (_target == target)
                return;

            UnsubscribeTarget();

            _target = target;

            if (isActiveAndEnabled && _target != null)
                SubscribeTarget(_target);
        }

        void SubscribeTarget(TextStudioText target)
        {
            if (_subscribedTarget == target)
                return;

            UnsubscribeTarget();

            if (target == null)
                return;

            target.OnShowComplete += HandleLineComplete;
            target.RegisterExternalTextSource(this);
            _subscribedTarget = target;
        }

        void UnsubscribeTarget()
        {
            if (_subscribedTarget == null)
                return;

            _subscribedTarget.OnShowComplete -= HandleLineComplete;
            _subscribedTarget.UnregisterExternalTextSource(this);
            _subscribedTarget = null;
        }

        void HandleLineComplete()
        {
            _onLineComplete.Invoke();
        }

        string ApplyMarkupPreprocessors(string markup, TextStudioText target, bool isAppend)
        {
            BuildMarkupPreprocessorList();
            TextStudioMarkupPreprocessContext context = new TextStudioMarkupPreprocessContext(
                this,
                target,
                nameof(TextStudioDialogueSource),
                isAppend);
            return TextStudioMarkupPreprocessorChain.Apply(markup, _preprocessorScratch, in context);
        }

        bool HasMarkupPreprocessors()
        {
            BuildMarkupPreprocessorList();
            return _preprocessorScratch.Count > 0;
        }

        void BuildMarkupPreprocessorList()
        {
            _preprocessorScratch.Clear();

            for (int i = 0; i < _markupPreprocessors.Count; i++)
            {
                MonoBehaviour behaviour = _markupPreprocessors[i];
                if (behaviour == null || !behaviour.isActiveAndEnabled)
                    continue;

                AddMarkupPreprocessor(behaviour as ITextStudioMarkupPreprocessor);
            }

            for (int i = 0; i < _registeredMarkupPreprocessors.Count; i++)
                AddMarkupPreprocessor(_registeredMarkupPreprocessors[i]);
        }

        void AddMarkupPreprocessor(ITextStudioMarkupPreprocessor preprocessor)
        {
            if (TextStudioMarkupPreprocessorChain.IsNull(preprocessor) ||
                _preprocessorScratch.Contains(preprocessor))
            {
                return;
            }

            _preprocessorScratch.Add(preprocessor);
        }
    }
}
