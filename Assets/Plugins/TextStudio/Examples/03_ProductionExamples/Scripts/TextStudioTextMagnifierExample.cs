using ImpossibleRobert.Common.Timing;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TextStudio.Examples.Production
{
    [ExecuteAlways]
    [DefaultExecutionOrder(-500)]
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class TextStudioTextMagnifierExample : TextStudioProductionExampleBase,
        ITextStudioEditorTickTarget
    {
        [Header("Magnifier Effect")]
        [SerializeField] TextStudioComposedEffectAsset _magnifierEffect;
        [SerializeField] TextStudioText _magnifiedText;
        [SerializeField] RectTransform _magnifierTarget;
        [SerializeField] RectTransform _dragBounds;
        [SerializeField, TextArea(5, 12)] string _documentMarkup;

        [Header("Presentation")]
        [SerializeField] TMP_Text _status;
        [SerializeField] TMP_Text _autoScanButtonLabel;
        [SerializeField] Vector2 _homeNormalizedPosition = new Vector2(0.42f, 0.58f);
        [SerializeField] Vector2 _normalizedPosition = new Vector2(0.42f, 0.58f);
        [SerializeField] bool _autoScanEnabled = true;
        [SerializeField] bool _scopedFallback;
        [SerializeField] TMP_Text _renderingModeLabel;

        ITextStudioEffectDefinition _registeredEffect;
        ITextStudioEffectDefinition _previousEffect;
        Vector2 _autoScanCenter;
        float _autoScanTime;
        double _lastHostTime;
        bool _dragging;
        TextStudioMotionPolicy _lastMotionPolicy;

        public TextStudioComposedEffectAsset MagnifierEffect => _magnifierEffect;
        public TextStudioText MagnifiedText => _magnifiedText;
        public RectTransform MagnifierTarget => _magnifierTarget;
        public RectTransform DragBounds => _dragBounds;
        public string DocumentMarkup => _documentMarkup;
        public Vector2 HomeNormalizedPosition => _homeNormalizedPosition;
        public Vector2 NormalizedPosition => _normalizedPosition;
        public bool AutoScanEnabled => _autoScanEnabled;
        public bool ScopedFallback => _scopedFallback;

        public void ConfigureRenderingComparison(TMP_Text modeLabel)
        {
            _renderingModeLabel = modeLabel;
            SetScopedFallback(false);
        }

        public void ToggleRenderingMode() => SetScopedFallback(!_scopedFallback);

        public void SetScopedFallback(bool enabled)
        {
            _scopedFallback = enabled;
            RestoreSceneState();
        }

        public void ConfigureScene(
            TextStudioComposedEffectAsset magnifierEffect,
            TextStudioText magnifiedText,
            RectTransform magnifierTarget,
            RectTransform dragBounds,
            TMP_Text status,
            TMP_Text autoScanButtonLabel,
            string documentMarkup,
            Vector2 homeNormalizedPosition,
            bool autoScanEnabled = true)
        {
            UnregisterEffect();
            _magnifierEffect = magnifierEffect;
            _magnifiedText = magnifiedText;
            _magnifierTarget = magnifierTarget;
            _dragBounds = dragBounds;
            _status = status;
            _autoScanButtonLabel = autoScanButtonLabel;
            _documentMarkup = documentMarkup ?? string.Empty;
            _homeNormalizedPosition = ClampNormalized(homeNormalizedPosition);
            _normalizedPosition = _homeNormalizedPosition;
            _autoScanEnabled = autoScanEnabled;
            _autoScanCenter = _normalizedPosition;
            _autoScanTime = 0f;
            RegisterEffect();
            RestoreSceneState();
        }

        void OnEnable()
        {
            HostTime.Reset(ref _lastHostTime, ClockMode.Unscaled);
            HostTime.RequestEditorFrame(this, repaintAllViews: false);
            RegisterEffect();
            _autoScanCenter = _normalizedPosition;
            _autoScanTime = 0f;
            RestoreSceneState();
            if (!Application.isPlaying)
                TextStudioEditorHooks.NotifyEditorTickTargetEnabled(this);
        }

        void OnDisable()
        {
            TextStudioEditorHooks.NotifyEditorTickTargetDisabled(this);
            RestorePlainDocument();
            if (Application.isPlaying &&
                _magnifiedText != null &&
                _magnifiedText.InfluenceTarget == _magnifierTarget)
            {
                _magnifiedText.InfluenceTarget = null;
            }
            UnregisterEffect();
        }

        protected override void Update()
        {
            if (!Application.isPlaying) return;
            float deltaTime = HostTime.GetDeltaTime(ref _lastHostTime, clockMode: ClockMode.Unscaled);
            AdvanceMotion(deltaTime);
        }

        bool ITextStudioEditorTickTarget.AdvanceEditorPreview(float deltaTime) =>
            AdvanceMotion(deltaTime);

        bool AdvanceMotion(float deltaTime)
        {
            bool changed = false;
            if (_lastMotionPolicy != TextStudioMotionSettings.GlobalPolicy)
            {
                _lastMotionPolicy = TextStudioMotionSettings.GlobalPolicy;
                RefreshPresentation();
                changed = true;
            }
            if (!_autoScanEnabled || _dragging || _dragBounds == null ||
                TextStudioMotionSettings.GlobalPolicy == TextStudioMotionPolicy.Reduced ||
                TextStudioMotionSettings.GlobalPolicy == TextStudioMotionPolicy.None)
                return changed;

            AdvanceAutoScan(deltaTime);
            return deltaTime > 0f;
        }

        public void ToggleAutoScan()
        {
            _autoScanEnabled = !_autoScanEnabled;
            if (_autoScanEnabled)
            {
                HostTime.Reset(ref _lastHostTime, ClockMode.Unscaled);
                HostTime.RequestEditorFrame(this, repaintAllViews: false);
                _autoScanCenter = _normalizedPosition;
                _autoScanTime = 0f;
            }
            RefreshPresentation();
        }

        public void ResetMagnifier()
        {
            _autoScanEnabled = false;
            _dragging = false;
            _autoScanTime = 0f;
            _autoScanCenter = _homeNormalizedPosition;
            SetMagnifierNormalizedPosition(_homeNormalizedPosition);
            RefreshPresentation();
        }

        public void SetMagnifierNormalizedPosition(Vector2 normalizedPosition)
        {
            _normalizedPosition = ClampNormalized(normalizedPosition);
            ApplyNormalizedPosition();
        }

        internal void BeginPointerDrag(PointerEventData eventData)
        {
            _dragging = true;
            _autoScanEnabled = false;
            SetPointerPosition(eventData);
            RefreshPresentation();
        }

        internal void ContinuePointerDrag(PointerEventData eventData)
        {
            if (!_dragging)
                _dragging = true;
            SetPointerPosition(eventData);
        }

        internal void EndPointerDrag(PointerEventData eventData)
        {
            SetPointerPosition(eventData);
            _dragging = false;
            RefreshPresentation();
        }

        internal void AdvanceAutoScan(float deltaTime)
        {
            if (!_autoScanEnabled || deltaTime <= 0f)
                return;

            _autoScanTime += deltaTime * 0.72f;
            float horizontalRange = Mathf.Min(_autoScanCenter.x, 1f - _autoScanCenter.x) * 0.72f;
            float verticalRange = Mathf.Min(_autoScanCenter.y, 1f - _autoScanCenter.y) * 0.58f;
            Vector2 next = new Vector2(
                _autoScanCenter.x + Mathf.Sin(_autoScanTime) * horizontalRange,
                _autoScanCenter.y + Mathf.Sin(_autoScanTime * 2f) * verticalRange);
            SetMagnifierNormalizedPosition(next);
        }

        void RegisterEffect()
        {
            if (_magnifierEffect == null || string.IsNullOrWhiteSpace(_magnifierEffect.Id))
                return;

            if (_registeredEffect == null)
                TextStudioEffects.TryGetDefinition(_magnifierEffect.Id, out _previousEffect);
            TextStudioEffects.Register(_magnifierEffect);
            if (TextStudioEffects.TryGetDefinition(
                    _magnifierEffect.Id,
                    out ITextStudioEffectDefinition registered) &&
                ReferenceEquals(registered, _magnifierEffect))
            {
                _registeredEffect = registered;
            }
        }

        void UnregisterEffect()
        {
            if (_registeredEffect == null || string.IsNullOrWhiteSpace(_registeredEffect.Id))
                return;

            if (TextStudioEffects.TryGetDefinition(
                    _registeredEffect.Id,
                    out ITextStudioEffectDefinition current) &&
                ReferenceEquals(current, _registeredEffect))
            {
                if (!ReferenceEquals(_previousEffect, _registeredEffect))
                {
                    TextStudioEffects.Unregister(_registeredEffect.Id);
                    if (_previousEffect != null) TextStudioEffects.Register(_previousEffect);
                }
            }
            _registeredEffect = null;
            _previousEffect = null;
        }

        void RestoreSceneState()
        {
            if (_magnifiedText != null)
            {
                _magnifiedText.InfluenceTarget = _magnifierTarget;
                string magnifiedMarkup = BuildMagnifiedMarkup();
                if (_magnifiedText.Markup != magnifiedMarkup)
                    _magnifiedText.SetText(magnifiedMarkup, true);
                _magnifiedText.SkipReveal();
                _magnifiedText.RefreshEffectBindings();
            }
            ApplyNormalizedPosition();
            RefreshPresentation();
        }

        void RestorePlainDocument()
        {
            if (_magnifiedText == null || _magnifiedText.Markup == _documentMarkup)
                return;

            _magnifiedText.SetText(_documentMarkup, true);
            _magnifiedText.SkipReveal();
        }

        string BuildMagnifiedMarkup()
        {
            if (_magnifierEffect == null || string.IsNullOrWhiteSpace(_magnifierEffect.Id))
                return _documentMarkup ?? string.Empty;

            string document = _documentMarkup ?? string.Empty;
            int split = _scopedFallback ? document.IndexOf('\n') + 1 : 0;
            return document.Substring(0, split) + "<" + _magnifierEffect.Id + ">" +
                   document.Substring(split) +
                   "</" + _magnifierEffect.Id + ">";
        }

        void SetPointerPosition(PointerEventData eventData)
        {
            if (_dragBounds == null || eventData == null)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _dragBounds,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPosition))
            {
                return;
            }

            Rect bounds = _dragBounds.rect;
            if (bounds.width <= 0.001f || bounds.height <= 0.001f)
                return;

            SetMagnifierNormalizedPosition(new Vector2(
                Mathf.InverseLerp(bounds.xMin, bounds.xMax, localPosition.x),
                Mathf.InverseLerp(bounds.yMin, bounds.yMax, localPosition.y)));
        }

        void ApplyNormalizedPosition()
        {
            if (_magnifierTarget == null || _dragBounds == null)
                return;

            if (_magnifierTarget.parent == _dragBounds)
            {
                _magnifierTarget.anchorMin = _normalizedPosition;
                _magnifierTarget.anchorMax = _normalizedPosition;
                _magnifierTarget.anchoredPosition = Vector2.zero;
                return;
            }

            Rect bounds = _dragBounds.rect;
            Vector2 localPosition = new Vector2(
                Mathf.Lerp(bounds.xMin, bounds.xMax, _normalizedPosition.x),
                Mathf.Lerp(bounds.yMin, bounds.yMax, _normalizedPosition.y));
            Vector3 worldPosition = _dragBounds.TransformPoint(localPosition);
            _magnifierTarget.position = worldPosition;
        }

        void RefreshPresentation()
        {
            if (_renderingModeLabel != null) _renderingModeLabel.text = _scopedFallback ? "Use full-document lens" : "Compare scoped fallback";
            if (_status != null)
            {
                _status.text = _scopedFallback
                    ? "Four-corner fallback\nThe heading is outside the effect range. Scoped text uses corner mapping; it cannot preserve continuous detail inside each glyph."
                    : _renderingModeLabel != null
                    ? "Full-document Hero lens\nOne persistent, unscoped lens resamples the complete live text surface. Drag the lens across a line to compare spacing and detail."
                    : _autoScanEnabled
                    ? "AUTO SCAN ACTIVE\nThe lens Transform drives exact radial resampling."
                    : "MANUAL LENS CONTROL\nDrag the aperture to inspect live, non-overlapping text.";
            }

            if (_autoScanButtonLabel != null)
                _autoScanButtonLabel.text = _autoScanEnabled
                    ? TextStudioMotionSettings.GlobalPolicy == TextStudioMotionPolicy.Reduced || TextStudioMotionSettings.GlobalPolicy == TextStudioMotionPolicy.None ? "Scan paused" : "Pause Scan"
                    : "Resume Scan";
        }

        static Vector2 ClampNormalized(Vector2 value)
        {
            return new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
        }
    }
}
