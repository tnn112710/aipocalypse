using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TextStudio
{
    [Flags]
    internal enum TextStudio2DVisualModifierOperationFlags : ushort
    {
        None = 0,
        PerTextClock = 1 << 0
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TextStudio2DGpuTextUniform
    {
        internal Vector4 TimeAndProgress;
        internal Vector4 PolicyAndGeneration;

        internal bool Equals(in TextStudio2DGpuTextUniform other)
        {
            return TimeAndProgress.Equals(other.TimeAndProgress) &&
                   PolicyAndGeneration.Equals(other.PolicyAndGeneration);
        }
    }

    /// <summary>
    /// One allocation-free dynamic record for a fused visual program. Static modifier parameters
    /// remain in the shared style table; only clocks, progress, policy, and discontinuity change
    /// per text and frame.
    /// </summary>
    internal readonly struct TextStudio2DVisualModifierUniformState
    {
        internal readonly float ContinuousTime;
        internal readonly float DeltaTime;
        internal readonly float RevealProgress;
        internal readonly TextStudioMotionPolicy MotionPolicy;
        internal readonly uint DiscontinuityGeneration;

        internal TextStudio2DVisualModifierUniformState(
            float continuousTime,
            float deltaTime,
            float revealProgress,
            TextStudioMotionPolicy motionPolicy,
            uint discontinuityGeneration)
        {
            ContinuousTime = float.IsFinite(continuousTime)
                ? Mathf.Max(0f, continuousTime)
                : 0f;
            DeltaTime = float.IsFinite(deltaTime)
                ? Mathf.Max(0f, deltaTime)
                : 0f;
            RevealProgress = float.IsFinite(revealProgress)
                ? Mathf.Clamp01(revealProgress)
                : 0f;
            MotionPolicy = motionPolicy == TextStudioMotionPolicy.Inherit
                ? TextStudioMotionPolicy.Full
                : motionPolicy;
            DiscontinuityGeneration = discontinuityGeneration == 0u
                ? 1u
                : discontinuityGeneration;
        }
    }

    /// <summary>
    /// Canonical CPU packing for the bounded dynamic record consumed by fused visual modifiers.
    /// Static effect parameters remain exclusively in the existing style operation table.
    /// </summary>
    internal static class TextStudio2DVisualModifierPacking
    {
        internal static bool IsFusedModifier(
            in TextStudio2DCompiledOperation operation)
        {
            if (operation.Kind != TextStudio2DLayerKind.AnimatedEffect ||
                operation.ExecutionPath != TextStudio2DExecutionPath.FusedField ||
                !TextStudioVisualPrimitiveCatalog.TryGet(
                    operation.AnimatedEffect.Kind,
                    out TextStudioVisualPrimitiveContract primitive))
            {
                return false;
            }

            return !primitive.RequiresPostProcess;
        }

        internal static TextStudio2DVisualModifierOperationFlags ResolveOperationFlags(
            in TextStudio2DCompiledOperation operation)
        {
            return IsFusedModifier(in operation)
                ? TextStudio2DVisualModifierOperationFlags.PerTextClock
                : TextStudio2DVisualModifierOperationFlags.None;
        }

        internal static TextStudio2DGpuTextUniform PackUniform(
            in TextStudio2DVisualModifierUniformState state)
        {
            uint generation = state.DiscontinuityGeneration;
            return new TextStudio2DGpuTextUniform
            {
                TimeAndProgress = new Vector4(
                    state.ContinuousTime,
                    state.DeltaTime,
                    state.RevealProgress,
                    0f),
                PolicyAndGeneration = new Vector4(
                    (float)state.MotionPolicy,
                    generation & 0xffffu,
                    generation >> 16,
                    0f)
            };
        }
    }
}
