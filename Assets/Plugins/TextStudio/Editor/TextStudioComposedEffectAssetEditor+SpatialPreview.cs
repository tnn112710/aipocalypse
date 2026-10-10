using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal sealed class TextStudioSpatialPreviewFieldDescriptor
    {
        internal readonly TextStudioInfluenceField Field;
        internal TextStudioPhaseMask Phases;
        internal int BindingCount;
        internal string DirectPropertyPath;
        internal string DirectLayerStableId;
        internal string DirectLayerName;

        internal TextStudioSpatialPreviewFieldDescriptor(
            TextStudioInfluenceField field,
            TextStudioPhaseMask phases)
        {
            Field = field;
            Phases = phases;
            BindingCount = 1;
            DirectPropertyPath = string.Empty;
            DirectLayerStableId = string.Empty;
            DirectLayerName = string.Empty;
        }

        internal bool IsActive(AnimationPhase phase)
        {
            TextStudioPhaseMask required = phase switch
            {
                AnimationPhase.Appearance => TextStudioPhaseMask.Reveal,
                AnimationPhase.Disappearance => TextStudioPhaseMask.Hide,
                _ => TextStudioPhaseMask.Active
            };
            return (Phases & required) != 0;
        }
    }

    internal static class TextStudioSpatialPreviewFieldCollector
    {
        internal static void Collect(
            TextStudioCompiledPlan plan,
            TextStudioComposedEffectAsset rootEffect,
            List<TextStudioSpatialPreviewFieldDescriptor> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            output.Clear();
            if (plan?.EffectBindings == null || plan.EffectBindingCount <= 0)
                return;

            Dictionary<TextStudioInfluenceField, TextStudioSpatialPreviewFieldDescriptor> descriptors =
                new Dictionary<TextStudioInfluenceField, TextStudioSpatialPreviewFieldDescriptor>();
            List<TextStudioInfluenceField> scopeFields =
                new List<TextStudioInfluenceField>(8);
            HashSet<TextStudioInfluenceField> bindingFields =
                new HashSet<TextStudioInfluenceField>();
            int bindingCount = Mathf.Min(
                plan.EffectBindingCount,
                plan.EffectBindings.Length);
            for (int bindingIndex = 0;
                 bindingIndex < bindingCount;
                 bindingIndex++)
            {
                EffectBinding binding = plan.EffectBindings[bindingIndex];
                if (binding.scope == null)
                    continue;

                scopeFields.Clear();
                bindingFields.Clear();
                binding.scope.AppendInfluenceFields(scopeFields);
                TextStudioPhaseMask phases = binding.phaseMask == TextStudioPhaseMask.None
                    ? TextStudioPhaseMask.All
                    : binding.phaseMask;
                for (int fieldIndex = 0;
                     fieldIndex < scopeFields.Count;
                     fieldIndex++)
                {
                    TextStudioInfluenceField field = scopeFields[fieldIndex];
                    if (field == null ||
                        !field.Enabled ||
                        !bindingFields.Add(field))
                    {
                        continue;
                    }

                    if (descriptors.TryGetValue(
                            field,
                            out TextStudioSpatialPreviewFieldDescriptor descriptor))
                    {
                        descriptor.Phases |= phases;
                        descriptor.BindingCount++;
                        continue;
                    }

                    descriptor = new TextStudioSpatialPreviewFieldDescriptor(
                        field,
                        phases);
                    descriptors.Add(field, descriptor);
                    output.Add(descriptor);
                }
            }

            MatchDirectFields(rootEffect, output);
        }

        static void MatchDirectFields(
            TextStudioComposedEffectAsset rootEffect,
            List<TextStudioSpatialPreviewFieldDescriptor> descriptors)
        {
            if (rootEffect == null || descriptors.Count == 0)
                return;

            IReadOnlyList<TextStudioCompositionLayer> layers = rootEffect.Layers;
            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                TextStudioCompositionLayer layer = layers[layerIndex];
                if (layer == null)
                    continue;
                IReadOnlyList<TextStudioInfluenceField> fields =
                    layer.SpatialInfluence.Fields;
                if (fields == null)
                    continue;
                for (int fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
                {
                    TextStudioInfluenceField field = fields[fieldIndex];
                    if (field == null)
                        continue;
                    for (int descriptorIndex = 0;
                         descriptorIndex < descriptors.Count;
                         descriptorIndex++)
                    {
                        TextStudioSpatialPreviewFieldDescriptor descriptor =
                            descriptors[descriptorIndex];
                        if (!ReferenceEquals(descriptor.Field, field))
                            continue;
                        descriptor.DirectPropertyPath =
                            "_layers.Array.data[" + layerIndex +
                            "]._spatialInfluence._fields.Array.data[" +
                            fieldIndex + "]";
                        descriptor.DirectLayerStableId = layer.StableId;
                        descriptor.DirectLayerName = layer.DisplayName;
                        break;
                    }
                }
            }
        }
    }

    internal sealed class TextStudioSpatialPreviewOverlay : VisualElement
    {
        readonly TextStudioComposedEffectAssetEditor _owner;
        int _capturedPointerId = -1;

        internal TextStudioSpatialPreviewOverlay(
            TextStudioComposedEffectAssetEditor owner)
        {
            _owner = owner;
            name = "ts-composed-spatial-preview-overlay";
            AddToClassList("ts-composed-spatial-preview-overlay");
            focusable = true;
            tabIndex = 0;
            generateVisualContent += context => _owner?.DrawSpatialPreview(context);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(_ => _capturedPointerId = -1);
            RegisterCallback<DetachFromPanelEvent>(_ => ReleasePointerCapture());
            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        internal void ReleasePointerCapture()
        {
            if (_capturedPointerId < 0)
                return;
            if (PointerCaptureHelper.HasPointerCapture(this, _capturedPointerId))
                PointerCaptureHelper.ReleasePointer(this, _capturedPointerId);
            _capturedPointerId = -1;
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || !_owner.CanInteractWithSpatialTarget)
                return;
            Focus();
            _capturedPointerId = evt.pointerId;
            PointerCaptureHelper.CapturePointer(this, evt.pointerId);
            _owner.SetSpatialPreviewTargetFromPanel(evt.localPosition);
            evt.StopPropagation();
        }

        void OnPointerMove(PointerMoveEvent evt)
        {
            if (_capturedPointerId != evt.pointerId ||
                !PointerCaptureHelper.HasPointerCapture(this, evt.pointerId))
            {
                return;
            }
            _owner.SetSpatialPreviewTargetFromPanel(evt.localPosition);
            evt.StopPropagation();
        }

        void OnPointerUp(PointerUpEvent evt)
        {
            if (_capturedPointerId != evt.pointerId)
                return;
            _owner.SetSpatialPreviewTargetFromPanel(evt.localPosition);
            ReleasePointerCapture();
            evt.StopPropagation();
        }

        void OnKeyDown(KeyDownEvent evt)
        {
            Vector2 direction = evt.keyCode switch
            {
                KeyCode.LeftArrow => Vector2.left,
                KeyCode.RightArrow => Vector2.right,
                KeyCode.UpArrow => Vector2.up,
                KeyCode.DownArrow => Vector2.down,
                _ => Vector2.zero
            };
            if (direction == Vector2.zero || !_owner.CanInteractWithSpatialTarget)
                return;
            _owner.MoveSpatialPreviewTarget(
                direction * (evt.shiftKey ? 0.05f : 0.01f));
            evt.StopPropagation();
        }
    }

    internal partial class TextStudioComposedEffectAssetEditor
    {
        const float SpatialPreviewViewportMargin = 0.05f;
        const int SpatialNoiseColumns = 20;
        const int SpatialNoiseRows = 8;

        static readonly Color SpatialActiveColor =
            new Color(0.52f, 0.87f, 0.82f, 0.92f);
        static readonly Color SpatialInactiveColor =
            new Color(0.68f, 0.71f, 0.74f, 0.58f);
        static readonly Color SpatialTargetColor =
            new Color(1f, 0.76f, 0.30f, 0.96f);

        readonly List<TextStudioSpatialPreviewFieldDescriptor> _spatialPreviewFields =
            new List<TextStudioSpatialPreviewFieldDescriptor>(8);
        TextStudioSpatialPreviewOverlay _spatialPreviewOverlay;
        VisualElement _spatialPreviewControls;
        Button _spatialPreviousButton;
        Button _spatialNextButton;
        Button _spatialAutoMoveButton;
        Button _spatialCenterButton;
        Label _spatialFieldLabel;
        Label _spatialPreviewGuidance;
        TextStudioInfluenceField _spatialFocusedField;
        TextStudioInfluenceField _spatialPreferredField;
        TextStudioCompiledPlan _spatialDescriptorPlan;
        int _spatialDescriptorDirtyCount = int.MinValue;
        string _spatialDescriptorSoloLayer = string.Empty;
        bool _spatialDescriptorsDirty = true;
        bool _spatialHasTargetField;
        bool _spatialRendererAvailable;
        bool _spatialAutoMove = true;
        bool _spatialPresentationDirty = true;
        AnimationPhase _spatialPresentationPhase;
        double _spatialAutoMoveEpoch;
        Vector2 _spatialTargetViewportPosition = new Vector2(0.5f, 0.5f);
        TextStudioPreviewSpatialTarget _spatialPreviewTarget;
        bool _hasResolvedSpatialPreviewTarget;
        float[] _spatialNoiseSamples;
        TextStudioInfluenceField _spatialNoiseField;
        ITextStudioInteractiveSpatialPreviewRenderer _spatialNoiseRenderer;
        Rect _spatialNoiseRect;
        bool _spatialNoiseDirty = true;

        internal bool CanInteractWithSpatialTarget =>
            _spatialHasTargetField &&
            _spatialRendererAvailable &&
            _spatialPreviewOverlay != null;

        internal bool SpatialAutoMove => _spatialAutoMove;

        internal Vector2 SpatialTargetViewportPosition =>
            _spatialTargetViewportPosition;

        internal int SpatialPreviewFieldCount => _spatialPreviewFields.Count;

        void InitializeSpatialPreviewState()
        {
            _spatialPreviewFields.Clear();
            _spatialFocusedField = null;
            _spatialPreferredField = null;
            _spatialDescriptorPlan = null;
            _spatialDescriptorDirtyCount = int.MinValue;
            _spatialDescriptorSoloLayer = string.Empty;
            _spatialHasTargetField = false;
            _spatialAutoMove = true;
            _spatialAutoMoveEpoch = EditorApplication.timeSinceStartup;
            _spatialTargetViewportPosition = ResolveAutoMovePosition(0f);
            _spatialDescriptorsDirty = true;
            _spatialRendererAvailable = false;
            _spatialPresentationDirty = true;
            _hasResolvedSpatialPreviewTarget = false;
            _spatialNoiseField = null;
            _spatialNoiseRenderer = null;
            _spatialNoiseRect = default;
            _spatialNoiseDirty = true;
        }

        void CreateSpatialPreviewElements(VisualElement section)
        {
            _spatialPreviewOverlay = new TextStudioSpatialPreviewOverlay(this)
            {
                pickingMode = PickingMode.Ignore
            };
            _spatialPreviewOverlay.style.display = DisplayStyle.None;
            _previewCanvas.Add(_spatialPreviewOverlay);

            _spatialPreviewControls = new VisualElement
            {
                name = "ts-composed-spatial-preview-controls"
            };
            _spatialPreviewControls.AddToClassList(
                "ts-composed-spatial-preview-controls");
            _spatialPreviousButton = new Button(() => CycleSpatialField(-1))
            {
                name = "ts-composed-spatial-preview-previous",
                text = "‹",
                tooltip = "Focus the previous resolved spatial influence field."
            };
            _spatialPreviousButton.AddToClassList("ts-spatial-preview-navigation");
            _spatialFieldLabel = new Label
            {
                name = "ts-composed-spatial-preview-field"
            };
            _spatialFieldLabel.AddToClassList("ts-spatial-preview-field-label");
            _spatialNextButton = new Button(() => CycleSpatialField(1))
            {
                name = "ts-composed-spatial-preview-next",
                text = "›",
                tooltip = "Focus the next resolved spatial influence field."
            };
            _spatialNextButton.AddToClassList("ts-spatial-preview-navigation");
            _spatialAutoMoveButton = new Button(ToggleSpatialAutoMove)
            {
                name = "ts-composed-spatial-preview-auto-move",
                tooltip = "Move the shared preview target automatically along the Scene 16 demonstration path."
            };
            _spatialCenterButton = new Button(CenterSpatialPreviewTarget)
            {
                name = "ts-composed-spatial-preview-center",
                text = "Center",
                tooltip = "Center the shared preview target and switch to manual control."
            };
            _spatialPreviewControls.Add(_spatialPreviousButton);
            _spatialPreviewControls.Add(_spatialFieldLabel);
            _spatialPreviewControls.Add(_spatialNextButton);
            _spatialPreviewControls.Add(_spatialAutoMoveButton);
            _spatialPreviewControls.Add(_spatialCenterButton);
            _spatialPreviewControls.style.display = DisplayStyle.None;
            section.Add(_spatialPreviewControls);

            _spatialPreviewGuidance = TextStudioInspectorElements.CreateMutedText(
                "Spatial targets and guides are preview-only. Local target fields are fitted to this sample text. Export uses each field's authored Fallback Center.",
                "ts-composed-spatial-preview-guidance");
            _spatialPreviewGuidance.style.display = DisplayStyle.None;
            section.Add(_spatialPreviewGuidance);
        }

        void RegisterSpatialPreviewFocusCallbacks(VisualElement root)
        {
            root.RegisterCallback<FocusInEvent>(evt =>
                TryFocusSpatialFieldFromElement(evt.target as VisualElement));
            root.RegisterCallback<PointerDownEvent>(evt =>
                TryFocusSpatialFieldFromElement(evt.target as VisualElement));
        }

        void TryFocusSpatialFieldFromElement(VisualElement element)
        {
            for (VisualElement current = element;
                 current != null;
                 current = current.parent)
            {
                if (current.userData is string propertyPath &&
                    TryFocusSpatialField(propertyPath))
                {
                    return;
                }
            }
        }

        internal bool TryFocusSpatialField(string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath))
                return false;
            for (int i = 0; i < _spatialPreviewFields.Count; i++)
            {
                TextStudioSpatialPreviewFieldDescriptor descriptor =
                    _spatialPreviewFields[i];
                if (!string.Equals(
                        descriptor.DirectPropertyPath,
                        propertyPath,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                SetFocusedSpatialField(descriptor.Field);
                return true;
            }
            return false;
        }

        void RefreshSpatialPreviewDescriptors(
            TextStudioCompiledPlan plan,
            TextStudioComposedEffectAsset effect)
        {
            int dirtyCount = effect != null
                ? EditorUtility.GetDirtyCount(effect)
                : 0;
            if (!_spatialDescriptorsDirty &&
                ReferenceEquals(_spatialDescriptorPlan, plan) &&
                _spatialDescriptorDirtyCount == dirtyCount &&
                string.Equals(
                    _spatialDescriptorSoloLayer,
                    SoloLayerStableId,
                    StringComparison.Ordinal))
            {
                return;
            }

            TextStudioInfluenceField previousFocus = _spatialFocusedField;
            int previousFocusIndex = GetFocusedSpatialFieldIndex();
            TextStudioSpatialPreviewFieldCollector.Collect(
                plan,
                effect,
                _spatialPreviewFields);
            _spatialDescriptorPlan = plan;
            _spatialDescriptorDirtyCount = dirtyCount;
            _spatialDescriptorSoloLayer = SoloLayerStableId;
            _spatialDescriptorsDirty = false;
            _spatialHasTargetField = false;
            _spatialFocusedField = null;
            TextStudioInfluenceField resolvedPreviousFocus = null;
            TextStudioInfluenceField resolvedPreferredFocus = null;
            for (int i = 0; i < _spatialPreviewFields.Count; i++)
            {
                TextStudioInfluenceField field = _spatialPreviewFields[i].Field;
                if (field.Shape == TextStudioInfluenceFieldShape.TargetDistance)
                    _spatialHasTargetField = true;
                if (ReferenceEquals(field, previousFocus))
                    resolvedPreviousFocus = field;
                if (ReferenceEquals(field, _spatialPreferredField))
                    resolvedPreferredFocus = field;
            }
            _spatialFocusedField = resolvedPreferredFocus ?? resolvedPreviousFocus;
            if (_spatialFocusedField == null && _spatialPreviewFields.Count > 0)
            {
                int fallbackIndex = Mathf.Clamp(
                    previousFocusIndex,
                    0,
                    _spatialPreviewFields.Count - 1);
                _spatialFocusedField =
                    _spatialPreviewFields[fallbackIndex].Field;
            }

            _spatialNoiseDirty = true;
            _spatialPresentationDirty = true;
            RefreshSpatialPreviewPresentation();
        }

        void RefreshSpatialPreviewPresentation()
        {
            if (!_spatialPresentationDirty &&
                _spatialPresentationPhase == _previewPhase)
            {
                return;
            }
            _spatialPresentationDirty = false;
            _spatialPresentationPhase = _previewPhase;
            bool hasFields = _spatialPreviewFields.Count > 0;
            if (_spatialPreviewControls != null)
            {
                _spatialPreviewControls.style.display = hasFields
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }
            if (_spatialPreviewGuidance != null)
            {
                _spatialPreviewGuidance.style.display = hasFields
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                _spatialPreviewGuidance.text = _spatialRendererAvailable
                    ? "Spatial targets and guides are preview-only. Local target fields are fitted to this sample text. Export uses each field's authored Fallback Center."
                    : "Spatial guides are unavailable for this renderer. Export uses each field's authored Fallback Center.";
            }
            if (!hasFields)
            {
                SetSpatialRendererAvailability(false);
                return;
            }

            int focusedIndex = GetFocusedSpatialFieldIndex();
            if (focusedIndex < 0)
            {
                _spatialFocusedField = _spatialPreviewFields[0].Field;
                focusedIndex = 0;
            }
            TextStudioSpatialPreviewFieldDescriptor descriptor =
                _spatialPreviewFields[focusedIndex];
            bool active = descriptor.IsActive(_previewPhase);
            string label = (focusedIndex + 1) + " / " +
                _spatialPreviewFields.Count + " · " +
                FormatSpatialEnum(descriptor.Field.Shape) + " (" +
                FormatSpatialEnum(descriptor.Field.Space) + ")";
            if (IsFrontCrossSection(descriptor.Field))
                label += " · Front cross-section";
            if (!active)
                label += " · Inactive in current phase";
            _spatialFieldLabel.text = label;
            _spatialFieldLabel.tooltip = string.IsNullOrEmpty(
                    descriptor.DirectLayerName)
                ? "This resolved field comes from a nested Composed Effect or Tag Stack binding."
                : "Direct field on " + descriptor.DirectLayerName + ".";
            SetClass(
                _spatialFieldLabel,
                "ts-spatial-preview-field-inactive",
                !active);
            bool canCycle = _spatialPreviewFields.Count > 1;
            _spatialPreviousButton.SetEnabled(canCycle);
            _spatialNextButton.SetEnabled(canCycle);
            _spatialAutoMoveButton.style.display = _spatialHasTargetField
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _spatialCenterButton.style.display = _spatialHasTargetField
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            RefreshSpatialAutoMoveButton();
            if (_spatialPreviewOverlay != null)
                _spatialPreviewOverlay.MarkDirtyRepaint();
        }

        void SetSpatialRendererAvailability(bool available)
        {
            bool resolvedAvailability = available &&
                _spatialPreviewFields.Count > 0;
            bool availabilityChanged =
                _spatialRendererAvailable != resolvedAvailability;
            _spatialRendererAvailable = resolvedAvailability;
            if (_spatialPreviewOverlay != null)
            {
                _spatialPreviewOverlay.style.display = _spatialRendererAvailable
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                _spatialPreviewOverlay.pickingMode =
                    _spatialRendererAvailable && _spatialHasTargetField
                        ? PickingMode.Position
                        : PickingMode.Ignore;
                if (!_spatialRendererAvailable)
                    _spatialPreviewOverlay.ReleasePointerCapture();
            }
            if (availabilityChanged)
                RefreshSpatialPreviewPresentationWithoutAvailabilityRecursion();
        }

        void RefreshSpatialPreviewPresentationWithoutAvailabilityRecursion()
        {
            if (_spatialPreviewGuidance == null ||
                _spatialPreviewFields.Count == 0)
            {
                return;
            }
            _spatialPreviewGuidance.text = _spatialRendererAvailable
                ? "Spatial targets and guides are preview-only. Local target fields are fitted to this sample text. Export uses each field's authored Fallback Center."
                : "Spatial guides are unavailable for this renderer. Export uses each field's authored Fallback Center.";
        }

        void ApplySpatialPreviewTargetToRenderer()
        {
            ITextStudioInteractiveSpatialPreviewRenderer renderer =
                _livePreviewRenderer as ITextStudioInteractiveSpatialPreviewRenderer;
            SetSpatialRendererAvailability(renderer != null);
            if (renderer == null)
                return;

            if (!_spatialHasTargetField)
            {
                TextStudioPreviewSpatialTarget empty = default;
                renderer.SetPreviewTarget(false, in empty);
                _hasResolvedSpatialPreviewTarget = false;
                return;
            }

            if (_spatialAutoMove)
            {
                float elapsed = (float)Math.Max(
                    0.0,
                    EditorApplication.timeSinceStartup - _spatialAutoMoveEpoch);
                _spatialTargetViewportPosition = ResolveAutoMovePosition(elapsed);
            }
            _spatialTargetViewportPosition = ClampSpatialViewportPosition(
                _spatialTargetViewportPosition);
            if (renderer.TryResolveTarget(
                    _spatialTargetViewportPosition,
                    out TextStudioPreviewSpatialTarget target))
            {
                _spatialPreviewTarget = target;
                _hasResolvedSpatialPreviewTarget = true;
                renderer.SetPreviewTarget(true, in target);
            }
            else
            {
                TextStudioPreviewSpatialTarget empty = default;
                _hasResolvedSpatialPreviewTarget = false;
                renderer.SetPreviewTarget(false, in empty);
            }
            _spatialPreviewOverlay?.MarkDirtyRepaint();
        }

        internal static Vector2 ResolveAutoMovePosition(float time)
        {
            float safeTime = Mathf.Max(0f, time);
            return ClampSpatialViewportPosition(new Vector2(
                0.5f + Mathf.Sin(safeTime * 0.72f) * 0.40f,
                0.52f + Mathf.Sin(safeTime * 1.13f + 1.2f) * 0.28f));
        }

        static Vector2 ClampSpatialViewportPosition(Vector2 position)
        {
            return new Vector2(
                Mathf.Clamp(
                    position.x,
                    SpatialPreviewViewportMargin,
                    1f - SpatialPreviewViewportMargin),
                Mathf.Clamp(
                    position.y,
                    SpatialPreviewViewportMargin,
                    1f - SpatialPreviewViewportMargin));
        }

        internal void SetSpatialPreviewTargetFromPanel(Vector2 panelPosition)
        {
            if (!CanInteractWithSpatialTarget ||
                !TryGetSpatialRenderRect(out Rect renderRect))
            {
                return;
            }

            Vector2 viewport = new Vector2(
                Mathf.InverseLerp(renderRect.xMin, renderRect.xMax, panelPosition.x),
                1f - Mathf.InverseLerp(renderRect.yMin, renderRect.yMax, panelPosition.y));
            SetManualSpatialTarget(viewport);
        }

        internal void MoveSpatialPreviewTarget(Vector2 viewportDelta)
        {
            if (!CanInteractWithSpatialTarget)
                return;
            SetManualSpatialTarget(_spatialTargetViewportPosition + viewportDelta);
        }

        void SetManualSpatialTarget(Vector2 viewportPosition)
        {
            _spatialAutoMove = false;
            _spatialTargetViewportPosition = ClampSpatialViewportPosition(
                viewportPosition);
            RefreshSpatialAutoMoveButton();
            RenderPreview(refreshInspectorState: false);
        }

        void ToggleSpatialAutoMove()
        {
            _spatialAutoMove = !_spatialAutoMove;
            if (_spatialAutoMove)
                _spatialAutoMoveEpoch = EditorApplication.timeSinceStartup;
            RefreshSpatialAutoMoveButton();
            RenderPreview(refreshInspectorState: false);
        }

        void CenterSpatialPreviewTarget()
        {
            SetManualSpatialTarget(new Vector2(0.5f, 0.5f));
        }

        void RefreshSpatialAutoMoveButton()
        {
            if (_spatialAutoMoveButton == null)
                return;
            _spatialAutoMoveButton.text = _spatialAutoMove
                ? "Auto Move: On"
                : "Auto Move: Off";
            SetClass(
                _spatialAutoMoveButton,
                "ts-authoring-mode-active",
                _spatialAutoMove);
        }

        void CycleSpatialField(int direction)
        {
            if (_spatialPreviewFields.Count == 0)
                return;
            int current = GetFocusedSpatialFieldIndex();
            int next = (current + direction) % _spatialPreviewFields.Count;
            if (next < 0)
                next += _spatialPreviewFields.Count;
            SetFocusedSpatialField(_spatialPreviewFields[next].Field);
        }

        void SetFocusedSpatialField(TextStudioInfluenceField field)
        {
            if (field == null)
                return;
            _spatialPreferredField = field;
            if (ReferenceEquals(_spatialFocusedField, field))
                return;
            _spatialFocusedField = field;
            _spatialNoiseDirty = true;
            _spatialPresentationDirty = true;
            RefreshSpatialPreviewPresentation();
        }

        int GetFocusedSpatialFieldIndex()
        {
            for (int i = 0; i < _spatialPreviewFields.Count; i++)
            {
                if (ReferenceEquals(
                        _spatialPreviewFields[i].Field,
                        _spatialFocusedField))
                {
                    return i;
                }
            }
            return -1;
        }

        bool IsFrontCrossSection(TextStudioInfluenceField field)
        {
            if (target is not TextStudioComposedEffectAsset effect ||
                effect.AuthoringSpace == TextStudioCompositionAuthoringSpace.ThreeD)
            {
                return false;
            }
            return field.Axis == TextStudioInfluenceFieldAxis.Z &&
                   (field.Shape == TextStudioInfluenceFieldShape.Linear ||
                    field.Shape == TextStudioInfluenceFieldShape.Cylinder ||
                    field.Shape == TextStudioInfluenceFieldShape.Torus);
        }

        static string FormatSpatialEnum(Enum value)
        {
            return ObjectNames.NicifyVariableName(value.ToString());
        }

        bool TryGetSpatialRenderRect(out Rect renderRect)
        {
            renderRect = default;
            if (_rendererPreviewImage == null ||
                _spatialPreviewOverlay == null ||
                _rendererPreviewImage.image == null)
            {
                return false;
            }

            Vector2 imageOrigin = _rendererPreviewImage.ChangeCoordinatesTo(
                _spatialPreviewOverlay,
                _rendererPreviewImage.contentRect.position);
            Rect available = new Rect(
                imageOrigin,
                _rendererPreviewImage.contentRect.size);
            Texture texture = _rendererPreviewImage.image;
            if (available.width <= 0f ||
                available.height <= 0f ||
                texture.width <= 0 ||
                texture.height <= 0)
            {
                return false;
            }

            float scale = Mathf.Min(
                available.width / texture.width,
                available.height / texture.height);
            Vector2 size = new Vector2(
                texture.width * scale,
                texture.height * scale);
            renderRect = new Rect(
                available.center - size * 0.5f,
                size);
            return true;
        }

        internal void DrawSpatialPreview(MeshGenerationContext context)
        {
            int focusedIndex = GetFocusedSpatialFieldIndex();
            ITextStudioInteractiveSpatialPreviewRenderer renderer =
                _livePreviewRenderer as ITextStudioInteractiveSpatialPreviewRenderer;
            if (focusedIndex < 0 ||
                renderer == null ||
                !TryGetSpatialRenderRect(out Rect renderRect))
            {
                return;
            }

            TextStudioSpatialPreviewFieldDescriptor descriptor =
                _spatialPreviewFields[focusedIndex];
            Painter2D painter = context.painter2D;
            Color guideColor = descriptor.IsActive(_previewPhase)
                ? SpatialActiveColor
                : SpatialInactiveColor;
            if (descriptor.Field.Shape == TextStudioInfluenceFieldShape.Noise)
                DrawNoiseSlice(painter, renderer, descriptor.Field, renderRect, guideColor);
            else
                DrawFieldGuide(painter, renderer, descriptor.Field, renderRect, guideColor);

            DrawSharedTargetMarker(painter, renderer, renderRect);
        }

        void DrawFieldGuide(
            Painter2D painter,
            ITextStudioInteractiveSpatialPreviewRenderer renderer,
            TextStudioInfluenceField field,
            Rect renderRect,
            Color color)
        {
            painter.strokeColor = color;
            painter.lineWidth = 1.6f;
            switch (field.Shape)
            {
                case TextStudioInfluenceFieldShape.Linear:
                    DrawLinearGuide(painter, renderer, field, renderRect);
                    break;
                case TextStudioInfluenceFieldShape.Sphere:
                    DrawSphereGuide(
                        painter,
                        renderer,
                        field.Space,
                        field.Center,
                        field.Radius,
                        renderRect);
                    break;
                case TextStudioInfluenceFieldShape.Box:
                    DrawBoxGuide(painter, renderer, field, renderRect);
                    break;
                case TextStudioInfluenceFieldShape.Cylinder:
                    DrawCylinderGuide(painter, renderer, field, renderRect);
                    break;
                case TextStudioInfluenceFieldShape.Torus:
                    DrawTorusGuide(painter, renderer, field, renderRect);
                    break;
                case TextStudioInfluenceFieldShape.TargetDistance:
                    if (_hasResolvedSpatialPreviewTarget)
                    {
                        DrawSphereGuide(
                            painter,
                            renderer,
                            field.Space,
                            GetTargetPosition(field.Space),
                            field.Radius,
                            renderRect);
                    }
                    break;
            }
        }

        void DrawLinearGuide(
            Painter2D painter,
            ITextStudioInteractiveSpatialPreviewRenderer renderer,
            TextStudioInfluenceField field,
            Rect renderRect)
        {
            Vector3 axis = ResolveSpatialAxis(field.Axis);
            float radius = field.Radius;
            DrawProjectedLine(
                painter,
                renderer,
                field.Space,
                field.Center - axis * radius,
                field.Center + axis * radius,
                renderRect);
            DrawProjectedPlane(
                painter,
                renderer,
                field.Space,
                field.Center,
                axis,
                radius * 0.55f,
                renderRect);
            DrawProjectedPlane(
                painter,
                renderer,
                field.Space,
                field.Center - axis * radius,
                axis,
                radius * 0.35f,
                renderRect);
            DrawProjectedPlane(
                painter,
                renderer,
                field.Space,
                field.Center + axis * radius,
                axis,
                radius * 0.35f,
                renderRect);
        }

        void DrawBoxGuide(
            Painter2D painter,
            ITextStudioInteractiveSpatialPreviewRenderer renderer,
            TextStudioInfluenceField field,
            Rect renderRect)
        {
            Vector3 half = new Vector3(
                Mathf.Abs(field.Size.x) * 0.5f,
                Mathf.Abs(field.Size.y) * 0.5f,
                Mathf.Abs(field.Size.z) * 0.5f);
            bool isThreeD = target is TextStudioComposedEffectAsset effect &&
                effect.AuthoringSpace == TextStudioCompositionAuthoringSpace.ThreeD;
            if (!isThreeD)
            {
                Vector3 bottomLeft = field.Center + new Vector3(-half.x, -half.y, 0f);
                Vector3 topLeft = field.Center + new Vector3(-half.x, half.y, 0f);
                Vector3 topRight = field.Center + new Vector3(half.x, half.y, 0f);
                Vector3 bottomRight = field.Center + new Vector3(half.x, -half.y, 0f);
                DrawProjectedLine(painter, renderer, field.Space, bottomLeft, topLeft, renderRect);
                DrawProjectedLine(painter, renderer, field.Space, topLeft, topRight, renderRect);
                DrawProjectedLine(painter, renderer, field.Space, topRight, bottomRight, renderRect);
                DrawProjectedLine(painter, renderer, field.Space, bottomRight, bottomLeft, renderRect);
                return;
            }

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    Vector3 first = field.Center + new Vector3(
                        half.x * x,
                        half.y * y,
                        -half.z);
                    Vector3 second = first + Vector3.forward * half.z * 2f;
                    DrawProjectedLine(painter, renderer, field.Space, first, second, renderRect);
                }
            }
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 bottomLeft = field.Center + new Vector3(-half.x, -half.y, half.z * z);
                Vector3 topLeft = field.Center + new Vector3(-half.x, half.y, half.z * z);
                Vector3 topRight = field.Center + new Vector3(half.x, half.y, half.z * z);
                Vector3 bottomRight = field.Center + new Vector3(half.x, -half.y, half.z * z);
                DrawProjectedLine(painter, renderer, field.Space, bottomLeft, topLeft, renderRect);
                DrawProjectedLine(painter, renderer, field.Space, topLeft, topRight, renderRect);
                DrawProjectedLine(painter, renderer, field.Space, topRight, bottomRight, renderRect);
                DrawProjectedLine(painter, renderer, field.Space, bottomRight, bottomLeft, renderRect);
            }
        }

        void DrawCylinderGuide(
            Painter2D painter,
            ITextStudioInteractiveSpatialPreviewRenderer renderer,
            TextStudioInfluenceField field,
            Rect renderRect)
        {
            Vector3 axis = ResolveSpatialAxis(field.Axis);
            float radius = field.Radius;
            DrawProjectedCircle(
                painter,
                renderer,
                field.Space,
                field.Center - axis * radius,
                axis,
                radius,
                renderRect);
            DrawProjectedCircle(
                painter,
                renderer,
                field.Space,
                field.Center + axis * radius,
                axis,
                radius,
                renderRect);
            ResolvePlaneBasis(axis, out Vector3 first, out Vector3 second);
            DrawProjectedLine(
                painter,
                renderer,
                field.Space,
                field.Center - axis * radius + first * radius,
                field.Center + axis * radius + first * radius,
                renderRect);
            DrawProjectedLine(
                painter,
                renderer,
                field.Space,
                field.Center - axis * radius - first * radius,
                field.Center + axis * radius - first * radius,
                renderRect);
            DrawProjectedLine(
                painter,
                renderer,
                field.Space,
                field.Center - axis * radius + second * radius,
                field.Center + axis * radius + second * radius,
                renderRect);
            DrawProjectedLine(
                painter,
                renderer,
                field.Space,
                field.Center - axis * radius - second * radius,
                field.Center + axis * radius - second * radius,
                renderRect);
        }

        void DrawTorusGuide(
            Painter2D painter,
            ITextStudioInteractiveSpatialPreviewRenderer renderer,
            TextStudioInfluenceField field,
            Rect renderRect)
        {
            Vector3 axis = ResolveSpatialAxis(field.Axis);
            float major = field.MajorRadius;
            float tube = field.Radius;
            DrawProjectedCircle(
                painter,
                renderer,
                field.Space,
                field.Center,
                axis,
                major,
                renderRect);
            DrawProjectedCircle(
                painter,
                renderer,
                field.Space,
                field.Center,
                axis,
                major + tube,
                renderRect);
            DrawProjectedCircle(
                painter,
                renderer,
                field.Space,
                field.Center,
                axis,
                Mathf.Max(0f, major - tube),
                renderRect);

            bool isThreeD = target is TextStudioComposedEffectAsset effect &&
                effect.AuthoringSpace == TextStudioCompositionAuthoringSpace.ThreeD;
            if (!isThreeD)
                return;
            ResolvePlaneBasis(axis, out Vector3 first, out Vector3 second);
            for (int i = 0; i < 8; i++)
            {
                float angle = i / 8f * Mathf.PI * 2f;
                Vector3 radial = first * Mathf.Cos(angle) + second * Mathf.Sin(angle);
                Vector3 center = field.Center + radial * major;
                Vector3 tangent = Vector3.Cross(axis, radial).normalized;
                DrawProjectedCircle(
                    painter,
                    renderer,
                    field.Space,
                    center,
                    tangent,
                    tube,
                    renderRect,
                    18);
            }
        }

        void DrawSphereGuide(
            Painter2D painter,
            ITextStudioInteractiveSpatialPreviewRenderer renderer,
            TextStudioInfluenceFieldSpace space,
            Vector3 center,
            float radius,
            Rect renderRect)
        {
            DrawProjectedCircle(
                painter,
                renderer,
                space,
                center,
                Vector3.forward,
                radius,
                renderRect);
            bool isThreeD = target is TextStudioComposedEffectAsset effect &&
                effect.AuthoringSpace == TextStudioCompositionAuthoringSpace.ThreeD;
            if (!isThreeD)
                return;
            DrawProjectedCircle(
                painter,
                renderer,
                space,
                center,
                Vector3.right,
                radius,
                renderRect);
            DrawProjectedCircle(
                painter,
                renderer,
                space,
                center,
                Vector3.up,
                radius,
                renderRect);
        }

        void DrawProjectedPlane(
            Painter2D painter,
            ITextStudioInteractiveSpatialPreviewRenderer renderer,
            TextStudioInfluenceFieldSpace space,
            Vector3 center,
            Vector3 normal,
            float extent,
            Rect renderRect)
        {
            ResolvePlaneBasis(normal, out Vector3 first, out Vector3 second);
            Vector3 firstCorner = center - first * extent - second * extent;
            Vector3 secondCorner = center - first * extent + second * extent;
            Vector3 thirdCorner = center + first * extent + second * extent;
            Vector3 fourthCorner = center + first * extent - second * extent;
            DrawProjectedLine(painter, renderer, space, firstCorner, secondCorner, renderRect);
            DrawProjectedLine(painter, renderer, space, secondCorner, thirdCorner, renderRect);
            DrawProjectedLine(painter, renderer, space, thirdCorner, fourthCorner, renderRect);
            DrawProjectedLine(painter, renderer, space, fourthCorner, firstCorner, renderRect);
        }

        void DrawProjectedCircle(
            Painter2D painter,
            ITextStudioInteractiveSpatialPreviewRenderer renderer,
            TextStudioInfluenceFieldSpace space,
            Vector3 center,
            Vector3 normal,
            float radius,
            Rect renderRect,
            int samples = 40)
        {
            if (radius <= 0.0001f)
                return;
            ResolvePlaneBasis(normal, out Vector3 first, out Vector3 second);
            bool started = false;
            painter.BeginPath();
            for (int i = 0; i <= samples; i++)
            {
                float angle = i / (float)samples * Mathf.PI * 2f;
                Vector3 point = center +
                    (first * Mathf.Cos(angle) + second * Mathf.Sin(angle)) * radius;
                if (!TryProjectToPanel(renderer, space, point, renderRect, out Vector2 panel))
                    continue;
                if (!started)
                {
                    painter.MoveTo(panel);
                    started = true;
                }
                else
                {
                    painter.LineTo(panel);
                }
            }
            if (started)
                painter.Stroke();
        }

        static void ResolvePlaneBasis(
            Vector3 normal,
            out Vector3 first,
            out Vector3 second)
        {
            Vector3 normalized = normal.sqrMagnitude > 0.0001f
                ? normal.normalized
                : Vector3.forward;
            Vector3 reference = Mathf.Abs(Vector3.Dot(normalized, Vector3.up)) > 0.95f
                ? Vector3.right
                : Vector3.up;
            first = Vector3.Cross(normalized, reference).normalized;
            second = Vector3.Cross(normalized, first).normalized;
        }

        void DrawProjectedLine(
            Painter2D painter,
            ITextStudioInteractiveSpatialPreviewRenderer renderer,
            TextStudioInfluenceFieldSpace space,
            Vector3 first,
            Vector3 second,
            Rect renderRect)
        {
            if (!TryProjectToPanel(renderer, space, first, renderRect, out Vector2 firstPanel) ||
                !TryProjectToPanel(renderer, space, second, renderRect, out Vector2 secondPanel))
            {
                return;
            }
            painter.BeginPath();
            painter.MoveTo(firstPanel);
            painter.LineTo(secondPanel);
            painter.Stroke();
        }

        static bool TryProjectToPanel(
            ITextStudioInteractiveSpatialPreviewRenderer renderer,
            TextStudioInfluenceFieldSpace space,
            Vector3 point,
            Rect renderRect,
            out Vector2 panelPosition)
        {
            panelPosition = default;
            if (!renderer.TryProjectPoint(space, point, out Vector2 viewport))
                return false;
            panelPosition = new Vector2(
                Mathf.LerpUnclamped(renderRect.xMin, renderRect.xMax, viewport.x),
                Mathf.LerpUnclamped(renderRect.yMax, renderRect.yMin, viewport.y));
            return float.IsFinite(panelPosition.x) && float.IsFinite(panelPosition.y);
        }

        void DrawSharedTargetMarker(
            Painter2D painter,
            ITextStudioInteractiveSpatialPreviewRenderer renderer,
            Rect renderRect)
        {
            if (!_spatialHasTargetField || !_hasResolvedSpatialPreviewTarget)
                return;

            TextStudioInfluenceField targetField = null;
            for (int i = 0; i < _spatialPreviewFields.Count; i++)
            {
                if (_spatialPreviewFields[i].Field.Shape ==
                    TextStudioInfluenceFieldShape.TargetDistance)
                {
                    targetField = _spatialPreviewFields[i].Field;
                    break;
                }
            }
            if (targetField == null ||
                !TryProjectToPanel(
                    renderer,
                    targetField.Space,
                    GetTargetPosition(targetField.Space),
                    renderRect,
                    out Vector2 position))
            {
                return;
            }

            painter.strokeColor = SpatialTargetColor;
            painter.lineWidth = 2f;
            const float radius = 6f;
            painter.BeginPath();
            painter.Arc(position, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.Stroke();
            painter.BeginPath();
            painter.MoveTo(position + Vector2.left * 9f);
            painter.LineTo(position + Vector2.right * 9f);
            painter.MoveTo(position + Vector2.down * 9f);
            painter.LineTo(position + Vector2.up * 9f);
            painter.Stroke();
        }

        Vector3 GetTargetPosition(TextStudioInfluenceFieldSpace space)
        {
            return space switch
            {
                TextStudioInfluenceFieldSpace.SourceTextNormalized =>
                    _spatialPreviewTarget.SourceTextNormalizedPosition,
                TextStudioInfluenceFieldSpace.World =>
                    _spatialPreviewTarget.WorldPosition,
                _ => _spatialPreviewTarget.LocalPosition
            };
        }

        void DrawNoiseSlice(
            Painter2D painter,
            ITextStudioInteractiveSpatialPreviewRenderer renderer,
            TextStudioInfluenceField field,
            Rect renderRect,
            Color guideColor)
        {
            EnsureNoiseSamples(renderer, field, renderRect);
            if (_spatialNoiseSamples == null)
                return;
            float cellWidth = renderRect.width / SpatialNoiseColumns;
            float cellHeight = renderRect.height / SpatialNoiseRows;
            for (int row = 0; row < SpatialNoiseRows; row++)
            {
                for (int column = 0; column < SpatialNoiseColumns; column++)
                {
                    float value = _spatialNoiseSamples[
                        row * SpatialNoiseColumns + column];
                    Color color = guideColor;
                    color.a = Mathf.Lerp(0.015f, 0.22f, value);
                    FillRect(
                        painter,
                        new Rect(
                            renderRect.xMin + column * cellWidth,
                            renderRect.yMin + row * cellHeight,
                            cellWidth + 0.5f,
                            cellHeight + 0.5f),
                        color);
                }
            }
        }

        void EnsureNoiseSamples(
            ITextStudioInteractiveSpatialPreviewRenderer renderer,
            TextStudioInfluenceField field,
            Rect renderRect)
        {
            if (!_spatialNoiseDirty &&
                ReferenceEquals(_spatialNoiseField, field) &&
                ReferenceEquals(_spatialNoiseRenderer, renderer) &&
                _spatialNoiseRect.Equals(renderRect))
            {
                return;
            }
            _spatialNoiseSamples ??= new float[
                SpatialNoiseColumns * SpatialNoiseRows];
            for (int row = 0; row < SpatialNoiseRows; row++)
            {
                for (int column = 0; column < SpatialNoiseColumns; column++)
                {
                    Vector2 viewport = new Vector2(
                        (column + 0.5f) / SpatialNoiseColumns,
                        1f - (row + 0.5f) / SpatialNoiseRows);
                    float value = 0f;
                    if (renderer.TryResolveTarget(
                            viewport,
                            out TextStudioPreviewSpatialTarget sample))
                    {
                        EffectEvaluationContext evaluation =
                            new EffectEvaluationContext(
                                0,
                                1,
                                0,
                                0,
                                AnimationPhase.Persistent,
                                1f,
                                0f,
                                0f,
                                0f)
                            .WithSpatialPositions(
                                sample.LocalPosition,
                                sample.LocalPosition,
                                sample.WorldPosition)
                            .WithSourceContext(
                                sample.SourceTextNormalizedPosition,
                                Color.white,
                                false);
                        value = field.Evaluate(in evaluation);
                    }
                    _spatialNoiseSamples[
                        row * SpatialNoiseColumns + column] =
                        Mathf.Clamp01(value);
                }
            }
            _spatialNoiseField = field;
            _spatialNoiseRenderer = renderer;
            _spatialNoiseRect = renderRect;
            _spatialNoiseDirty = false;
        }

        static void FillRect(Painter2D painter, Rect rect, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }

        static Vector3 ResolveSpatialAxis(TextStudioInfluenceFieldAxis axis)
        {
            return axis switch
            {
                TextStudioInfluenceFieldAxis.Y => Vector3.up,
                TextStudioInfluenceFieldAxis.Z => Vector3.forward,
                _ => Vector3.right
            };
        }

        void InvalidateSpatialPreviewDescriptors()
        {
            _spatialDescriptorsDirty = true;
            _spatialNoiseDirty = true;
        }

        void HideSpatialPreviewForUnavailablePlan()
        {
            _spatialPreviewFields.Clear();
            _spatialFocusedField = null;
            _spatialHasTargetField = false;
            _spatialDescriptorsDirty = true;
            _spatialPresentationDirty = true;
            if (_spatialPreviewControls != null)
                _spatialPreviewControls.style.display = DisplayStyle.None;
            if (_spatialPreviewGuidance != null)
                _spatialPreviewGuidance.style.display = DisplayStyle.None;
            SetSpatialRendererAvailability(false);
        }

        void DisposeSpatialPreviewInteraction()
        {
            _spatialPreviewOverlay?.ReleasePointerCapture();
            _spatialRendererAvailable = false;
            _hasResolvedSpatialPreviewTarget = false;
            _spatialNoiseRenderer = null;
        }
    }
}
