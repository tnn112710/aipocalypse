using ImpossibleRobert.Common.Timing;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal static class TextGalleryPreviewPalette
    {
        internal static readonly Color Background = new Color(0.1f, 0.105f, 0.11f, 1f);
    }

    internal static class TextGalleryLiveRenderedPreview
    {
        const int StandardWidth = 640;
        const int StandardHeight = 300;
        const int LargeWidth = 960;
        const int LargeHeight = 450;
        const long TickIntervalMilliseconds = 33;

        internal static VisualElement Create(
            string markup,
            bool largePreview,
            TextStudioPreviewSegment segments = TextStudioPreviewSegment.All)
        {
            VisualElement root = new VisualElement();
            root.style.flexGrow = 1f;
            root.style.justifyContent = Justify.Center;
            root.style.alignItems = Align.Center;
            root.style.overflow = Overflow.Hidden;
            root.style.borderTopLeftRadius = 4f;
            root.style.borderTopRightRadius = 4f;
            root.style.borderBottomLeftRadius = 4f;
            root.style.borderBottomRightRadius = 4f;
            root.style.backgroundColor = TextGalleryPreviewPalette.Background;

            Image image = new Image
            {
                scaleMode = ScaleMode.ScaleToFit
            };
            image.style.flexGrow = 1f;
            image.style.width = Length.Percent(100f);
            image.style.height = Length.Percent(100f);
            root.Add(image);

            Label fallback = new Label();
            fallback.style.position = Position.Absolute;
            fallback.style.left = 12f;
            fallback.style.right = 12f;
            fallback.style.top = 0f;
            fallback.style.bottom = 0f;
            fallback.style.display = DisplayStyle.None;
            fallback.style.unityTextAlign = TextAnchor.MiddleCenter;
            fallback.style.whiteSpace = WhiteSpace.Normal;
            fallback.style.opacity = 0.68f;
            root.Add(fallback);

            PreviewController controller = new PreviewController(
                root,
                image,
                fallback,
                markup,
                segments,
                largePreview ? LargeWidth : StandardWidth,
                largePreview ? LargeHeight : StandardHeight);
            root.userData = controller;
            root.RegisterCallback<AttachToPanelEvent>(_ => controller.Attach());
            root.RegisterCallback<DetachFromPanelEvent>(_ => controller.Detach());
            root.RegisterCallback<GeometryChangedEvent>(_ => controller.RefreshVisibility());
            return root;
        }

        internal static bool IsScheduled(VisualElement preview)
        {
            return preview?.userData is PreviewController controller &&
                   controller.HasScheduledItem;
        }

        sealed class PreviewController
        {
            readonly VisualElement _root;
            readonly Image _image;
            readonly Label _fallback;
            readonly string _markup;
            readonly TextStudioPreviewSegment _segments;
            readonly int _width;
            readonly int _height;

            TextStudioPreviewExportSnapshot _snapshot;
            ITextStudioPreviewExportFrameRenderer _renderer;
            IVisualElementScheduledItem _scheduledItem;
            ScrollView _scrollView;
            double _lastHostTime;
            float _continuousTime;
            float _previousLifecycleTime;

            internal bool HasScheduledItem => _scheduledItem != null;

            internal PreviewController(
                VisualElement root,
                Image image,
                Label fallback,
                string markup,
                TextStudioPreviewSegment segments,
                int width,
                int height)
            {
                _root = root;
                _image = image;
                _fallback = fallback;
                _markup = markup;
                _segments = segments;
                _width = width;
                _height = height;
            }

            internal void Attach()
            {
                if (_renderer != null)
                    return;

                if (!TextStudioPreviewExportSnapshot.TryCreate(
                        _markup,
                        _segments,
                        1f,
                        out _snapshot,
                        out string error))
                {
                    ShowFallback(error);
                    return;
                }

                try
                {
                    _snapshot.BackgroundColor = TextGalleryPreviewPalette.Background;
                    _renderer = TextStudioPreviewExportRendererRegistry.Create(
                        TextStudioPreviewExportRendererRegistry.FlatRendererId,
                        _snapshot,
                        _width,
                        _height,
                        false);
                    _image.image = _renderer.TargetTexture;
                    _fallback.style.display = DisplayStyle.None;
                    _continuousTime = 0f;
                    _previousLifecycleTime = 0f;
                    _lastHostTime = HostTime.CurrentHostTime(ClockMode.Realtime);
                    RegisterScrollVisibilityCallback();
                    RenderCurrentFrame();
                    RefreshVisibility();
                }
                catch (System.Exception exception)
                {
                    DisposeRenderer();
                    ShowFallback("Rendered preview unavailable: " + exception.Message);
                }
            }

            internal void Detach()
            {
                UnregisterScrollVisibilityCallback();
                PauseScheduling();
                DisposeRenderer();
            }

            internal void RefreshVisibility()
            {
                if (_renderer == null)
                    return;

                if (!IsInsideVisiblePanel())
                {
                    PauseScheduling();
                    return;
                }

                _lastHostTime = HostTime.CurrentHostTime(ClockMode.Realtime);
                if (_scheduledItem == null)
                    _scheduledItem = _root.schedule.Execute(Tick).Every(TickIntervalMilliseconds);
                else
                    _scheduledItem.Resume();
            }

            void Tick()
            {
                if (_renderer == null || _snapshot == null)
                    return;

                if (!IsInsideVisiblePanel())
                {
                    PauseScheduling();
                    return;
                }

                double now = HostTime.CurrentHostTime(ClockMode.Realtime);
                float deltaTime = HostTime.ClampDelta(
                    (float)(now - _lastHostTime),
                    HostTime.DefaultMaxDeltaTime);
                _lastHostTime = now;
                _continuousTime += deltaTime * _snapshot.Speed;

                RenderCurrentFrame();
            }

            void PauseScheduling()
            {
                _scheduledItem?.Pause();
                _scheduledItem = null;
            }

            void RegisterScrollVisibilityCallback()
            {
                _scrollView = FindParentScrollView();
                if (_scrollView != null)
                    _scrollView.verticalScroller.valueChanged += OnVerticalScrollChanged;
            }

            void UnregisterScrollVisibilityCallback()
            {
                if (_scrollView != null)
                    _scrollView.verticalScroller.valueChanged -= OnVerticalScrollChanged;

                _scrollView = null;
            }

            void OnVerticalScrollChanged(float value)
            {
                RefreshVisibility();
            }

            void RenderCurrentFrame()
            {
                float lifecycleTime = Mathf.Repeat(
                    _continuousTime,
                    Mathf.Max(0.0001f, _snapshot.SourceDuration));
                if (lifecycleTime < _previousLifecycleTime)
                    _renderer.ResetEvaluation();

                _renderer.RenderToTarget(_continuousTime, lifecycleTime);
                _previousLifecycleTime = lifecycleTime;
            }

            bool IsInsideVisiblePanel()
            {
                if (_root.panel == null)
                    return false;

                VisualElement viewport = _scrollView != null
                    ? _scrollView.contentViewport
                    : _root.panel.visualTree;
                return viewport != null && _root.worldBound.Overlaps(viewport.worldBound);
            }

            ScrollView FindParentScrollView()
            {
                VisualElement current = _root.parent;
                while (current != null)
                {
                    if (current is ScrollView scrollView)
                        return scrollView;

                    current = current.parent;
                }

                return null;
            }

            void DisposeRenderer()
            {
                _image.image = null;
                _renderer?.Dispose();
                _renderer = null;
                _snapshot?.Dispose();
                _snapshot = null;
            }

            void ShowFallback(string message)
            {
                _fallback.text = string.IsNullOrWhiteSpace(message)
                    ? "Rendered preview unavailable."
                    : message;
                _fallback.style.display = DisplayStyle.Flex;
            }
        }
    }
}
