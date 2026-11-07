//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
namespace DistantLands.Cozy
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/FX/Audio FX", order = 361)]
    public class AudioFX : ReferenceFX<AudioSource>
    {
        public AudioClip clip;
        [Tooltip("The audio mixer group that the COZY weather audio FX will use.")]
        public AudioMixerGroup audioMixer;
        [Range(0, 1)]
        public float maximumVolume = 1;

        public override void GetDefaultInstance()
        {
            instance.clip = clip;
            instance.outputAudioMixerGroup = audioMixer;
            instance.playOnAwake = false;
            instance.volume = 0;
            instance.loop = true;
        }

        public override void UpdateReference(float weight)
        {
            if (!instance.isPlaying) instance.Play();
            
            instance.volume = maximumVolume *  weight;
        }

        public override void StopReference()
        {
            instance.Stop();
            instance.volume = 0;
        }
    }
}
