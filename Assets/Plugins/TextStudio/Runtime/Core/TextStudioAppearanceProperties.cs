using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Identifies an appearance-layer property that can be read or overridden, such as paint color, opacity, stroke width, shadow offset, or glow radius.</summary>
    public enum TextStudioAppearanceProperty
    {
        PaintColor = 0, Opacity = 1, Enabled = 2, AnimatedTint = 3, BlendMode = 4,
        StrokeWidth = 5, StrokeSoftness = 6, StrokeAlignment = 7,
        ShadowX = 8, ShadowY = 9, ShadowBlur = 10, ShadowSpread = 11,
        GlowRadius = 12, GlowSpread = 13, FillDilation = 14, FillSoftness = 15, NoiseSeed = 16
    }
    /// <summary>Identifies the color, numeric, Boolean, or enum value accepted by an appearance-property descriptor.</summary>
    public enum TextStudioAppearanceValueType { Color, Float, Integer, Boolean, Enum }

    /// <summary>A typed leaf binding. Reordering layers or changing their labels does not change the target.</summary>
    [Serializable]
    public struct TextStudioAppearanceExposure
    {
        [SerializeField] string _name;
        [SerializeField] TextStudioStackItemId _layerId;
        [SerializeField] TextStudioStackItemId _treatmentId;
        [SerializeField] TextStudioAppearanceProperty _property;
        public string Name => _name ?? string.Empty;
        public TextStudioStackItemId LayerId => _layerId;
        public TextStudioStackItemId TreatmentId => _treatmentId;
        public TextStudioAppearanceProperty Property => _property;
        /// <summary>Creates a named parameter binding to a persistent layer identity, typed property, and optional decoration-treatment identity.</summary>
        public TextStudioAppearanceExposure(string name, TextStudioStackItemId layerId, TextStudioAppearanceProperty property, TextStudioStackItemId treatmentId = default)
        { _name = name; _layerId = layerId; _property = property; _treatmentId = treatmentId; }
        /// <summary>Checks whether a parameter name is a valid appearance identifier that avoids reserved invocation parameters.</summary>
        public static bool IsValidName(string name) => TextStudioAppearanceActivation.IsValidId(name) && !IsReserved(name);
        /// <summary>Checks case-insensitively whether a name is ref, mode, tint, or opacity.</summary>
        public static bool IsReserved(string name) => string.Equals(name, "ref", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "mode", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "tint", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "opacity", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Metadata describing an exposed appearance property, its value type, units, and named enum choices.</summary>
    public sealed class TextStudioAppearancePropertyDescriptor
    {
        public TextStudioAppearanceProperty Property { get; }
        public string Label { get; }
        public TextStudioAppearanceValueType ValueType { get; }
        public string Unit { get; }
        public IReadOnlyList<string> EnumValues { get; }
        internal TextStudioAppearancePropertyDescriptor(TextStudioAppearanceProperty property, string label, TextStudioAppearanceValueType type, string unit = "", string[] enumValues = null)
        { Property = property; Label = label; ValueType = type; Unit = unit; EnumValues = Array.AsReadOnly(enumValues ?? Array.Empty<string>()); }
    }

    /// <summary>Typed property metadata and validated reading, writing, and target resolution for exposed appearance parameters.</summary>
#if UNITY_6000_7_OR_NEWER
    // These descriptors are immutable metadata, with no per-session or asset ownership.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class TextStudioAppearanceProperties
    {
        public static IReadOnlyList<TextStudioAppearancePropertyDescriptor> All { get; } = Array.AsReadOnly(new[]
        {
            new TextStudioAppearancePropertyDescriptor(TextStudioAppearanceProperty.PaintColor, "Paint color", TextStudioAppearanceValueType.Color, ""),
            new TextStudioAppearancePropertyDescriptor(TextStudioAppearanceProperty.Opacity, "Layer opacity", TextStudioAppearanceValueType.Float, "0..1"),
            new TextStudioAppearancePropertyDescriptor(TextStudioAppearanceProperty.Enabled, "Enabled", TextStudioAppearanceValueType.Boolean, ""),
            new TextStudioAppearancePropertyDescriptor(TextStudioAppearanceProperty.AnimatedTint, "Animated tint", TextStudioAppearanceValueType.Boolean, ""),
            new TextStudioAppearancePropertyDescriptor(TextStudioAppearanceProperty.BlendMode, "Blend mode", TextStudioAppearanceValueType.Enum, "", Enum.GetNames(typeof(TextStudio2DBlendMode))),
            new TextStudioAppearancePropertyDescriptor(TextStudioAppearanceProperty.StrokeWidth, "Stroke width", TextStudioAppearanceValueType.Float, "layer units"),
            new TextStudioAppearancePropertyDescriptor(TextStudioAppearanceProperty.StrokeSoftness, "Stroke softness", TextStudioAppearanceValueType.Float, "layer units"),
            new TextStudioAppearancePropertyDescriptor(TextStudioAppearanceProperty.StrokeAlignment, "Stroke alignment", TextStudioAppearanceValueType.Float, "-1..1"),
            new TextStudioAppearancePropertyDescriptor(TextStudioAppearanceProperty.ShadowX, "Shadow horizontal offset", TextStudioAppearanceValueType.Float, "layer units"),
            new TextStudioAppearancePropertyDescriptor(TextStudioAppearanceProperty.ShadowY, "Shadow vertical offset", TextStudioAppearanceValueType.Float, "layer units"),
            new TextStudioAppearancePropertyDescriptor(TextStudioAppearanceProperty.ShadowBlur, "Shadow blur", TextStudioAppearanceValueType.Float, "layer units"),
            new TextStudioAppearancePropertyDescriptor(TextStudioAppearanceProperty.ShadowSpread, "Shadow spread", TextStudioAppearanceValueType.Float, "layer units"),
            new TextStudioAppearancePropertyDescriptor(TextStudioAppearanceProperty.GlowRadius, "Glow radius", TextStudioAppearanceValueType.Float, "layer units"),
            new TextStudioAppearancePropertyDescriptor(TextStudioAppearanceProperty.GlowSpread, "Glow spread", TextStudioAppearanceValueType.Float, "layer units"),
            new TextStudioAppearancePropertyDescriptor(TextStudioAppearanceProperty.FillDilation, "Fill expansion", TextStudioAppearanceValueType.Float, "layer units"),
            new TextStudioAppearancePropertyDescriptor(TextStudioAppearanceProperty.FillSoftness, "Fill softness", TextStudioAppearanceValueType.Float, "layer units"),
            new TextStudioAppearancePropertyDescriptor(TextStudioAppearanceProperty.NoiseSeed, "Noise seed", TextStudioAppearanceValueType.Integer, ""),
        });

        /// <summary>Attempts to retrieve the descriptor for a supported appearance-property identifier.</summary>
        public static bool TryGet(TextStudioAppearanceProperty property, out TextStudioAppearancePropertyDescriptor descriptor)
        {
            int index = (int)property;
            descriptor = index >= 0 && index < All.Count ? All[index] : null;
            return descriptor != null;
        }

        /// <summary>Checks whether a layer supports exposing the specified property, including restrictions on referenced paints and preserved native fills.</summary>
        public static bool IsSupported(in TextStudio2DLayer layer, TextStudioAppearanceProperty property)
        {
            if (layer.Kind == TextStudio2DLayerKind.Fill && layer.Fill.PreservesNativeAppearance &&
                (property == TextStudioAppearanceProperty.PaintColor || property == TextStudioAppearanceProperty.NoiseSeed ||
                 property == TextStudioAppearanceProperty.FillDilation || property == TextStudioAppearanceProperty.FillSoftness)) return false;
            switch (property)
            {
                case TextStudioAppearanceProperty.PaintColor: return TextStudio2DStyleCompiler.UsesPaint(layer.Kind) && layer.Paint.Source == null && layer.Paint.Kind == TextStudio2DPaintKind.Solid;
                case TextStudioAppearanceProperty.NoiseSeed: return TextStudio2DStyleCompiler.UsesPaint(layer.Kind) && layer.Paint.Source == null && layer.Paint.Kind == TextStudio2DPaintKind.ProceduralNoise;
                case TextStudioAppearanceProperty.Opacity:
                case TextStudioAppearanceProperty.Enabled:
                case TextStudioAppearanceProperty.AnimatedTint:
                case TextStudioAppearanceProperty.BlendMode: return true;
                case TextStudioAppearanceProperty.StrokeWidth:
                case TextStudioAppearanceProperty.StrokeSoftness:
                case TextStudioAppearanceProperty.StrokeAlignment: return layer.Kind == TextStudio2DLayerKind.Stroke;
                case TextStudioAppearanceProperty.ShadowX:
                case TextStudioAppearanceProperty.ShadowY:
                case TextStudioAppearanceProperty.ShadowBlur:
                case TextStudioAppearanceProperty.ShadowSpread: return layer.Kind == TextStudio2DLayerKind.InnerShadow || layer.Kind == TextStudio2DLayerKind.OuterShadow;
                case TextStudioAppearanceProperty.GlowRadius:
                case TextStudioAppearanceProperty.GlowSpread: return layer.Kind == TextStudio2DLayerKind.InnerGlow || layer.Kind == TextStudio2DLayerKind.OuterGlow;
                case TextStudioAppearanceProperty.FillDilation:
                case TextStudioAppearanceProperty.FillSoftness: return layer.Kind == TextStudio2DLayerKind.Fill;
                default: return false;
            }
        }

        /// <summary>Attempts to format a supported layer property as an authored parameter value using culture-independent numeric formatting.</summary>
        public static bool TryRead(in TextStudio2DLayer layer, TextStudioAppearanceProperty property, out string authoredValue)
        {
            authoredValue = string.Empty;
            if (!IsSupported(in layer, property)) return false;
            switch (property)
            {
                case TextStudioAppearanceProperty.PaintColor: authoredValue = TextStudioColorUtility.ToHex(layer.Paint.Color); return true;
                case TextStudioAppearanceProperty.Enabled: authoredValue = layer.IsEnabled ? "true" : "false"; return true;
                case TextStudioAppearanceProperty.AnimatedTint: authoredValue = layer.ParticipatesInAnimatedTint ? "true" : "false"; return true;
                case TextStudioAppearanceProperty.BlendMode: authoredValue = layer.BlendMode.ToString(); return true;
                case TextStudioAppearanceProperty.NoiseSeed: authoredValue = layer.Paint.NoiseSeed.ToString(CultureInfo.InvariantCulture); return true;
            }
            float value = property switch
            {
                TextStudioAppearanceProperty.Opacity => layer.Opacity,
                TextStudioAppearanceProperty.StrokeWidth => layer.Stroke.Width,
                TextStudioAppearanceProperty.StrokeSoftness => layer.Stroke.Softness,
                TextStudioAppearanceProperty.StrokeAlignment => layer.Stroke.Alignment,
                TextStudioAppearanceProperty.ShadowX => layer.Shadow.Offset.x,
                TextStudioAppearanceProperty.ShadowY => layer.Shadow.Offset.y,
                TextStudioAppearanceProperty.ShadowBlur => layer.Shadow.Blur,
                TextStudioAppearanceProperty.ShadowSpread => layer.Shadow.Spread,
                TextStudioAppearanceProperty.GlowRadius => layer.Glow.Radius,
                TextStudioAppearanceProperty.GlowSpread => layer.Glow.Spread,
                TextStudioAppearanceProperty.FillDilation => layer.Fill.Dilation,
                TextStudioAppearanceProperty.FillSoftness => layer.Fill.Softness,
                _ => 0f
            };
            authoredValue = value.ToString("R", CultureInfo.InvariantCulture);
            return true;
        }

        /// <summary>Attempts to parse and validate a typed parameter value, returning a changed layer or preserving the original with a diagnostic.</summary>
        public static bool TryWrite(in TextStudio2DLayer layer, TextStudioAppearanceProperty property, string value, out TextStudio2DLayer changed, out string error)
        {
            changed = layer;
            error = "This field is unavailable on the bound layer.";
            if (!IsSupported(in layer, property) || !TryGet(property, out TextStudioAppearancePropertyDescriptor descriptor)) return false;
            switch (descriptor.ValueType)
            {
                case TextStudioAppearanceValueType.Color:
                    if (TextStudioColorUtility.TryParseColor(value, out Color32 color)) { changed = layer.WithPaint(TextStudio2DPaint.Solid(color)); error = string.Empty; return true; }
                    error = "Use a named color or a valid hexadecimal color."; return false;
                case TextStudioAppearanceValueType.Boolean:
                    if (bool.TryParse(value, out bool flag)) { changed = property == TextStudioAppearanceProperty.Enabled ? layer.WithState(flag, layer.IsSolo) : layer.WithAnimatedTint(flag); error = string.Empty; return true; }
                    error = "Use true or false."; return false;
                case TextStudioAppearanceValueType.Enum:
                    if (Enum.TryParse(value, true, out TextStudio2DBlendMode blend) && Enum.IsDefined(typeof(TextStudio2DBlendMode), blend)) { changed = layer.WithCompositing(layer.Opacity, blend); error = string.Empty; return true; }
                    error = "Use one of the field's named choices."; return false;
                case TextStudioAppearanceValueType.Integer:
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed)) { changed = layer.WithPaint(layer.Paint.WithAppearanceNoiseSeed(seed)); error = string.Empty; return true; }
                    error = "Use a whole number."; return false;
            }
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) || !float.IsFinite(number)) { error = "Use a finite number."; return false; }
            if (property == TextStudioAppearanceProperty.Opacity && (number < 0f || number > 1f)) { error = "Opacity must be between 0 and 1."; return false; }
            if (property == TextStudioAppearanceProperty.StrokeAlignment && (number < -1f || number > 1f)) { error = "Alignment must be between -1 and 1."; return false; }
            if (number < 0f && property != TextStudioAppearanceProperty.ShadowX && property != TextStudioAppearanceProperty.ShadowY && property != TextStudioAppearanceProperty.FillDilation && property != TextStudioAppearanceProperty.StrokeAlignment) { error = "This distance cannot be negative."; return false; }
            changed = layer.WithAppearanceNumber(property, number);
            error = string.Empty;
            return true;
        }

        /// <summary>Resolves a decoration treatment as a layer value, or returns the original layer when no treatment identity is supplied.</summary>
        public static bool TryGetTarget(in TextStudio2DLayer layer, TextStudioStackItemId treatmentId, out TextStudio2DLayer target)
        {
            target = layer;
            if (!treatmentId.IsValid) return true;
            for (int index = 0; index < layer.Decoration.TreatmentCount; index++)
            {
                TextStudio2DSurfaceTreatment treatment = layer.Decoration.GetTreatment(index);
                if (!treatment.ItemId.Equals(treatmentId)) continue;
                TextStudio2DLayer owner = layer.WithState(true).WithAnimatedTint(true);
                target = treatment.ToLayer(in owner);
                return true;
            }
            return false;
        }
    }

    /// <summary>Applies named appearance-tag arguments to explicitly exposed layer and decoration-treatment properties.</summary>
    public static class TextStudioAppearanceParameterBinder
    {
        /// <summary>Applies the last assignment of each named exposed parameter to copied effective settings, retaining authored values when binding or validation fails.</summary>
        public static TextStudio2DStyleSettings Apply(in TextStudio2DStyleSettings settings, TextStudioTagParameters parameters, List<string> diagnostics)
        {
            TextStudio2DStyleSettings result = settings.BaseStyle != null && settings.InheritanceMode != TextStudio2DStyleInheritanceMode.Replace
                ? TextStudio2DStyleCompiler.FlattenAuthoredSettings(in settings, diagnostics) : settings.Clone();
            TextStudioAppearanceExposure[] exposures = result.Exposures;
            TextStudio2DLayer[] layers = result.CloneAuthoredLayers();
            Dictionary<string, TextStudioAppearanceExposure> bindings = new Dictionary<string, TextStudioAppearanceExposure>(StringComparer.OrdinalIgnoreCase);
            foreach (TextStudioAppearanceExposure exposure in exposures)
            {
                if (!TextStudioAppearanceExposure.IsValidName(exposure.Name)) { diagnostics?.Add("Invalid or reserved exposed parameter '" + exposure.Name + "'."); continue; }
                if (bindings.ContainsKey(exposure.Name)) { diagnostics?.Add("Duplicate exposed parameter '" + exposure.Name + "'. The first binding is used."); continue; }
                bindings.Add(exposure.Name, exposure);
                if (!TryFindTarget(layers, exposure, out _, out _)) diagnostics?.Add("Exposed parameter '" + exposure.Name + "' has a missing target. Repair or remove its binding.");
            }
            for (int parameterIndex = 0; parameterIndex < parameters.NamedCount; parameterIndex++)
            {
                TextStudioNamedTagParameter parameter = parameters.Named[parameterIndex];
                if (TextStudioAppearanceExposure.IsReserved(parameter.Name)) continue;
                // The shared parameter model uses the last authored assignment.
                bool replaced = false;
                for (int later = parameterIndex + 1; later < parameters.NamedCount; later++)
                    if (string.Equals(parameter.Name, parameters.Named[later].Name, StringComparison.OrdinalIgnoreCase)) { replaced = true; break; }
                if (replaced) continue;
                if (!bindings.TryGetValue(parameter.Name, out TextStudioAppearanceExposure binding)) { diagnostics?.Add("Unknown appearance parameter '" + parameter.Name + "'. Expose the intended field in the style first."); continue; }
                if (!TryFindTarget(layers, binding, out int layerIndex, out TextStudio2DLayer target)) continue;
                string value = parameter.IsFlag ? "true" : parameter.Value;
                if (parameter.Operation != TextStudioTagParameterOperation.Set) { diagnostics?.Add("Appearance parameter '" + parameter.Name + "' requires a direct value."); continue; }
                if (!TextStudioAppearanceProperties.TryWrite(in target, binding.Property, value, out TextStudio2DLayer changed, out string error)) { diagnostics?.Add(parameter.Name + ": " + error + " The authored value is retained."); continue; }
                layers[layerIndex] = binding.TreatmentId.IsValid ? layers[layerIndex].WithAppearanceTreatment(binding.TreatmentId, in changed) : changed;
            }
            result.SetLayers(layers);
            return result;
        }

        static bool TryFindTarget(TextStudio2DLayer[] layers, TextStudioAppearanceExposure binding, out int index, out TextStudio2DLayer target)
        {
            for (index = 0; index < layers.Length; index++)
                if (layers[index].ItemId.Equals(binding.LayerId))
                    return TextStudioAppearanceProperties.TryGetTarget(in layers[index], binding.TreatmentId, out target) && TextStudioAppearanceProperties.IsSupported(in target, binding.Property);
            index = -1; target = default; return false;
        }
    }

    public partial struct TextStudio2DLayer
    {
        internal TextStudio2DLayer WithAppearanceNumber(TextStudioAppearanceProperty property, float number)
        {
            TextStudio2DLayer copy = this;
            switch (property)
            {
                case TextStudioAppearanceProperty.Opacity: copy._opacity = number; break;
                case TextStudioAppearanceProperty.StrokeWidth: copy._stroke = _stroke.WithAppearanceWidth(number); break;
                case TextStudioAppearanceProperty.StrokeSoftness: copy._stroke = _stroke.WithSoftness(number); break;
                case TextStudioAppearanceProperty.StrokeAlignment: copy._stroke = _stroke.WithAlignment(number); break;
                case TextStudioAppearanceProperty.ShadowX:
                case TextStudioAppearanceProperty.ShadowY:
                case TextStudioAppearanceProperty.ShadowBlur:
                case TextStudioAppearanceProperty.ShadowSpread:
                    Vector2 offset = _shadow.Offset;
                    if (property == TextStudioAppearanceProperty.ShadowX) offset.x = number;
                    if (property == TextStudioAppearanceProperty.ShadowY) offset.y = number;
                    copy._shadow = new TextStudio2DShadowSpec(offset, property == TextStudioAppearanceProperty.ShadowBlur ? number : _shadow.Blur, property == TextStudioAppearanceProperty.ShadowSpread ? number : _shadow.Spread, _shadow.Units).WithRendering(_shadow.Rendering); break;
                case TextStudioAppearanceProperty.GlowRadius:
                case TextStudioAppearanceProperty.GlowSpread:
                    copy._glow = new TextStudio2DGlowSpec(property == TextStudioAppearanceProperty.GlowRadius ? number : _glow.Radius, property == TextStudioAppearanceProperty.GlowSpread ? number : _glow.Spread, _glow.Units).WithRendering(_glow.Rendering); break;
                case TextStudioAppearanceProperty.FillDilation:
                case TextStudioAppearanceProperty.FillSoftness:
                    copy._fill = new TextStudio2DFillSpec(property == TextStudioAppearanceProperty.FillDilation ? number : _fill.Dilation, property == TextStudioAppearanceProperty.FillSoftness ? number : _fill.Softness, _fill.Units, _fill.ColorFace); break;
            }
            return copy;
        }

        internal TextStudio2DLayer WithAppearanceTreatment(TextStudioStackItemId id, in TextStudio2DLayer changed)
        {
            TextStudio2DLayer copy = this;
            TextStudio2DSurfaceTreatment[] treatments = new TextStudio2DSurfaceTreatment[_decoration.TreatmentCount];
            for (int index = 0; index < treatments.Length; index++)
            {
                treatments[index] = _decoration.GetTreatment(index);
                if (treatments[index].ItemId.Equals(id)) treatments[index] = treatments[index].WithAppearanceFields(in changed);
            }
            copy._decoration = _decoration.WithTreatments(treatments);
            return copy;
        }
    }

    public partial struct TextStudio2DPaint
    {
        internal TextStudio2DPaint WithAppearanceNoiseSeed(int seed) { TextStudio2DPaint copy = Clone(); copy._noiseSeed = seed; return copy; }
    }
    public partial struct TextStudio2DStrokeSpec
    {
        internal TextStudio2DStrokeSpec WithAppearanceWidth(float width) { TextStudio2DStrokeSpec copy = Clone(); copy._width = width; return copy; }
    }
    public partial struct TextStudio2DSurfaceTreatment
    {
        internal TextStudio2DSurfaceTreatment WithAppearanceFields(in TextStudio2DLayer layer)
        {
            TextStudio2DSurfaceTreatment copy = this;
            copy._enabled = layer.IsEnabled; copy._opacity = layer.Opacity; copy._blendMode = layer.BlendMode;
            copy._participatesInAnimatedTint = layer.ParticipatesInAnimatedTint; copy._paint = layer.Paint.Clone();
            copy._fill = layer.Fill; copy._stroke = layer.Stroke.Clone(); copy._shadow = layer.Shadow; copy._glow = layer.Glow;
            return copy;
        }
    }
}
