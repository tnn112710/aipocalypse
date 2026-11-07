//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections.Generic;
using UnityEngine;

namespace DistantLands.Cozy
{
    [AddComponentMenu("Distant Lands/Cozy/Shuriken Manager")]
    [RequireComponent(typeof(ParticleSystem))]
    public class CozyShurikenInstance : MonoBehaviour
    {
        private bool playingAtMaxIntensity = false;
        private bool stopped = false;
        private bool useIntensity = true;

        [SerializeField]
        private ParticleSystem[] m_ParticleSystems;

        [System.Serializable]
        public class ParticleType
        {
            public ParticleSystem particleSystem;
            public float emissionAmount;
        }


        [HideInInspector]
        public List<ParticleType> m_ParticleTypes;

        void OnEnable()
        {
            m_ParticleSystems ??= new ParticleSystem[0];

            if (m_ParticleSystems.Length == 0)
                m_ParticleSystems = GetComponentsInChildren<ParticleSystem>();


            foreach (ParticleSystem particleSystem in m_ParticleSystems)
            {
                if (particleSystem == null)
                    continue;

                ParticleType particleType = new()
                {
                    particleSystem = particleSystem,
                    emissionAmount = particleSystem.emission.rateOverTime.constant
                };
                m_ParticleTypes.Add(particleType);

                particleSystem.Play();
            }

            foreach (ParticleType particleType in m_ParticleTypes)
            {
                ParticleSystem.EmissionModule k = particleType.particleSystem.emission;
                ParticleSystem.MinMaxCurve j = k.rateOverTime;

                j.constant = 0;
                k.rateOverTime = j;
            }
        }

        public void Play(float strength)
        {
            if (playingAtMaxIntensity && strength == 1) return;
            if (stopped && strength == 0) return;

            if (stopped)
                foreach (ParticleSystem particleSystem in m_ParticleSystems)
                    particleSystem.Play();


            if (useIntensity)
                foreach (ParticleType particle in m_ParticleTypes)
                {
                    ParticleSystem.EmissionModule emissionModule = particle.particleSystem.emission;
                    ParticleSystem.MinMaxCurve rateOverTime = emissionModule.rateOverTime;

                    rateOverTime.constant = particle.emissionAmount * strength;

                    emissionModule.rateOverTime = rateOverTime;
                }

            playingAtMaxIntensity = strength == 1;
            stopped = strength == 0;

            if (stopped)
                foreach (ParticleSystem particleSystem in m_ParticleSystems)
                    particleSystem.Stop();
        }

    }
}