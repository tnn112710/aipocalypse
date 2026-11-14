//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.VFX;

namespace DistantLands.Cozy
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/FX/Thunder FX", order = 361)]
    public class ThunderFX : ReferenceFX<CozyThunderManager>
    {
        public Vector2 timeBetweenStrikes;
        public VisualEffect thunderPrefab = null;
        public AudioClip[] thunderSounds;
        [Tooltip("The audio mixer group that the COZY thunder audio will use.")]
        public AudioMixerGroup audioMixer;
        public AnimationCurve sunLightDimming;
        public AnimationCurve lightningLightIntensityCurve;
        public float lightningLightIntensity;
        public Vector2 thunderAudioDelay;

        public override bool IsValid => false;
        public override TransitionTimeModifier TransitionTimeModifier => TransitionTimeModifier.Linear;

        public override void GetDefaultInstance()
        {
            instance.thunderFX = this;
            instance.thunderPrefab = thunderPrefab;
            instance.thunderSounds = thunderSounds;
            instance.sunLightDimming = sunLightDimming;
            instance.lightningLightIntensity = lightningLightIntensity;
            instance.lightningLightIntensityCurve = lightningLightIntensityCurve;
            instance.thunderAudioDelay = thunderAudioDelay;

            instance.SetupInstance();
        }

        public override void UpdateReference(float weight)
        {
            instance.PlayEffect(weight);
        }

        public override void StopReference()
        {
            instance.StopEffect();
        }
    }
}
