//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Runtime.CompilerServices;
using UnityEngine;

namespace DistantLands.Cozy
{
    /// <summary>
    /// Base class for any FX profile in the COZY system.
    /// </summary>
    public abstract class CozyFXProfile : ScriptableObject
    {
        public abstract bool IsValid { get; }

        /// <summary>
        /// Change the easing function for the FX profile weighting
        /// </summary>
        public abstract TransitionTimeModifier TransitionTimeModifier { get; }

        /// <summary>
        /// Called before this profile starts playing. Sets up references to modules, instantiates necessary objects in the scene, and verifies that the setup is complete.
        /// </summary>
        public abstract bool InitializeEffect();

        /// <summary>
        /// Plays the effect at a given weight. When the weight is 0, calls stop effect instead.
        /// </summary>
        public abstract void PlayEffect(float weight);

        /// <summary>
        /// Stops this FX from playing.
        /// </summary>
        public abstract void StopEffect();

        /// <summary>
        /// Cleans up any objects that this FX profile created to prevent memory leaks.
        /// </summary>
        public abstract void DeinitializeEffect();
    }

    
    public static class Easing
    {
        public static float Evaluate(float input, TransitionTimeModifier modifier)
        {
            switch (modifier)
            {
                default:
                    return Linear(input);
                case TransitionTimeModifier.CubicIn:
                    return CubicIn(input);
                case TransitionTimeModifier.CubicOut:
                    return CubicOut(input);
                case TransitionTimeModifier.SmoothStep:
                    return SmoothStep(input);
                case TransitionTimeModifier.SineInOut:
                    return SineInOut(input);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Linear(float t)
        {
            return t;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CubicIn(float t)
        {
            return t * t * t;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CubicOut(float t)
        {
            float f = t - 1f;
            return f * f * f + 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SineInOut(float t)
        {
            return 0.5f * (1f - Mathf.Cos(t * Mathf.PI));
        }
    }

    public enum TransitionTimeModifier
    {
        Linear,
        CubicIn,
        CubicOut,
        SmoothStep,
        SineInOut
    }

}