//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

namespace DistantLands.Cozy
{
    /// <summary>
    /// Controls lightning in COZY. To prevent confusion between lighting & lightning, all references to the electrical discharge produced by storms is referred to as thunder in COZY despite not being completely accurate.
    /// </summary>
    [AddComponentMenu("Distant Lands/Cozy/Thunder Manager")]
    public class CozyThunderManager : MonoBehaviour
    {
        private float thunderTimer = 0;
        public ThunderFX thunderFX;
        public CozyWeather weatherSphere;
        private IAtmosphereModule atmosphereModule;
        public VisualEffect thunderPrefab;
        private static Light thunderLight;

        public float duration = 1.5f;
        public AnimationCurve sunLightDimming;
        public AnimationCurve lightningLightIntensityCurve;
        public float lightningLightIntensity;

        public Vector2 thunderAudioDelay;
        public AudioClip[] thunderSounds;

        private static (AudioSource source, AudioLowPassFilter lp)[] audioSources;
        private int thunderAudioSourceIndex;



        public void SetupInstance()
        {
            CozyWeather.Instance.GetModule(out atmosphereModule);
            SetupThunderLight();
            SetupThunderAudioSources();
        }

        private void SetupThunderAudioSources()
        {
            audioSources ??= new (AudioSource source, AudioLowPassFilter lp)[0];

            if (audioSources.Length > 0)
            {
                for (int i = audioSources.Length; i > 0; --i)
                {
                    if (audioSources[i].source != null && audioSources[i].source.gameObject)
                    Destroy(audioSources[i].source.gameObject);
                }
            }

            if (weatherSphere.VFXProvider.GetFXParent<CozyThunderManager>(out Transform parent))
            {
                (AudioSource, AudioLowPassFilter) CreateAudioSource()
                {
                    GameObject audioSourceObject = new("Thunder Audio");
                    audioSourceObject.transform.parent = parent;
                    audioSourceObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    AudioSource source = audioSourceObject.AddComponent<AudioSource>();
                    AudioLowPassFilter lp = audioSourceObject.AddComponent<AudioLowPassFilter>();

                    source.spatialBlend = 1f;
                    source.playOnAwake = false;
                    source.loop = false;
                    source.rolloffMode = AudioRolloffMode.Linear;
                    source.minDistance = 10f;
                    source.maxDistance = 500f;
                    source.dopplerLevel = 0f;
                    source.spread = 180f;

                    lp.cutoffFrequency = 5000f;
                    lp.lowpassResonanceQ = 1f;

                    return (source, lp);
                }

                audioSources = new (AudioSource, AudioLowPassFilter)[4]
                {
                    CreateAudioSource(),
                    CreateAudioSource(),
                    CreateAudioSource(),
                    CreateAudioSource()
                };
            }
        }

        private void SetupThunderLight()
        {
            weatherSphere = CozyWeather.Instance;

            if (thunderLight != null)
                return;

            if (weatherSphere.VFXProvider.GetFXParent<CozyThunderManager>(out Transform parent))
            {
                if (parent.GetComponentInChildren<Light>())
                {
                    thunderLight = parent.GetComponentInChildren<Light>();
                    return;
                }

                GameObject lightObject = new("Lightning & Thunder Light");
                lightObject.transform.parent = parent;
                lightObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                thunderLight = lightObject.AddComponent<Light>();
                thunderLight.color = new Color(1.2f, 1, 1.5f);
                thunderLight.intensity = 0;
                thunderLight.enabled = false;
                thunderLight.shadows = LightShadows.Soft;
                thunderLight.type = LightType.Directional;
            }
        }

        public void PlayEffect(float weight)
        {
            if (!Application.isPlaying)
                return;

            if (weight > 0.4f)
            {
                thunderTimer -= Time.deltaTime;

                if (thunderTimer <= 0)
                {
                    Strike();
                }
                if (thunderTimer > thunderFX.timeBetweenStrikes.y)
                {
                    thunderTimer = thunderFX.timeBetweenStrikes.y;
                }

            }
        }

        public void StopEffect()
        {

        }

        public void Strike()
        {
            if (!weatherSphere.CozyCamera) return;

            CozyLightningAttractor attractor = CozyLightningAttractor.GetRandomAttractor();
            Vector3 position = Vector3.zero;

            if (attractor)
            {
                position = attractor.transform.position;
                attractor.Strike();
            }
            else
            {
                position = weatherSphere.CozyCamera.transform.position + new Vector3(Random.Range(-1, 1) * 500, 0, Random.Range(-1, 1) * 500);
            }

            thunderLight.transform.forward = ((weatherSphere.CozyCamera.transform.position - position).normalized - Vector3.up).normalized;

            StartCoroutine(LightningCoroutine(position));
            StartCoroutine(ThunderCoroutine(position));

            thunderTimer = Random.Range(thunderFX.timeBetweenStrikes.x, thunderFX.timeBetweenStrikes.y);
        }

        private void PlayThunderAudio(Vector3 position)
        {
            if (audioSources.Length == 0)
                SetupThunderAudioSources();

            (AudioSource source, AudioLowPassFilter lp) source = audioSources[thunderAudioSourceIndex];

            source.source.clip = thunderSounds[Random.Range(0, thunderSounds.Length)];
            source.source.transform.position = position;
            source.source.Play();

            thunderAudioSourceIndex++;

            if (thunderAudioSourceIndex >= audioSources.Length)
            {
                thunderAudioSourceIndex = 0;
            }
        }

        IEnumerator LightningCoroutine(Vector3 position)
        {
            float timer = 0;

            VisualEffect instance = Instantiate(thunderPrefab, position, Quaternion.identity);

            while (timer < duration)
            {
                atmosphereModule.LightningLightingOverride = sunLightDimming.Evaluate(timer / duration);
                thunderLight.intensity = Mathf.Max(0, lightningLightIntensityCurve.Evaluate(timer / duration) * lightningLightIntensity);
                thunderLight.enabled = thunderLight.intensity > 0;
                yield return new WaitForEndOfFrame();
                timer += Time.deltaTime;
            }

            thunderLight.enabled = false;
            atmosphereModule.LightningLightingOverride = 0;
            Destroy(instance.gameObject);
        }
        IEnumerator ThunderCoroutine(Vector3 position)
        {
            yield return new WaitForSeconds(Random.Range(thunderAudioDelay.x, thunderAudioDelay.y));
            PlayThunderAudio(position);

        }

    }
}