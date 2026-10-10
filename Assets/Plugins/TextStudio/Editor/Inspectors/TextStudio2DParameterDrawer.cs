using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    [CustomPropertyDrawer(typeof(TextStudio2DParameterAttribute))]
    internal sealed class TextStudio2DParameterDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            TextStudio2DParameterAttribute parameter = (TextStudio2DParameterAttribute)attribute;
            string label = string.IsNullOrEmpty(preferredLabel) ? property.displayName : preferredLabel;
            return CreateField(property, label, parameter.Kind, parameter.Minimum);
        }

        internal static VisualElement CreateField(
            SerializedProperty property,
            string label,
            TextStudio2DParameterKind kind,
            float lowerBound = 0f)
        {
            BindableElement field;
            if (kind == TextStudio2DParameterKind.Minimum)
            {
                field = new MinimumFloatField(label, lowerBound) { tooltip = property.tooltip };
            }
            else if (kind == TextStudio2DParameterKind.PatternScale)
            {
                field = new RangedFloatField(label,
                    TextStudio2DParameterAttribute.MinimumPatternScale,
                    TextStudio2DParameterAttribute.MaximumPatternScale,
                    logarithmic: true, clampMaximum: true)
                {
                    tooltip = "Pattern frequency from 0.01 to 4096. The logarithmic slider gives equal space to equal frequency ratios; the numeric input remains exact."
                };
            }
            else if (kind == TextStudio2DParameterKind.Softness)
            {
                RangedFloatField softness = new RangedFloatField(label, 0f,
                    TextStudio2DParameterAttribute.SuggestedEmSoftness);
                string parentPath = property.propertyPath.Substring(0, property.propertyPath.LastIndexOf('.'));
                SerializedProperty units = property.serializedObject.FindProperty(parentPath + "._units");
                string description = property.tooltip;
                void UpdateUnits(SerializedProperty unitProperty)
                {
                    bool pixels = unitProperty != null && unitProperty.intValue == (int)TextStudio2DUnit.Pixels;
                    softness.SetSliderMaximum(pixels
                        ? TextStudio2DParameterAttribute.SuggestedPixelSoftness
                        : TextStudio2DParameterAttribute.SuggestedEmSoftness);
                    softness.tooltip = description + (pixels
                        ? " The slider covers 0 to 16 pixels."
                        : " The slider covers 0 to 0.25 em.") +
                        " Enter larger values numerically when needed. Edge softening is limited by the font's available distance field.";
                }
                UpdateUnits(units);
                if (units != null)
                    softness.TrackPropertyValue(units, UpdateUnits);
                field = softness;
            }
            else if (kind == TextStudio2DParameterKind.CornerRadii)
            {
                field = new CornerRadiiField(label)
                {
                    tooltip = "Non-negative radius for each corner. Zero uses the plate's default corner radius; the shape limits rounding to its available size."
                };
            }
            else if (kind == TextStudio2DParameterKind.HalftoneMinimumRadius ||
                     kind == TextStudio2DParameterKind.HalftoneMaximumRadius)
            {
                bool minimum = kind == TextStudio2DParameterKind.HalftoneMinimumRadius;
                string parentPath = property.propertyPath.Substring(0, property.propertyPath.LastIndexOf('.'));
                SerializedProperty other = property.serializedObject.FindProperty(
                    parentPath + (minimum ? "._maximumRadius" : "._minimumRadius"));
                field = new HalftoneRadiusSlider(label, other, minimum)
                {
                    tooltip = minimum
                        ? "Smallest dot radius as a fraction of its cell, from 0 to 0.5. Cannot exceed Maximum Radius."
                        : "Largest dot radius as a fraction of its cell, from 0 to 0.5. Cannot be less than Minimum Radius."
                };
            }
            else
            {
                field = new ConstrainedVector2Field(label, kind)
                {
                    tooltip = kind == TextStudio2DParameterKind.PaintScale
                        ? "Scale paint along each mapping axis. Negative values mirror the paint; each axis must remain nonzero."
                        : kind == TextStudio2DParameterKind.DistanceRange
                            ? "Map distinct start and end distances to the gradient. Reverse their order to reverse the ramp."
                            : "Direction of the animated treatment. Both signs are supported; vector length is limited to 8. Zero uses the effect's default direction."
                };
            }
            field.bindingPath = property.propertyPath;
            field.AddToClassList(BaseField<float>.alignedFieldUssClassName);
            field.AddToClassList("ts-property-field");
            return field;
        }

        internal static Vector2 ConstrainVector(
            TextStudio2DParameterKind kind,
            Vector2 candidate,
            Vector2 previous)
        {
            candidate.x = FiniteOrPrevious(candidate.x, previous.x);
            candidate.y = FiniteOrPrevious(candidate.y, previous.y);
            switch (kind)
            {
                case TextStudio2DParameterKind.PaintScale:
                    candidate.x = NonZeroScale(candidate.x, previous.x);
                    candidate.y = NonZeroScale(candidate.y, previous.y);
                    break;
                case TextStudio2DParameterKind.DistanceRange:
                    if (Mathf.Abs(candidate.y - candidate.x) <= 0.000001f)
                    {
                        // Keep the untouched endpoint and the authored direction, including reversed ramps.
                        float separation = Mathf.Max(0.0001f, Mathf.Abs(candidate.x) * 0.000001f);
                        if (candidate.x != previous.x)
                            candidate.x = candidate.y + (previous.x > previous.y ? separation : -separation);
                        else
                            candidate.y = candidate.x + (previous.y < previous.x ? -separation : separation);
                    }
                    break;
                case TextStudio2DParameterKind.AnimationDirection:
                    float largest = Mathf.Max(Mathf.Abs(candidate.x), Mathf.Abs(candidate.y));
                    if (largest > 8f)
                    {
                        // Scale before measuring length so even a finite, very large input cannot overflow.
                        candidate /= largest;
                        candidate = candidate.normalized * 8f;
                    }
                    else
                    {
                        candidate = Vector2.ClampMagnitude(candidate, 8f);
                    }
                    if (candidate.sqrMagnitude > 64f)
                        candidate *= 0.9999998f;
                    break;
            }
            return candidate;
        }

        internal static IntegerField CreateSelectorIndexField(
            SerializedProperty node, SerializedProperty property, string label)
        {
            SerializedProperty other = node.FindPropertyRelative(property.name == "_start" ? "_count" : "_start");
            IntegerField field = new SelectorIndexField(label, other)
            {
                bindingPath = property.propertyPath,
                tooltip = "Non-negative glyph index or count. Start plus Count must fit within the supported integer index range."
            };
            field.AddToClassList(BaseField<int>.alignedFieldUssClassName);
            return field;
        }

        static float NonZeroScale(float candidate, float previous)
        {
            if (Mathf.Abs(candidate) > 0.000001f)
                return candidate;
            return candidate < 0f || candidate == 0f && previous < 0f ? -0.0001f : 0.0001f;
        }

        static float FiniteOrPrevious(float candidate, float previous)
        {
            return float.IsFinite(candidate) ? candidate : float.IsFinite(previous) ? previous : 0f;
        }

        sealed class RangedFloatField : BaseField<float>
        {
            readonly Slider _slider;
            readonly FloatField _number;
            readonly float _minimum;
            readonly bool _logarithmic;
            readonly bool _clampMaximum;
            float _maximum;
            bool _updatingRange;

            internal RangedFloatField(string label, float minimum, float maximum,
                bool logarithmic = false, bool clampMaximum = false)
                : this(label, new VisualElement(), minimum, maximum, logarithmic, clampMaximum) { }

            RangedFloatField(string label, VisualElement input, float minimum, float maximum,
                bool logarithmic, bool clampMaximum) : base(label, input)
            {
                _minimum = minimum;
                _maximum = maximum;
                _logarithmic = logarithmic;
                _clampMaximum = clampMaximum;
                input.style.flexDirection = FlexDirection.Row;
                input.style.minWidth = 0f;
                _slider = new Slider(ToSlider(minimum), ToSlider(maximum));
                _slider.style.flexGrow = 1f;
                _slider.style.minWidth = 24f;
                _number = new FloatField();
                _number.style.width = 72f;
                _number.style.flexShrink = 0f;
                input.Add(_slider);
                input.Add(_number);
                _slider.RegisterValueChangedCallback(evt =>
                {
                    if (!_updatingRange)
                        value = _logarithmic ? Mathf.Pow(10f, evt.newValue) : evt.newValue;
                });
                _number.RegisterValueChangedCallback(evt => value = evt.newValue);
            }

            internal void SetSliderMaximum(float maximum)
            {
                _maximum = maximum;
                _updatingRange = true;
                _slider.highValue = ToSlider(maximum);
                _slider.SetValueWithoutNotify(ToSlider(base.value));
                _updatingRange = false;
            }

            public override float value
            {
                get => base.value;
                set
                {
                    float constrained = Clamp(value, base.value);
                    base.value = constrained;
                    SetValueWithoutNotify(constrained);
                }
            }

            public override void SetValueWithoutNotify(float newValue)
            {
                float constrained = Clamp(newValue, base.value);
                base.SetValueWithoutNotify(constrained);
                _slider?.SetValueWithoutNotify(ToSlider(constrained));
                _number?.SetValueWithoutNotify(constrained);
            }

            float ToSlider(float value)
            {
                return _logarithmic ? Mathf.Log10(Mathf.Max(_minimum, value)) : value;
            }

            float Clamp(float value, float previous)
            {
                float constrained = Mathf.Max(_minimum, FiniteOrPrevious(value, previous));
                return _clampMaximum ? Mathf.Min(_maximum, constrained) : constrained;
            }
        }

        sealed class ConstrainedVector2Field : Vector2Field
        {
            readonly TextStudio2DParameterKind _kind;

            internal ConstrainedVector2Field(string label, TextStudio2DParameterKind kind) : base(label)
            {
                _kind = kind;
            }

            public override Vector2 value
            {
                get => base.value;
                set
                {
                    Vector2 constrained = ConstrainVector(_kind, value, base.value);
                    base.value = constrained;
                    // An edit clamped back to the previous value must also refresh the individual numeric inputs.
                    base.SetValueWithoutNotify(constrained);
                }
            }
        }

        sealed class MinimumFloatField : FloatField
        {
            readonly float _minimum;

            internal MinimumFloatField(string label, float minimum) : base(label)
            {
                _minimum = minimum;
            }

            public override float value
            {
                get => base.value;
                set
                {
                    float constrained = Mathf.Max(_minimum, FiniteOrPrevious(value, base.value));
                    base.value = constrained;
                    base.SetValueWithoutNotify(constrained);
                }
            }
        }

        sealed class SelectorIndexField : IntegerField
        {
            readonly SerializedProperty _other;

            internal SelectorIndexField(string label, SerializedProperty other) : base(label)
            {
                _other = other;
            }

            public override int value
            {
                get => base.value;
                set
                {
                    int other = _other != null ? Mathf.Max(0, _other.intValue) : 0;
                    int constrained = Mathf.Clamp(value, 0, int.MaxValue - other);
                    base.value = constrained;
                    base.SetValueWithoutNotify(constrained);
                }
            }
        }

        sealed class CornerRadiiField : Vector4Field
        {
            internal CornerRadiiField(string label) : base(label) { }

            public override Vector4 value
            {
                get => base.value;
                set
                {
                    Vector4 previous = base.value;
                    Vector4 constrained = new Vector4(
                        Mathf.Max(0f, FiniteOrPrevious(value.x, previous.x)),
                        Mathf.Max(0f, FiniteOrPrevious(value.y, previous.y)),
                        Mathf.Max(0f, FiniteOrPrevious(value.z, previous.z)),
                        Mathf.Max(0f, FiniteOrPrevious(value.w, previous.w)));
                    base.value = constrained;
                    base.SetValueWithoutNotify(constrained);
                }
            }
        }

        sealed class HalftoneRadiusSlider : Slider
        {
            readonly SerializedProperty _otherRadius;
            readonly bool _minimum;

            internal HalftoneRadiusSlider(string label, SerializedProperty otherRadius, bool minimum)
                : base(label, 0f, 0.5f)
            {
                _otherRadius = otherRadius;
                _minimum = minimum;
                showInputField = true;
            }

            public override float value
            {
                get => base.value;
                set
                {
                    float other = _otherRadius != null
                        ? Mathf.Clamp(FiniteOrPrevious(_otherRadius.floatValue, 0f), 0f, 0.5f)
                        : _minimum ? 0.5f : 0f;
                    float constrained = Mathf.Clamp(
                        FiniteOrPrevious(value, base.value),
                        _minimum ? 0f : other,
                        _minimum ? other : 0.5f);
                    base.value = constrained;
                    base.SetValueWithoutNotify(constrained);
                }
            }
        }
    }
}
