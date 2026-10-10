using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TextStudio.Editor
{
    internal enum TextStudioEffectCodeAssetKind
    {
        Composed = 1,
        Serialized = 2
    }

    internal sealed class TextStudioEffectCodePackage
    {
        public int SchemaMinor;
        public string WriterVersion = string.Empty;
        public int RootIndex;
        public readonly List<int> RequiredCapabilities = new List<int>();
        public readonly List<TextStudioEffectCodeRequirement> RequiredEffects =
            new List<TextStudioEffectCodeRequirement>();
        public readonly List<TextStudioEffectCodeAsset> Assets = new List<TextStudioEffectCodeAsset>();

        public TextStudioEffectCodeAsset Root => RootIndex >= 0 && RootIndex < Assets.Count
            ? Assets[RootIndex]
            : null;
    }

    internal sealed class TextStudioEffectCodeAsset
    {
        public int Index;
        public TextStudioEffectCodeAssetKind Kind;
        public string Name = string.Empty;
        public string TypeName = string.Empty;
        public string AssemblyName = string.Empty;
        public string DeclaredEffectId = string.Empty;
        public string Json = string.Empty;
        public TextStudioEffectCodeComposition Composition;
        public readonly List<TextStudioEffectCodeReference> References = new List<TextStudioEffectCodeReference>();
        public int SerializationSchema = 1;

        public string EffectId => Composition != null ? Composition.Id : DeclaredEffectId;
    }

    internal sealed class TextStudioEffectCodeRequirement
    {
        public string EffectId = string.Empty;
        public string TypeName = string.Empty;
        public string AssemblyName = string.Empty;
    }

    internal sealed class TextStudioEffectCodeComposition
    {
        public string Id = "composed";
        public TextStudioCompositionAuthoringSpace AuthoringSpace =
            TextStudioCompositionAuthoringSpace.TwoD;
        public float RevealActiveBlendDuration;
        public bool TagEnabled = true;
        public string OpeningMarkup = string.Empty;
        public TextStudioTagExpansionClosingMode ClosingMode = TextStudioTagExpansionClosingMode.Auto;
        public string CustomClosingMarkup = string.Empty;
        public readonly List<TextStudioEffectCodeLayer> Layers = new List<TextStudioEffectCodeLayer>();
    }

    internal sealed class TextStudioEffectCodeLayer
    {
        public bool Enabled = true;
        public TextStudioCompositionLayerKind Kind;
        public TextStudioCompositionChannel Channel;
        public string RenderPropertyId = string.Empty;
        public string EffectId = TextStudioBuiltInEffectIds.Wave;
        public string Name = string.Empty;
        public string EffectParameters = string.Empty;
        public TextStudioPhaseMask Phases;
        public TextStudioEffectPlaybackPreset Playback;
        public int CustomPlaybackIndex = -1;
        public TextStudioEffectCurvePreset Curve;
        public int CustomCurveAssetIndex = -1;
        public int WritingStyleIndex = -1;
        public AnimationCurve CustomCurve;
        public readonly List<TextStudioEffectCodeParameter> Parameters =
            new List<TextStudioEffectCodeParameter>();
        public Vector3 Vector;
        public Vector2 BottomLeft;
        public Vector2 TopLeft;
        public Vector2 TopRight;
        public Vector2 BottomRight;
        public TextStudioCompositionColorMode ColorMode;
        public Gradient ColorGradient;
        public float Scalar;
        public uint Unsigned = 1u;
        public float Intensity;
        public float TimeOffset;
        public PhaseParams Timing;
        public TextStudioSequenceSettings Sequence;
        public TextStudioEffectAreaSettings Area;
        public EffectBlendMode BlendMode;
        public TextStudioSurfaceMask SurfaceMask = TextStudioSurfaceMask.All;
        public string SpatialInfluenceJson = string.Empty;
        public string SourceColorSelectorJson = string.Empty;
        public string SelectorJson = string.Empty;
    }

    internal sealed class TextStudioEffectCodeParameter
    {
        public string Name = string.Empty;
        public string Value = string.Empty;
        public TextStudioTagParameterOperation Operation = TextStudioTagParameterOperation.Set;
        public string Baseline = string.Empty;
    }

    internal sealed class TextStudioEffectCodeReference
    {
        public string PropertyPath = string.Empty;
        public int LocalAssetIndex = -1;
        public string Guid = string.Empty;
        public long LocalFileId;
        public string TypeName = string.Empty;
        public string AssemblyName = string.Empty;
    }

    internal static class TextStudioEffectCodeFormat
    {
        internal const string RawPrefix = "TSFX1R.";
        internal const string BrotliPrefix = "TSFX1B.";
        internal const int MaxEncodedCharacters = 262144;
        internal const int MaxExpandedBytes = 2097152;
        internal const int MaxAssetCount = 256;
        internal const int MaxLayerCount = 4096;

        const int CurrentSchemaMinor = 4;
        const int CapabilityComposedDelta = 1;
        const int CapabilitySerializedAsset = 2;
        const int CapabilityExtensionChannels = 3;
        const int CapabilitySpatialChannels = 4;
        const int CapabilityEffectSequencing = 5;
        const int CapabilityCompositionAuthoringSpace = 6;
        const int CapabilitySourceColorSelection = 7;
        const int CapabilityStructuredEffectLayers = 8;
        const int CapabilityWritingLayers = 9;

        static readonly string[] s_knownEffectIds =
        {
            string.Empty,
            TextStudioBuiltInEffectIds.Wave,
            TextStudioBuiltInEffectIds.Shake,
            TextStudioBuiltInEffectIds.Wiggle,
            TextStudioBuiltInEffectIds.Bounce,
            TextStudioBuiltInEffectIds.Swing,
            TextStudioBuiltInEffectIds.Pendulum,
            TextStudioBuiltInEffectIds.Dangle,
            TextStudioBuiltInEffectIds.Pulse,
            TextStudioBuiltInEffectIds.Blink,
            TextStudioBuiltInEffectIds.Shimmer,
            TextStudioBuiltInEffectIds.GradientFlow,
            TextStudioBuiltInEffectIds.ColorSweep,
            TextStudioBuiltInEffectIds.ColorPulse,
            TextStudioBuiltInEffectIds.Pop,
            TextStudioBuiltInEffectIds.Float,
            TextStudioBuiltInEffectIds.Rainbow,
            TextStudioBuiltInEffectIds.Fade,
            TextStudioBuiltInEffectIds.Size,
            TextStudioBuiltInEffectIds.Rotate,
            TextStudioBuiltInEffectIds.Shear,
            TextStudioBuiltInEffectIds.SlideHorizontal,
            TextStudioBuiltInEffectIds.SlideVertical,
            TextStudioBuiltInEffectIds.Expand,
            TextStudioBuiltInEffectIds.Typewriter,
            TextStudioBuiltInEffectIds.Decrypt,
            TextStudioBuiltInEffectIds.Glitch,
            TextStudioBuiltInEffectIds.Emphasis,
            TextStudioBuiltInEffectIds.Elastic,
            TextStudioBuiltInEffectIds.Scatter2D,
            TextStudioBuiltInEffectIds.Tracking,
            TextStudioBuiltInEffectIds.CornerWarp,
            TextStudioBuiltInEffectIds.Focus,
            TextStudioBuiltInEffectIds.Dissolve,
            TextStudioBuiltInEffectIds.OutlineGlow,
            TextStudioBuiltInEffectIds.SurfaceSweep,
            TextStudioBuiltInEffectIds.ChromaticGlitch,
            TextStudioBuiltInEffectIds.PixelGrid,
            TextStudioBuiltInEffectIds.Burn
        };

        internal static string Encode(TextStudioEffectCodePackage package, out int payloadBytes)
        {
            byte[] raw = WritePackage(package);
            byte[] compressed = Compress(raw);
            bool useCompressed = compressed.Length + BrotliPrefix.Length < raw.Length + RawPrefix.Length;
            byte[] selected = useCompressed ? compressed : raw;
            payloadBytes = selected.Length;
            return (useCompressed ? BrotliPrefix : RawPrefix) + ToBase64Url(selected);
        }

        internal static bool TryDecode(string source, out TextStudioEffectCodePackage package, out string error)
        {
            package = null;
            error = string.Empty;
            if (!TryExtractToken(source, out string token, out bool compressed, out error))
                return false;

            if (token.Length > MaxEncodedCharacters)
            {
                error = "The Effect Code is larger than the supported 256 KB limit.";
                return false;
            }

            try
            {
                byte[] encoded = FromBase64Url(token);
                byte[] raw = compressed ? Decompress(encoded) : encoded;
                if (raw.Length > MaxExpandedBytes)
                {
                    error = "The Effect Code expands beyond the supported 2 MB limit.";
                    return false;
                }

                package = ReadPackage(raw);
                if (!ValidatePackage(package, out error))
                {
                    package = null;
                    return false;
                }
                return true;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                error = "The Effect Code is malformed or incomplete. " + exception.Message;
                package = null;
                return false;
            }
        }

        internal static bool TryExtractToken(
            string source,
            out string encodedPayload,
            out bool compressed,
            out string error)
        {
            encodedPayload = string.Empty;
            compressed = false;
            error = string.Empty;
            string text = source ?? string.Empty;
            int rawIndex = text.IndexOf(RawPrefix, StringComparison.Ordinal);
            int brotliIndex = text.IndexOf(BrotliPrefix, StringComparison.Ordinal);
            int prefixIndex;
            string prefix;
            if (rawIndex < 0 && brotliIndex < 0)
            {
                error = "No Text Studio Effect Code was found. Expected a code beginning with TSFX1R. or TSFX1B.";
                return false;
            }
            if (rawIndex >= 0 && (brotliIndex < 0 || rawIndex < brotliIndex))
            {
                prefixIndex = rawIndex;
                prefix = RawPrefix;
            }
            else
            {
                prefixIndex = brotliIndex;
                prefix = BrotliPrefix;
                compressed = true;
            }

            int start = prefixIndex + prefix.Length;
            int end = start;
            while (end < text.Length && IsBase64UrlCharacter(text[end]))
                end++;
            if (end == start)
            {
                error = "The Text Studio Effect Code has no payload.";
                return false;
            }

            encodedPayload = text.Substring(start, end - start);
            return true;
        }

        internal static string GetKnownEffectId(int wireId)
        {
            return wireId > 0 && wireId < s_knownEffectIds.Length ? s_knownEffectIds[wireId] : string.Empty;
        }

        internal static int GetKnownEffectWireId(string effectId)
        {
            for (int i = 1; i < s_knownEffectIds.Length; i++)
            {
                if (string.Equals(s_knownEffectIds[i], effectId, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return 0;
        }

        internal static TextStudioEffectCodeLayer CreateFrozenDefaults(
            TextStudioCompositionLayerKind kind,
            TextStudioCompositionChannel channel,
            string effectId)
        {
            string normalizedEffectId = string.IsNullOrWhiteSpace(effectId)
                ? TextStudioBuiltInEffectIds.Wave
                : effectId;
            TextStudioEffectCodeLayer layer = new TextStudioEffectCodeLayer
            {
                Enabled = true,
                Kind = kind,
                Channel = channel,
                EffectId = normalizedEffectId,
                Name = kind == TextStudioCompositionLayerKind.Channel
                    ? GetDefaultChannelName(channel)
                    : normalizedEffectId,
                EffectParameters = string.Empty,
                Phases = kind == TextStudioCompositionLayerKind.Channel ? TextStudioPhaseMask.Active : TextStudioPhaseMask.All,
                Playback = TextStudioEffectPlaybackPreset.Loop,
                Curve = TextStudioEffectCurvePreset.Sine,
                CustomCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f),
                Vector = new Vector3(0f, 0.15f, 0f),
                BottomLeft = Vector2.zero,
                TopLeft = new Vector2(-0.08f, 0f),
                TopRight = new Vector2(0.08f, 0f),
                BottomRight = Vector2.zero,
                ColorMode = TextStudioCompositionColorMode.MultiplyExistingColor,
                ColorGradient = CreateFrozenDefaultGradient(),
                Scalar = 0f,
                Unsigned = 1u,
                Intensity = 1f,
                TimeOffset = 0f,
                Timing = PhaseParams.NoOffset,
                Sequence = TextStudioSequenceSettings.Unstaggered,
                Area = TextStudioEffectAreaSettings.Default,
                BlendMode = EffectBlendMode.Additive,
                SurfaceMask = TextStudioSurfaceMask.All,
                SpatialInfluenceJson = string.Empty,
                SourceColorSelectorJson = string.Empty,
                SelectorJson = string.Empty
            };

            if (kind == TextStudioCompositionLayerKind.Writing)
            {
                layer.Name = "Writing";
                layer.Phases = TextStudioPhaseMask.Reveal;
                layer.Playback = TextStudioEffectPlaybackPreset.External;
                layer.Curve = TextStudioEffectCurvePreset.Linear;
                return layer;
            }

            if (kind != TextStudioCompositionLayerKind.Channel)
                return layer;

            switch (channel)
            {
                case TextStudioCompositionChannel.Rotation:
                    layer.Vector = new Vector3(0f, 0f, 12f);
                    break;
                case TextStudioCompositionChannel.Scale:
                    layer.Vector = new Vector3(1.15f, 1.15f, 1f);
                    layer.BlendMode = EffectBlendMode.Multiply;
                    break;
                case TextStudioCompositionChannel.Alpha:
                    layer.Vector = Vector3.zero;
                    layer.Curve = TextStudioEffectCurvePreset.HalfSine;
                    layer.BlendMode = EffectBlendMode.Multiply;
                    break;
                case TextStudioCompositionChannel.Color:
                    layer.Vector = Vector3.zero;
                    layer.Curve = TextStudioEffectCurvePreset.HalfSine;
                    layer.BlendMode = EffectBlendMode.Lerp;
                    break;
                case TextStudioCompositionChannel.Skew:
                    layer.Vector = new Vector3(0.12f, 0f, 0f);
                    break;
                case TextStudioCompositionChannel.UVOffset:
                    layer.Vector = new Vector3(0.08f, 0f, 0f);
                    break;
                case TextStudioCompositionChannel.Pivot:
                    layer.Vector = new Vector3(0f, 0.5f, 0f);
                    break;
                case TextStudioCompositionChannel.CornerDeformation:
                    layer.Vector = Vector3.zero;
                    break;
            }
            return layer;
        }

        internal static string GetDefaultChannelName(TextStudioCompositionChannel channel)
        {
            return channel == TextStudioCompositionChannel.UVOffset ? "UV Offset" : channel.ToString();
        }

        internal static Gradient CreateFrozenDefaultGradient()
        {
            Color warmGold = new Color(1f, 0.75f, 0.25f, 1f);
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(warmGold, 0f),
                    new GradientColorKey(warmGold, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }

        static byte[] WritePackage(TextStudioEffectCodePackage package)
        {
            TlvWriter writer = new TlvWriter();
            writer.UInt(1, CurrentSchemaMinor);
            if (!string.IsNullOrWhiteSpace(package.WriterVersion))
                writer.String(2, package.WriterVersion);
            writer.UInt(3, package.RootIndex);
            for (int i = 0; i < package.RequiredCapabilities.Count; i++)
                writer.UInt(4, package.RequiredCapabilities[i]);
            for (int i = 0; i < package.RequiredEffects.Count; i++)
                writer.Record(5, WriteRequirement(package.RequiredEffects[i]));
            for (int i = 0; i < package.Assets.Count; i++)
                writer.Record(6, WriteAsset(package.Assets[i]));
            return writer.ToArray();
        }

        static byte[] WriteAsset(TextStudioEffectCodeAsset asset)
        {
            TlvWriter writer = new TlvWriter();
            writer.UInt(1, asset.Index);
            writer.UInt(2, (int)asset.Kind);
            if (!string.IsNullOrWhiteSpace(asset.Name))
                writer.String(3, asset.Name);
            if (!string.IsNullOrWhiteSpace(asset.TypeName))
                writer.String(4, asset.TypeName);
            if (!string.IsNullOrWhiteSpace(asset.AssemblyName))
                writer.String(5, asset.AssemblyName);
            if (asset.Kind == TextStudioEffectCodeAssetKind.Composed)
                writer.Record(6, WriteComposition(asset.Composition));
            else
                writer.String(7, asset.Json);
            for (int i = 0; i < asset.References.Count; i++)
                writer.Record(8, WriteReference(asset.References[i]));
            if (!string.IsNullOrWhiteSpace(asset.DeclaredEffectId))
                writer.String(9, asset.DeclaredEffectId);
            if (asset.Kind == TextStudioEffectCodeAssetKind.Serialized)
                writer.UInt(10, asset.SerializationSchema);
            return writer.ToArray();
        }

        static byte[] WriteRequirement(TextStudioEffectCodeRequirement requirement)
        {
            TlvWriter writer = new TlvWriter();
            writer.String(1, requirement.EffectId);
            if (!string.IsNullOrWhiteSpace(requirement.TypeName))
                writer.String(2, requirement.TypeName);
            if (!string.IsNullOrWhiteSpace(requirement.AssemblyName))
                writer.String(3, requirement.AssemblyName);
            return writer.ToArray();
        }

        static byte[] WriteReference(TextStudioEffectCodeReference reference)
        {
            TlvWriter writer = new TlvWriter();
            writer.String(1, reference.PropertyPath);
            if (reference.LocalAssetIndex >= 0)
                writer.UInt(2, reference.LocalAssetIndex + 1);
            if (!string.IsNullOrWhiteSpace(reference.Guid))
            {
                writer.String(3, reference.Guid);
                writer.ULong(4, unchecked((ulong)reference.LocalFileId));
                writer.String(5, reference.TypeName);
                writer.String(6, reference.AssemblyName);
            }
            return writer.ToArray();
        }

        static byte[] WriteComposition(TextStudioEffectCodeComposition composition)
        {
            TlvWriter writer = new TlvWriter();
            writer.String(1, composition.Id);
            if (composition.RevealActiveBlendDuration != 0f)
                writer.Float(2, composition.RevealActiveBlendDuration);
            if (!composition.TagEnabled)
                writer.Bool(3, false);
            if (!string.IsNullOrEmpty(composition.OpeningMarkup))
                writer.String(4, composition.OpeningMarkup);
            if (composition.ClosingMode != TextStudioTagExpansionClosingMode.Auto)
                writer.UInt(5, ClosingModeToWire(composition.ClosingMode));
            if (!string.IsNullOrEmpty(composition.CustomClosingMarkup))
                writer.String(6, composition.CustomClosingMarkup);
            for (int i = 0; i < composition.Layers.Count; i++)
                writer.Record(7, WriteLayer(composition.Layers[i]));
            if (composition.AuthoringSpace != TextStudioCompositionAuthoringSpace.TwoD)
                writer.UInt(8, (int)composition.AuthoringSpace + 1);
            return writer.ToArray();
        }

        static byte[] WriteLayer(TextStudioEffectCodeLayer layer)
        {
            TlvWriter writer = new TlvWriter();
            writer.UInt(1, LayerKindToWire(layer.Kind));
            if (layer.Kind == TextStudioCompositionLayerKind.Channel)
            {
                writer.UInt(2, ChannelToWire(layer.Channel));
                if (!string.IsNullOrEmpty(layer.RenderPropertyId))
                    writer.String(26, layer.RenderPropertyId);
            }
            else if (layer.Kind == TextStudioCompositionLayerKind.Effect)
            {
                int knownEffect = GetKnownEffectWireId(layer.EffectId);
                if (knownEffect != 0)
                    writer.UInt(3, knownEffect);
                else
                    writer.String(4, layer.EffectId);
            }

            TextStudioEffectCodeLayer defaults = CreateFrozenDefaults(layer.Kind, layer.Channel, layer.EffectId);
            if (!layer.Enabled)
                writer.Bool(5, false);
            if (!string.Equals(layer.Name, defaults.Name, StringComparison.Ordinal))
                writer.String(6, layer.Name);
            if (!string.IsNullOrEmpty(layer.EffectParameters))
                writer.String(7, layer.EffectParameters);
            if (layer.Phases != defaults.Phases)
                writer.UInt(8, PhaseMaskToWire(layer.Phases));
            if (layer.Playback != defaults.Playback)
                writer.UInt(9, PlaybackPresetToWire(layer.Playback));
            if (layer.CustomPlaybackIndex >= 0)
                writer.UInt(10, layer.CustomPlaybackIndex + 1);
            if (layer.Curve != defaults.Curve)
                writer.UInt(11, CurvePresetToWire(layer.Curve));
            if (layer.Curve == TextStudioEffectCurvePreset.Custom && !CurvesEqual(layer.CustomCurve, defaults.CustomCurve))
                writer.Record(12, WriteCurve(layer.CustomCurve));
            WriteVector3Delta(writer, 13, layer.Vector, defaults.Vector);
            WriteVector2Delta(writer, 14, layer.BottomLeft, defaults.BottomLeft);
            WriteVector2Delta(writer, 15, layer.TopLeft, defaults.TopLeft);
            WriteVector2Delta(writer, 16, layer.TopRight, defaults.TopRight);
            WriteVector2Delta(writer, 17, layer.BottomRight, defaults.BottomRight);
            if (layer.ColorMode != defaults.ColorMode)
                writer.UInt(18, ColorModeToWire(layer.ColorMode));
            if (layer.Channel == TextStudioCompositionChannel.Color && !GradientsEqual(layer.ColorGradient, defaults.ColorGradient))
                writer.Record(19, WriteGradient(layer.ColorGradient));
            if (layer.Scalar != defaults.Scalar)
                writer.Float(20, layer.Scalar);
            if (layer.Intensity != defaults.Intensity)
                writer.Float(21, layer.Intensity);
            if (layer.TimeOffset != defaults.TimeOffset)
                writer.Float(22, layer.TimeOffset);
            if (!TimingEquals(layer.Timing, defaults.Timing))
                writer.Record(23, WriteTiming(layer.Timing, defaults.Timing));
            if (!layer.Area.Equals(defaults.Area))
                writer.Record(24, WriteArea(layer.Area));
            if (layer.BlendMode != defaults.BlendMode)
                writer.UInt(25, BlendModeToWire(layer.BlendMode));
            if (layer.SurfaceMask != defaults.SurfaceMask)
                writer.UInt(27, (int)layer.SurfaceMask);
            if (!string.IsNullOrEmpty(layer.SpatialInfluenceJson))
                writer.String(28, layer.SpatialInfluenceJson);
            if (layer.Sequence.Order != defaults.Sequence.Order)
                writer.UInt(29, (int)layer.Sequence.Order);
            if (layer.Sequence.Spread != defaults.Sequence.Spread)
                writer.Float(30, layer.Sequence.Spread);
            if (layer.Sequence.Seed != defaults.Sequence.Seed)
                writer.ULong(31, EncodeSignedInt(layer.Sequence.Seed));
            if (!string.IsNullOrEmpty(layer.SourceColorSelectorJson))
                writer.String(32, layer.SourceColorSelectorJson);
            for (int i = 0; i < layer.Parameters.Count; i++)
                writer.Record(33, WriteParameter(layer.Parameters[i]));
            if (layer.CustomCurveAssetIndex >= 0)
                writer.UInt(34, layer.CustomCurveAssetIndex + 1);
            if (layer.Unsigned != defaults.Unsigned)
                writer.ULong(35, layer.Unsigned);
            if (!string.IsNullOrEmpty(layer.SelectorJson))
                writer.String(38, layer.SelectorJson);
            if (layer.WritingStyleIndex >= 0)
                writer.UInt(36, layer.WritingStyleIndex + 1);
            if (layer.Sequence.Direction != defaults.Sequence.Direction)
                writer.UInt(37, (int)layer.Sequence.Direction);
            return writer.ToArray();
        }

        static byte[] WriteParameter(TextStudioEffectCodeParameter parameter)
        {
            TlvWriter writer = new TlvWriter();
            writer.String(1, parameter.Name);
            writer.String(2, parameter.Value);
            if (parameter.Operation != TextStudioTagParameterOperation.Set)
                writer.UInt(3, (int)parameter.Operation + 1);
            writer.String(4, parameter.Baseline);
            return writer.ToArray();
        }

        static TextStudioEffectCodeParameter ReadParameter(byte[] bytes)
        {
            TextStudioEffectCodeParameter parameter = new TextStudioEffectCodeParameter();
            TlvReader reader = new TlvReader(bytes);
            while (reader.TryRead(out int field, out byte[] value))
            {
                switch (field)
                {
                    case 1: parameter.Name = ReadString(value); break;
                    case 2: parameter.Value = ReadString(value); break;
                    case 3:
                        parameter.Operation = (TextStudioTagParameterOperation)(ReadInt(value) - 1);
                        break;
                    case 4: parameter.Baseline = ReadString(value); break;
                }
            }
            return parameter;
        }

        static TextStudioEffectCodePackage ReadPackage(byte[] bytes)
        {
            TextStudioEffectCodePackage package = new TextStudioEffectCodePackage();
            TlvReader reader = new TlvReader(bytes);
            while (reader.TryRead(out int field, out byte[] value))
            {
                switch (field)
                {
                    case 1: package.SchemaMinor = ReadInt(value); break;
                    case 2: package.WriterVersion = ReadString(value); break;
                    case 3: package.RootIndex = ReadInt(value); break;
                    case 4: package.RequiredCapabilities.Add(ReadInt(value)); break;
                    case 5: package.RequiredEffects.Add(ReadRequirement(value)); break;
                    case 6: package.Assets.Add(ReadAsset(value)); break;
                }
            }
            return package;
        }

        static TextStudioEffectCodeAsset ReadAsset(byte[] bytes)
        {
            TextStudioEffectCodeAsset asset = new TextStudioEffectCodeAsset();
            TlvReader reader = new TlvReader(bytes);
            while (reader.TryRead(out int field, out byte[] value))
            {
                switch (field)
                {
                    case 1: asset.Index = ReadInt(value); break;
                    case 2: asset.Kind = (TextStudioEffectCodeAssetKind)ReadInt(value); break;
                    case 3: asset.Name = ReadString(value); break;
                    case 4: asset.TypeName = ReadString(value); break;
                    case 5: asset.AssemblyName = ReadString(value); break;
                    case 6: asset.Composition = ReadComposition(value); break;
                    case 7: asset.Json = ReadString(value); break;
                    case 8: asset.References.Add(ReadReference(value)); break;
                    case 9: asset.DeclaredEffectId = ReadString(value); break;
                    case 10: asset.SerializationSchema = ReadInt(value); break;
                }
            }
            return asset;
        }

        static TextStudioEffectCodeRequirement ReadRequirement(byte[] bytes)
        {
            TextStudioEffectCodeRequirement requirement = new TextStudioEffectCodeRequirement();
            TlvReader reader = new TlvReader(bytes);
            while (reader.TryRead(out int field, out byte[] value))
            {
                if (field == 1) requirement.EffectId = ReadString(value);
                else if (field == 2) requirement.TypeName = ReadString(value);
                else if (field == 3) requirement.AssemblyName = ReadString(value);
            }
            return requirement;
        }

        static TextStudioEffectCodeReference ReadReference(byte[] bytes)
        {
            TextStudioEffectCodeReference reference = new TextStudioEffectCodeReference();
            TlvReader reader = new TlvReader(bytes);
            while (reader.TryRead(out int field, out byte[] value))
            {
                switch (field)
                {
                    case 1: reference.PropertyPath = ReadString(value); break;
                    case 2: reference.LocalAssetIndex = ReadInt(value) - 1; break;
                    case 3: reference.Guid = ReadString(value); break;
                    case 4: reference.LocalFileId = unchecked((long)ReadULong(value)); break;
                    case 5: reference.TypeName = ReadString(value); break;
                    case 6: reference.AssemblyName = ReadString(value); break;
                }
            }
            return reference;
        }

        static TextStudioEffectCodeComposition ReadComposition(byte[] bytes)
        {
            TextStudioEffectCodeComposition composition = new TextStudioEffectCodeComposition();
            TlvReader reader = new TlvReader(bytes);
            while (reader.TryRead(out int field, out byte[] value))
            {
                switch (field)
                {
                    case 1: composition.Id = ReadString(value); break;
                    case 2: composition.RevealActiveBlendDuration = ReadFloat(value); break;
                    case 3: composition.TagEnabled = ReadBool(value); break;
                    case 4: composition.OpeningMarkup = ReadString(value); break;
                    case 5: composition.ClosingMode = WireToClosingMode(ReadInt(value)); break;
                    case 6: composition.CustomClosingMarkup = ReadString(value); break;
                    case 7: composition.Layers.Add(ReadLayer(value)); break;
                    case 8:
                        composition.AuthoringSpace = (TextStudioCompositionAuthoringSpace)Mathf.Clamp(
                            ReadInt(value) - 1,
                            (int)TextStudioCompositionAuthoringSpace.TwoD,
                            (int)TextStudioCompositionAuthoringSpace.ThreeD);
                        break;
                }
            }
            return composition;
        }

        static TextStudioEffectCodeLayer ReadLayer(byte[] bytes)
        {
            int kindWire = 1;
            int channelWire = 1;
            int knownEffectWire = 1;
            string customEffectId = string.Empty;
            string renderPropertyId = string.Empty;
            List<KeyValuePair<int, byte[]>> fields = new List<KeyValuePair<int, byte[]>>();
            TlvReader reader = new TlvReader(bytes);
            while (reader.TryRead(out int field, out byte[] value))
            {
                fields.Add(new KeyValuePair<int, byte[]>(field, value));
                if (field == 1) kindWire = ReadInt(value);
                else if (field == 2) channelWire = ReadInt(value);
                else if (field == 3) knownEffectWire = ReadInt(value);
                else if (field == 4) customEffectId = ReadString(value);
                else if (field == 26) renderPropertyId = ReadString(value);
            }

            TextStudioCompositionLayerKind kind = WireToLayerKind(kindWire);
            TextStudioCompositionChannel channel = WireToChannel(channelWire);
            string effectId = !string.IsNullOrEmpty(customEffectId)
                ? customEffectId
                : GetKnownEffectId(knownEffectWire);
            if (kind == TextStudioCompositionLayerKind.Effect && string.IsNullOrEmpty(effectId))
                throw new InvalidDataException("The code uses an unknown built-in effect identifier.");
            TextStudioEffectCodeLayer layer = CreateFrozenDefaults(kind, channel, effectId);
            layer.RenderPropertyId = renderPropertyId;
            for (int i = 0; i < fields.Count; i++)
            {
                int field = fields[i].Key;
                byte[] value = fields[i].Value;
                switch (field)
                {
                    case 5: layer.Enabled = ReadBool(value); break;
                    case 6: layer.Name = ReadString(value); break;
                    case 7: layer.EffectParameters = ReadString(value); break;
                    case 8: layer.Phases = WireToPhaseMask(ReadInt(value)); break;
                    case 9: layer.Playback = WireToPlaybackPreset(ReadInt(value)); break;
                    case 10: layer.CustomPlaybackIndex = ReadInt(value) - 1; break;
                    case 11: layer.Curve = WireToCurvePreset(ReadInt(value)); break;
                    case 12: layer.CustomCurve = ReadCurve(value); break;
                    case 13: layer.Vector = ReadVector3Delta(value, layer.Vector); break;
                    case 14: layer.BottomLeft = ReadVector2Delta(value, layer.BottomLeft); break;
                    case 15: layer.TopLeft = ReadVector2Delta(value, layer.TopLeft); break;
                    case 16: layer.TopRight = ReadVector2Delta(value, layer.TopRight); break;
                    case 17: layer.BottomRight = ReadVector2Delta(value, layer.BottomRight); break;
                    case 18: layer.ColorMode = WireToColorMode(ReadInt(value)); break;
                    case 19: layer.ColorGradient = ReadGradient(value); break;
                    case 20: layer.Scalar = ReadFloat(value); break;
                    case 21: layer.Intensity = ReadFloat(value); break;
                    case 22: layer.TimeOffset = ReadFloat(value); break;
                    case 23: layer.Timing = ReadTiming(value, layer.Timing); break;
                    case 24: layer.Area = ReadArea(value); break;
                    case 25: layer.BlendMode = WireToBlendMode(ReadInt(value)); break;
                    case 27: layer.SurfaceMask = (TextStudioSurfaceMask)ReadInt(value); break;
                    case 28: layer.SpatialInfluenceJson = ReadString(value); break;
                    case 29: layer.Sequence.Order = (TextStudioSequenceOrder)ReadInt(value); break;
                    case 30: layer.Sequence.Spread = ReadFloat(value); break;
                    case 31: layer.Sequence.Seed = DecodeSignedInt(ReadULong(value)); break;
                    case 32: layer.SourceColorSelectorJson = ReadString(value); break;
                    case 33: layer.Parameters.Add(ReadParameter(value)); break;
                    case 34: layer.CustomCurveAssetIndex = ReadInt(value) - 1; break;
                    case 35: layer.Unsigned = checked((uint)ReadULong(value)); break;
                    case 36: layer.WritingStyleIndex = ReadInt(value) - 1; break;
                    case 38: layer.SelectorJson = ReadString(value); break;
                    case 37: layer.Sequence.Direction = (TextStudioSequenceDirection)ReadInt(value); break;
                }
            }
            return layer;
        }

        static bool ValidatePackage(TextStudioEffectCodePackage package, out string error)
        {
            error = string.Empty;
            if (package.SchemaMinor > CurrentSchemaMinor)
            {
                // A newer minor schema remains readable when it only adds optional length-delimited fields.
            }
            for (int i = 0; i < package.RequiredCapabilities.Count; i++)
            {
                int capability = package.RequiredCapabilities[i];
                if (capability != CapabilityComposedDelta &&
                    capability != CapabilitySerializedAsset &&
                    capability != CapabilityExtensionChannels &&
                    capability != CapabilitySpatialChannels &&
                    capability != CapabilityEffectSequencing &&
                    capability != CapabilityCompositionAuthoringSpace &&
                    capability != CapabilitySourceColorSelection &&
                    capability != CapabilityStructuredEffectLayers &&
                    capability != CapabilityWritingLayers)
                {
                    error = "This Effect Code requires a newer Text Studio capability (" + capability + ").";
                    return false;
                }
            }
            if (package.Assets.Count == 0 || package.Assets.Count > MaxAssetCount)
            {
                error = "The Effect Code contains an unsupported number of assets.";
                return false;
            }
            if (package.RootIndex < 0 || package.RootIndex >= package.Assets.Count)
            {
                error = "The Effect Code does not identify a valid root effect.";
                return false;
            }
            int layerCount = 0;
            HashSet<int> indices = new HashSet<int>();
            HashSet<string> requiredEffectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < package.RequiredEffects.Count; i++)
            {
                string requiredId = package.RequiredEffects[i].EffectId;
                if (string.IsNullOrWhiteSpace(requiredId) || !requiredEffectIds.Add(requiredId))
                {
                    error = "The Effect Code contains an invalid or duplicate required effect id.";
                    return false;
                }
            }
            for (int i = 0; i < package.Assets.Count; i++)
            {
                TextStudioEffectCodeAsset asset = package.Assets[i];
                if (asset.Index < 0 || asset.Index >= package.Assets.Count || !indices.Add(asset.Index))
                {
                    error = "The Effect Code contains invalid asset indices.";
                    return false;
                }
                if (asset.Kind != TextStudioEffectCodeAssetKind.Composed &&
                    asset.Kind != TextStudioEffectCodeAssetKind.Serialized)
                {
                    error = "This Effect Code contains an asset kind that this version of Text Studio cannot import.";
                    return false;
                }
                if (asset.Kind == TextStudioEffectCodeAssetKind.Serialized && asset.SerializationSchema != 1)
                {
                    error = "This Effect Code uses a serialized custom-effect schema that this Text Studio version cannot import.";
                    return false;
                }
                if (asset.Kind == TextStudioEffectCodeAssetKind.Composed)
                {
                    if (asset.Composition == null)
                    {
                        error = "A composed effect record is incomplete.";
                        return false;
                    }
                    layerCount += asset.Composition.Layers.Count;
                    for (int layerIndex = 0; layerIndex < asset.Composition.Layers.Count; layerIndex++)
                    {
                        int playbackIndex = asset.Composition.Layers[layerIndex].CustomPlaybackIndex;
                        if (playbackIndex < -1 || playbackIndex >= package.Assets.Count)
                        {
                            error = "A composed layer references an invalid bundled playback asset.";
                            return false;
                        }
                        int curveIndex = asset.Composition.Layers[layerIndex].CustomCurveAssetIndex;
                        if (curveIndex < -1 || curveIndex >= package.Assets.Count)
                        {
                            error = "A composed layer references an invalid bundled Curve asset.";
                            return false;
                        }
                        TextStudioEffectCodeLayer layer =
                            asset.Composition.Layers[layerIndex];
                        int writingStyleIndex = layer.WritingStyleIndex;
                        if (writingStyleIndex < -1 ||
                            writingStyleIndex >= package.Assets.Count ||
                            (layer.Kind == TextStudioCompositionLayerKind.Writing &&
                             writingStyleIndex < 0) ||
                            (layer.Kind != TextStudioCompositionLayerKind.Writing &&
                             writingStyleIndex >= 0))
                        {
                            error = "A composed layer has an invalid bundled Writing Style reference.";
                            return false;
                        }
                    }
                }
                for (int j = 0; j < asset.References.Count; j++)
                {
                    int localIndex = asset.References[j].LocalAssetIndex;
                    if (localIndex >= package.Assets.Count)
                    {
                        error = "An embedded asset reference points outside the Effect Code.";
                        return false;
                    }
                }
            }
            if (layerCount > MaxLayerCount)
            {
                error = "The Effect Code contains more than 4096 layers.";
                return false;
            }
            package.Assets.Sort((left, right) => left.Index.CompareTo(right.Index));
            for (int i = 0; i < package.Assets.Count; i++)
            {
                if (package.Assets[i].Index != i)
                {
                    error = "The Effect Code asset graph has missing indices.";
                    return false;
                }
            }
            return true;
        }

        internal static void AddRequiredCapabilities(TextStudioEffectCodePackage package)
        {
            bool hasComposed = false;
            bool hasSerialized = false;
            bool hasExtensionChannels = false;
            bool hasSpatialChannels = false;
            bool hasEffectSequencing = false;
            bool hasCompositionAuthoringSpace = false;
            bool hasSourceColorSelection = false;
            bool hasStructuredEffectLayers = false;
            bool hasWritingLayers = false;
            for (int i = 0; i < package.Assets.Count; i++)
            {
                hasComposed |= package.Assets[i].Kind == TextStudioEffectCodeAssetKind.Composed;
                hasSerialized |= package.Assets[i].Kind == TextStudioEffectCodeAssetKind.Serialized;
                TextStudioEffectCodeComposition composition = package.Assets[i].Composition;
                if (composition != null)
                {
                    hasCompositionAuthoringSpace |= composition.AuthoringSpace !=
                        TextStudioCompositionAuthoringSpace.TwoD;
                    for (int layerIndex = 0; layerIndex < composition.Layers.Count; layerIndex++)
                    {
                        hasExtensionChannels |= !string.IsNullOrEmpty(
                            composition.Layers[layerIndex].RenderPropertyId);
                        hasSpatialChannels |= composition.Layers[layerIndex].SurfaceMask != TextStudioSurfaceMask.All ||
                            !string.IsNullOrEmpty(composition.Layers[layerIndex].SpatialInfluenceJson);
                        hasEffectSequencing |= !composition.Layers[layerIndex].Sequence.Equals(
                            TextStudioSequenceSettings.Unstaggered);
                        hasSourceColorSelection |= !string.IsNullOrEmpty(
                            composition.Layers[layerIndex].SourceColorSelectorJson);
                        hasStructuredEffectLayers |=
                            composition.Layers[layerIndex].Parameters.Count > 0 ||
                            composition.Layers[layerIndex].CustomCurveAssetIndex >= 0;
                        hasWritingLayers |= composition.Layers[layerIndex].Kind ==
                            TextStudioCompositionLayerKind.Writing;
                    }
                }
            }
            if (hasComposed)
                package.RequiredCapabilities.Add(CapabilityComposedDelta);
            if (hasSerialized)
                package.RequiredCapabilities.Add(CapabilitySerializedAsset);
            if (hasExtensionChannels)
                package.RequiredCapabilities.Add(CapabilityExtensionChannels);
            if (hasSpatialChannels)
                package.RequiredCapabilities.Add(CapabilitySpatialChannels);
            if (hasEffectSequencing)
                package.RequiredCapabilities.Add(CapabilityEffectSequencing);
            if (hasCompositionAuthoringSpace)
                package.RequiredCapabilities.Add(CapabilityCompositionAuthoringSpace);
            if (hasSourceColorSelection)
                package.RequiredCapabilities.Add(CapabilitySourceColorSelection);
            if (hasStructuredEffectLayers)
                package.RequiredCapabilities.Add(CapabilityStructuredEffectLayers);
            if (hasWritingLayers)
                package.RequiredCapabilities.Add(CapabilityWritingLayers);
        }

        static ulong EncodeSignedInt(int value)
        {
            return (uint)((value << 1) ^ (value >> 31));
        }

        static int DecodeSignedInt(ulong value)
        {
            uint encoded = checked((uint)value);
            return (int)(encoded >> 1) ^ -((int)encoded & 1);
        }

        static void WriteVector3Delta(TlvWriter writer, int field, Vector3 value, Vector3 defaults)
        {
            if (value == defaults)
                return;
            TlvWriter delta = new TlvWriter();
            if (value.x != defaults.x) delta.Float(1, value.x);
            if (value.y != defaults.y) delta.Float(2, value.y);
            if (value.z != defaults.z) delta.Float(3, value.z);
            writer.Record(field, delta.ToArray());
        }

        static void WriteVector2Delta(TlvWriter writer, int field, Vector2 value, Vector2 defaults)
        {
            if (value == defaults)
                return;
            TlvWriter delta = new TlvWriter();
            if (value.x != defaults.x) delta.Float(1, value.x);
            if (value.y != defaults.y) delta.Float(2, value.y);
            writer.Record(field, delta.ToArray());
        }

        static Vector3 ReadVector3Delta(byte[] bytes, Vector3 value)
        {
            TlvReader reader = new TlvReader(bytes);
            while (reader.TryRead(out int field, out byte[] data))
            {
                if (field == 1) value.x = ReadFloat(data);
                else if (field == 2) value.y = ReadFloat(data);
                else if (field == 3) value.z = ReadFloat(data);
            }
            return value;
        }

        static Vector2 ReadVector2Delta(byte[] bytes, Vector2 value)
        {
            TlvReader reader = new TlvReader(bytes);
            while (reader.TryRead(out int field, out byte[] data))
            {
                if (field == 1) value.x = ReadFloat(data);
                else if (field == 2) value.y = ReadFloat(data);
            }
            return value;
        }

        static byte[] WriteTiming(PhaseParams value, PhaseParams defaults)
        {
            TlvWriter writer = new TlvWriter();
            if (value.charOffset != defaults.charOffset) writer.Float(1, value.charOffset);
            if (value.wordOffset != defaults.wordOffset) writer.Float(2, value.wordOffset);
            if (value.lineOffset != defaults.lineOffset) writer.Float(3, value.lineOffset);
            if (value.speed != defaults.speed) writer.Float(4, value.speed);
            return writer.ToArray();
        }

        static PhaseParams ReadTiming(byte[] bytes, PhaseParams value)
        {
            TlvReader reader = new TlvReader(bytes);
            while (reader.TryRead(out int field, out byte[] data))
            {
                if (field == 1) value.charOffset = ReadFloat(data);
                else if (field == 2) value.wordOffset = ReadFloat(data);
                else if (field == 3) value.lineOffset = ReadFloat(data);
                else if (field == 4) value.speed = ReadFloat(data);
            }
            return value;
        }

        static byte[] WriteArea(TextStudioEffectAreaSettings area)
        {
            TlvWriter writer = new TlvWriter();
            if (area.Mode != TextStudioEffectAreaMode.Glyph)
                writer.UInt(1, AreaModeToWire(area.Mode));
            if (area.GroupSize != 1)
                writer.UInt(2, area.GroupSize);
            return writer.ToArray();
        }

        static TextStudioEffectAreaSettings ReadArea(byte[] bytes)
        {
            TextStudioEffectAreaMode mode = TextStudioEffectAreaMode.Glyph;
            int groupSize = 1;
            TlvReader reader = new TlvReader(bytes);
            while (reader.TryRead(out int field, out byte[] data))
            {
                if (field == 1) mode = WireToAreaMode(ReadInt(data));
                else if (field == 2) groupSize = ReadInt(data);
            }
            return new TextStudioEffectAreaSettings(mode, groupSize);
        }

        static byte[] WriteCurve(AnimationCurve curve)
        {
            AnimationCurve safeCurve = curve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
            TlvWriter writer = new TlvWriter();
            writer.UInt(1, WrapModeToWire(safeCurve.preWrapMode));
            writer.UInt(2, WrapModeToWire(safeCurve.postWrapMode));
            Keyframe[] keys = safeCurve.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                Keyframe key = keys[i];
                TlvWriter keyWriter = new TlvWriter();
                keyWriter.Float(1, key.time);
                keyWriter.Float(2, key.value);
                keyWriter.Float(3, key.inTangent);
                keyWriter.Float(4, key.outTangent);
                keyWriter.Float(5, key.inWeight);
                keyWriter.Float(6, key.outWeight);
                keyWriter.UInt(7, WeightedModeToWire(key.weightedMode));
                writer.Record(3, keyWriter.ToArray());
            }
            return writer.ToArray();
        }

        static AnimationCurve ReadCurve(byte[] bytes)
        {
            WrapMode pre = WrapMode.Default;
            WrapMode post = WrapMode.Default;
            List<Keyframe> keys = new List<Keyframe>();
            TlvReader reader = new TlvReader(bytes);
            while (reader.TryRead(out int field, out byte[] data))
            {
                if (field == 1) pre = WireToWrapMode(ReadInt(data));
                else if (field == 2) post = WireToWrapMode(ReadInt(data));
                else if (field == 3) keys.Add(ReadKeyframe(data));
            }
            AnimationCurve curve = new AnimationCurve(keys.ToArray())
            {
                preWrapMode = pre,
                postWrapMode = post
            };
            return curve;
        }

        static Keyframe ReadKeyframe(byte[] bytes)
        {
            float time = 0f;
            float value = 0f;
            float inTangent = 0f;
            float outTangent = 0f;
            float inWeight = 0f;
            float outWeight = 0f;
            WeightedMode weightedMode = WeightedMode.None;
            TlvReader reader = new TlvReader(bytes);
            while (reader.TryRead(out int field, out byte[] data))
            {
                switch (field)
                {
                    case 1: time = ReadFloat(data); break;
                    case 2: value = ReadFloat(data); break;
                    case 3: inTangent = ReadFloat(data); break;
                    case 4: outTangent = ReadFloat(data); break;
                    case 5: inWeight = ReadFloat(data); break;
                    case 6: outWeight = ReadFloat(data); break;
                    case 7: weightedMode = WireToWeightedMode(ReadInt(data)); break;
                }
            }
            Keyframe key = new Keyframe(time, value, inTangent, outTangent, inWeight, outWeight)
            {
                weightedMode = weightedMode
            };
            return key;
        }

        static byte[] WriteGradient(Gradient gradient)
        {
            Gradient safeGradient = gradient ?? CreateFrozenDefaultGradient();
            TlvWriter writer = new TlvWriter();
            writer.UInt(1, GradientModeToWire(safeGradient.mode));
            GradientColorKey[] colors = safeGradient.colorKeys;
            for (int i = 0; i < colors.Length; i++)
            {
                TlvWriter key = new TlvWriter();
                key.Float(1, colors[i].time);
                key.Float(2, colors[i].color.r);
                key.Float(3, colors[i].color.g);
                key.Float(4, colors[i].color.b);
                writer.Record(2, key.ToArray());
            }
            GradientAlphaKey[] alphas = safeGradient.alphaKeys;
            for (int i = 0; i < alphas.Length; i++)
            {
                TlvWriter key = new TlvWriter();
                key.Float(1, alphas[i].time);
                key.Float(2, alphas[i].alpha);
                writer.Record(3, key.ToArray());
            }
            return writer.ToArray();
        }

        static Gradient ReadGradient(byte[] bytes)
        {
            GradientMode mode = GradientMode.Blend;
            List<GradientColorKey> colors = new List<GradientColorKey>();
            List<GradientAlphaKey> alphas = new List<GradientAlphaKey>();
            TlvReader reader = new TlvReader(bytes);
            while (reader.TryRead(out int field, out byte[] data))
            {
                if (field == 1) mode = WireToGradientMode(ReadInt(data));
                else if (field == 2) colors.Add(ReadGradientColorKey(data));
                else if (field == 3) alphas.Add(ReadGradientAlphaKey(data));
            }
            Gradient gradient = new Gradient { mode = mode };
            gradient.SetKeys(colors.ToArray(), alphas.ToArray());
            return gradient;
        }

        static GradientColorKey ReadGradientColorKey(byte[] bytes)
        {
            float time = 0f;
            Color color = Color.white;
            TlvReader reader = new TlvReader(bytes);
            while (reader.TryRead(out int field, out byte[] data))
            {
                if (field == 1) time = ReadFloat(data);
                else if (field == 2) color.r = ReadFloat(data);
                else if (field == 3) color.g = ReadFloat(data);
                else if (field == 4) color.b = ReadFloat(data);
            }
            return new GradientColorKey(color, time);
        }

        static GradientAlphaKey ReadGradientAlphaKey(byte[] bytes)
        {
            float time = 0f;
            float alpha = 1f;
            TlvReader reader = new TlvReader(bytes);
            while (reader.TryRead(out int field, out byte[] data))
            {
                if (field == 1) time = ReadFloat(data);
                else if (field == 2) alpha = ReadFloat(data);
            }
            return new GradientAlphaKey(alpha, time);
        }

        static bool TimingEquals(PhaseParams left, PhaseParams right)
        {
            return left.charOffset == right.charOffset && left.wordOffset == right.wordOffset &&
                   left.lineOffset == right.lineOffset && left.speed == right.speed;
        }

        static bool CurvesEqual(AnimationCurve left, AnimationCurve right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.preWrapMode != right.preWrapMode ||
                left.postWrapMode != right.postWrapMode || left.length != right.length)
                return false;
            Keyframe[] leftKeys = left.keys;
            Keyframe[] rightKeys = right.keys;
            for (int i = 0; i < leftKeys.Length; i++)
            {
                if (!leftKeys[i].Equals(rightKeys[i]))
                    return false;
            }
            return true;
        }

        static bool GradientsEqual(Gradient left, Gradient right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.mode != right.mode)
                return false;
            GradientColorKey[] leftColors = left.colorKeys;
            GradientColorKey[] rightColors = right.colorKeys;
            GradientAlphaKey[] leftAlphas = left.alphaKeys;
            GradientAlphaKey[] rightAlphas = right.alphaKeys;
            if (leftColors.Length != rightColors.Length || leftAlphas.Length != rightAlphas.Length)
                return false;
            for (int i = 0; i < leftColors.Length; i++)
            {
                if (!leftColors[i].Equals(rightColors[i])) return false;
            }
            for (int i = 0; i < leftAlphas.Length; i++)
            {
                if (!leftAlphas[i].Equals(rightAlphas[i])) return false;
            }
            return true;
        }

        static byte[] Compress(byte[] raw)
        {
            using (MemoryStream output = new MemoryStream())
            {
                using (BrotliStream stream = new BrotliStream(
                           output,
                           System.IO.Compression.CompressionLevel.Optimal,
                           true))
                    stream.Write(raw, 0, raw.Length);
                return output.ToArray();
            }
        }

        static byte[] Decompress(byte[] compressed)
        {
            using (MemoryStream input = new MemoryStream(compressed, false))
            using (BrotliStream stream = new BrotliStream(input, CompressionMode.Decompress))
            using (MemoryStream output = new MemoryStream())
            {
                byte[] buffer = new byte[8192];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, read);
                    if (output.Length > MaxExpandedBytes)
                        throw new InvalidDataException("Expanded payload exceeds the supported limit.");
                }
                return output.ToArray();
            }
        }

        static string ToBase64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        static byte[] FromBase64Url(string value)
        {
            string padded = value.Replace('-', '+').Replace('_', '/');
            int remainder = padded.Length % 4;
            if (remainder == 2) padded += "==";
            else if (remainder == 3) padded += "=";
            else if (remainder == 1) throw new FormatException("Invalid Base64URL length.");
            return Convert.FromBase64String(padded);
        }

        static bool IsBase64UrlCharacter(char value)
        {
            return value >= 'A' && value <= 'Z' || value >= 'a' && value <= 'z' ||
                   value >= '0' && value <= '9' || value == '-' || value == '_';
        }

        static string ReadString(byte[] value) => Encoding.UTF8.GetString(value);
        static int ReadInt(byte[] value) => checked((int)ReadULong(value));
        static bool ReadBool(byte[] value) => ReadInt(value) != 0;

        static ulong ReadULong(byte[] value)
        {
            int offset = 0;
            ulong result = TlvReader.ReadVarUInt(value, ref offset);
            if (offset != value.Length)
                throw new InvalidDataException("A numeric field contains trailing bytes.");
            return result;
        }

        static float ReadFloat(byte[] value)
        {
            if (value.Length != 4)
                throw new InvalidDataException("A float field has an invalid length.");
            byte[] bytes = (byte[])value.Clone();
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return BitConverter.ToSingle(bytes, 0);
        }

        static int LayerKindToWire(TextStudioCompositionLayerKind value)
        {
            return value switch
            {
                TextStudioCompositionLayerKind.Channel => 1,
                TextStudioCompositionLayerKind.Effect => 2,
                TextStudioCompositionLayerKind.Writing => 3,
                _ => throw new InvalidDataException("Unknown layer kind.")
            };
        }

        static TextStudioCompositionLayerKind WireToLayerKind(int value)
        {
            return value switch
            {
                1 => TextStudioCompositionLayerKind.Channel,
                2 => TextStudioCompositionLayerKind.Effect,
                3 => TextStudioCompositionLayerKind.Writing,
                _ => throw new InvalidDataException("Unknown layer kind.")
            };
        }
        static int ChannelToWire(TextStudioCompositionChannel value) => value switch
        {
            TextStudioCompositionChannel.Position => 1, TextStudioCompositionChannel.Rotation => 2,
            TextStudioCompositionChannel.Scale => 3, TextStudioCompositionChannel.Alpha => 4,
            TextStudioCompositionChannel.Color => 5, TextStudioCompositionChannel.Skew => 6,
            TextStudioCompositionChannel.UVOffset => 7, TextStudioCompositionChannel.Pivot => 8,
            TextStudioCompositionChannel.CornerDeformation => 9, _ => throw new InvalidDataException("Unknown channel.")
        };
        static TextStudioCompositionChannel WireToChannel(int value) => value switch
        {
            1 => TextStudioCompositionChannel.Position, 2 => TextStudioCompositionChannel.Rotation,
            3 => TextStudioCompositionChannel.Scale, 4 => TextStudioCompositionChannel.Alpha,
            5 => TextStudioCompositionChannel.Color, 6 => TextStudioCompositionChannel.Skew,
            7 => TextStudioCompositionChannel.UVOffset, 8 => TextStudioCompositionChannel.Pivot,
            9 => TextStudioCompositionChannel.CornerDeformation, _ => throw new InvalidDataException("Unknown channel.")
        };
        static int PhaseMaskToWire(TextStudioPhaseMask value) =>
            ((value & TextStudioPhaseMask.Reveal) != 0 ? 1 : 0) |
            ((value & TextStudioPhaseMask.Active) != 0 ? 2 : 0) |
            ((value & TextStudioPhaseMask.Hide) != 0 ? 4 : 0);
        static TextStudioPhaseMask WireToPhaseMask(int value)
        {
            if ((value & ~7) != 0) throw new InvalidDataException("Unknown phase mask bits.");
            TextStudioPhaseMask result = TextStudioPhaseMask.None;
            if ((value & 1) != 0) result |= TextStudioPhaseMask.Reveal;
            if ((value & 2) != 0) result |= TextStudioPhaseMask.Active;
            if ((value & 4) != 0) result |= TextStudioPhaseMask.Hide;
            return result;
        }
        static int PlaybackPresetToWire(TextStudioEffectPlaybackPreset value) => value switch
        {
            TextStudioEffectPlaybackPreset.Once => 1, TextStudioEffectPlaybackPreset.Loop => 2,
            TextStudioEffectPlaybackPreset.PingPong => 3, TextStudioEffectPlaybackPreset.Cycle => 4,
            TextStudioEffectPlaybackPreset.Weighted => 5, TextStudioEffectPlaybackPreset.Appearance => 6,
            TextStudioEffectPlaybackPreset.External => 7, TextStudioEffectPlaybackPreset.Custom => 8,
            _ => throw new InvalidDataException("Unknown playback preset.")
        };
        static TextStudioEffectPlaybackPreset WireToPlaybackPreset(int value) => value switch
        {
            1 => TextStudioEffectPlaybackPreset.Once, 2 => TextStudioEffectPlaybackPreset.Loop,
            3 => TextStudioEffectPlaybackPreset.PingPong, 4 => TextStudioEffectPlaybackPreset.Cycle,
            5 => TextStudioEffectPlaybackPreset.Weighted, 6 => TextStudioEffectPlaybackPreset.Appearance,
            7 => TextStudioEffectPlaybackPreset.External, 8 => TextStudioEffectPlaybackPreset.Custom,
            _ => throw new InvalidDataException("Unknown playback preset.")
        };
        static int CurvePresetToWire(TextStudioEffectCurvePreset value) => value switch
        {
            TextStudioEffectCurvePreset.Linear => 1, TextStudioEffectCurvePreset.Sine => 2,
            TextStudioEffectCurvePreset.HalfSine => 3, TextStudioEffectCurvePreset.Square => 4,
            TextStudioEffectCurvePreset.Step => 5, TextStudioEffectCurvePreset.Hold => 6,
            TextStudioEffectCurvePreset.Bounce => 7, TextStudioEffectCurvePreset.EaseIn => 8,
            TextStudioEffectCurvePreset.EaseOut => 9, TextStudioEffectCurvePreset.EaseInOut => 10,
            TextStudioEffectCurvePreset.BackOut => 11, TextStudioEffectCurvePreset.ElasticOut => 12,
            TextStudioEffectCurvePreset.Custom => 13, TextStudioEffectCurvePreset.ReverseLinear => 14,
            _ => throw new InvalidDataException("Unknown curve preset.")
        };
        static TextStudioEffectCurvePreset WireToCurvePreset(int value) => value switch
        {
            1 => TextStudioEffectCurvePreset.Linear, 2 => TextStudioEffectCurvePreset.Sine,
            3 => TextStudioEffectCurvePreset.HalfSine, 4 => TextStudioEffectCurvePreset.Square,
            5 => TextStudioEffectCurvePreset.Step, 6 => TextStudioEffectCurvePreset.Hold,
            7 => TextStudioEffectCurvePreset.Bounce, 8 => TextStudioEffectCurvePreset.EaseIn,
            9 => TextStudioEffectCurvePreset.EaseOut, 10 => TextStudioEffectCurvePreset.EaseInOut,
            11 => TextStudioEffectCurvePreset.BackOut, 12 => TextStudioEffectCurvePreset.ElasticOut,
            13 => TextStudioEffectCurvePreset.Custom, 14 => TextStudioEffectCurvePreset.ReverseLinear,
            _ => throw new InvalidDataException("Unknown curve preset.")
        };
        static int ColorModeToWire(TextStudioCompositionColorMode value) => value == TextStudioCompositionColorMode.Replace ? 1 : 2;
        static TextStudioCompositionColorMode WireToColorMode(int value) => value == 1
            ? TextStudioCompositionColorMode.Replace
            : value == 2 ? TextStudioCompositionColorMode.MultiplyExistingColor : throw new InvalidDataException("Unknown color mode.");
        static int BlendModeToWire(EffectBlendMode value) => value switch
        {
            EffectBlendMode.Additive => 1, EffectBlendMode.Multiply => 2,
            EffectBlendMode.Override => 3, EffectBlendMode.Lerp => 4,
            _ => throw new InvalidDataException("Unknown blend mode.")
        };
        static EffectBlendMode WireToBlendMode(int value) => value switch
        {
            1 => EffectBlendMode.Additive, 2 => EffectBlendMode.Multiply,
            3 => EffectBlendMode.Override, 4 => EffectBlendMode.Lerp,
            _ => throw new InvalidDataException("Unknown blend mode.")
        };
        static int AreaModeToWire(TextStudioEffectAreaMode value) => value switch
        {
            TextStudioEffectAreaMode.Glyph => 1, TextStudioEffectAreaMode.Word => 2,
            TextStudioEffectAreaMode.Line => 3, TextStudioEffectAreaMode.Paragraph => 4,
            TextStudioEffectAreaMode.Range => 5, _ => throw new InvalidDataException("Unknown area mode.")
        };
        static TextStudioEffectAreaMode WireToAreaMode(int value) => value switch
        {
            1 => TextStudioEffectAreaMode.Glyph, 2 => TextStudioEffectAreaMode.Word,
            3 => TextStudioEffectAreaMode.Line, 4 => TextStudioEffectAreaMode.Paragraph,
            5 => TextStudioEffectAreaMode.Range, _ => throw new InvalidDataException("Unknown area mode.")
        };
        static int ClosingModeToWire(TextStudioTagExpansionClosingMode value) => value == TextStudioTagExpansionClosingMode.Custom ? 2 : 1;
        static TextStudioTagExpansionClosingMode WireToClosingMode(int value) => value == 1
            ? TextStudioTagExpansionClosingMode.Auto
            : value == 2 ? TextStudioTagExpansionClosingMode.Custom : throw new InvalidDataException("Unknown closing mode.");
        static int WrapModeToWire(WrapMode value) => value switch
        {
            WrapMode.Default => 1, WrapMode.Once => 2, WrapMode.Loop => 3,
            WrapMode.PingPong => 4, WrapMode.ClampForever => 5,
            _ => throw new InvalidDataException("Unknown curve wrap mode.")
        };
        static WrapMode WireToWrapMode(int value) => value switch
        {
            1 => WrapMode.Default, 2 => WrapMode.Once, 3 => WrapMode.Loop,
            4 => WrapMode.PingPong, 5 => WrapMode.ClampForever, 6 => WrapMode.Clamp,
            _ => throw new InvalidDataException("Unknown curve wrap mode.")
        };
        static int WeightedModeToWire(WeightedMode value) => value switch
        {
            WeightedMode.None => 1, WeightedMode.In => 2, WeightedMode.Out => 3,
            WeightedMode.Both => 4, _ => throw new InvalidDataException("Unknown weighted mode.")
        };
        static WeightedMode WireToWeightedMode(int value) => value switch
        {
            1 => WeightedMode.None, 2 => WeightedMode.In, 3 => WeightedMode.Out,
            4 => WeightedMode.Both, _ => throw new InvalidDataException("Unknown weighted mode.")
        };
        static int GradientModeToWire(GradientMode value) => value switch
        {
            GradientMode.Blend => 1,
            GradientMode.Fixed => 2,
            GradientMode.PerceptualBlend => 3,
            _ => throw new InvalidDataException("Unknown gradient mode.")
        };
        static GradientMode WireToGradientMode(int value) => value switch
        {
            1 => GradientMode.Blend,
            2 => GradientMode.Fixed,
            3 => GradientMode.PerceptualBlend,
            _ => throw new InvalidDataException("Unknown gradient mode.")
        };

        sealed class TlvWriter
        {
            readonly MemoryStream _stream = new MemoryStream();

            public void UInt(int field, int value) => ULong(field, checked((ulong)value));
            public void Bool(int field, bool value) => UInt(field, value ? 1 : 0);
            public void String(int field, string value) => Bytes(field, Encoding.UTF8.GetBytes(value ?? string.Empty));
            public void Record(int field, byte[] value) => Bytes(field, value);

            public void ULong(int field, ulong value)
            {
                using (MemoryStream payload = new MemoryStream())
                {
                    WriteVarUInt(payload, value);
                    Bytes(field, payload.ToArray());
                }
            }

            public void Float(int field, float value)
            {
                byte[] bytes = BitConverter.GetBytes(value);
                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(bytes);
                Bytes(field, bytes);
            }

            public void Bytes(int field, byte[] value)
            {
                WriteVarUInt(_stream, checked((ulong)field));
                WriteVarUInt(_stream, checked((ulong)value.Length));
                _stream.Write(value, 0, value.Length);
            }

            public byte[] ToArray() => _stream.ToArray();

            static void WriteVarUInt(Stream stream, ulong value)
            {
                while (value >= 0x80)
                {
                    stream.WriteByte((byte)(value | 0x80));
                    value >>= 7;
                }
                stream.WriteByte((byte)value);
            }
        }

        sealed class TlvReader
        {
            readonly byte[] _bytes;
            int _offset;

            public TlvReader(byte[] bytes)
            {
                _bytes = bytes ?? Array.Empty<byte>();
            }

            public bool TryRead(out int field, out byte[] value)
            {
                field = 0;
                value = null;
                if (_offset >= _bytes.Length)
                    return false;
                ulong rawField = ReadVarUInt(_bytes, ref _offset);
                ulong rawLength = ReadVarUInt(_bytes, ref _offset);
                if (rawField == 0 || rawField > int.MaxValue || rawLength > int.MaxValue ||
                    rawLength > (ulong)(_bytes.Length - _offset))
                    throw new InvalidDataException("A tagged field has an invalid id or length.");
                field = (int)rawField;
                int length = (int)rawLength;
                value = new byte[length];
                Buffer.BlockCopy(_bytes, _offset, value, 0, length);
                _offset += length;
                return true;
            }

            public static ulong ReadVarUInt(byte[] bytes, ref int offset)
            {
                ulong result = 0;
                int shift = 0;
                for (int i = 0; i < 10; i++)
                {
                    if (offset >= bytes.Length)
                        throw new EndOfStreamException("Unexpected end of a numeric field.");
                    byte current = bytes[offset++];
                    result |= (ulong)(current & 0x7f) << shift;
                    if ((current & 0x80) == 0)
                        return result;
                    shift += 7;
                }
                throw new InvalidDataException("A numeric field is too long.");
            }
        }
    }
}
