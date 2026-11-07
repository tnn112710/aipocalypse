//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEngine.VFX;

namespace DistantLands.Cozy
{
    [AddComponentMenu("Distant Lands/Cozy/Visual Effect Manager")]
    [RequireComponent(typeof(VisualEffect))]
    public class CozyVFXInstance : MonoBehaviour
    {
        private bool playingAtMaxIntensity = false;
        private bool stopped = false;
        private bool useIntensity = true;
        private bool useWind = true;
        private VisualEffect visualEffect;

        public static int renderDistanceID;
        public static int windDirectionID;
        public static int depthTextureScaleID;
        private static int intensityID;

        CozyWindModule wind;

        void OnEnable()
        {
            SetupShaderIDs();

            visualEffect = GetComponent<VisualEffect>();

            if (visualEffect.HasFloat(renderDistanceID))
                visualEffect.SetFloat(renderDistanceID, CozyPreferences.FXRenderDistance);

            useWind = CozyWeather.Instance && CozyWeather.Instance.GetModule(out wind) && visualEffect.HasVector3(windDirectionID);

            if (!visualEffect.HasFloat(intensityID))
            {
                Debug.LogWarning("COZY VFX need to have a property called 'Intensity' in order to be manipulable. This system will only toggle rather than adjust intensity.");
                useIntensity = false;
            }
        }

        void SetupShaderIDs()
        {
            renderDistanceID = Shader.PropertyToID("RenderDistance");
            depthTextureScaleID = Shader.PropertyToID("DepthTextureScale");
            intensityID = Shader.PropertyToID("Intensity");
            windDirectionID = Shader.PropertyToID("WindDirection");
        }

        public void Play(float strength, float targetWeight)
        {
            if (playingAtMaxIntensity && strength == 1) return;
            if (stopped && strength == 0) return;

            if (stopped) visualEffect.Play();

            if (useIntensity)
                visualEffect.SetFloat(intensityID, strength * targetWeight);

            if (useWind)
                visualEffect.SetVector3(windDirectionID, wind.WindDirection * wind.WindStrength);

            playingAtMaxIntensity = strength == 1;
            stopped = strength == 0;

            if (stopped)
                visualEffect.Stop();
        }

    }
}