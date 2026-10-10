using System;
using System.Collections.Generic;
using System.Globalization;
using TextStudio.BuiltIns;
using UnityEngine;
using Random = System.Random;

namespace TextStudio.Editor
{
    internal enum TextStudioInspirationFamily
    {
        Flowing,
        Elastic,
        Hanging,
        Chromatic,
        RevealDriven,
        Digital
    }

    internal readonly struct TextStudioInspirationResult
    {
        public readonly int Seed;
        public readonly TextStudioInspirationFamily Family;
        public readonly int ChannelLayerCount;
        public readonly int EffectLayerCount;

        public TextStudioInspirationResult(
            int seed,
            TextStudioInspirationFamily family,
            int channelLayerCount,
            int effectLayerCount)
        {
            Seed = seed;
            Family = family;
            ChannelLayerCount = channelLayerCount;
            EffectLayerCount = effectLayerCount;
        }
    }

#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioComposedEffectInspirationGenerator
    {
        [Flags]
        enum FamilyMask
        {
            Flowing = 1 << 0,
            Elastic = 1 << 1,
            Hanging = 1 << 2,
            Chromatic = 1 << 3,
            RevealDriven = 1 << 4,
            Digital = 1 << 5
        }

        readonly struct EffectProfile
        {
            public readonly CodeEffectBase Effect;
            public readonly FamilyMask Families;
            public readonly TextStudioPhaseMask SupportedPhases;

            public EffectProfile(
                CodeEffectBase effect,
                FamilyMask families,
                TextStudioPhaseMask supportedPhases)
            {
                Effect = effect;
                Families = families;
                SupportedPhases = supportedPhases;
            }

            public bool Supports(TextStudioInspirationFamily family)
            {
                FamilyMask mask = (FamilyMask)(1 << (int)family);
                return (Families & mask) != 0;
            }
        }

        readonly struct CornerShape
        {
            public readonly Vector2 BottomLeft;
            public readonly Vector2 TopLeft;
            public readonly Vector2 TopRight;
            public readonly Vector2 BottomRight;

            public CornerShape(
                Vector2 bottomLeft,
                Vector2 topLeft,
                Vector2 topRight,
                Vector2 bottomRight)
            {
                BottomLeft = bottomLeft;
                TopLeft = topLeft;
                TopRight = topRight;
                BottomRight = bottomRight;
            }
        }

#if UNITY_6000_7_OR_NEWER
        // These private tables are immutable effect metadata. Only s_lastFamily is play-session state.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        static readonly TextStudioEffectCurvePreset[] s_presetCurves =
        {
            TextStudioEffectCurvePreset.Linear,
            TextStudioEffectCurvePreset.Sine,
            TextStudioEffectCurvePreset.HalfSine,
            TextStudioEffectCurvePreset.Square,
            TextStudioEffectCurvePreset.Step,
            TextStudioEffectCurvePreset.Hold,
            TextStudioEffectCurvePreset.Bounce,
            TextStudioEffectCurvePreset.EaseIn,
            TextStudioEffectCurvePreset.EaseOut,
            TextStudioEffectCurvePreset.EaseInOut,
            TextStudioEffectCurvePreset.BackOut,
            TextStudioEffectCurvePreset.ElasticOut,
            TextStudioEffectCurvePreset.ReverseLinear
        };

#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        // Generated compositions have no influence target, so target-dependent Lens Magnifier is excluded.
        static readonly EffectProfile[] s_effectProfiles =
        {
            new EffectProfile(BuiltInEffects.Wave, FamilyMask.Flowing | FamilyMask.Chromatic, TextStudioPhaseMask.Active),
            new EffectProfile(BuiltInEffects.Shake, FamilyMask.Digital | FamilyMask.Elastic, TextStudioPhaseMask.Active),
            new EffectProfile(BuiltInEffects.Wiggle, FamilyMask.Flowing | FamilyMask.Digital, TextStudioPhaseMask.Active),
            new EffectProfile(BuiltInEffects.Bounce, FamilyMask.Flowing | FamilyMask.Elastic, TextStudioPhaseMask.Active),
            new EffectProfile(BuiltInEffects.Swing, FamilyMask.Hanging | FamilyMask.Flowing, TextStudioPhaseMask.Active),
            new EffectProfile(BuiltInEffects.Pendulum, FamilyMask.Hanging, TextStudioPhaseMask.Active),
            new EffectProfile(BuiltInEffects.Dangle, FamilyMask.Hanging | FamilyMask.Digital, TextStudioPhaseMask.Active),
            new EffectProfile(BuiltInEffects.Pulse, FamilyMask.Elastic | FamilyMask.Chromatic, TextStudioPhaseMask.Active),
            new EffectProfile(BuiltInEffects.Blink, FamilyMask.Digital | FamilyMask.RevealDriven, TextStudioPhaseMask.Active),
            new EffectProfile(BuiltInEffects.Shimmer, FamilyMask.Chromatic | FamilyMask.Flowing, TextStudioPhaseMask.Active | TextStudioPhaseMask.Reveal),
            new EffectProfile(BuiltInEffects.GradientFlow, FamilyMask.Chromatic | FamilyMask.Flowing, TextStudioPhaseMask.Active),
            new EffectProfile(BuiltInEffects.ColorSweep, FamilyMask.Chromatic | FamilyMask.RevealDriven, TextStudioPhaseMask.Active | TextStudioPhaseMask.Reveal),
            new EffectProfile(BuiltInEffects.ColorPulse, FamilyMask.Chromatic | FamilyMask.Elastic, TextStudioPhaseMask.Active),
            new EffectProfile(BuiltInEffects.Pop, FamilyMask.Elastic | FamilyMask.RevealDriven, TextStudioPhaseMask.Reveal | TextStudioPhaseMask.Hide),
            new EffectProfile(BuiltInEffects.Float, FamilyMask.Flowing | FamilyMask.Hanging, TextStudioPhaseMask.Active),
            new EffectProfile(BuiltInEffects.Rainbow, FamilyMask.Chromatic | FamilyMask.Digital, TextStudioPhaseMask.Active),
            new EffectProfile(BuiltInEffects.Fade, FamilyMask.Chromatic | FamilyMask.RevealDriven, TextStudioPhaseMask.All),
            new EffectProfile(BuiltInEffects.Size, FamilyMask.Elastic | FamilyMask.RevealDriven, TextStudioPhaseMask.All),
            new EffectProfile(BuiltInEffects.Rotate, FamilyMask.Hanging | FamilyMask.Digital | FamilyMask.RevealDriven, TextStudioPhaseMask.All),
            new EffectProfile(BuiltInEffects.Shear, FamilyMask.Digital | FamilyMask.Flowing, TextStudioPhaseMask.Active),
            new EffectProfile(BuiltInEffects.SlideHorizontal, FamilyMask.Digital | FamilyMask.RevealDriven, TextStudioPhaseMask.All),
            new EffectProfile(BuiltInEffects.SlideVertical, FamilyMask.Flowing | FamilyMask.RevealDriven, TextStudioPhaseMask.All),
            new EffectProfile(BuiltInEffects.Expand, FamilyMask.Elastic | FamilyMask.RevealDriven, TextStudioPhaseMask.All),
            new EffectProfile(BuiltInEffects.Decrypt, FamilyMask.Digital | FamilyMask.RevealDriven, TextStudioPhaseMask.Reveal),
            new EffectProfile(BuiltInEffects.Glitch, FamilyMask.Digital, TextStudioPhaseMask.Active),
            new EffectProfile(BuiltInEffects.Emphasis, FamilyMask.Elastic | FamilyMask.Chromatic, TextStudioPhaseMask.Active),
            new EffectProfile(BuiltInEffects.HeroDepthRise, FamilyMask.Flowing | FamilyMask.RevealDriven, TextStudioPhaseMask.Reveal),
            new EffectProfile(BuiltInEffects.CardFlipY, FamilyMask.Digital | FamilyMask.RevealDriven, TextStudioPhaseMask.Reveal),
            new EffectProfile(BuiltInEffects.TopHinge, FamilyMask.Hanging | FamilyMask.RevealDriven, TextStudioPhaseMask.Reveal),
            new EffectProfile(BuiltInEffects.DominoLine, FamilyMask.Hanging | FamilyMask.RevealDriven, TextStudioPhaseMask.Reveal),
            new EffectProfile(BuiltInEffects.SpiralAssemble, FamilyMask.Flowing | FamilyMask.RevealDriven, TextStudioPhaseMask.Reveal),
            new EffectProfile(BuiltInEffects.SpatialScatter, FamilyMask.Elastic | FamilyMask.Digital | FamilyMask.RevealDriven, TextStudioPhaseMask.Reveal),
            new EffectProfile(BuiltInEffects.FoldAway, FamilyMask.Hanging | FamilyMask.RevealDriven, TextStudioPhaseMask.Hide),
            new EffectProfile(BuiltInEffects.RollingRibbon, FamilyMask.Flowing | FamilyMask.Digital, TextStudioPhaseMask.Active),
            new EffectProfile(BuiltInEffects.SoftSpatialFloat, FamilyMask.Flowing, TextStudioPhaseMask.Active),
            new EffectProfile(BuiltInEffects.Accordion, FamilyMask.Elastic | FamilyMask.RevealDriven, TextStudioPhaseMask.Reveal),
            new EffectProfile(BuiltInEffects.Orbit, FamilyMask.Flowing | FamilyMask.Digital, TextStudioPhaseMask.Active),
            new EffectProfile(BuiltInEffects.Elastic, FamilyMask.Elastic | FamilyMask.RevealDriven, TextStudioPhaseMask.Reveal | TextStudioPhaseMask.Hide),
            new EffectProfile(BuiltInEffects.Scatter2D, FamilyMask.Elastic | FamilyMask.Digital | FamilyMask.RevealDriven, TextStudioPhaseMask.Reveal | TextStudioPhaseMask.Hide),
            new EffectProfile(BuiltInEffects.Tracking, FamilyMask.Flowing | FamilyMask.RevealDriven, TextStudioPhaseMask.Reveal | TextStudioPhaseMask.Hide),
            new EffectProfile(BuiltInEffects.CornerWarp, FamilyMask.Flowing | FamilyMask.Elastic, TextStudioPhaseMask.Active),
            new EffectProfile(BuiltInEffects.Focus, FamilyMask.Chromatic | FamilyMask.RevealDriven, TextStudioPhaseMask.Reveal | TextStudioPhaseMask.Hide),
            new EffectProfile(BuiltInEffects.Dissolve, FamilyMask.Digital | FamilyMask.RevealDriven, TextStudioPhaseMask.Reveal | TextStudioPhaseMask.Hide),
            new EffectProfile(BuiltInEffects.Burn, FamilyMask.Digital | FamilyMask.Chromatic | FamilyMask.RevealDriven, TextStudioPhaseMask.Reveal | TextStudioPhaseMask.Hide),
            new EffectProfile(BuiltInEffects.OutlineGlow, FamilyMask.Chromatic | FamilyMask.Elastic, TextStudioPhaseMask.Active),
            new EffectProfile(BuiltInEffects.SurfaceSweep, FamilyMask.Chromatic | FamilyMask.Flowing, TextStudioPhaseMask.Active),
            new EffectProfile(BuiltInEffects.ChromaticGlitch, FamilyMask.Digital | FamilyMask.Chromatic, TextStudioPhaseMask.Active),
            new EffectProfile(BuiltInEffects.PixelGrid, FamilyMask.Digital | FamilyMask.RevealDriven, TextStudioPhaseMask.All),
            new EffectProfile(BuiltInEffects.Squiggle, FamilyMask.Elastic | FamilyMask.Flowing, TextStudioPhaseMask.Active)
        };

#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        static readonly TextStudioCompositionChannel[][] s_familyChannels =
        {
            new[]
            {
                TextStudioCompositionChannel.Position,
                TextStudioCompositionChannel.Rotation,
                TextStudioCompositionChannel.Color,
                TextStudioCompositionChannel.UVOffset,
                TextStudioCompositionChannel.Skew
            },
            new[]
            {
                TextStudioCompositionChannel.Scale,
                TextStudioCompositionChannel.Position,
                TextStudioCompositionChannel.Alpha,
                TextStudioCompositionChannel.CornerDeformation,
                TextStudioCompositionChannel.Rotation
            },
            new[]
            {
                TextStudioCompositionChannel.Pivot,
                TextStudioCompositionChannel.Rotation,
                TextStudioCompositionChannel.Position,
                TextStudioCompositionChannel.Skew,
                TextStudioCompositionChannel.Scale
            },
            new[]
            {
                TextStudioCompositionChannel.Color,
                TextStudioCompositionChannel.Alpha,
                TextStudioCompositionChannel.UVOffset,
                TextStudioCompositionChannel.Scale,
                TextStudioCompositionChannel.Position
            },
            new[]
            {
                TextStudioCompositionChannel.Position,
                TextStudioCompositionChannel.Scale,
                TextStudioCompositionChannel.Alpha,
                TextStudioCompositionChannel.Rotation,
                TextStudioCompositionChannel.CornerDeformation
            },
            new[]
            {
                TextStudioCompositionChannel.Skew,
                TextStudioCompositionChannel.UVOffset,
                TextStudioCompositionChannel.Position,
                TextStudioCompositionChannel.Color,
                TextStudioCompositionChannel.CornerDeformation
            }
        };

#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        static readonly Color32[][] s_familyPalettes =
        {
            new[]
            {
                new Color32(72, 211, 255, 255),
                new Color32(80, 255, 201, 255),
                new Color32(89, 132, 255, 255),
                new Color32(214, 248, 255, 255)
            },
            new[]
            {
                new Color32(255, 190, 73, 255),
                new Color32(255, 105, 121, 255),
                new Color32(255, 231, 154, 255),
                new Color32(255, 144, 77, 255)
            },
            new[]
            {
                new Color32(189, 152, 255, 255),
                new Color32(112, 199, 255, 255),
                new Color32(236, 216, 255, 255),
                new Color32(91, 119, 205, 255)
            },
            new[]
            {
                new Color32(255, 87, 171, 255),
                new Color32(81, 224, 255, 255),
                new Color32(255, 213, 75, 255),
                new Color32(166, 105, 255, 255)
            },
            new[]
            {
                new Color32(105, 241, 196, 255),
                new Color32(255, 244, 190, 255),
                new Color32(121, 178, 255, 255),
                new Color32(255, 140, 196, 255)
            },
            new[]
            {
                new Color32(77, 244, 224, 255),
                new Color32(255, 79, 111, 255),
                new Color32(143, 102, 255, 255),
                new Color32(225, 244, 255, 255)
            }
        };

        static int s_lastFamily = -1;

        internal static int EligibleEffectCount => s_effectProfiles.Length;
        internal static int SupportedChannelCount => Enum.GetValues(typeof(TextStudioCompositionChannel)).Length;

        internal static int GetEligibleEffectCount(TextStudioRenderDomain requiredDomain)
        {
            int count = 0;
            for (int i = 0; i < s_effectProfiles.Length; i++)
            {
                if ((s_effectProfiles[i].Effect.Capabilities.SupportedRenderDomains & requiredDomain) != 0)
                    count++;
            }

            return count;
        }

        internal static bool IsEligibleBuiltIn(string effectId)
        {
            for (int i = 0; i < s_effectProfiles.Length; i++)
            {
                if (string.Equals(s_effectProfiles[i].Effect.Id, effectId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        internal static TextStudioInspirationResult Generate(TextStudioComposedEffectAsset effect)
        {
            if (effect == null)
                throw new ArgumentNullException(nameof(effect));

            int seed = Guid.NewGuid().GetHashCode();
            TextStudioInspirationFamily family = FamilyForSeed(seed);
            while ((int)family == s_lastFamily)
            {
                seed = unchecked(seed + 1);
                family = FamilyForSeed(seed);
            }

            s_lastFamily = (int)family;
            return Generate(effect, seed);
        }

        internal static TextStudioInspirationResult Generate(
            TextStudioComposedEffectAsset effect,
            int seed)
        {
            if (effect == null)
                throw new ArgumentNullException(nameof(effect));

            TextStudioInspirationFamily family = FamilyForSeed(seed);
            Random random = new Random(unchecked(seed ^ (int)0x5F3759DF));
            Color32[] palette = s_familyPalettes[(int)family];
            int channelCount = random.Next(1, 4);
            int effectCount = random.Next(1, 3);

            List<TextStudioCompositionChannel> channels = ChooseChannels(family, channelCount, random);
            List<EffectProfile> effectProfiles = ChooseEffects(
                family,
                effectCount,
                random,
                channels.Contains(TextStudioCompositionChannel.Alpha),
                effect.RequiredRenderDomain);

            effect.ClearLayersForAuthoring();
            for (int i = 0; i < channels.Count; i++)
                AddChannelLayer(effect, channels[i], family, palette, random, seed, i, i == 0);
            for (int i = 0; i < effectProfiles.Count; i++)
                AddEffectLayer(effect, effectProfiles[i], family, palette, random, seed, i, i == 0);

            effect.TagStack.ConfigureForAuthoring(
                false,
                string.Empty,
                TextStudioTagExpansionClosingMode.Auto,
                string.Empty);

            return new TextStudioInspirationResult(
                seed,
                family,
                channels.Count,
                effectProfiles.Count);
        }

        static void AddChannelLayer(
            TextStudioComposedEffectAsset effect,
            TextStudioCompositionChannel channel,
            TextStudioInspirationFamily family,
            Color32[] palette,
            Random random,
            int seed,
            int layerIndex,
            bool guaranteeActive)
        {
            TextStudioEffectAreaSettings area = CreateArea(random);
            TextStudioPhaseMask phases = guaranteeActive
                ? TextStudioPhaseMask.Active
                : ChooseChannelPhases(family, random);
            TextStudioEffectPlaybackPreset playback = ChoosePlayback(phases, random);
            TextStudioEffectCurvePreset curve = ChooseCurve(random);
            AnimationCurve customCurve = curve == TextStudioEffectCurvePreset.Custom
                ? CreateCustomCurve(random)
                : AnimationCurve.Linear(0f, 0f, 1f, 1f);
            Vector3 vector = CreateChannelVector(channel, family, random);
            float scalar = channel == TextStudioCompositionChannel.Alpha
                ? NextFloat(random, 0.5f, 0.8f)
                : 0f;
            Gradient gradient = CreateGradient(palette, random);
            TextStudioCompositionColorMode colorMode = random.NextDouble() < 0.75
                ? TextStudioCompositionColorMode.Replace
                : TextStudioCompositionColorMode.MultiplyExistingColor;
            float intensity = NextFloat(random, 0.7f, 1f);
            float timeOffset = NextFloat(random, -0.35f, 0.35f);
            PhaseParams timing = CreateTiming(area, random, true);
            EffectBlendMode blend = ChooseBlend(channel, random);
            CornerShape corners = CreateCornerShape(random);
            string displayName = FormatAreaName(area) + " " + ChannelRole(channel, family);

            TextStudioCompositionLayer layer = effect.AddChannelLayer(channel);
            layer.ConfigureChannelForAuthoring(
                CreateStableId(seed, "channel", layerIndex),
                displayName,
                channel,
                playback,
                curve,
                vector,
                scalar,
                gradient,
                colorMode,
                phases,
                intensity,
                timeOffset,
                timing,
                area,
                blend);
            layer.ConfigureChannelShapeForAuthoring(
                customCurve,
                corners.BottomLeft,
                corners.TopLeft,
                corners.TopRight,
                corners.BottomRight);
        }

        static void AddEffectLayer(
            TextStudioComposedEffectAsset effect,
            EffectProfile profile,
            TextStudioInspirationFamily family,
            Color32[] palette,
            Random random,
            int seed,
            int layerIndex,
            bool guaranteeActive)
        {
            TextStudioEffectAreaSettings area = CreateArea(random);
            TextStudioPhaseMask phases = ChooseEffectPhases(profile, family, random, guaranteeActive);
            PhaseParams timing = CreateTiming(area, random, false);
            TextStudioTagParameters parameters = BuildEffectParameters(profile, palette, phases, random);
            string displayName = FormatAreaName(area);
            TextStudioCompositionLayer layer = effect.AddEffectLayer(profile.Effect.Id);
            layer.ConfigureEffectForAuthoring(
                CreateStableId(seed, "effect", layerIndex),
                displayName,
                profile.Effect.Id,
                parameters.Raw,
                phases,
                NextFloat(random, 0.7f, 1f),
                NextFloat(random, -0.35f, 0.35f),
                area,
                timing);
        }

        static TextStudioTagParameters BuildEffectParameters(
            EffectProfile profile,
            Color32[] palette,
            TextStudioPhaseMask phases,
            Random random)
        {
            TextStudioTagParameterDefinition[] definitions =
                TextStudioBuiltInParameterMetadata.Create(
                    profile.Effect,
                    Array.Empty<string>());
            List<TextStudioNamedTagParameter> named =
                new List<TextStudioNamedTagParameter>(definitions.Length);
            for (int i = 0; i < definitions.Length; i++)
            {
                TextStudioTagParameterDefinition parameter = definitions[i];
                if (TextStudioEffectParameterOwnership.IsOwnedByProgressLayer(
                        profile.Effect.Metadata,
                        parameter.Name))
                {
                    continue;
                }
                string value = RandomizeParameterValue(
                    profile,
                    parameter,
                    palette,
                    phases,
                    random);
                named.Add(new TextStudioNamedTagParameter(
                    parameter.Name,
                    value,
                    false));
            }

            return TextStudioEffectParameterUtility.CreateCanonicalParameters(
                Array.Empty<string>(),
                named);
        }

        static string RandomizeParameterValue(
            EffectProfile profile,
            TextStudioTagParameterDefinition parameter,
            Color32[] palette,
            TextStudioPhaseMask phases,
            Random random)
        {
            switch (parameter.Kind)
            {
                case TextStudioTagParameterKind.Float:
                    float.TryParse(
                        parameter.DefaultValue,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float floatValue);
                    return FloatString(RandomizeFloat(
                        profile.Effect.Id,
                        parameter.Name,
                        floatValue,
                        random));
                case TextStudioTagParameterKind.Integer:
                    int.TryParse(
                        parameter.DefaultValue,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int intValue);
                    return Math.Max(
                            0,
                            (int)Math.Round(intValue * NextFloat(random, 0.65f, 1.35f)))
                        .ToString(CultureInfo.InvariantCulture);
                case TextStudioTagParameterKind.Boolean:
                    return RandomizeBoolean(
                            profile.Effect.Id,
                            parameter.Name,
                            phases,
                            random)
                        ? "true"
                        : "false";
                case TextStudioTagParameterKind.Color:
                    return ColorString(palette[random.Next(0, palette.Length)]);
                case TextStudioTagParameterKind.Pivot:
                    return VectorString(RandomizeVector(
                        profile.Effect.Id,
                        parameter.Name,
                        random));
                case TextStudioTagParameterKind.String:
                    if (string.Equals(
                            parameter.Name,
                            "direction",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return VectorString(RandomizeVector(
                            profile.Effect.Id,
                            parameter.Name,
                            random));
                    }

                    return RandomizeStringOrDefault(
                        profile.Effect.Id,
                        parameter,
                        phases,
                        random);
                case TextStudioTagParameterKind.Enum:
                    return RandomizeStringOrDefault(
                        profile.Effect.Id,
                        parameter,
                        phases,
                        random);
                default:
                    return parameter.DefaultValue;
            }
        }

        static string RandomizeStringOrDefault(
            string effectId,
            TextStudioTagParameterDefinition parameter,
            TextStudioPhaseMask phases,
            Random random)
        {
            string value = RandomizeString(effectId, parameter.Name, phases, random);
            return string.IsNullOrEmpty(value) ? parameter.DefaultValue : value;
        }

        static string VectorString(Vector2 value)
        {
            return FloatString(value.x) + "," + FloatString(value.y);
        }

        static float RandomizeFloat(string effectId, string fieldName, float defaultValue, Random random)
        {
            if (string.Equals(effectId, TextStudioBuiltInEffectIds.Squiggle, StringComparison.OrdinalIgnoreCase))
            {
                return fieldName switch
                {
                    "strength" => NextFloat(random, .02f, .05f),
                    "detail" => random.Next(3, 7),
                    "rate" => NextFloat(random, 3f, 8f),
                    "seed" => random.Next(0, 1024),
                    _ => defaultValue
                };
            }
            if (string.Equals(effectId, TextStudioBuiltInEffectIds.PixelGrid, StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(fieldName, "size", StringComparison.Ordinal))
                    return NextFloat(random, 4f, 12f);
                if (string.Equals(fieldName, "active", StringComparison.Ordinal))
                    return NextFloat(random, 0.35f, 0.85f);
                if (string.Equals(fieldName, "transition", StringComparison.Ordinal))
                    return NextFloat(random, 0.7f, 1f);
                if (string.Equals(fieldName, "pulse", StringComparison.Ordinal))
                    return random.NextDouble() < 0.65 ? 0f : NextFloat(random, 0.25f, 1.5f);
                if (string.Equals(fieldName, "min", StringComparison.Ordinal))
                    return NextFloat(random, 2f, 6f);
                if (string.Equals(fieldName, "max", StringComparison.Ordinal))
                    return NextFloat(random, 8f, 16f);
            }

            if (string.Equals(fieldName, "min", StringComparison.Ordinal))
                return NextFloat(random, 0.4f, 0.65f);
            if (string.Equals(fieldName, "max", StringComparison.Ordinal))
                return NextFloat(random, 0.88f, 1f);
            if (string.Equals(fieldName, "duty", StringComparison.Ordinal))
                return NextFloat(random, 0.35f, 0.72f);
            if (string.Equals(fieldName, "saturation", StringComparison.Ordinal))
                return NextFloat(random, 0.75f, 1f);
            if (string.Equals(fieldName, "value", StringComparison.Ordinal))
                return NextFloat(random, 0.9f, 1f);
            if (string.Equals(fieldName, "width", StringComparison.Ordinal))
                return NextFloat(random, 0.09f, 0.24f);
            if (string.Equals(fieldName, "softness", StringComparison.Ordinal))
                return NextFloat(random, 0.06f, 0.24f);
            if (string.Equals(
                    effectId,
                    TextStudioBuiltInEffectIds.Shimmer,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(fieldName, "scale", StringComparison.Ordinal))
            {
                return NextFloat(random, 0.015f, 0.07f);
            }
            if (string.Equals(fieldName, "start", StringComparison.Ordinal))
            {
                return string.Equals(effectId, TextStudioBuiltInEffectIds.Pop, StringComparison.OrdinalIgnoreCase)
                    ? NextFloat(random, 0.15f, 0.55f)
                    : NextFloat(random, 0.72f, 0.94f);
            }
            if (string.Equals(fieldName, "end", StringComparison.Ordinal))
                return NextFloat(random, 0.97f, 1.08f);
            if (string.Equals(fieldName, "overshoot", StringComparison.Ordinal))
                return NextFloat(random, 0.1f, 0.3f);
            if (string.Equals(fieldName, "amplitude", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(effectId, TextStudioBuiltInEffectIds.Shear, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(effectId, TextStudioBuiltInEffectIds.Expand, StringComparison.OrdinalIgnoreCase))
                {
                    return NextFloat(random, 0.65f, 1.2f);
                }

                return Math.Min(Math.Abs(defaultValue) * NextFloat(random, 0.85f, 1.5f), 0.28f);
            }
            if (fieldName.IndexOf("Amplitude", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fieldName.EndsWith("Strength", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fieldName, "height", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fieldName, "distance", StringComparison.OrdinalIgnoreCase))
            {
                return Math.Min(Math.Abs(defaultValue) * NextFloat(random, 0.85f, 1.5f), 0.28f);
            }
            if (string.Equals(fieldName, "angle", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fieldName, "rotation", StringComparison.OrdinalIgnoreCase))
            {
                return NextFloat(random, 6f, 24f);
            }
            if (string.Equals(fieldName, "loop", StringComparison.OrdinalIgnoreCase))
                return NextFloat(random, 270f, 450f);
            if (string.Equals(fieldName, "duration", StringComparison.OrdinalIgnoreCase))
                return NextFloat(random, 0.6f, 1.6f);
            if (string.Equals(effectId, TextStudioBuiltInEffectIds.Pulse, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(fieldName, "stagger", StringComparison.OrdinalIgnoreCase))
            {
                return NextFloat(random, 0.08f, 0.28f);
            }
            if (string.Equals(fieldName, "stagger", StringComparison.OrdinalIgnoreCase))
                return NextFloat(random, 0.015f, 0.07f);
            if (fieldName.IndexOf("speed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fieldName.IndexOf("frequency", StringComparison.OrdinalIgnoreCase) >= 0 ||
                string.Equals(fieldName, "rate", StringComparison.OrdinalIgnoreCase))
            {
                return Math.Abs(defaultValue) * NextFloat(random, 0.65f, 1.5f);
            }
            if (string.Equals(fieldName, "spread", StringComparison.OrdinalIgnoreCase))
                return Math.Abs(defaultValue) * NextFloat(random, 0.65f, 1.35f);
            if (Math.Abs(defaultValue) > 0.0001f)
                return defaultValue * NextFloat(random, 0.6f, 1.35f);
            return NextFloat(random, 0.02f, 0.12f);
        }

        static bool RandomizeBoolean(
            string effectId,
            string fieldName,
            TextStudioPhaseMask phases,
            Random random)
        {
            if (string.Equals(fieldName, "progress", StringComparison.Ordinal))
                return phases != TextStudioPhaseMask.Active && random.NextDouble() < 0.7;
            if (string.Equals(fieldName, "wrap", StringComparison.Ordinal))
                return random.NextDouble() < 0.75;
            if (string.Equals(fieldName, "dynamic", StringComparison.Ordinal))
                return random.NextDouble() < 0.75;
            return random.NextDouble() < 0.5;
        }

        static string RandomizeString(
            string effectId,
            string fieldName,
            TextStudioPhaseMask phases,
            Random random)
        {
            if (string.Equals(fieldName, "playback", StringComparison.OrdinalIgnoreCase))
            {
                if ((phases & TextStudioPhaseMask.Active) == 0)
                    return random.NextDouble() < 0.5 ? "once" : "short";
                string[] values = { "infinite", "infinite", "loop", "pingpong" };
                return values[random.Next(0, values.Length)];
            }

            if ((string.Equals(
                     effectId,
                     TextStudioBuiltInEffectIds.Dissolve,
                     StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(
                     effectId,
                     TextStudioBuiltInEffectIds.Burn,
                     StringComparison.OrdinalIgnoreCase)) &&
                string.Equals(fieldName, "space", StringComparison.OrdinalIgnoreCase))
            {
                return random.NextDouble() < 0.7 ? "text" : "glyph";
            }

            if (string.Equals(fieldName, "mode", StringComparison.OrdinalIgnoreCase))
            {
                string[] values;
                if (string.Equals(effectId, TextStudioBuiltInEffectIds.Fade, StringComparison.OrdinalIgnoreCase))
                    values = new[] { "full", "alpha", "multiply" };
                else if (string.Equals(effectId, TextStudioBuiltInEffectIds.CornerWarp, StringComparison.OrdinalIgnoreCase))
                    values = new[] { "ripple", "flag", "squeeze", "bulge" };
                else
                    values = new[] { "Both", "HorizontallyFromCenter", "VerticallyFromCenter" };
                return values[random.Next(0, values.Length)];
            }

            if (string.Equals(fieldName, "origin", StringComparison.OrdinalIgnoreCase))
            {
                string[] values = { "center", "left", "right", "top", "bottom" };
                return values[random.Next(0, values.Length)];
            }

            if (string.Equals(fieldName, "anchor", StringComparison.OrdinalIgnoreCase))
            {
                string[] values = { "center", "left", "right" };
                return values[random.Next(0, values.Length)];
            }

            if (string.Equals(fieldName, "axis", StringComparison.OrdinalIgnoreCase))
            {
                string[] values = { "x", "y", "z" };
                return values[random.Next(0, values.Length)];
            }

            if (string.Equals(effectId, TextStudioBuiltInEffectIds.PixelGrid, StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(fieldName, "space", StringComparison.OrdinalIgnoreCase))
                    return random.NextDouble() < 0.65 ? "screen" : "glyph";
                if (string.Equals(fieldName, "quality", StringComparison.OrdinalIgnoreCase))
                    return random.NextDouble() < 0.75 ? "preserve" : "crisp";
            }

            if (string.Equals(fieldName, "horizontal", StringComparison.OrdinalIgnoreCase))
            {
                string[] values = string.Equals(effectId, TextStudioBuiltInEffectIds.Shear, StringComparison.OrdinalIgnoreCase)
                    ? new[] { "left", "right", "allSides" }
                    : new[] { "left", "right", "allSides" };
                return values[random.Next(0, values.Length)];
            }

            if (string.Equals(fieldName, "vertical", StringComparison.OrdinalIgnoreCase))
            {
                string[] values = string.Equals(effectId, TextStudioBuiltInEffectIds.Shear, StringComparison.OrdinalIgnoreCase)
                    ? new[] { "top", "bottom", "allSides" }
                    : new[] { "top", "bottom", "allSides" };
                return values[random.Next(0, values.Length)];
            }

            if (string.Equals(fieldName, "scale", StringComparison.OrdinalIgnoreCase))
            {
                float x = NextFloat(random, 0.82f, 1.15f);
                float y = NextFloat(random, 0.82f, 1.15f);
                return FloatString(x) + "," + FloatString(y) + ",1";
            }

            return string.Empty;
        }

        static Vector2 RandomizeVector(string effectId, string fieldName, Random random)
        {
            if (string.Equals(fieldName, "direction", StringComparison.OrdinalIgnoreCase))
            {
                Vector2[] directions =
                {
                    Vector2.up,
                    Vector2.down,
                    Vector2.left,
                    Vector2.right,
                    new Vector2(1f, 1f).normalized,
                    new Vector2(-1f, 1f).normalized
                };
                return directions[random.Next(0, directions.Length)];
            }

            if (string.Equals(fieldName, "pivot", StringComparison.OrdinalIgnoreCase))
            {
                float y = string.Equals(effectId, TextStudioBuiltInEffectIds.Rotate, StringComparison.OrdinalIgnoreCase)
                    ? NextFloat(random, 0.35f, 0.75f)
                    : NextFloat(random, 0.78f, 1f);
                return new Vector2(NextFloat(random, 0.35f, 0.65f), y);
            }

            return Vector2.zero;
        }

        static List<TextStudioCompositionChannel> ChooseChannels(
            TextStudioInspirationFamily family,
            int count,
            Random random)
        {
            List<TextStudioCompositionChannel> candidates = new List<TextStudioCompositionChannel>(
                s_familyChannels[(int)family]);
            Shuffle(candidates, random);
            List<TextStudioCompositionChannel> selected = candidates.GetRange(0, Math.Min(count, candidates.Count));
            if (selected.Contains(TextStudioCompositionChannel.Pivot) &&
                !selected.Contains(TextStudioCompositionChannel.Rotation))
            {
                if (selected.Count < count)
                    selected.Add(TextStudioCompositionChannel.Rotation);
                else
                    selected[selected.Count - 1] = TextStudioCompositionChannel.Rotation;
            }
            return selected;
        }

        static List<EffectProfile> ChooseEffects(
            TextStudioInspirationFamily family,
            int count,
            Random random,
            bool hasAlphaChannel,
            TextStudioRenderDomain requiredDomain)
        {
            List<EffectProfile> candidates = new List<EffectProfile>(s_effectProfiles.Length);
            for (int i = 0; i < s_effectProfiles.Length; i++)
            {
                EffectProfile profile = s_effectProfiles[i];
                if (profile.Supports(family) &&
                    (profile.Effect.Capabilities.SupportedRenderDomains & requiredDomain) != 0)
                    candidates.Add(profile);
            }

            Shuffle(candidates, random);
            List<EffectProfile> selected = new List<EffectProfile>(Math.Min(count, candidates.Count));
            bool hasVisibilityLayer = hasAlphaChannel;

            for (int i = 0; i < candidates.Count; i++)
            {
                EffectProfile profile = candidates[i];
                if ((profile.SupportedPhases & TextStudioPhaseMask.Active) == 0 ||
                    (hasVisibilityLayer && IsVisibilityEffect(profile)))
                {
                    continue;
                }

                selected.Add(profile);
                hasVisibilityLayer |= IsVisibilityEffect(profile);
                candidates.RemoveAt(i);
                break;
            }

            for (int i = 0; i < candidates.Count && selected.Count < count; i++)
            {
                EffectProfile profile = candidates[i];
                if (hasVisibilityLayer && IsVisibilityEffect(profile))
                    continue;

                selected.Add(profile);
                hasVisibilityLayer |= IsVisibilityEffect(profile);
            }

            return selected;
        }

        static bool IsVisibilityEffect(EffectProfile profile)
        {
            return string.Equals(profile.Effect.Id, BuiltInEffects.Blink.Id, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(profile.Effect.Id, BuiltInEffects.Fade.Id, StringComparison.OrdinalIgnoreCase);
        }

        static void Shuffle<T>(List<T> values, Random random)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int other = random.Next(0, i + 1);
                T value = values[i];
                values[i] = values[other];
                values[other] = value;
            }
        }

        static TextStudioEffectAreaSettings CreateArea(Random random)
        {
            int roll = random.Next(0, 100);
            TextStudioEffectAreaMode mode = roll < 35
                ? TextStudioEffectAreaMode.Glyph
                : roll < 60
                    ? TextStudioEffectAreaMode.Word
                    : roll < 76
                        ? TextStudioEffectAreaMode.Line
                        : roll < 88
                            ? TextStudioEffectAreaMode.Paragraph
                            : TextStudioEffectAreaMode.Range;
            int groupRoll = random.Next(0, 100);
            int groupSize = groupRoll < 55 ? 1 : groupRoll < 90 ? 2 : 3;
            return mode == TextStudioEffectAreaMode.Range
                ? TextStudioEffectAreaSettings.Range
                : new TextStudioEffectAreaSettings(mode, groupSize);
        }

        static PhaseParams CreateTiming(
            TextStudioEffectAreaSettings area,
            Random random,
            bool includeSpeed)
        {
            PhaseParams timing = PhaseParams.NoOffset;
            timing.charOffset = random.NextDouble() < 0.55 ? NextFloat(random, 0.004f, 0.035f) : 0f;
            timing.wordOffset = random.NextDouble() < 0.45 ? NextFloat(random, 0.015f, 0.15f) : 0f;
            timing.lineOffset = random.NextDouble() < 0.3 ? NextFloat(random, 0.04f, 0.3f) : 0f;
            if (area.Mode == TextStudioEffectAreaMode.Word && timing.wordOffset <= 0f)
                timing.wordOffset = NextFloat(random, 0.015f, 0.1f);
            else if (area.Mode == TextStudioEffectAreaMode.Line && timing.lineOffset <= 0f)
                timing.lineOffset = NextFloat(random, 0.04f, 0.2f);
            else if (area.Mode == TextStudioEffectAreaMode.Glyph && timing.charOffset <= 0f)
                timing.charOffset = NextFloat(random, 0.004f, 0.025f);
            timing.speed = includeSpeed ? NextFloat(random, 0.65f, 1.6f) : 1f;
            return timing;
        }

        static TextStudioPhaseMask ChooseChannelPhases(
            TextStudioInspirationFamily family,
            Random random)
        {
            int roll = random.Next(0, 100);
            if (family == TextStudioInspirationFamily.RevealDriven)
            {
                if (roll < 50) return TextStudioPhaseMask.Active;
                if (roll < 72) return TextStudioPhaseMask.Reveal;
                if (roll < 86) return TextStudioPhaseMask.Hide;
                return TextStudioPhaseMask.Reveal | TextStudioPhaseMask.Hide;
            }

            if (roll < 78) return TextStudioPhaseMask.Active;
            if (roll < 92) return TextStudioPhaseMask.Active | TextStudioPhaseMask.Reveal;
            if (roll < 97) return TextStudioPhaseMask.Reveal;
            return TextStudioPhaseMask.Hide;
        }

        static TextStudioPhaseMask ChooseEffectPhases(
            EffectProfile profile,
            TextStudioInspirationFamily family,
            Random random,
            bool guaranteeActive)
        {
            TextStudioPhaseMask supported = profile.SupportedPhases;
            if (guaranteeActive && (supported & TextStudioPhaseMask.Active) != 0)
                return TextStudioPhaseMask.Active;

            if (supported == TextStudioPhaseMask.Active ||
                supported == TextStudioPhaseMask.Reveal ||
                supported == TextStudioPhaseMask.Hide)
            {
                return supported;
            }

            List<TextStudioPhaseMask> choices = new List<TextStudioPhaseMask>(4);
            if ((supported & TextStudioPhaseMask.Active) != 0)
                choices.Add(TextStudioPhaseMask.Active);
            if ((supported & TextStudioPhaseMask.Reveal) != 0)
                choices.Add(TextStudioPhaseMask.Reveal);
            if ((supported & TextStudioPhaseMask.Hide) != 0)
                choices.Add(TextStudioPhaseMask.Hide);
            if ((supported & (TextStudioPhaseMask.Reveal | TextStudioPhaseMask.Hide)) ==
                (TextStudioPhaseMask.Reveal | TextStudioPhaseMask.Hide))
            {
                choices.Add(TextStudioPhaseMask.Reveal | TextStudioPhaseMask.Hide);
            }

            if (family != TextStudioInspirationFamily.RevealDriven &&
                choices.Contains(TextStudioPhaseMask.Active) &&
                random.NextDouble() < 0.65)
            {
                return TextStudioPhaseMask.Active;
            }

            return choices[random.Next(0, choices.Count)];
        }

        static TextStudioEffectPlaybackPreset ChoosePlayback(TextStudioPhaseMask phases, Random random)
        {
            TextStudioEffectPlaybackPreset[] values = (phases & TextStudioPhaseMask.Active) != 0
                ? new[]
                {
                    TextStudioEffectPlaybackPreset.Loop,
                    TextStudioEffectPlaybackPreset.Loop,
                    TextStudioEffectPlaybackPreset.PingPong,
                    TextStudioEffectPlaybackPreset.PingPong,
                    TextStudioEffectPlaybackPreset.Cycle
                }
                : new[]
                {
                    TextStudioEffectPlaybackPreset.Once,
                    TextStudioEffectPlaybackPreset.Appearance,
                    TextStudioEffectPlaybackPreset.Weighted
                };
            return values[random.Next(0, values.Length)];
        }

        static TextStudioEffectCurvePreset ChooseCurve(Random random)
        {
            return random.NextDouble() < 0.15
                ? TextStudioEffectCurvePreset.Custom
                : s_presetCurves[random.Next(0, s_presetCurves.Length)];
        }

        static AnimationCurve CreateCustomCurve(Random random)
        {
            AnimationCurve curve;
            switch (random.Next(0, 4))
            {
                case 0:
                    curve = new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(NextFloat(random, 0.42f, 0.58f), 1f),
                        new Keyframe(1f, 0f));
                    break;
                case 1:
                    float delay = NextFloat(random, 0.15f, 0.35f);
                    curve = new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(delay, 0f),
                        new Keyframe(1f, 1f));
                    break;
                case 2:
                    curve = new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(NextFloat(random, 0.58f, 0.75f), NextFloat(random, 1.03f, 1.1f)),
                        new Keyframe(1f, 1f));
                    break;
                default:
                    curve = new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(0.25f, NextFloat(random, 0.35f, 0.55f)),
                        new Keyframe(0.7f, NextFloat(random, 0.85f, 1f)),
                        new Keyframe(1f, 1f));
                    break;
            }

            for (int i = 0; i < curve.length; i++)
                curve.SmoothTangents(i, 0f);
            return curve;
        }

        static Vector3 CreateChannelVector(
            TextStudioCompositionChannel channel,
            TextStudioInspirationFamily family,
            Random random)
        {
            switch (channel)
            {
                case TextStudioCompositionChannel.Position:
                {
                    float magnitude = NextFloat(random, 0.06f, 0.22f);
                    Vector2 direction = family == TextStudioInspirationFamily.Flowing ||
                                        family == TextStudioInspirationFamily.RevealDriven
                        ? new Vector2(NextFloat(random, -0.45f, 0.45f), random.NextDouble() < 0.8 ? 1f : -1f).normalized
                        : new Vector2(NextFloat(random, -1f, 1f), NextFloat(random, -1f, 1f)).normalized;
                    return new Vector3(direction.x * magnitude, direction.y * magnitude, 0f);
                }
                case TextStudioCompositionChannel.Rotation:
                    return new Vector3(0f, 0f, Signed(random) * NextFloat(random, 7f, 22f));
                case TextStudioCompositionChannel.Scale:
                {
                    float scale = NextFloat(random, 1.1f, 1.28f);
                    return new Vector3(
                        scale,
                        scale * NextFloat(random, 0.94f, 1.06f),
                        1f);
                }
                case TextStudioCompositionChannel.Skew:
                    return new Vector3(
                        Signed(random) * NextFloat(random, 0.055f, 0.14f),
                        Signed(random) * NextFloat(random, 0.04f, 0.12f),
                        0f);
                case TextStudioCompositionChannel.UVOffset:
                    return new Vector3(
                        Signed(random) * NextFloat(random, 0.03f, 0.1f),
                        Signed(random) * NextFloat(random, 0.02f, 0.08f),
                        0f);
                case TextStudioCompositionChannel.Pivot:
                    return new Vector3(
                        NextFloat(random, 0.35f, 0.65f),
                        NextFloat(random, 0.5f, 0.95f),
                        0f);
                default:
                    return Vector3.zero;
            }
        }

        static Gradient CreateGradient(Color32[] palette, Random random)
        {
            int first = random.Next(0, palette.Length);
            int second = (first + random.Next(1, palette.Length)) % palette.Length;
            bool threeColors = random.NextDouble() < 0.35;
            Gradient gradient = new Gradient();
            if (threeColors)
            {
                int third = (second + random.Next(1, palette.Length)) % palette.Length;
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey((Color)palette[first], 0f),
                        new GradientColorKey((Color)palette[second], 0.5f),
                        new GradientColorKey((Color)palette[third], 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(1f, 1f)
                    });
            }
            else
            {
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey((Color)palette[first], 0f),
                        new GradientColorKey((Color)palette[second], 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(1f, 1f)
                    });
            }

            return gradient;
        }

        static CornerShape CreateCornerShape(Random random)
        {
            float amount = NextFloat(random, 0.04f, 0.12f);
            switch (random.Next(0, 4))
            {
                case 0:
                    return new CornerShape(
                        Vector2.zero,
                        new Vector2(-amount, 0f),
                        new Vector2(amount, 0f),
                        Vector2.zero);
                case 1:
                    return new CornerShape(
                        new Vector2(-amount, 0f),
                        Vector2.zero,
                        Vector2.zero,
                        new Vector2(amount, 0f));
                case 2:
                    return new CornerShape(
                        new Vector2(-amount * 0.5f, -amount * 0.35f),
                        new Vector2(amount * 0.5f, amount * 0.35f),
                        new Vector2(amount, amount * 0.2f),
                        new Vector2(0f, -amount * 0.2f));
                default:
                    return new CornerShape(
                        new Vector2(amount, 0f),
                        new Vector2(amount * 0.5f, 0f),
                        new Vector2(-amount * 0.5f, 0f),
                        new Vector2(-amount, 0f));
            }
        }

        static EffectBlendMode ChooseBlend(TextStudioCompositionChannel channel, Random random)
        {
            if (channel == TextStudioCompositionChannel.Scale)
            {
                EffectBlendMode[] scale = { EffectBlendMode.Multiply, EffectBlendMode.Lerp, EffectBlendMode.Additive };
                return scale[random.Next(0, scale.Length)];
            }
            if (channel == TextStudioCompositionChannel.Alpha)
                return random.NextDouble() < 0.65 ? EffectBlendMode.Multiply : EffectBlendMode.Lerp;
            if (channel == TextStudioCompositionChannel.Color)
                return EffectBlendMode.Lerp;

            int roll = random.Next(0, 100);
            if (roll < 65) return EffectBlendMode.Additive;
            if (roll < 92) return EffectBlendMode.Lerp;
            return EffectBlendMode.Override;
        }

        static string ChannelRole(
            TextStudioCompositionChannel channel,
            TextStudioInspirationFamily family)
        {
            switch (channel)
            {
                case TextStudioCompositionChannel.Position:
                    return family == TextStudioInspirationFamily.Elastic ? "Lift" : "Drift";
                case TextStudioCompositionChannel.Rotation:
                    return family == TextStudioInspirationFamily.Hanging ? "Swing" : "Turn";
                case TextStudioCompositionChannel.Scale:
                    return "Breath";
                case TextStudioCompositionChannel.Alpha:
                    return "Glow";
                case TextStudioCompositionChannel.Color:
                    return "Color Flow";
                case TextStudioCompositionChannel.Skew:
                    return "Lean";
                case TextStudioCompositionChannel.UVOffset:
                    return "Texture Flow";
                case TextStudioCompositionChannel.Pivot:
                    return "Anchor";
                case TextStudioCompositionChannel.CornerDeformation:
                    return "Shape";
                default:
                    return channel.ToString();
            }
        }

        static string FormatAreaName(TextStudioEffectAreaSettings area)
        {
            string name;
            switch (area.Mode)
            {
                case TextStudioEffectAreaMode.Glyph:
                    name = "Glyph";
                    break;
                case TextStudioEffectAreaMode.Word:
                    name = "Word";
                    break;
                case TextStudioEffectAreaMode.Line:
                    name = "Line";
                    break;
                case TextStudioEffectAreaMode.Paragraph:
                    name = "Paragraph";
                    break;
                default:
                    name = "Range";
                    break;
            }
            return area.Mode != TextStudioEffectAreaMode.Range && area.GroupSize > 1
                ? name + " ×" + area.GroupSize.ToString(CultureInfo.InvariantCulture)
                : name;
        }

        static TextStudioInspirationFamily FamilyForSeed(int seed)
        {
            int value = seed % 6;
            if (value < 0)
                value += 6;
            return (TextStudioInspirationFamily)value;
        }

        static string CreateStableId(int seed, string kind, int index)
        {
            return "inspire-" + unchecked((uint)seed).ToString("x8", CultureInfo.InvariantCulture) +
                   "-" + kind + "-" + index.ToString(CultureInfo.InvariantCulture);
        }

        static float NextFloat(Random random, float minimum, float maximum)
        {
            return minimum + (maximum - minimum) * (float)random.NextDouble();
        }

        static float Signed(Random random)
        {
            return random.NextDouble() < 0.5 ? -1f : 1f;
        }

        static string FloatString(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        static string ColorString(Color32 color)
        {
            return "#" + ColorUtility.ToHtmlStringRGBA(color);
        }
    }
}
