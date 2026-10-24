using UnityEngine;

namespace SkyboxPlus {

    [ExecuteInEditMode]
    public class SkyboxPlus : MonoBehaviour {

        static class ShaderParams {
            public static readonly int LightWorldToLocal = Shader.PropertyToID("_LightWL");
            public static readonly int LightDir = Shader.PropertyToID("_WorldSpaceSunDir");
            public static readonly int MoonLightWorldToLocal = Shader.PropertyToID("_MoonLightWL");
            public static readonly int MoonLightDir = Shader.PropertyToID("_WorldSpaceMoonDir");
            public static readonly int CustomPositions = Shader.PropertyToID("_SBPCustomPositions");
        }

        public Transform sun;
        public Transform moon;

        private void OnEnable() {
            if (sun == null) sun = transform;
            Shader.SetGlobalFloat(ShaderParams.CustomPositions, 1f);
        }

        private void OnDisable() {
            Shader.SetGlobalFloat(ShaderParams.CustomPositions, 0f);
        }

        void LateUpdate() {
            Shader.SetGlobalMatrix(ShaderParams.LightWorldToLocal, sun.worldToLocalMatrix);
            Shader.SetGlobalVector(ShaderParams.LightDir, -sun.forward);
            if (moon == null) {
                Shader.SetGlobalMatrix(ShaderParams.MoonLightWorldToLocal, sun.worldToLocalMatrix);
                Vector4 moonDir = sun.forward;
                moonDir.w = -1;
                Shader.SetGlobalVector(ShaderParams.MoonLightDir, moonDir);
            } else {
                Shader.SetGlobalMatrix(ShaderParams.MoonLightWorldToLocal, moon.worldToLocalMatrix);
                Vector4 moonDir = -moon.forward;
                moonDir.w = -1;
                Shader.SetGlobalVector(ShaderParams.MoonLightDir, moonDir);
            }
        }

    }

}