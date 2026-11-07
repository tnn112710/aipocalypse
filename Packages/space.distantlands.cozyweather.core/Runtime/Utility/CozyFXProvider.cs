//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;

namespace DistantLands.Cozy
{
    /// <summary>
    /// Handles FX Profile playing
    /// </summary>
    public class CozyFXProvider : IFXProvider
    {
        private FXParameters fxParameters = new();
        private FXParameters lastFrameParameters = new(true);
        /// <summary>
        /// Holds all the FX parameters currently running on the COZY system. These are used to apply visual changes to the GPU.
        /// </summary>
        public FXParameters FXParameters { get => fxParameters; set => fxParameters = value; }


        public CozyFXProvider()
        {
            fxParameters = new();
            lastFrameParameters = new(true);
        }

        public void ResetFXParameters()
        {
            fxParameters.Reset();
        }

        public void PassFXVariables()
        {
            if (fxParameters.fogCoverage != lastFrameParameters.fogCoverage) Shader.SetGlobalFloat(CozyShaderIDs.baseWeatherFogCoverage, fxParameters.fogCoverage);
            if (fxParameters.fogHeightCoverage != lastFrameParameters.fogHeightCoverage) Shader.SetGlobalFloat(CozyShaderIDs.baseWeatherFogHeightCoverage, fxParameters.fogHeightCoverage);

            if (fxParameters.filterSaturation != lastFrameParameters.filterSaturation) Shader.SetGlobalFloat(CozyShaderIDs.baseWeatherSaturationFilter, fxParameters.filterSaturation);
            if (fxParameters.filterValue != lastFrameParameters.filterValue) Shader.SetGlobalFloat(CozyShaderIDs.baseWeatherValueFilter, fxParameters.filterValue);
            if (fxParameters.filterColor != lastFrameParameters.filterColor) Shader.SetGlobalColor(CozyShaderIDs.baseWeatherFilterColor, fxParameters.filterColor);
            if (fxParameters.sunFilter != lastFrameParameters.sunFilter) Shader.SetGlobalColor(CozyShaderIDs.baseWeatherSunFilter, fxParameters.sunFilter);
            if (fxParameters.cloudFilter != lastFrameParameters.cloudFilter) Shader.SetGlobalColor(CozyShaderIDs.baseWeatherCloudFilter, fxParameters.cloudFilter);

            if (fxParameters.altocumulusCoverage != lastFrameParameters.altocumulusCoverage) Shader.SetGlobalFloat(CozyShaderIDs.baseWeatherAltocumulusCoverage, fxParameters.altocumulusCoverage);
            if (fxParameters.contrailsCoverage != lastFrameParameters.contrailsCoverage) Shader.SetGlobalFloat(CozyShaderIDs.baseWeatherContrailCoverage, fxParameters.contrailsCoverage);
            if (fxParameters.cirrusCoverage != lastFrameParameters.cirrusCoverage) Shader.SetGlobalFloat(CozyShaderIDs.baseWeatherCirrusCoverage, fxParameters.cirrusCoverage);
            if (fxParameters.cirrostratusCoverage != lastFrameParameters.cirrostratusCoverage) Shader.SetGlobalFloat(CozyShaderIDs.baseWeatherCirrostratusCoverage, fxParameters.cirrostratusCoverage);
            if (fxParameters.cumulonimbusCoverage != lastFrameParameters.cumulonimbusCoverage) Shader.SetGlobalFloat(CozyShaderIDs.baseWeatherCumulonimbusCoverage, fxParameters.cumulonimbusCoverage);
            if (fxParameters.cumulusCoverage != lastFrameParameters.cumulusCoverage) Shader.SetGlobalFloat(CozyShaderIDs.baseWeatherCumulusCoverage, fxParameters.cumulusCoverage);
            if (fxParameters.borderCloudsCoverage != lastFrameParameters.borderCloudsCoverage) Shader.SetGlobalFloat(CozyShaderIDs.baseWeatherBorderCoverage, fxParameters.borderCloudsCoverage);

            //Save the parameters used this frame to catch changes next frame. This stops sending duplicate values every frame.
            lastFrameParameters.Copy(fxParameters);
        }

        public void Center()
        {

        }
    }
}