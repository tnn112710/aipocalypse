using ImpossibleRobert.Common.Timing;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>Optional lesson interaction that moves a target without implementing text effects.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class TextStudioSpatialTargetInteraction : MonoBehaviour,
        ITextStudioEditorTickTarget,
        IBeginDragHandler,
        IDragHandler,
        IMoveHandler,
        ICancelHandler
    {
        const float TargetMargin = 34f;

        [SerializeField, Tooltip("Visible GameObject users drag to reposition the shared target. This helper does not implement any text effect.")]
        RectTransform _target;
        [SerializeField, Tooltip("Text-local rectangle that constrains target movement.")]
        RectTransform _dragBounds;
        [SerializeField, Tooltip("Live mode, target position, and interaction feedback shown by the lesson.")]
        TMP_Text _positionFeedback;
        [SerializeField, Tooltip("Normalized target position restored by Reset Target.")]
        Vector2 _homeNormalizedPosition = new Vector2(0.28f, 0.52f);
        [SerializeField, Tooltip("Moves the target automatically until the user drags it. Respects the global motion policy.")]
        bool _autoMove = true;
        [SerializeField, HideInInspector] Vector2 _normalizedPosition;
        [SerializeField, HideInInspector] bool _hasNormalizedPosition;
        [SerializeField] Selectable _keyboardExit;

        double _lastHostTime;
        float _travelTime;
        string _feedbackFormat = string.Empty;
        Vector2 _lastFeedbackPosition = new Vector2(float.NaN, float.NaN);
        Vector2 _lastBoundsSize;
        TextStudioMotionPolicy _lastMotionPolicy;

        public RectTransform Target => _target;
        public RectTransform DragBounds => _dragBounds;
        public bool AutoMove => _autoMove;
        public Vector2 TargetNormalizedPosition => GetTargetNormalizedPosition();

        public void ConfigureKeyboardExit(Selectable control) => _keyboardExit = control;

        public void OnCancel(BaseEventData eventData)
        {
            if (_keyboardExit == null) return;
            _keyboardExit.Select(); eventData?.Use();
        }

        public void ConfigureForScene(
            RectTransform target,
            RectTransform dragBounds,
            TMP_Text positionFeedback,
            Vector2 homeNormalizedPosition,
            bool autoMove)
        {
            _target = target;
            _dragBounds = dragBounds;
            _positionFeedback = positionFeedback;
            _homeNormalizedPosition = ClampNormalized(homeNormalizedPosition);
            _autoMove = autoMove;
            SetTargetNormalizedPositionInternal(_homeNormalizedPosition);
            RebuildFeedbackFormat();
            UpdateFeedback(true);
        }

        void OnEnable()
        {
            HostTime.Reset(ref _lastHostTime);
            HostTime.RequestEditorFrame(this, repaintAllViews: false);
            if (!_hasNormalizedPosition && _dragBounds != null && _dragBounds.rect.width > TargetMargin * 2)
            {
                _normalizedPosition = GetTargetNormalizedPosition();
                _hasNormalizedPosition = true;
            }
            RefreshLayout();
            RebuildFeedbackFormat();
            UpdateFeedback(true);
            if (!Application.isPlaying)
                TextStudioEditorHooks.NotifyEditorTickTargetEnabled(this);
        }

        void OnDisable() => TextStudioEditorHooks.NotifyEditorTickTargetDisabled(this);

        void Update()
        {
            if (!Application.isPlaying) return;
            float deltaTime = HostTime.GetDeltaTime(ref _lastHostTime, clockMode: ClockMode.Unscaled);
            AdvanceMotion(deltaTime);
        }

        bool ITextStudioEditorTickTarget.AdvanceEditorPreview(float deltaTime) =>
            AdvanceMotion(deltaTime);

        bool AdvanceMotion(float deltaTime)
        {
            RefreshLayout();
            bool moved = false;
            if (_autoMove && _target != null && _dragBounds != null &&
                TextStudioMotionSettings.GlobalPolicy != TextStudioMotionPolicy.Reduced &&
                TextStudioMotionSettings.GlobalPolicy != TextStudioMotionPolicy.None)
            {
                _travelTime += deltaTime;
                float time = _travelTime;
                Vector2 normalized = new Vector2(
                    0.5f + Mathf.Sin(time * 0.72f) * 0.40f,
                    0.52f + Mathf.Sin(time * 1.13f + 1.2f) * 0.28f);
                SetTargetNormalizedPositionInternal(normalized);
                moved = deltaTime > 0f;
            }

            UpdateFeedback(false);
            return moved;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            SetAutoMove(false);
        }

        public void OnMove(AxisEventData eventData)
        {
            if (eventData == null) return;
            SetTargetNormalizedPosition(TargetNormalizedPosition + eventData.moveVector * .08f);
            eventData.Use();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_target == null || _dragBounds == null || eventData == null)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _dragBounds,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                return;
            }

            Rect bounds = _dragBounds.rect;
            localPoint.x = Mathf.Clamp(localPoint.x, bounds.xMin + TargetMargin, bounds.xMax - TargetMargin);
            localPoint.y = Mathf.Clamp(localPoint.y, bounds.yMin + TargetMargin, bounds.yMax - TargetMargin);
            _target.anchoredPosition = localPoint;
            _normalizedPosition = GetTargetNormalizedPosition();
            _hasNormalizedPosition = true;
            UpdateFeedback(true);
        }

        public void ToggleAutoMove()
        {
            SetAutoMove(!_autoMove);
        }

        public void SetAutoMove(bool enabled)
        {
            _autoMove = enabled;
            if (enabled) HostTime.RequestEditorFrame(this, repaintAllViews: false);
            RebuildFeedbackFormat();
            UpdateFeedback(true);
        }

        public void ResetTarget()
        {
            _autoMove = false;
            SetTargetNormalizedPositionInternal(_homeNormalizedPosition);
            RebuildFeedbackFormat();
            UpdateFeedback(true);
        }

        public void SetTargetNormalizedPosition(Vector2 normalizedPosition)
        {
            _autoMove = false;
            SetTargetNormalizedPositionInternal(normalizedPosition);
            RebuildFeedbackFormat();
            UpdateFeedback(true);
        }

        void SetTargetNormalizedPositionInternal(Vector2 normalizedPosition)
        {
            _normalizedPosition = ClampNormalized(normalizedPosition);
            _hasNormalizedPosition = true;
            if (_target == null || _dragBounds == null)
                return;

            Vector2 normalized = ClampNormalized(normalizedPosition);
            Rect bounds = _dragBounds.rect;
            float minX = bounds.xMin + TargetMargin;
            float maxX = bounds.xMax - TargetMargin;
            float minY = bounds.yMin + TargetMargin;
            float maxY = bounds.yMax - TargetMargin;
            _target.anchoredPosition = new Vector2(
                Mathf.Lerp(minX, maxX, normalized.x),
                Mathf.Lerp(minY, maxY, normalized.y));
        }

        public void RefreshLayout()
        {
            if (_dragBounds == null) return;
            Vector2 size = _dragBounds.rect.size;
            bool changed = size != _lastBoundsSize || _lastMotionPolicy != TextStudioMotionSettings.GlobalPolicy;
            if (size != _lastBoundsSize && _hasNormalizedPosition && size.x > TargetMargin * 2 && size.y > TargetMargin * 2)
            {
                _lastBoundsSize = size;
                SetTargetNormalizedPositionInternal(_normalizedPosition);
            }
            if (!changed) return;
            _lastMotionPolicy = TextStudioMotionSettings.GlobalPolicy;
            RebuildFeedbackFormat();
            UpdateFeedback(true);
        }

        Vector2 GetTargetNormalizedPosition()
        {
            if (_target == null || _dragBounds == null)
                return Vector2.zero;

            Rect bounds = _dragBounds.rect;
            float minX = bounds.xMin + TargetMargin;
            float maxX = bounds.xMax - TargetMargin;
            float minY = bounds.yMin + TargetMargin;
            float maxY = bounds.yMax - TargetMargin;
            Vector2 position = _target.anchoredPosition;
            return new Vector2(
                Mathf.InverseLerp(minX, maxX, position.x),
                Mathf.InverseLerp(minY, maxY, position.y));
        }

        void RebuildFeedbackFormat()
        {
            bool reduced = TextStudioMotionSettings.GlobalPolicy == TextStudioMotionPolicy.Reduced || TextStudioMotionSettings.GlobalPolicy == TextStudioMotionPolicy.None;
            string interaction = _autoMove ? (reduced ? "Travel paused for reduced motion" : "Auto travel. Drag to take control") : "Drag or use arrow keys on the target";
            if (!_autoMove && _keyboardExit != null) interaction = "Drag or use arrow keys. Escape returns to controls";
            _feedbackFormat = "Target {0:0.00}, {1:0.00}  •  " + interaction;
        }

        void UpdateFeedback(bool force)
        {
            if (_positionFeedback == null)
                return;

            Vector2 normalized = GetTargetNormalizedPosition();
            if (!force && (normalized - _lastFeedbackPosition).sqrMagnitude < 0.000001f)
                return;

            _lastFeedbackPosition = normalized;
            _positionFeedback.SetText(_feedbackFormat, normalized.x, normalized.y);
        }

        static Vector2 ClampNormalized(Vector2 value)
        {
            return new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
        }
    }
}
