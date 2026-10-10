using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    public sealed partial class TextStudioText
    {
        readonly List<ITextStudioTextBackend> _textBackends =
            new List<ITextStudioTextBackend>();
        readonly TextStudioBackendLayoutBuilder _backendLayoutBuilder =
            new TextStudioBackendLayoutBuilder();

        ITextStudioTextBackend _activeTextBackend;
        ITextStudioTextSourceBackend _activeTextSourceBackend;
        ITextStudioLayoutBackend _activeLayoutBackend;
        ITextStudioMeshBackend _activeMeshBackend;
        bool _textBackendLifecycleActive;

        string ITextStudioBackendHost.ActiveBackendId => _activeTextBackend != null
            ? _activeTextBackend.BackendId
            : TextStudioTextBackendIds.TextMeshPro;

        ITextStudioTextBackend ITextStudioBackendHost.ActiveBackend => _activeTextBackend;
        IReadOnlyList<ITextStudioTextBackend> ITextStudioBackendHost.AvailableBackends => _textBackends;

        internal bool UsesCustomTextBackend => _activeTextBackend != null;
        internal bool SupportsActiveTextStudio2DAppearance =>
            SupportsTextStudio2DAppearance(_activeTextBackend);
        internal CompiledTextStudio2DStyle ActiveCompiledAppearanceStyle =>
            SupportsActiveTextStudio2DAppearance
                ? _compiledAppearanceStyle
                : null;

        void ITextStudioBackendHost.Refresh()
        {
            DiscoverTextBackends();
        }

        internal void DiscoverTextBackends()
        {
            ITextStudioTextBackend previous = _activeTextBackend;
            bool previousSupportsAppearance =
                SupportsTextStudio2DAppearance(previous);
            RefreshTextBackendList();
            ITextStudioTextBackend selected = SelectTextBackend();
            if (ReferenceEquals(previous, selected))
                return;
            bool selectedSupportsAppearance =
                SupportsTextStudio2DAppearance(selected);

            string initialText = GetTextBackendText(selected);
            bool adoptInitialText =
                string.IsNullOrEmpty(_markup) &&
                !string.IsNullOrEmpty(initialText);

            StopSelectedTextBackend();
            SetSelectedTextBackend(selected);
            if (_textBackendLifecycleActive)
            {
                StartSelectedTextBackend();
                if (adoptInitialText)
                    ImportCanonicalBackendSource(initialText);
            }

            DirtyFlags dirty =
                DirtyFlags.Text |
                DirtyFlags.Topology |
                DirtyFlags.Layout |
                DirtyFlags.Placement |
                DirtyFlags.Animation |
                DirtyFlags.Visual;
            if (previousSupportsAppearance != selectedSupportsAppearance)
                dirty |= DirtyFlags.Appearance;
            _runtime.SetDirty(dirty);

            if (_textBackendLifecycleActive)
                SyncTextImmediateForCurrentMode();

            if (!Application.isPlaying)
                TextStudioEditorHooks.RequestRepaint();
        }

        void InitializeTextBackendIntegration(MonoBehaviour[] components)
        {
            _textBackendLifecycleActive = true;
            RefreshTextBackendList(components);
            SetSelectedTextBackend(SelectTextBackend());
            StartSelectedTextBackend();
        }

        void DisposeTextBackendIntegration()
        {
            StopSelectedTextBackend();
            _textBackendLifecycleActive = false;
            _activeTextBackend = null;
            _activeTextSourceBackend = null;
            _activeLayoutBackend = null;
            _activeMeshBackend = null;
        }

        void RefreshTextBackendList()
        {
            RefreshTextBackendList(GetComponents<MonoBehaviour>());
        }

        void RefreshTextBackendList(MonoBehaviour[] components)
        {
            _textBackends.Clear();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is not ITextStudioTextBackend backend)
                    continue;
                if (!IsCompleteTextBackend(backend))
                    continue;
                _textBackends.Add(backend);
            }
        }

        ITextStudioTextBackend SelectTextBackend()
        {
            ITextStudioTextBackend best = null;
            for (int i = 0; i < _textBackends.Count; i++)
            {
                ITextStudioTextBackend backend = _textBackends[i];
                if (!IsUsableTextBackend(backend))
                    continue;

                if (best == null || backend.Priority > best.Priority)
                    best = backend;
            }

            return best;
        }

        string GetTextBackendText(ITextStudioTextBackend backend)
        {
            if (backend is ITextStudioTextSourceBackend sourceBackend)
                return sourceBackend.CurrentText ?? string.Empty;

            TmpBridge.FindTMP();
            return TmpBridge.TMPText != null
                ? TmpBridge.CurrentSourceText
                : string.Empty;
        }

        static bool IsCompleteTextBackend(ITextStudioTextBackend backend)
        {
            return backend is ITextStudioTextSourceBackend &&
                   backend is ITextStudioLayoutBackend &&
                   backend is ITextStudioMeshBackend;
        }

        static bool SupportsTextStudio2DAppearance(
            ITextStudioTextBackend backend)
        {
            return backend is not ITextStudioTextBackendAppearanceSupport support ||
                   support.SupportsTextStudio2DAppearance;
        }

        static bool IsUsableTextBackend(ITextStudioTextBackend backend)
        {
            if (backend == null || !backend.IsAvailable)
                return false;
            if (backend is UnityEngine.Object unityObject && unityObject == null)
                return false;
            if (backend is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                return false;
            return IsCompleteTextBackend(backend);
        }

        void SetSelectedTextBackend(ITextStudioTextBackend backend)
        {
            _activeTextBackend = backend;
            _activeTextSourceBackend = backend as ITextStudioTextSourceBackend;
            _activeLayoutBackend = backend as ITextStudioLayoutBackend;
            _activeMeshBackend = backend as ITextStudioMeshBackend;
        }

        void StartSelectedTextBackend()
        {
            if (_activeTextBackend == null)
            {
                TmpBridge.Initialize();
                TmpBridge.Subscribe();
                TmpBridge.OnRegenerated -= HandleTMPRegenerated;
                TmpBridge.OnRegenerated += HandleTMPRegenerated;
                return;
            }

            TmpBridge.FindTMP();
            _activeTextBackend.Changed -= HandleTextBackendChanged;
            _activeTextBackend.Changed += HandleTextBackendChanged;
            try
            {
                _activeTextBackend.Attach(this);
                if (_runtime.ReactionsRequireOutgoingCapture)
                    PrepareReactionCaptureProvider();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                _activeTextBackend.Changed -= HandleTextBackendChanged;
                SetSelectedTextBackend(null);
                TmpBridge.Initialize();
                TmpBridge.Subscribe();
                TmpBridge.OnRegenerated -= HandleTMPRegenerated;
                TmpBridge.OnRegenerated += HandleTMPRegenerated;
            }
        }

        void StopSelectedTextBackend()
        {
            if (_activeTextBackend == null)
            {
                TmpBridge.OnRegenerated -= HandleTMPRegenerated;
                TmpBridge.Unsubscribe();
                TmpBridge.RestoreVisibility();
                TmpBridge.Dispose();
                return;
            }

            _activeTextBackend.Changed -= HandleTextBackendChanged;
            try
            {
                _activeMeshBackend?.RestoreOutput();
                _activeTextBackend.Detach();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        void HandleTextBackendChanged(TextStudioTextBackendChange change)
        {
            bool pulledText = ProcessTextBackendPull();
            if (change == TextStudioTextBackendChange.Text || pulledText)
            {
                _runtime.SetDirty(
                    DirtyFlags.Text |
                    DirtyFlags.Topology |
                    DirtyFlags.Layout |
                    DirtyFlags.Placement |
                    DirtyFlags.Animation |
                    DirtyFlags.Visual);
            }
            else if (change == TextStudioTextBackendChange.Layout)
            {
                _runtime.SetDirty(
                    DirtyFlags.Topology |
                    DirtyFlags.Layout |
                    DirtyFlags.Placement |
                    DirtyFlags.Animation |
                    DirtyFlags.Visual);
            }
            else if (IsDrivingTextStudioOutput && !IsRenderingRawExternalOutput)
            {
                _runtime.SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
            }

            if (!Application.isPlaying)
                TextStudioEditorHooks.RequestRepaint();
        }

        bool HasPrimaryTextBackendTarget()
        {
            return _activeTextBackend != null
                ? IsUsableTextBackend(_activeTextBackend)
                : TmpBridge.TMPText != null;
        }

        string GetPrimaryTextBackendText()
        {
            if (_activeTextSourceBackend != null)
                return _activeTextSourceBackend.CurrentText ?? string.Empty;
            return TmpBridge.TMPText != null
                ? TmpBridge.CurrentSourceText
                : string.Empty;
        }

        bool HasUnobservedTextBackendChange =>
            _activeTextSourceBackend != null
                ? _activeTextSourceBackend.HasUnobservedTextChange
                : TmpBridge.HasUnobservedTextChange;

        bool IsTextBackendVisualOutputEnabled =>
            _activeMeshBackend != null
                ? _activeMeshBackend.IsVisualOutputEnabled
                : TmpBridge.IsVisualOutputEnabled;

        bool TextBackendNeedsComposedGlyphState =>
            _activeMeshBackend != null && _activeMeshBackend.NeedsComposedGlyphState;

        bool ProcessTextBackendPull()
        {
            return ProcessTextBackendPull(IsDrivingTextBackend);
        }

        bool ProcessTextBackendPull(bool isDriving)
        {
            TextStudioBackendWriteHandling handling = isDriving
                ? BackendWriteHandling
                : TextStudioBackendWriteHandling.ImportAsSource;

            if (_activeTextSourceBackend == null)
                return ProcessTMPTextPullCore(isDriving, handling);

            if (_activeTextSourceBackend.TryPullText(
                    isDriving,
                    handling,
                    _markup,
                    out string newMarkup,
                    out bool ignoredExternalWrite))
            {
                _lastIgnoredBackendWrite = string.Empty;
                ImportCanonicalBackendSource(newMarkup);
                return true;
            }

            if (ignoredExternalWrite)
                _lastIgnoredBackendWrite =
                    _activeTextSourceBackend.CurrentText ?? string.Empty;
            return false;
        }

        bool ProcessTMPTextPullCore(
            bool isDriving,
            TextStudioBackendWriteHandling handling)
        {
            if (TmpBridge.TryPullText(
                    isDriving,
                    handling,
                    _markup,
                    out string newMarkup,
                    out bool ignoredExternalWrite))
            {
                _lastIgnoredBackendWrite = string.Empty;
                ImportCanonicalBackendSource(newMarkup);
                return true;
            }

            if (ignoredExternalWrite)
                _lastIgnoredBackendWrite =
                    TmpBridge.CurrentSourceText;
            return false;
        }

        void ImportCanonicalBackendSource(string markup)
        {
            markup ??= string.Empty;
            if (string.Equals(_markup, markup, StringComparison.Ordinal))
                return;

            TextStudioEditorHooks.NotifyCanonicalSourceImporting(this);
            RequestTextChange(
                markup,
                false,
                false,
                preferIncrementalAppend: false,
                raiseTextSetEvent: false);
            if (!Application.isPlaying)
                TextStudioEditorHooks.RequestRepaint();
        }

        void PushTextBackend(bool isDriving)
        {
            if (_activeTextSourceBackend == null)
            {
                TmpBridge.SetLiteralMarkupRendering(IsRenderingRawExternalOutput);
                TmpBridge.PushText(isDriving, _runtime.Plan, _runtime.Markup);
                return;
            }

            TextStudioBackendTextRequest request = new TextStudioBackendTextRequest(
                _runtime.Plan,
                _runtime.Markup,
                isDriving,
                IsRenderingRawExternalOutput);
            _activeTextSourceBackend.PushText(in request);
        }

        void PushRawAuthoringTextBackend()
        {
            if (_activeTextSourceBackend != null)
            {
                _activeTextSourceBackend.PushRawAuthoringText(_markup);
                return;
            }

            TmpBridge.SyncRawTextImmediate(
                _markup,
                HasPrimaryOutputOwnerReusingBackendComponents());
        }

        bool TryBuildTextBackendLayout(
            bool cacheSourceVertexData,
            TextStudioSourceContextRequirements sourceContextRequirements)
        {
            if (_activeLayoutBackend == null)
            {
                return TmpBridge.RunLayout(
                    _runtime.Plan,
                    _layout,
                    cacheSourceVertexData,
                    _runtime.RequiresExternalMaterialEffect,
                    sourceContextRequirements,
                    ActiveCompiledAppearanceStyle,
                    _compiledWritingStyle,
                    ActiveCompiledAppearancePlan);
            }

            TextStudioBackendLayoutRequest request = new TextStudioBackendLayoutRequest(
                _runtime.Plan,
                cacheSourceVertexData,
                sourceContextRequirements);
            _backendLayoutBuilder.Reset(
                _layout,
                _runtime.Plan,
                sourceContextRequirements);
            return _activeLayoutBackend.TryBuildLayout(
                       in request,
                       _backendLayoutBuilder) &&
                   _backendLayoutBuilder.HasCommittedLayout;
        }

        void RenderTextBackend(in TextStudioFrame frame, bool suppressOutput)
        {
            if (_activeMeshBackend != null)
            {
                _activeMeshBackend.Render(in frame, suppressOutput);
                TmpBridge.UpdateVisibility(
                    true,
                    HasPrimaryOutputOwnerReusingBackendComponents());
                return;
            }

            bool applyPlacement =
                _placementMode != PlacementMode.Linear ||
                _placementRotation.HasEffect || _textChangeLayoutDuration > 0f;
            TmpBridge.RenderFrame(
                in frame,
                _presentationGlyphs,
                _glyphProvider,
                suppressOutput,
                applyPlacement);
        }

        void UpdateTextBackendVisibility(bool suppressOutput)
        {
            if (_activeMeshBackend != null)
            {
                TmpBridge.UpdateVisibility(
                    true,
                    HasPrimaryOutputOwnerReusingBackendComponents());
                return;
            }

            TmpBridge.UpdateVisibility(
                suppressOutput,
                HasPrimaryOutputOwnerReusingBackendComponents());
        }

        void ReleaseTextBackendMaterialEffects()
        {
            if (_activeMeshBackend != null)
                return;
            TmpBridge.ReleaseMaterialEffectsIfUnused(
                _runtime.Plan,
                _runtime.RequiresExternalMaterialEffect ||
                TextStudioWritingRenderer.RequiresWritingMaterial(
                    _runtime.Plan,
                    _compiledWritingStyle));
        }

        internal void PrepareForEditorSerialization()
        {
            TmpBridge.RestoreTransientMaterialState();
            // Forced world shadow modes are transient; the next layout after the save re-applies them.
            TmpBridge.RestoreWorldShadowState();
        }

        internal void ResumeAfterEditorSerialization()
        {
            if (!isActiveAndEnabled)
                return;

            ScheduleLayoutRebuild();
            TextStudioEditorHooks.RequestRepaint();
        }
    }
}
